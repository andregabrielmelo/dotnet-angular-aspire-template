using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Outbox;

/// <summary>
/// Maps stable contract keys (<c>user.provisioned.v1</c>) to the registered event types, both
/// ways. Stored messages name a key, never a CLR type, so a row can't make the relay
/// instantiate an arbitrary type, and renaming a class doesn't strand pending messages.
/// </summary>
public sealed class IntegrationEventRegistry(IOptions<OutboxOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyDictionary<string, Type> _types = options.Value.EventTypes;

    public (string Key, string Payload) Serialize(IIntegrationEvent integrationEvent)
    {
        var type = integrationEvent.GetType();
        var key = type.GetCustomAttribute<IntegrationEventAttribute>()?.Key;
        if (key is null || !_types.TryGetValue(key, out var registered) || registered != type)
        {
            throw new InvalidOperationException(
                $"{type.Name} isn't registered with the outbox. Add it in AddOutbox(options => options.AddEvent<{type.Name}>())."
            );
        }
        return (key, JsonSerializer.Serialize(integrationEvent, type, Json));
    }

    /// <summary>Null for a key nobody registered: the message is dead-lettered unread.</summary>
    public Type? TypeOf(string key) => _types.GetValueOrDefault(key);

    public static IIntegrationEvent Deserialize(string payload, Type type) =>
        (IIntegrationEvent)(
            JsonSerializer.Deserialize(payload, type, Json)
            ?? throw new JsonException("The payload is null.")
        );
}
