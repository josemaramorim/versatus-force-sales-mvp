namespace Versatus.ForcaVendas.Application.Catalogo;

public interface IProductCatalogRepository
{
    Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(
    CatalogSearchRequest request,
        CancellationToken cancellationToken = default);
}