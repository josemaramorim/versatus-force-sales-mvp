namespace Versatus.ForcaVendas.Application.Licenca;

public interface ITenantSubscriptionRepository
{
    Task<TenantSubscription?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
}
