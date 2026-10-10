using System.Reflection;

namespace AppTemplate.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Messages one relay run claims at a time.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// How long a claim lasts. A worker still busy after this loses exclusivity: another may
    /// claim and deliver the same message, which is why handlers must be idempotent.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Delivery attempts before a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Back-off after the first failure; it doubles per attempt up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan FirstRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);

    internal Dictionary<string, Type> EventTypes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Allows <typeparamref name="TEvent"/> into the outbox under its
    /// <see cref="IntegrationEventAttribute"/> key. Only registered keys are ever deserialized.
    /// </summary>
    public OutboxOptions AddEvent<TEvent>()
        where TEvent : IIntegrationEvent
    {
        var contract =
            typeof(TEvent).GetCustomAttribute<IntegrationEventAttribute>()
            ?? throw new InvalidOperationException(
                $"{typeof(TEvent).Name} needs an [IntegrationEvent(name, version)] attribute."
            );
        if (
            !EventTypes.TryAdd(contract.Key, typeof(TEvent))
            && EventTypes[contract.Key] != typeof(TEvent)
        )
        {
            throw new InvalidOperationException(
                $"'{contract.Key}' is registered for both {EventTypes[contract.Key].Name} and {typeof(TEvent).Name}."
            );
        }
        return this;
    }
}
