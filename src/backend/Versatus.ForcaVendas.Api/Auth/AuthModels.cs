namespace Versatus.ForcaVendas.Api.Auth;

public sealed record LoginRequest(string TenantId, string Username, string Password)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(TenantId))
        {
            errors["tenantId"] = ["tenantId is required."];
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            errors["username"] = ["username is required."];
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            errors["password"] = ["password is required."];
        }

        return errors;
    }
}

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    long ExpiresIn,
    string TokenType);

public sealed record RefreshTokenRequest(string RefreshToken)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(RefreshToken))
        {
            errors["refreshToken"] = ["refreshToken is required."];
        }

        return errors;
    }
}
