using System.Collections.Concurrent;

namespace Versatus.ForcaVendas.Api.Auth;

public interface IRefreshTokenStore
{
    void Save(string refreshToken, string userId, string tenantId, DateTime expiresAtUtc);
}

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenInfo> _tokens = new();

    public void Save(string refreshToken, string userId, string tenantId, DateTime expiresAtUtc)
    {
        _tokens[refreshToken] = new RefreshTokenInfo(userId, tenantId, expiresAtUtc);
    }

    private sealed record RefreshTokenInfo(string UserId, string TenantId, DateTime ExpiresAtUtc);
}
