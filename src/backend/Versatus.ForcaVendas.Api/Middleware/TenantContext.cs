namespace Versatus.ForcaVendas.Api.Middleware;

public interface ITenantContext
{
    string? TenantId { get; }
    string? SessionId { get; }
    string? UserId { get; }
    bool HasTenant { get; }
}

public sealed class TenantContext : ITenantContext
{
    public string? TenantId { get; set; }
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
}
