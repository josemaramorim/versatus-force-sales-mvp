namespace Versatus.ForcaVendas.Domain.Auditoria;

public enum SessionAuditEventType
{
    Login,
    Logout
}

public sealed record SessionAuditEvent(
    string Id,
    string UserId,
    string TenantId,
    SessionAuditEventType EventType,
    DateTimeOffset Timestamp,
    string? IpAddress = null,
    string? UserAgent = null
);