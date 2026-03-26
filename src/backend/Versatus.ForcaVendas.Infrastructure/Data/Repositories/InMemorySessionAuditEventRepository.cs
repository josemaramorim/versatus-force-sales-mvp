using Versatus.ForcaVendas.Domain.Auditoria;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

/// <summary>
/// In-memory implementation for development/testing. Replace with persistent storage for production.
/// </summary>
public sealed class InMemorySessionAuditEventRepository : ISessionAuditEventRepository
{
    private readonly List<SessionAuditEvent> _events = new();
    private readonly object _lock = new();

    public Task AddAsync(SessionAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _events.Add(auditEvent);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SessionAuditEvent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult((IReadOnlyList<SessionAuditEvent>)_events.Where(e => e.UserId == userId).ToList());
        }
    }

    public Task<IReadOnlyList<SessionAuditEvent>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult((IReadOnlyList<SessionAuditEvent>)_events.Where(e => e.TenantId == tenantId).ToList());
        }
    }

    public Task<IReadOnlyList<SessionAuditEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult((IReadOnlyList<SessionAuditEvent>)_events.ToList());
        }
    }
}
