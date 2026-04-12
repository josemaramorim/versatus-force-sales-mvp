using Versatus.ForcaVendas.Domain.Auditoria;

namespace Versatus.ForcaVendas.Infrastructure.Data;

/// <summary>
/// Entidade EF Core mapeada para a tabela audit_events.
/// </summary>
public sealed class SessionAuditEventEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public SessionAuditEventType EventType { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
