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

    public Task AddAsync(string sessionId, string userId, string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _sessions[sessionId] = new SessionInfo(sessionId, userId, tenantId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
        return Task.CompletedTask;
    }

    public Task HeartbeatAsync(string sessionId, string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
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
            _sessions.Remove(sessionId);
        }
        return Task.CompletedTask;
    }

    public Task<int> CountActiveAsync(string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_sessions.Values.Count(s => s.TenantId == tenantId));
        }
    }

    public Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(string tenantId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var list = _sessions.Values.Where(s => s.TenantId == tenantId).ToList();
            return Task.FromResult((IReadOnlyList<SessionInfo>)list);
        }
    }
}
