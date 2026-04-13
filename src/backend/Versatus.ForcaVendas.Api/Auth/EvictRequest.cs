namespace Versatus.ForcaVendas.Api.Auth;

public sealed class EvictRequest
{
    public string? SessionId { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserId { get; set; }

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(SessionId))
        {
            errors["sessionId"] = ["sessionId is required."];
        }

        return errors;
    }
}
