using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.ProvisionCurrent;

/// <param name="Created">False when the user already existed (the command is idempotent).</param>
public record ProvisionCurrentUserResult(CurrentUserDto User, bool Created);

/// <summary>
/// Just-in-time provisioning: registration happens in the OpenID Connect provider (Keycloak),
/// so the domain <see cref="User"/> row is created the first time that identity signs in to
/// the app. Idempotent - calling it for an existing user just returns that user - and safe
/// under concurrency (two tabs, a retry): the unique index on the external id decides the
/// winner, and the loser returns the winner's row instead of failing.
/// </summary>
public record ProvisionCurrentUserCommand(string ExternalId, UserName Name, EmailAddress Email)
    : ICommand<Result<ProvisionCurrentUserResult>>;

public class ProvisionCurrentUserHandler(
    IRepository<User> _repository,
    ICacheInvalidator _cacheInvalidator
) : ICommandHandler<ProvisionCurrentUserCommand, Result<ProvisionCurrentUserResult>>
{
    public async ValueTask<Result<ProvisionCurrentUserResult>> Handle(
        ProvisionCurrentUserCommand command,
        CancellationToken cancellationToken
    )
    {
        // Straight to the database, not the cache: this is a write path, and a cached "no
        // such user" from an earlier GET may not have been invalidated yet.
        var existingUser = await FindByExternalIdAsync(command.ExternalId, cancellationToken);
        if (existingUser is not null)
        {
            return Existing(existingUser);
        }

        // Email is unique in the domain model too - a different identity already claiming this
        // address (e.g. a seed fixture, or an account re-created in Keycloak) is a conflict.
        var emailTaken = await _repository.AnyAsync(
            new UserByEmailSpecification(command.Email),
            cancellationToken
        );
        if (emailTaken)
        {
            return await ResolveConflictAsync(command.ExternalId, cancellationToken);
        }

        User createdUser;
        try
        {
            // Saving raises UserCreatedEvent, which schedules the welcome email.
            createdUser = await _repository.AddAsync(
                User.Create(command.ExternalId, command.Name, command.Email),
                cancellationToken
            );
        }
        catch (UniqueConstraintViolationException)
        {
            // Lost a race: a conflicting row was inserted between the checks above and this
            // insert.
            return await ResolveConflictAsync(command.ExternalId, cancellationToken);
        }

        // Drops the cached "no such user" for this identity, and user lists that lack it.
        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);

        return new ProvisionCurrentUserResult(CurrentUserDto.FromEntity(createdUser), true);
    }

    /// <summary>
    /// The email (or external id) is already taken. If that's by this same identity - another
    /// request provisioning it concurrently, e.g. a second tab, committed after our lookup -
    /// its row is the answer; otherwise the email belongs to someone else.
    /// </summary>
    private async Task<Result<ProvisionCurrentUserResult>> ResolveConflictAsync(
        string externalId,
        CancellationToken cancellationToken
    )
    {
        var winner = await FindByExternalIdAsync(externalId, cancellationToken);
        if (winner is null)
        {
            return EmailConflict();
        }

        await _cacheInvalidator.InvalidateAsync(CacheTags.Users, cancellationToken);
        return Existing(winner);
    }

    private Task<User?> FindByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken
    ) =>
        _repository.FirstOrDefaultAsync(
            new UserByExternalIdSpecification(externalId),
            cancellationToken
        );

    private static Result<ProvisionCurrentUserResult> Existing(User user) =>
        new ProvisionCurrentUserResult(CurrentUserDto.FromEntity(user), false);

    private static Result<ProvisionCurrentUserResult> EmailConflict() =>
        Result<ProvisionCurrentUserResult>.Conflict("Email is already linked to another user");
}
