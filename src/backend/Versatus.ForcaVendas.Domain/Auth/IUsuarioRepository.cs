namespace Versatus.ForcaVendas.Domain.Auth;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
