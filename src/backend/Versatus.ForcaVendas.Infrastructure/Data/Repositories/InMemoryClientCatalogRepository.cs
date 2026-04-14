using Versatus.ForcaVendas.Application.Catalogo;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class InMemoryClientCatalogRepository : IClientCatalogRepository
{
    private static readonly ClientRecord[] Clients =
    [
        new("00000000-0000-0000-0000-000000000001", "cli-001", "Supermercado Bom Preço", "12.345.678/0001-90", "Centro"),
        new("00000000-0000-0000-0000-000000000001", "cli-002", "Atacado Expresso Ltda", "98.765.432/0001-21", "Zona Norte"),
        new("00000000-0000-0000-0000-000000000001", "cli-003", "Mercearia São João", "11.222.333/0001-44", "Bairro Novo"),
        new("00000000-0000-0000-0000-000000000001", "cli-004", "Padaria Central", "33.444.555/0001-77", "Centro"),
        new("00000000-0000-0000-0000-000000000002", "cli-005", "Distribuidora Sul", "44.555.666/0001-88", "Sul"),
    ];

    public Task<IReadOnlyList<ClientSummary>> SearchClientsAsync(
        CatalogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = request.Query?.Trim() ?? string.Empty;

        var filtered = Clients
            .Where(c => string.Equals(c.TenantId, request.TenantId, StringComparison.OrdinalIgnoreCase))
            .Where(c => string.IsNullOrWhiteSpace(normalizedQuery)
                || c.Nome.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || c.Documento.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(request.Limit)
            .Select(c => new ClientSummary(c.ClientId, c.Nome, c.Documento, c.AreaVenda))
            .ToList();

        return Task.FromResult((IReadOnlyList<ClientSummary>)filtered);
    }

    private sealed record ClientRecord(string TenantId, string ClientId, string Nome, string Documento, string? AreaVenda);
}
