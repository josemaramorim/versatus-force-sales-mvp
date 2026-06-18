using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Versatus.ForcaVendas.ErpAdapter.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// Configura o provedor de transporte de integração compartilhado
builder.Services.AddIntegrationTransport(builder.Configuration);

// Registra os BackgroundServices do adaptador ERP
builder.Services.AddHostedService<CatalogExporter>();
builder.Services.AddHostedService<OrderImporter>();

var host = builder.Build();
host.Run();
