using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Configurations;

public sealed class RequestTimeoutSettings
{
    public const string SectionName = "RequestTimeouts";

    /// <summary>How long any request may run before its <c>CancellationToken</c> is cancelled.</summary>
    public TimeSpan Default { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>For endpoints that wait on an external service with retries (see <see cref="RequestTimeoutPolicies.ExternalCall"/>).</summary>
    public TimeSpan ExternalCall { get; set; } = TimeSpan.FromSeconds(45);
}

/// <summary>
/// Named policies an endpoint opts into with <c>Options(x => x.WithRequestTimeout(name))</c>
/// when the default doesn't fit.
/// </summary>
public static class RequestTimeoutPolicies
{
    /// <summary>
    /// Longer than the outbound HttpClient's standard resilience total timeout (30 seconds,
    /// retries included), so the request isn't cut off while a retry could still succeed.
    /// </summary>
    public const string ExternalCall = "external-call";
}

/// <summary>
/// A request that runs past its timeout has its <c>HttpContext.RequestAborted</c> token
/// cancelled. EF Core and HttpClient calls observe it and throw, and the client gets a 504
/// problem details response. A timeout doesn't undo anything: a transaction that already
/// committed, or an email already sent, stays done.
/// </summary>
public static class RequestTimeoutConfigurations
{
    public static IServiceCollection AddRequestTimeoutConfigurations(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<RequestTimeoutSettings>()
            .Bind(configuration.GetSection(RequestTimeoutSettings.SectionName))
            .Validate(
                settings =>
                    settings.Default > TimeSpan.Zero && settings.ExternalCall > TimeSpan.Zero,
                "RequestTimeouts:Default and ExternalCall must be positive."
            )
            .ValidateOnStart();

        services.AddRequestTimeouts();
        // Read from the registered settings when the options are built, never at registration.
        services
            .AddOptions<RequestTimeoutOptions>()
            .Configure<IOptions<RequestTimeoutSettings>>(
                (options, registered) =>
                {
                    var settings = registered.Value;
                    options.DefaultPolicy = new RequestTimeoutPolicy
                    {
                        Timeout = settings.Default,
                        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                    };
                    options.AddPolicy(
                        RequestTimeoutPolicies.ExternalCall,
                        new RequestTimeoutPolicy
                        {
                            Timeout = settings.ExternalCall,
                            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                        }
                    );
                }
            );

        return services;
    }
}
