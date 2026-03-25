namespace Versatus.ForcaVendas.Api.Auth;

public sealed class EvictRequest
{
    public string? SessionId { get; set; }
    public string? RefreshToken { get; set; }
}
