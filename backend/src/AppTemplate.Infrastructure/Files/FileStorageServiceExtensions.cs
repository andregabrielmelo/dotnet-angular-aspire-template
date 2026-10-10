using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Files;

public static class FileStorageServiceExtensions
{
    /// <summary>The health check's tag in ServiceDefaults: an optional dependency, never readiness.</summary>
    private const string DependencyTag = "dependency";

    /// <summary>
    /// S3-compatible object storage (ADR 018), an optional feature: without
    /// <c>FileStorage:ServiceUrl</c> storage is off (an "unavailable" storage, no health check)
    /// and the app runs without it. With it, every setting is validated at startup, so a
    /// partial configuration fails instead of silently turning storage off.
    /// </summary>
    public static IServiceCollection AddFileStorage(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options =>
                    !options.IsConfigured
                    || (
                        Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out _)
                        && !string.IsNullOrWhiteSpace(options.AccessKey)
                        && !string.IsNullOrWhiteSpace(options.SecretKey)
                    ),
                "FileStorage needs an absolute ServiceUrl, an AccessKey and a SecretKey."
            )
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = options.ServiceUrl,
                    AuthenticationRegion = options.Region,
                    ForcePathStyle = true,
                    Timeout = options.Timeout,
                    MaxErrorRetry = 2,
                    // Only what S3 requires: S3-compatible services differ in which of the
                    // newer flexible checksums they accept.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                }
            );
        });
        // Whether storage is on is decided from the registered options when first resolved,
        // never from configuration read at registration. The S3 client is created only then.
        services.AddSingleton<IFileStorage>(provider =>
            provider.GetRequiredService<IOptions<FileStorageOptions>>().Value.IsConfigured
                ? ActivatorUtilities.CreateInstance<S3FileStorage>(provider)
                : new UnconfiguredFileStorage()
        );
        services.AddHealthChecks();
        services
            .AddOptions<HealthCheckServiceOptions>()
            .Configure<IOptions<FileStorageOptions>>(
                (health, storage) =>
                {
                    if (storage.Value.IsConfigured)
                    {
                        health.Registrations.Add(
                            new HealthCheckRegistration(
                                "file-storage",
                                provider =>
                                    ActivatorUtilities.CreateInstance<FileStorageHealthCheck>(
                                        provider
                                    ),
                                HealthStatus.Degraded,
                                [DependencyTag],
                                TimeSpan.FromSeconds(3)
                            )
                        );
                    }
                }
            );

        return services;
    }
}
