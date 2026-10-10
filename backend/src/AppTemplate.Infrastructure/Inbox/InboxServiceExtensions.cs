using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppTemplate.Infrastructure.Inbox;

public static class InboxServiceExtensions
{
    /// <summary>
    /// The inbox (ADR 016): deduplicates message consumption per consumer. The outbox relay
    /// uses it today; any future message consumer (a broker subscription, a webhook) can too.
    /// </summary>
    public static IServiceCollection AddInbox(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<InboxStore>();
        return services;
    }
}
