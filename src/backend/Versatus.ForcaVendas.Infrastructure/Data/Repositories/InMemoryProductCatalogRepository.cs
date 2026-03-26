using Versatus.ForcaVendas.Application.Catalogo;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class InMemoryProductCatalogRepository : IProductCatalogRepository
{
    private static readonly ProductRecord[] Products =
    [
        new("00000000-0000-0000-0000-000000000001", "prod-001", "SKU-CAFE-001", "Cafe Torrado 500g", "UN", 18.90m, 120m),
        new("00000000-0000-0000-0000-000000000001", "prod-002", "SKU-ACUC-001", "Acucar Refinado 1kg", "UN", 6.50m, 85m),
        new("00000000-0000-0000-0000-000000000001", "prod-003", "SKU-LEIT-001", "Leite Integral 1L", "UN", 4.99m, 240m),
        new("00000000-0000-0000-0000-000000000002", "prod-004", "SKU-CHOC-001", "Chocolate em Po 400g", "UN", 9.75m, 60m),
        new("00000000-0000-0000-0000-000000000002", "prod-005", "SKU-BISC-001", "Biscoito Maizena 350g", "UN", 5.25m, 150m)
    ];

    public Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(
        string tenantId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim();
        var filtered = Products
            .Where(product => string.Equals(product.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            .Where(product => string.IsNullOrWhiteSpace(normalizedQuery)
                || product.Sku.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || product.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(product => new ProductSummary(
                product.ProductId,
                product.Sku,
                product.Name,
                product.Unit,
                product.Price,
                product.AvailableStock))
            .ToList();

        return Task.FromResult((IReadOnlyList<ProductSummary>)filtered);
    }

    private sealed record ProductRecord(
        string TenantId,
        string ProductId,
        string Sku,
        string Name,
        string Unit,
        decimal Price,
        decimal AvailableStock);
}