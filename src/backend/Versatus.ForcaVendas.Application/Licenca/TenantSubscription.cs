namespace Versatus.ForcaVendas.Application.Licenca;

public sealed record TenantSubscription(
    Guid TenantId,
    string CompanyName,
    int MaxConcurrentUsers,
    bool IsActive)
{
    /// <summary>Returns true when the tenant is active and has room for at least one more concurrent user.</summary>
    public bool HasAvailableSeats(int activeSessions) => IsActive && activeSessions < MaxConcurrentUsers;
}
