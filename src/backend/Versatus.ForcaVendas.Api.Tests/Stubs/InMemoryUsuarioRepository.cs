using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Versatus.ForcaVendas.Domain.Auth;

namespace Versatus.ForcaVendas.Api.Tests.Stubs;

/// <summary>
/// Stub em memória de IUsuarioRepository para testes de integração.
/// Suporta lookup por username, email e id.
/// </summary>
public sealed class InMemoryUsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _usuarios;

    public InMemoryUsuarioRepository()
    {
        var seededPasswordHash = BCrypt.Net.BCrypt.HashPassword("Mudar@!123");

        _usuarios = new List<Usuario>
        {
            new()
            {
                Id = Guid.Parse("7c90e66f-0af5-4ded-90e2-0df0a0b2d001"),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "admin",
                Email = "admin@demo1.versatus.com",
                PasswordHash = seededPasswordHash,
                Role = "admin",
                Ativo = true,
                CriadoEm = new DateTimeOffset(2026, 4, 12, 16, 7, 19, TimeSpan.Zero)
            },
            new()
            {
                Id = Guid.Parse("7c90e66f-0af5-4ded-90e2-0df0a0b2d002"),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Username = "gestor",
                Email = "gestor@demo2.versatus.com",
                PasswordHash = seededPasswordHash,
                Role = "gestor",
                Ativo = true,
                CriadoEm = new DateTimeOffset(2026, 4, 12, 16, 7, 19, TimeSpan.Zero).AddTicks(10)
            }
        };
    }

    private InMemoryUsuarioRepository(IEnumerable<Usuario> usuarios)
    {
        _usuarios = new List<Usuario>(usuarios);
    }

    public static InMemoryUsuarioRepository FromUsers(IEnumerable<Usuario> usuarios)
        => new(usuarios);

    public Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = _usuarios.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) && u.Ativo);
        return Task.FromResult(user);
    }

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = _usuarios.FirstOrDefault(u => u.Id == id && u.Ativo);
        return Task.FromResult(user);
    }

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = _usuarios.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) && u.Ativo);
        return Task.FromResult(user);
    }
}
