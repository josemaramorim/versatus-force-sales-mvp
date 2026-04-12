using Microsoft.EntityFrameworkCore;
using Versatus.ForcaVendas.Domain.Auditoria;
using Versatus.ForcaVendas.Infrastructure.Data;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class NpgsqlSessionAuditEventRepository : ISessionAuditEventRepository
{
    private readonly PedidosDbContext _dbContext;

    public NpgsqlSessionAuditEventRepository(PedidosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SessionAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var entity = new SessionAuditEventEntity
        {
            Id = Guid.TryParse(auditEvent.Id, out var id) ? id : Guid.NewGuid(),
            UserId = auditEvent.UserId,
            TenantId = auditEvent.TenantId,
            EventType = auditEvent.EventType,
            Timestamp = auditEvent.Timestamp,
            IpAddress = auditEvent.IpAddress,
            UserAgent = auditEvent.UserAgent
        };

        _dbContext.AuditEvents.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionAuditEvent>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SessionAuditEvent>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SessionAuditEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.AuditEvents
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(1000)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    private static SessionAuditEvent MapToDomain(SessionAuditEventEntity entity)
    {
        return new SessionAuditEvent(
            entity.Id.ToString(),
            entity.UserId,
            entity.TenantId,
            entity.EventType,
            entity.Timestamp,
            entity.IpAddress,
            entity.UserAgent
        );
    }
}
