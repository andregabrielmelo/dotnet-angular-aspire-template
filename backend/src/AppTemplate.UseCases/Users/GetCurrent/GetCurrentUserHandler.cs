using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users.GetCurrent;

/// <summary>
/// Looks up the caller's profile by their identity provider id (<c>sub</c>). Read-only:
/// returns NotFound until <see cref="ProvisionCurrent.ProvisionCurrentUserCommand"/> has
/// created it. Called on every page load of the SPA, so it goes through HybridCache.
/// </summary>
public record GetCurrentUserQuery(string ExternalId) : IQuery<Result<CurrentUserDto>>;

public class GetCurrentUserHandler(IRepository<User> _repository, HybridCache _cache)
    : IQueryHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public async ValueTask<Result<CurrentUserDto>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken
    )
    {
        // A miss is cached too (as null), so provisioning must invalidate it - see
        // ProvisionCurrentUserHandler.
        var cachedUser = await _cache.GetOrCreateAsync(
            CachedUser.KeyByExternalId(query.ExternalId),
            (_repository, query.ExternalId),
            static async (state, token) =>
            {
                var (repository, externalId) = state;
                var entity = await repository.FirstOrDefaultAsync(
                    new UserByExternalIdSpecification(externalId),
                    token
                );
                return entity is null ? null : CachedUser.FromEntity(entity);
            },
            CachedUser.EntryOptions,
            CachedUser.Tags,
            cancellationToken
        );

        if (cachedUser is null)
        {
            return Result<CurrentUserDto>.NotFound();
        }

        return new CurrentUserDto(
            UserId.From(cachedUser.Id),
            UserName.From(cachedUser.Name),
            new EmailAddress(cachedUser.Email)
        );
    }
}
