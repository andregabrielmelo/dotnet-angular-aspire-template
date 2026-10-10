using Amazon.Runtime;
using Amazon.S3;
using AppTemplate.UseCases.Files;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Files;

public static class FileStorageServiceExtensions
{
    /// <summary>The health check's tag in ServiceDefaults: an optional dependency, never readiness.</summary>
    private const string DependencyTag = "dependency";

    /// <summary>
    /// S3-compatible object storage (ADR 018). Without
    /// <c>FileStorage:ServiceUrl</c> it registers an "unavailable" storage and no health check,
    /// so the app runs without it.
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

        var settings =
            configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
            ?? new FileStorageOptions();
        if (!settings.IsConfigured)
        {
            services.AddSingleton<IFileStorage, UnconfiguredFileStorage>();
            return services;
        }

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
        services.AddSingleton<IFileStorage, S3FileStorage>();
        services
            .AddHealthChecks()
            .AddCheck<FileStorageHealthCheck>(
                "file-storage",
                failureStatus: HealthStatus.Degraded,
                tags: [DependencyTag],
                timeout: TimeSpan.FromSeconds(3)
            );

        return services;
    }
}
