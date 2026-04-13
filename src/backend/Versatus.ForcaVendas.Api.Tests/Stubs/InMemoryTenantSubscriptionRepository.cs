using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Versatus.ForcaVendas.Application.Licenca;

namespace Versatus.ForcaVendas.Api.Tests.Stubs;

public sealed class InMemoryTenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly Dictionary<string, TenantSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["00000000-0000-0000-0000-000000000001"] = new TenantSubscription(
            TenantId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CompanyName: "Demo Corp",
            MaxConcurrentUsers: 4,
            IsActive: true)
    };

    public void ConfigureTenant(string tenantId, int maxConcurrentUsers, bool isActive = true, string? companyName = null)
    {
        if (!Guid.TryParse(tenantId, out var parsed))
        {
            return;
        }

        _subscriptions[tenantId] = new TenantSubscription(
            TenantId: parsed,
            CompanyName: string.IsNullOrWhiteSpace(companyName) ? "Configured Tenant" : companyName,
            MaxConcurrentUsers: maxConcurrentUsers,
            IsActive: isActive);
    }

    public Task<TenantSubscription?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (_subscriptions.TryGetValue(tenantId, out var subscription))
        {
            return Task.FromResult<TenantSubscription?>(subscription);
        }

        return Task.FromResult<TenantSubscription?>(null);
    }
}
