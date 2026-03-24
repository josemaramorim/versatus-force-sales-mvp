namespace Versatus.ForcaVendas.Application.Sessao;

public interface ISessionStore
{
    /// <summary>Registers a new active session for a user.</summary>
    Task AddAsync(string sessionId, string userId, string tenantId, CancellationToken cancellationToken);

    /// <summary>Renews the TTL of an existing session (keep-alive).</summary>
    Task HeartbeatAsync(string sessionId, string tenantId, CancellationToken cancellationToken);

    /// <summary>Removes a session (logout or forced eviction).</summary>
    Task RemoveAsync(string sessionId, string tenantId, CancellationToken cancellationToken);

    /// <summary>Returns the number of active (non-expired) sessions for a tenant.</summary>
    Task<int> CountActiveAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>Returns detailed info on all active sessions for a tenant.</summary>
    Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(string tenantId, CancellationToken cancellationToken);
}
