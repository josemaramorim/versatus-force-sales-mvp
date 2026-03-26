using Versatus.ForcaVendas.Domain.Auditoria;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public interface ISessionAuditEventRepository
{
    Task AddAsync(SessionAuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionAuditEvent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionAuditEvent>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionAuditEvent>> GetAllAsync(CancellationToken cancellationToken = default);
}
