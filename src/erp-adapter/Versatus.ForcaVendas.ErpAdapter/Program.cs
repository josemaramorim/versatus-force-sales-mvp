using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Versatus.ForcaVendas.ErpAdapter.Jobs;

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

// Limpa os provedores padrão para evitar erro do EventLog em publicação autossuficiente
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configura o provedor de transporte de integração compartilhado
builder.Services.AddIntegrationTransport(builder.Configuration);

// Registra os BackgroundServices do adaptador ERP
builder.Services.AddHostedService<CatalogExporter>();
builder.Services.AddHostedService<OrderImporter>();

var host = builder.Build();

// Loga a versão ao iniciar
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Inicializando {App} — Versão: {Version}", "Versatus.ForcaVendas.ErpAdapter", version);

host.Run();

