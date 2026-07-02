namespace Versatus.ForcaVendas.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public JwtOptions Jwt { get; init; } = new();
    public List<DemoUser> Users { get; init; } = [];
}

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "versatus-force-sales";
    public string Audience { get; init; } = "versatus-force-sales-clients";
    public string SecretKey { get; init; } = "CHANGE-ME-WITH-A-SECURE-SECRET-AT-LEAST-32-CHARS";
    public int AccessTokenMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 7;
    /// <summary>Inactivity window for the Redis session (heartbeat must renew within this period).</summary>
    public int SessionTimeoutMinutes { get; init; } = 20;
}

public sealed class DemoUser
{
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = "vendedor";
    public string Password { get; init; } = string.Empty;
}
