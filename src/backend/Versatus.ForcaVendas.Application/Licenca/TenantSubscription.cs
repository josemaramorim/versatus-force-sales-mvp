namespace Versatus.ForcaVendas.Application.Licenca;

public sealed record TenantSubscription(
    Guid TenantId,
    string CompanyName,
    int MaxConcurrentUsers,
    bool IsActive);
