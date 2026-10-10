namespace AppTemplate.SharedKernel;

/// <summary>
/// A message that must reliably cause work after a change commits, such as sending an email.
/// Unlike a domain event (dispatched in-process, best-effort), it's written to the
/// transactional outbox in the same transaction as the change, then delivered at least once
/// by a background relay. See docs/content/reliability-semantics.md.
/// <para>
/// The implementing record is the message contract, serialized with System.Text.Json: keep it
/// primitives-only, mark it with <see cref="IntegrationEventAttribute"/>, and register it with
/// the outbox. A breaking change to its shape is a new version (a new record), never an edit.
/// </para>
/// </summary>
public interface IIntegrationEvent : INotification;

/// <summary>The stable name and version an integration event is stored and looked up under.</summary>
/// <param name="name">Lowercase and dotted, such as <c>user.provisioned</c>. Never renamed once deployed.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventAttribute(string name, int version) : Attribute
{
    public string Name { get; } = name;

    public int Version { get; } = version;

    /// <summary>The key stored in the outbox, such as <c>user.provisioned.v1</c>.</summary>
    public string Key => $"{Name}.v{Version}";
}
