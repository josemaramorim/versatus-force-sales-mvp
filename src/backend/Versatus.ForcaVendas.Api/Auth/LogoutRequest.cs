namespace Versatus.ForcaVendas.Api.Auth;

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }

    public Dictionary<string, string[]> Validate()
    {
        // refresh token is optional on logout; session is always terminated via TenantContext.SessionId
        return new Dictionary<string, string[]>();
    }
}
