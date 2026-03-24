using Microsoft.Extensions.Configuration;
using Versatus.ForcaVendas.Application.Licenca;
using Npgsql;

namespace Versatus.ForcaVendas.Infrastructure.Data.Repositories;

public sealed class NpgsqlTenantSubscriptionRepository(IConfiguration configuration) : ITenantSubscriptionRepository
{
    public async Task<TenantSubscription?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(tenantId, out var parsedTenantId))
        {
            return null;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT tenant_id, nome_empresa, max_usuarios_simultaneos, ativo
            FROM infra.assinaturas
            WHERE tenant_id = @tenant_id
            LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tenant_id", parsedTenantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TenantSubscription(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetBoolean(3));
    }
}
