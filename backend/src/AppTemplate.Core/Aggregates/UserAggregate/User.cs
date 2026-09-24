namespace AppTemplate.Core.Aggregates.UserAggregate;

/// <summary>
/// The application's own view of a person. Credentials live in the OpenID Connect provider
/// (Keycloak), not here - <see cref="ExternalId"/> links this row to that identity via its
/// <c>sub</c> claim.
/// </summary>
public class User(string externalId, UserName name, EmailAddress email)
    : EntityBase<User, UserId>,
        IAggregateRoot
{
    public const int ExternalIdMaxLength = 64;

    public string ExternalId { get; private set; } = externalId;
    public UserName Name { get; private set; } = name;
    public EmailAddress Email { get; private set; } = email;
    public PhoneNumber? PhoneNumber { get; private set; }

    public static User Create(string externalId, UserName name, EmailAddress email)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id cannot be empty", nameof(externalId));

        if (externalId.Length > ExternalIdMaxLength)
            throw new ArgumentException(
                $"External id must not exceed {ExternalIdMaxLength} characters",
                nameof(externalId)
            );

        return new User(externalId, name, email);
    }

    public User UpdateName(UserName newName)
    {
        if (Name == newName)
        {
            return this;
        }

        Name = newName;
        return this;
    }

    public User UpdatePhoneNumber(PhoneNumber newPhoneNumber)
    {
        PhoneNumber = newPhoneNumber;
        return this;
    }
}
