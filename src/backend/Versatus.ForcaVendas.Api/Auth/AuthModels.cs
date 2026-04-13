namespace Versatus.ForcaVendas.Api.Auth;

public sealed record LoginRequest(string Email, string Password)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Email))
        {
            errors["email"] = ["email is required."];
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
    long ExpiresInSeconds,
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
