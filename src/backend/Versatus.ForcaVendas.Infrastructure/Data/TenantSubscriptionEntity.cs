namespace Versatus.ForcaVendas.Infrastructure.Data;

public sealed class TenantSubscriptionEntity
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int MaxConcurrentUsers { get; set; }
    public bool IsActive { get; set; }
}
