using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Worker.Jobs;

// Captura a versão embutida no assembly no momento da compilação
var version = typeof(Program).Assembly
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "1.0.0-unknown";

// Suporte ao argumento --version / -v: exibe a versão e encerra
if (args.Contains("-v") || args.Contains("--version"))
{
    Console.WriteLine(version);
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// Configura o banco de dados PostgreSQL
builder.Services.AddDbContext<PedidosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configura a conexão do cache Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false"));

// Configura o provedor de transporte de integração de forma compartilhada
builder.Services.AddIntegrationTransport(builder.Configuration);

// Registra os BackgroundServices de sincronização
builder.Services.AddHostedService<CatalogSyncJob>();
builder.Services.AddHostedService<ResultPollingJob>();

var host = builder.Build();

// Loga a versão ao iniciar
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Inicializando {App} — Versão: {Version}", "Versatus.ForcaVendas.Worker", version);

host.Run();

