using System;
using System.Threading;
using System.Threading.Tasks;
using Versatus.ForcaVendas.Application.Licenca;

namespace Versatus.ForcaVendas.Api.Stubs;

public sealed class InMemoryTenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly TenantSubscription _default = new(
        TenantId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
        CompanyName: "Demo Corp",
        MaxConcurrentUsers: 4,
        IsActive: true);

    public Task<TenantSubscription?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (string.Equals(tenantId, _default.TenantId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<TenantSubscription?>(_default);
        }

        return Task.FromResult<TenantSubscription?>(null);
    }
}
