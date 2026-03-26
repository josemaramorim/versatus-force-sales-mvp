namespace Versatus.ForcaVendas.Application.Catalogo;

public interface IProductCatalogRepository
{
    Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(
        string tenantId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);
}