namespace Versatus.ForcaVendas.Domain.Auth;

public sealed class Usuario
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = "vendedor";
    public bool Ativo { get; init; } = true;
    public DateTimeOffset CriadoEm { get; init; }
}
