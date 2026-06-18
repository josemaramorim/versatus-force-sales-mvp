using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Worker.Jobs;

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
host.Run();
