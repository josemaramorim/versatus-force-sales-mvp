using System;
using System.Collections.Generic;
using System.Linq;
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
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

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

                    List<ClienteCatalogDto> mergedClientes;
                    List<ProdutoCatalogDto> mergedProdutos;
                    List<TabelaPrecoCatalogDto> mergedPrecos;
                    List<CondicaoPagamentoCatalogDto> mergedCondicoes;

                    if (snapshot.IsFullSync)
                    {
                        mergedClientes = snapshot.Clientes.ToList();
                        mergedProdutos = snapshot.Produtos.ToList();
                        mergedPrecos = snapshot.TabelasPreco.ToList();
                        mergedCondicoes = snapshot.CondicoesPagamento.ToList();
                    }
                    else
                    {
                        var existingClientes = await GetExistingListAsync<ClienteCatalogDto>(redisDb, $"catalogo:{tenantId}:clientes");
                        var existingProdutos = await GetExistingListAsync<ProdutoCatalogDto>(redisDb, $"catalogo:{tenantId}:produtos");
                        var existingPrecos = await GetExistingListAsync<TabelaPrecoCatalogDto>(redisDb, $"catalogo:{tenantId}:precos");
                        var existingCondicoes = await GetExistingListAsync<CondicaoPagamentoCatalogDto>(redisDb, $"catalogo:{tenantId}:condicoes-pagamento");

                        mergedClientes = MergeDelta(existingClientes, snapshot.Clientes, c => c.ClienteIdERP);
                        mergedProdutos = MergeDelta(existingProdutos, snapshot.Produtos, p => p.ProdutoIdERP);
                        mergedPrecos = MergeDelta(existingPrecos, snapshot.TabelasPreco, tp => tp.TabelaPrecoEstoqueIdERP);
                        mergedCondicoes = MergeDelta(existingCondicoes, snapshot.CondicoesPagamento, cp => cp.CondicaoPagtoIdERP);
                    }

                    // Persiste cada segmento do catálogo no Redis
                    var clientesJson = JsonSerializer.Serialize(mergedClientes, _jsonOptions);
                    var produtosJson = JsonSerializer.Serialize(mergedProdutos, _jsonOptions);
                    var precosJson = JsonSerializer.Serialize(mergedPrecos, _jsonOptions);
                    var condicoesJson = JsonSerializer.Serialize(mergedCondicoes, _jsonOptions);

                    await redisDb.StringSetAsync($"catalogo:{tenantId}:clientes", clientesJson);
                    await redisDb.StringSetAsync($"catalogo:{tenantId}:produtos", produtosJson);
                    await redisDb.StringSetAsync($"catalogo:{tenantId}:precos", precosJson);
                    await redisDb.StringSetAsync($"catalogo:{tenantId}:condicoes-pagamento", condicoesJson);

                    _logger.LogInformation("Catálogo sincronizado com sucesso para tenant {TenantId}. IsFullSync: {IsFullSync}. Clientes: {Clientes} (Delta: {DeltaClientes}), Produtos: {Produtos} (Delta: {DeltaProdutos}), Preços: {Precos} (Delta: {DeltaPrecos}), Condições: {Condicoes} (Delta: {DeltaCondicoes})",
                        tenantId, snapshot.IsFullSync, 
                        mergedClientes.Count, snapshot.Clientes.Count, 
                        mergedProdutos.Count, snapshot.Produtos.Count, 
                        mergedPrecos.Count, snapshot.TabelasPreco.Count, 
                        mergedCondicoes.Count, snapshot.CondicoesPagamento.Count);

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

    private async Task<List<T>> GetExistingListAsync<T>(IDatabase redisDb, string key)
    {
        var json = await redisDb.StringGetAsync(key);
        if (json.IsNullOrEmpty)
        {
            return new List<T>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json!, _jsonOptions) ?? new List<T>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao desserializar dados existentes do Redis para chave {Key}. Iniciando nova lista.", key);
            return new List<T>();
        }
    }

    private List<T> MergeDelta<T, TId>(List<T> existingList, IReadOnlyList<T> deltaList, Func<T, TId> idSelector) where TId : notnull
    {
        var existingDict = existingList.ToDictionary(idSelector);
        foreach (var item in deltaList)
        {
            var id = idSelector(item);
            existingDict[id] = item;
        }
        return existingDict.Values.ToList();
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
