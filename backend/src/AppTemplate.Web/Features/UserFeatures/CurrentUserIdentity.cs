using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Users;

namespace AppTemplate.Web.Features.UserFeatures;

/// <param name="Permissions">What the caller may do - lets clients show or hide actions. The API still enforces every permission itself.</param>
public sealed record CurrentUserResponse(
    int Id,
    string Name,
    string Email,
    IReadOnlyList<string> Permissions
)
{
    public static CurrentUserResponse From(CurrentUserDto user, IEnumerable<string> permissions) =>
        new(
            user.Id.Value,
            user.Name.Value,
            user.Email.Value,
            permissions.Order(StringComparer.Ordinal).ToList()
        );
}

/// <summary>The caller's identity as the access token describes it (Keycloak claims).</summary>
public sealed record CurrentUserIdentity(string ExternalId, UserName Name, EmailAddress Email)
{
    public const string IncompleteTitle = "Incomplete identity";

    public static string? ReadExternalId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(sub) ? null : sub;
    }

    /// <summary>
    /// Reads <c>sub</c>, <c>email</c> and the first usable name (<c>name</c>, then
    /// <c>preferred_username</c>, then the email itself). False if any is missing.
    /// </summary>
    public static bool TryRead(
        ClaimsPrincipal principal,
        [NotNullWhen(true)] out CurrentUserIdentity? identity
    )
    {
        identity = null;
        var externalId = ReadExternalId(principal);
        var email = principal.FindFirst("email")?.Value;
        var name = FirstValidUserName(
            principal.FindFirst("name")?.Value,
            principal.FindFirst("preferred_username")?.Value,
            email
        );

        if (externalId is null || string.IsNullOrWhiteSpace(email) || name is null)
        {
            return false;
        }

        identity = new CurrentUserIdentity(externalId, name.Value, new EmailAddress(email));
        return true;
    }

    private static UserName? FirstValidUserName(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is not null && UserName.TryFrom(candidate, out var userName))
            {
                return userName;
            }
        }

        return null;
    }
}
