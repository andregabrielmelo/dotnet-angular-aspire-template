namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// Links an OAuth provider identity (e.g. Google) to a local User. Child entity of User -
/// never looked up on its own outside the owning aggregate, so it isn't an IAggregateRoot.
/// </summary>
public class ExternalLogin(
    UserId userId,
    string provider,
    string providerKey,
    DateTimeOffset linkedAtUtc
) : EntityBase<int>
{
    public UserId UserId { get; private set; } = userId;
    public string Provider { get; private set; } = provider;
    public string ProviderKey { get; private set; } = providerKey;
    public DateTimeOffset LinkedAtUtc { get; private set; } = linkedAtUtc;
}
