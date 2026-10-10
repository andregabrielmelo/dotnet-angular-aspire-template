using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Files;

/// <summary>
/// Lists at most one key: a cheap, read-only proof that the endpoint, the credentials and the
/// bucket all work. Registered only when storage is configured, with a <c>dependency</c> tag,
/// so an outage reports Degraded on <c>/health/dependencies</c> without failing readiness.
/// </summary>
internal sealed class FileStorageHealthCheck(IAmazonS3 client, IOptions<FileStorageOptions> options)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = options.Value.Bucket, MaxKeys = 1 },
                cancellationToken
            );
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested
            )
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}
