using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Versatus.ForcaVendas.Domain.Auth;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class NpgsqlUsuarioRepository : IUsuarioRepository
{
    private readonly string _connectionString;

    public NpgsqlUsuarioRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
    }

    public async Task<Usuario?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                id AS Id,
                tenant_id AS TenantId,
                username AS Username,
                password_hash AS PasswordHash,
                role AS Role,
                ativo AS Ativo,
                criado_em AS CriadoEm
            FROM usuarios
            WHERE username = @Username AND ativo = true
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var entity = await connection.QueryFirstOrDefaultAsync<UsuarioEntity>(
            new CommandDefinition(sql, new { Username = username }, cancellationToken: cancellationToken));

        if (entity is null)
            return null;

        return new Usuario
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            Role = entity.Role,
            Ativo = entity.Ativo,
            CriadoEm = entity.CriadoEm
        };
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                id AS Id,
                tenant_id AS TenantId,
                username AS Username,
                password_hash AS PasswordHash,
                role AS Role,
                ativo AS Ativo,
                criado_em AS CriadoEm
            FROM usuarios
            WHERE id = @Id AND ativo = true
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var entity = await connection.QueryFirstOrDefaultAsync<UsuarioEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        if (entity is null)
            return null;

        return new Usuario
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            Role = entity.Role,
            Ativo = entity.Ativo,
            CriadoEm = entity.CriadoEm
        };
    }
}
