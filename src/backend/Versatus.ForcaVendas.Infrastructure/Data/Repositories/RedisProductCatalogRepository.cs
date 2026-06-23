using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Versatus.ForcaVendas.Application.Catalogo;

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
                    // Tabela de preço padrão = 1
                    var priceDict = redisPrices?
                        .Where(p => p.TabelaPrecoIdERP == 1)
                        .ToDictionary(p => p.ProdutoIdERP, p => p.ValorUnitario) ?? [];

                    var normalizedQuery = request.Query?.Trim() ?? string.Empty;

                    return redisProducts
                        .Where(p => string.IsNullOrWhiteSpace(normalizedQuery)
                            || p.ProdutoIdERP.ToString().Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                            || p.Descricao.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                        .Take(request.Limit)
                        .Select(p => {
                            var price = priceDict.TryGetValue(p.ProdutoIdERP, out var pr) ? pr : 0.00m;
                            return new ProductSummary(
                                p.ProdutoIdERP.ToString(),
                                $"SKU-PRD-{p.ProdutoIdERP}",
                                p.Descricao,
                                p.SiglaUnidadeVenda,
                                price,
                                p.Saldo);
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
    private sealed record RedisPriceItem(int ProdutoIdERP, int TabelaPrecoIdERP, decimal ValorUnitario);
}
