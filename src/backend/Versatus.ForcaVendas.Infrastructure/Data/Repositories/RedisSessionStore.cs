using StackExchange.Redis;
using Versatus.ForcaVendas.Application.Sessao;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

/// <summary>
/// Redis-backed session store.
///
/// Redis structures:
///   Hash  "session:{sessionId}"                  — fields: userId, tenantId, loginAt (ms), lastHeartbeatAt (ms); TTL = SessionTtlSeconds
///   ZSet  "tenant:{tenantId}:sessions"            — score = expireAt (Unix seconds), member = sessionId
///
/// The sorted set lets us count/list active sessions in O(log N) and prune
/// expired entries without a separate background job.
/// </summary>
public sealed class RedisSessionStore(IConnectionMultiplexer redis) : ISessionStore
{
    private const int SessionTtlSeconds = 120;

    public async Task AddAsync(string sessionId, string userId, string tenantId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var now = DateTimeOffset.UtcNow;
        var expireAt = now.AddSeconds(SessionTtlSeconds);

        var hashKey = $"session:{sessionId}";
        await db.HashSetAsync(hashKey,
        [
            new HashEntry("userId",           userId),
            new HashEntry("tenantId",         tenantId),
            new HashEntry("loginAt",          now.ToUnixTimeMilliseconds()),
            new HashEntry("lastHeartbeatAt",  now.ToUnixTimeMilliseconds()),
        ]);
        await db.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(SessionTtlSeconds));
        await db.SortedSetAddAsync($"tenant:{tenantId}:sessions", sessionId, expireAt.ToUnixTimeSeconds());
    }

    public async Task HeartbeatAsync(string sessionId, string tenantId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var now = DateTimeOffset.UtcNow;
        var expireAt = now.AddSeconds(SessionTtlSeconds);

        var hashKey = $"session:{sessionId}";
        await db.HashSetAsync(hashKey, "lastHeartbeatAt", now.ToUnixTimeMilliseconds());
        await db.KeyExpireAsync(hashKey, TimeSpan.FromSeconds(SessionTtlSeconds));
        await db.SortedSetAddAsync($"tenant:{tenantId}:sessions", sessionId, expireAt.ToUnixTimeSeconds());
    }

    public async Task RemoveAsync(string sessionId, string tenantId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        await db.SortedSetRemoveAsync($"tenant:{tenantId}:sessions", sessionId);
        await db.KeyDeleteAsync($"session:{sessionId}");
    }

    public async Task<int> CountActiveAsync(string tenantId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var setKey = $"tenant:{tenantId}:sessions";

        // Purge entries whose score (expireAt) is in the past.
        await db.SortedSetRemoveRangeByScoreAsync(setKey, double.NegativeInfinity, nowUnix - 1);
        var count = await db.SortedSetLengthAsync(setKey);
        return (int)count;
    }

    public async Task<IReadOnlyList<SessionInfo>> GetActiveSessionsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var setKey = $"tenant:{tenantId}:sessions";

        await db.SortedSetRemoveRangeByScoreAsync(setKey, double.NegativeInfinity, nowUnix - 1);
        var sessionIds = await db.SortedSetRangeByScoreAsync(setKey, nowUnix, double.PositiveInfinity);

        var sessions = new List<SessionInfo>(sessionIds.Length);
        foreach (var id in sessionIds)
        {
            var sessionIdStr = (string?)id;
            if (string.IsNullOrEmpty(sessionIdStr)) continue;

            var fields = await db.HashGetAllAsync($"session:{sessionIdStr}");
            if (fields.Length == 0) continue;

            var dict = fields.ToDictionary(f => (string)f.Name!, f => (string?)f.Value);

            if (!dict.TryGetValue("userId", out var userId) || userId is null) continue;
            if (!dict.TryGetValue("loginAt", out var loginAtStr)
                || !long.TryParse(loginAtStr, out var loginAtMs)) continue;
            if (!dict.TryGetValue("lastHeartbeatAt", out var heartbeatStr)
                || !long.TryParse(heartbeatStr, out var heartbeatMs)) continue;

            sessions.Add(new SessionInfo(
                SessionId:       sessionIdStr,
                UserId:          userId,
                TenantId:        tenantId,
                LoginAt:         DateTimeOffset.FromUnixTimeMilliseconds(loginAtMs),
                LastHeartbeatAt: DateTimeOffset.FromUnixTimeMilliseconds(heartbeatMs)));
        }

        return sessions;
    }
}
