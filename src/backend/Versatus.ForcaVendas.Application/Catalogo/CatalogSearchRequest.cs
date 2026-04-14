namespace Versatus.ForcaVendas.Application.Catalogo;

public sealed record CatalogSearchRequest(
    string TenantId,
    string? Query,
    int Limit);
