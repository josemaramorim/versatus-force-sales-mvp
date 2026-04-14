namespace Versatus.ForcaVendas.Application.Catalogo;

public interface IClientCatalogRepository
{
    Task<IReadOnlyList<ClientSummary>> SearchClientsAsync(
    CatalogSearchRequest request,
        CancellationToken cancellationToken = default);
}
