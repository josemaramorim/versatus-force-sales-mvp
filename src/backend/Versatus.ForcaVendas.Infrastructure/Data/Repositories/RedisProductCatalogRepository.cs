using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class RedisProductCatalogRepository(IConnectionMultiplexer redis) : IProductCatalogRepository
{
    public async Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(
        CatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var prodKey = $"catalogo:{request.TenantId}:produtos";
        var priceKey = $"catalogo:{request.TenantId}:precos";

        try
        {
            var prodJson = await db.StringGetAsync(prodKey);
            var priceJson = await db.StringGetAsync(priceKey);

            if (prodJson.HasValue)
            {
                var redisProducts = JsonSerializer.Deserialize<List<RedisProductItem>>(prodJson!, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var redisPrices = priceJson.HasValue 
                    ? JsonSerializer.Deserialize<List<RedisPriceItem>>(priceJson!, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                    : [];

                if (redisProducts is not null && redisProducts.Count > 0)
                {
                    // Ler parâmetros do tenant para obter tabela padrão
                    var tenantParamsJson = await db.StringGetAsync($"catalogo:{request.TenantId}:tenant-parameters");
                    var defaultTableId = 1;
                    if (tenantParamsJson.HasValue)
                    {
                        try
                        {
                            var tenantParams = JsonSerializer.Deserialize<TenantParametersDto>(tenantParamsJson!, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                            if (tenantParams != null)
                            {
                                defaultTableId = tenantParams.TabelaPrecoIdDefault;
                            }
                        }
                        catch
                        {
                            // Fallback para 1
                        }
                    }

                    // Preço principal = tabela padrão do tenant
                    var priceDict = redisPrices?
                        .Where(p => p.TabelaPrecoIdERP == defaultTableId)
                        .ToDictionary(p => p.ProdutoIdERP, p => p.ValorUnitario) ?? [];

                    // Todos os preços agrupados por produto (para enviar ao frontend)
                    var pricesByProduct = redisPrices?
                        .GroupBy(p => p.ProdutoIdERP)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(p => new PriceTableEntry(
                                p.TabelaPrecoIdERP,
                                p.TabelaPrecoEstoqueIdERP,
                                p.Descricao ?? "Tabela de Preço",
                                p.ValorUnitario,
                                p.IsPromocional,
                                p.VigenciaInicio,
                                p.VigenciaFim
                            )).ToList()
                        ) ?? [];

                    var normalizedQuery = request.Query?.Trim() ?? string.Empty;

                    return redisProducts
                        .Where(p => string.IsNullOrWhiteSpace(normalizedQuery)
                            || p.ProdutoIdERP.ToString().Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                            || p.Descricao.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                        .Take(request.Limit)
                        .Select(p => {
                            var price = priceDict.TryGetValue(p.ProdutoIdERP, out var pr) ? pr : 0.00m;
                            pricesByProduct.TryGetValue(p.ProdutoIdERP, out var pricesList);
                            return new ProductSummary(
                                p.ProdutoIdERP.ToString(),
                                $"SKU-PRD-{p.ProdutoIdERP}",
                                p.Descricao,
                                p.SiglaUnidadeVenda,
                                price,
                                p.Saldo,
                                pricesList);
                        })
                        .ToList();
                }
            }
        }
        catch
        {
            // Ignora erro de rede/Redis
        }

        return Array.Empty<ProductSummary>();
    }

    private sealed record RedisProductItem(int ProdutoIdERP, string Descricao, string SiglaUnidadeVenda, decimal Saldo);
    private sealed record RedisPriceItem(
        int ProdutoIdERP, 
        int TabelaPrecoIdERP, 
        int TabelaPrecoEstoqueIdERP,
        string Descricao,
        decimal ValorUnitario,
        bool IsPromocional,
        DateTime? VigenciaInicio,
        DateTime? VigenciaFim);
}
