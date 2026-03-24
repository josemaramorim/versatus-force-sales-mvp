using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Versatus.ForcaVendas.Api.Auth;

public interface IJwtTokenService
{
    TokenPair Generate(DemoUser user);
}

public sealed class JwtTokenService(IOptions<AuthOptions> options) : IJwtTokenService
{
    public TokenPair Generate(DemoUser user)
    {
        var jwtOptions = options.Value.Jwt;
        var now = DateTimeOffset.UtcNow;
        var accessTokenExpiresAt = now.AddMinutes(jwtOptions.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(jwtOptions.RefreshTokenDays);

        var sessionId = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new("tenant_id", user.TenantId),
            new(JwtRegisteredClaimNames.Jti, sessionId)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        return new TokenPair(
            accessToken,
            refreshToken,
            (long)(accessTokenExpiresAt - now).TotalSeconds,
            refreshTokenExpiresAt.UtcDateTime,
            sessionId);
    }

    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes.ToArray());
    }
}

public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    long AccessTokenExpiresInSeconds,
    DateTime RefreshTokenExpiresAtUtc,
    string SessionId);
