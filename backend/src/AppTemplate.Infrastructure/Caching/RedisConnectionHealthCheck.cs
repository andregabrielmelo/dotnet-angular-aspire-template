using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace AppTemplate.Infrastructure.Caching;

/// <summary>
/// Degraded, never Unhealthy, when Redis is unreachable: it's an optional dependency. Reads
/// the client's connection state and pings only when connected, so the probe itself never
/// waits out a timeout.
/// </summary>
internal sealed class RedisConnectionHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (!redis.IsConnected)
        {
            return new HealthCheckResult(context.Registration.FailureStatus);
        }

        try
        {
            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}
