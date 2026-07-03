using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Versatus.ForcaVendas.Application.Catalogo;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class RedisClientCatalogRepository(IConnectionMultiplexer redis) : IClientCatalogRepository
{
    public async Task<IReadOnlyList<ClientSummary>> SearchClientsAsync(
        CatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = $"catalogo:{request.TenantId}:clientes";

        try
        {
            var json = await db.StringGetAsync(key);
            if (json.HasValue)
            {
                var redisItems = JsonSerializer.Deserialize<List<RedisClientItem>>((string)json!, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (redisItems is not null && redisItems.Count > 0)
                {
                    var normalizedQuery = request.Query?.Trim() ?? string.Empty;
                    return redisItems
                        .Where(c => string.IsNullOrWhiteSpace(normalizedQuery)
                            || c.Nome.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                            || c.Documento.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                        .Take(request.Limit)
                        .Select(c => new ClientSummary(
                            c.ClienteIdERP.ToString(),
                            c.Nome,
                            c.Documento,
                            c.AreaVendaId?.ToString() ?? "Geral"))
                        .ToList();
                }
            }
        }
        catch
        {
            // Ignora erro de rede/Redis
        }

        return Array.Empty<ClientSummary>();
    }

    private sealed record RedisClientItem(int ClienteIdERP, string Nome, string Documento, int? AreaVendaId);
}
