namespace Versatus.ForcaVendas.Application.Catalogo;

public interface IClientCatalogRepository
{
    Task<IReadOnlyList<ClientSummary>> SearchClientsAsync(
        string tenantId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);
}
