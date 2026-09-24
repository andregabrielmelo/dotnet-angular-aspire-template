using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.SyncProfiles;

/// <returns>How many users were updated.</returns>
public sealed record SyncUserProfilesCommand : ICommand<Result<int>>;

/// <summary>
/// Users edit their name and email in Keycloak's account console, and the domain copy only
/// learns about it here. Runs as a recurring background job, and is safe to repeat or retry:
/// it only ever copies the provider's current values and never deletes anything.
/// </summary>
public sealed class SyncUserProfilesHandler(
    IIdentityProviderDirectory _directory,
    IRepository<User> _repository,
    ICacheInvalidator _cacheInvalidator
) : ICommandHandler<SyncUserProfilesCommand, Result<int>>
{
    public const int BatchSize = 100;

    public async ValueTask<Result<int>> Handle(
        SyncUserProfilesCommand command,
        CancellationToken cancellationToken
    )
    {
        var updated = 0;
        var batch = new List<IdentityProviderUser>(BatchSize);

        await foreach (var identity in _directory.ListUsersAsync(cancellationToken))
        {
            batch.Add(identity);
            if (batch.Count == BatchSize)
            {
                updated += await SyncBatchAsync(batch, cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            updated += await SyncBatchAsync(batch, cancellationToken);
        }

        if (updated > 0)
        {
            await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);
        }

        return updated;
    }

    private async Task<int> SyncBatchAsync(
        IReadOnlyList<IdentityProviderUser> identities,
        CancellationToken cancellationToken
    )
    {
        var users = await _repository.ListAsync(
            new UsersByExternalIdsSpecification(identities.Select(i => i.Id).ToList()),
            cancellationToken
        );
        var usersByExternalId = users.ToDictionary(u => u.ExternalId, StringComparer.Ordinal);

        var changed = new List<User>();
        foreach (var identity in identities)
        {
            // Not provisioned yet (never called the API) - nothing to sync.
            if (!usersByExternalId.TryGetValue(identity.Id, out var user))
            {
                continue;
            }

            var isChanged = false;

            if (
                identity.Name is not null
                && UserName.TryFrom(identity.Name, out var name)
                && name != user.Name
            )
            {
                user.UpdateName(name);
                isChanged = true;
            }

            if (
                TryParseEmail(identity.Email, out var email)
                && email != user.Email
                // The email is unique: never take one another user already has.
                && !await _repository.AnyAsync(
                    new UserByEmailSpecification(email),
                    cancellationToken
                )
            )
            {
                user.UpdateEmail(email);
                isChanged = true;
            }

            if (isChanged)
            {
                changed.Add(user);
            }
        }

        if (changed.Count > 0)
        {
            await _repository.UpdateRangeAsync(changed, cancellationToken);
        }

        return changed.Count;
    }

    private static bool TryParseEmail(string? value, out EmailAddress email)
    {
        email = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            email = new EmailAddress(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
