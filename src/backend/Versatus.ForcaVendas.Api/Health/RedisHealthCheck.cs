using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Versatus.ForcaVendas.Api.Health;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connection;

    public RedisHealthCheck(IConnectionMultiplexer connection)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _connection.GetDatabase();
            var ping = await db.PingAsync();
            return HealthCheckResult.Healthy($"Pong: {ping.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
