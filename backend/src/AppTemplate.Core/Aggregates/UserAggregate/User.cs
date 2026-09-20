using AppTemplate.Core.ValueObjects;

namespace AppTemplate.Core.Aggregates.UserAggregate;

public class User(UserName name, EmailAddress email, string? password)
    : EntityBase<User, UserId>,
        IAggregateRoot
{
    public UserName Name { get; private set; } = name;
    public EmailAddress Email { get; private set; } = email;

    /// <summary>Null for an account that only ever signed in via an external provider.</summary>
    public string? Password { get; private set; } = password;
    public PhoneNumber? PhoneNumber { get; private set; }
    public bool EmailConfirmed { get; private set; }

    private readonly List<ExternalLogin> _externalLogins = [];
    public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins;

    private readonly List<UserToken> _tokens = [];
    public IReadOnlyCollection<UserToken> Tokens => _tokens;

    private readonly List<RefreshToken> _refreshTokens = [];
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    public static User Create(UserName name, EmailAddress email, string password) =>
        new User(name, email, password);

    /// <summary>An OAuth-only account: no password, and the provider already verified the email.</summary>
    public static User CreateExternal(UserName name, EmailAddress email) =>
        new User(name, email, password: null) { EmailConfirmed = true };

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

    public User UpdatePassword(string newPassword)
    {
        Password = newPassword;
        return this;
    }

    public void ConfirmEmail() => EmailConfirmed = true;

    public ExternalLogin AddExternalLogin(string provider, string providerKey)
    {
        var login = new ExternalLogin(Id, provider, providerKey, DateTimeOffset.UtcNow);
        _externalLogins.Add(login);
        return login;
    }

    public UserToken IssueToken(
        UserTokenPurpose purpose,
        string tokenHash,
        DateTimeOffset expiresAtUtc
    )
    {
        var token = new UserToken(Id, purpose, tokenHash, expiresAtUtc);
        _tokens.Add(token);
        return token;
    }

    public void ConsumeToken(UserToken token) => token.MarkConsumed(DateTimeOffset.UtcNow);

    public RefreshToken AddRefreshToken(string tokenHash, DateTimeOffset expiresAtUtc)
    {
        var token = new RefreshToken(Id, tokenHash, expiresAtUtc, DateTimeOffset.UtcNow);
        _refreshTokens.Add(token);
        return token;
    }

    public void RevokeRefreshToken(RefreshToken token, string? replacedByTokenHash = null) =>
        token.Revoke(DateTimeOffset.UtcNow, replacedByTokenHash);

    /// <summary>Kills every other outstanding session - called on password change/reset.</summary>
    public void RevokeAllRefreshTokens()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var token in _refreshTokens.Where(t => t.IsActive(now)))
        {
            token.Revoke(now);
        }
    }
}
