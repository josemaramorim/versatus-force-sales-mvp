using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public partial class Program
{
    /// <summary>
    /// Registra serviços de autenticação JWT Bearer e geração/renovação de tokens.
    /// Fail-closed: requisições sem token válido ou sem claim tenant_id são bloqueadas pelo middleware.
    /// </summary>
    internal static void AddAuthServices(WebApplicationBuilder builder)
    {
        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();

        var jwtSection = builder.Configuration.GetSection($"{AuthOptions.SectionName}:Jwt");
        var secretKey = jwtSection["SecretKey"] ?? "VersatusForceSalesDevSecretKey2026!";
        var issuer = jwtSection["Issuer"] ?? "versatus-force-sales";
        var audience = jwtSection["Audience"] ?? "versatus-force-sales-clients";

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        builder.Services.AddAuthorization();
    }

    /// <summary>
    /// Registra TenantContext (scoped per-request) e repositório de usuários.
    /// Isolamento multi-tenant: nenhum dado é acessível sem TenantId resolvido.
    /// </summary>
    internal static void AddTenantServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        builder.Services.AddScoped<Versatus.ForcaVendas.Domain.Auth.IUsuarioRepository, NpgsqlUsuarioRepository>();
        builder.Services.AddScoped<ITenantSubscriptionRepository, NpgsqlTenantSubscriptionRepository>();
        builder.Services.AddScoped<ISessionAuditEventRepository, NpgsqlSessionAuditEventRepository>();
    }

    /// <summary>
    /// Registra Redis (IConnectionMultiplexer singleton) e ISessionStore (controle de seats).
    /// </summary>
    internal static void AddRedisServices(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
        builder.Services.AddSingleton<ISessionStore, RedisSessionStore>();
    }

    /// <summary>
    /// Registra EF Core (PedidosDbContext via PostgreSQL) e catálogo/pedidos.
    /// </summary>
    internal static void AddDataServices(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<PedidosDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddSingleton<IProductCatalogRepository, InMemoryProductCatalogRepository>();
        builder.Services.AddSingleton<IClientCatalogRepository, InMemoryClientCatalogRepository>();
        builder.Services.AddSingleton<Versatus.ForcaVendas.Domain.Pedidos.Services.IPaymentConditionService,
            Versatus.ForcaVendas.Infrastructure.Data.Services.MockPaymentConditionService>();
        builder.Services.AddSingleton<IPedidoCache, InMemoryPedidoCache>();
    }

    /// <summary>
    /// Placeholder para registro do broker de mensageria (RabbitMQ).
    /// Ativado quando a connection string "RabbitMQ" está configurada.
    /// </summary>
    internal static void AddMessagingServices(WebApplicationBuilder builder)
    {
        var rabbitMqCs = builder.Configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrWhiteSpace(rabbitMqCs))
        {
            // Conexão RabbitMQ será injetada como singleton quando a infraestrutura de mensageria for registrada (T045/T046).
            // Por ora registra o connection string como IConfiguration acessível por nome.
            builder.Services.Configure<MessagingOptions>(
                builder.Configuration.GetSection(MessagingOptions.SectionName));
        }
    }
}

/// <summary>Opções de mensageria – placeholder para Phase 3 (US3).</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";
    public string BrokerUrl { get; set; } = string.Empty;
    public string PedidosExchange { get; set; } = "pedidos";
    public string RetornoQueue { get; set; } = "pedidos.retorno.erp";
}
