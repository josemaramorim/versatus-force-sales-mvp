using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Integration;

namespace Versatus.ForcaVendas.Worker.Jobs;

public sealed class CatalogSyncJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationTransport _transport;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConfiguration _config;
    private readonly ILogger<CatalogSyncJob> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public CatalogSyncJob(
        IServiceProvider serviceProvider,
        IIntegrationTransport transport,
        IConnectionMultiplexer redis,
        IConfiguration config,
        ILogger<CatalogSyncJob> logger)
    {
        _serviceProvider = serviceProvider;
        _transport = transport;
        _redis = redis;
        _config = config;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue<int>("Integration:Ftp:CatalogPollIntervalSeconds", 300);
        if (intervalSeconds <= 0) intervalSeconds = 300;

        _logger.LogInformation("Iniciando CatalogSyncJob com intervalo de {Interval} segundos.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SynchronizeAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro fatal durante o ciclo de sincronização do catálogo.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task SynchronizeAllTenantsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PedidosDbContext>();

        // Busca todos os tenants ativos no banco de dados
        var tenants = await db.TenantSubscriptions
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => t.TenantId.ToString())
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Encontrados {Count} tenants ativos para sincronização de catálogo.", tenants.Count);

        foreach (var tenantId in tenants)
        {
            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("Verificando atualizações de catálogo para tenant {TenantId}...", tenantId);

            try
            {
                var snapshot = await _transport.FetchCatalogAsync(tenantId, stoppingToken);

                if (snapshot is not null)
                {
                    var redisDb = _redis.GetDatabase();

                    // Persiste cada segmento do catálogo no Redis
                    var clientesJson = JsonSerializer.Serialize(snapshot.Clientes, _jsonOptions);
                    var produtosJson = JsonSerializer.Serialize(snapshot.Produtos, _jsonOptions);
                    var precosJson = JsonSerializer.Serialize(snapshot.TabelasPreco, _jsonOptions);
                    var condicoesJson = JsonSerializer.Serialize(snapshot.CondicoesPagamento, _jsonOptions);

                    await redisDb.StringSetAsync($"catalogo:{tenantId}:clientes", clientesJson);
                    await redisDb.StringSetAsync($"catalogo:{tenantId}:produtos", produtosJson);
                    await redisDb.StringSetAsync($"catalogo:{tenantId}:precos", precosJson);
                    await redisDb.StringSetAsync($"catalogo:{tenantId}:condicoes-pagamento", condicoesJson);

                    _logger.LogInformation("Catálogo sincronizado com sucesso para tenant {TenantId}. Clientes: {Clientes}, Produtos: {Produtos}, Preços: {Precos}, Condições: {Condicoes}",
                        tenantId, snapshot.Clientes.Count, snapshot.Produtos.Count, snapshot.TabelasPreco.Count, snapshot.CondicoesPagamento.Count);

                    // Opcional: Invalida o cache de buscas de catálogo na API para forçar releitura dos dados novos
                    await InvalidateApiSearchCacheAsync(tenantId);
                }
                else
                {
                    _logger.LogDebug("Nenhum catálogo novo disponível ou incompleto para tenant {TenantId}.", tenantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar sincronização do catálogo para tenant {TenantId}.", tenantId);
            }
        }
    }

    private async Task InvalidateApiSearchCacheAsync(string tenantId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var endpoints = _redis.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: $"catalog:*:{tenantId}:*").ToArray();
                if (keys.Length > 0)
                {
                    await db.KeyDeleteAsync(keys);
                    _logger.LogInformation("Invalidados {Count} caches de busca de catálogo para tenant {TenantId}.", keys.Length, tenantId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao invalidar cache de busca da API para o tenant {TenantId}.", tenantId);
        }
    }
}
