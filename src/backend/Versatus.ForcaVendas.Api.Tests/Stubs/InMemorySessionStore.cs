using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Versatus.ForcaVendas.Application.Sessao;

namespace Versatus.ForcaVendas.Api.Tests.Stubs;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private readonly object _lock = new();
    private TimeSpan _sessionTtl = TimeSpan.FromMinutes(20);

    public void SetSessionTtl(TimeSpan ttl)
    {
        _sessionTtl = ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : ttl;
    }

    public void ForceExpire(string sessionId)
    {
        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var s))
            {
                _sessions[sessionId] = s with { LastHeartbeatAt = DateTimeOffset.UtcNow.Subtract(_sessionTtl).Add(TimeSpan.FromSeconds(-1)) };
            }
        }
    }

    public void EvictByTenant(string tenantId)
    {
        lock (_lock)
        {
            var keys = _sessions.Values
                .Where(s => string.Equals(s.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.SessionId)
                .ToList();

            foreach (var key in keys)
            {
                _sessions.Remove(key);
            }
        }
    }

    private void CleanupExpiredUnsafe()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _sessions.Values
            .Where(s => now - s.LastHeartbeatAt > _sessionTtl)
            .Select(s => s.SessionId)
            .ToList();

        foreach (var sessionId in expired)
        {
            _sessions.Remove(sessionId);
        }
    }

    public Task AddAsync(string sessionId, string userId, string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            CleanupExpiredUnsafe();
            _sessions[sessionId] = new SessionInfo(sessionId, userId, tenantId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(string sessionId, string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            CleanupExpiredUnsafe();
            if (_sessions.TryGetValue(sessionId, out var s))
            {
                _sessions[sessionId] = s with { LastHeartbeatAt = DateTimeOffset.UtcNow };
            }
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string sessionId, string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            CleanupExpiredUnsafe();
            _sessions.Remove(sessionId);
        }
        return Task.CompletedTask;
    }

    public Task<int> CountActiveAsync(string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            CleanupExpiredUnsafe();
            return Task.FromResult(_sessions.Values.Count(s => s.TenantId == tenantId));
        }
    }

    public Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            CleanupExpiredUnsafe();
            var list = _sessions.Values.Where(s => s.TenantId == tenantId).ToList();
            return Task.FromResult((IReadOnlyList<SessionInfo>)list);
        }
    }
}
