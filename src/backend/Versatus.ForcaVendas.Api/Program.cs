using Microsoft.Extensions.Diagnostics.HealthChecks;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Versatus.ForcaVendas.Api;
using Versatus.ForcaVendas.Api.Health;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Application.Licenca;
using StackExchange.Redis;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Versatus Força de Vendas API", Version = "v1" });
    
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
builder.Services.AddScoped<ITenantSubscriptionRepository, NpgsqlTenantSubscriptionRepository>();
builder.Services.AddScoped<Versatus.ForcaVendas.Domain.Auth.IUsuarioRepository, NpgsqlUsuarioRepository>();
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.Configure<Versatus.ForcaVendas.Application.Sessao.SessionStoreOptions>(opts =>
{
    opts.TimeoutMinutes = builder.Configuration.GetValue<int>("Auth:Jwt:SessionTimeoutMinutes", 20);
});
builder.Services.AddSingleton<ISessionStore, RedisSessionStore>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ISessionAuditEventRepository, NpgsqlSessionAuditEventRepository>();
builder.Services.AddSingleton<IProductCatalogRepository, InMemoryProductCatalogRepository>();
builder.Services.AddSingleton<IClientCatalogRepository, InMemoryClientCatalogRepository>();
builder.Services.AddSingleton<Versatus.ForcaVendas.Domain.Pedidos.Services.IPaymentConditionService, Versatus.ForcaVendas.Infrastructure.Data.Services.MockPaymentConditionService>();
// Configure EF Core to use Postgres via the configured connection string
// in `appsettings.json` (ConnectionStrings:DefaultConnection).
builder.Services.AddDbContext<PedidosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMediatR(typeof(CriarPedidoCommand));
builder.Services.AddValidatorsFromAssemblyContaining<CriarPedidoRequestValidator>();
builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>("redis");

// In-memory pedido cache used as a test-host fallback
builder.Services.AddSingleton<IPedidoCache, InMemoryPedidoCache>();

// CORS: permite o frontend Next.js consumir a API em dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendDev");
app.UseMiddleware<TenantContextMiddleware>();

// Prometheus metrics for HTTP + metrics endpoint
app.UseHttpMetrics();

// Liveness: basic ping of the process (no external deps)
app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }))
    .WithName("Liveness");

// Readiness: execute registered health checks
app.MapGet("/health/ready", async (HealthCheckService hc) =>
{
    var report = await hc.CheckHealthAsync();
    var result = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds
        })
    };

    return Results.Json(result, statusCode: report.Status == HealthStatus.Healthy ? 200 : 503);
})
    .WithName("Readiness");

// Expose Prometheus metrics at /metrics
app.MapMetrics();
app.MapAuthSessionEndpoints();
app.MapCatalogoEndpoints();
app.MapPedidosEndpoints();

app.MapControllers();
app.Run();
