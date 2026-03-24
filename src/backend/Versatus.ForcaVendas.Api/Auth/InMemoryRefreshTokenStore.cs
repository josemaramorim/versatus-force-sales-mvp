using System.Collections.Concurrent;

namespace Versatus.ForcaVendas.Api.Auth;

public sealed record RefreshTokenInfo(string UserId, string TenantId, DateTime ExpiresAtUtc);

public interface IRefreshTokenStore
{
    void Save(string refreshToken, string userId, string tenantId, DateTime expiresAtUtc);
    bool TryGetActive(string refreshToken, out RefreshTokenInfo tokenInfo);
    void Revoke(string refreshToken);
}

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenInfo> _tokens = new();

    public void Save(string refreshToken, string userId, string tenantId, DateTime expiresAtUtc)
    {
        _tokens[refreshToken] = new RefreshTokenInfo(userId, tenantId, expiresAtUtc);
    }

    public bool TryGetActive(string refreshToken, out RefreshTokenInfo tokenInfo)
    {
        if (!_tokens.TryGetValue(refreshToken, out var currentTokenInfo))
        {
            tokenInfo = default!;
            return false;
        }

        if (currentTokenInfo.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _tokens.TryRemove(refreshToken, out _);
            tokenInfo = default!;
            return false;
        }

        tokenInfo = currentTokenInfo;
        return true;
    }

    public void Revoke(string refreshToken)
    {
        _tokens.TryRemove(refreshToken, out _);
    }

}
