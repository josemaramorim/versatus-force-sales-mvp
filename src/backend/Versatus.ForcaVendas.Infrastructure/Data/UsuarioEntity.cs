namespace Versatus.ForcaVendas.Infrastructure.Data;

/// <summary>
/// Entidade EF Core mapeada para a tabela usuarios.
/// Separada da entidade de domínio para não poluir o Domain com anotações de ORM.
/// </summary>
public sealed class UsuarioEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "vendedor";
    public bool Ativo { get; set; } = true;
    public DateTimeOffset CriadoEm { get; set; }
}
