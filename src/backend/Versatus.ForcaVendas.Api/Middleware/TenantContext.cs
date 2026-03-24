namespace Versatus.ForcaVendas.Api.Middleware;

public interface ITenantContext
{
    string? TenantId { get; }
    bool HasTenant { get; }
}

public sealed class TenantContext : ITenantContext
{
    public string? TenantId { get; set; }
    public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}
