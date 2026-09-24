using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using Microsoft.Extensions.Caching.Hybrid;

namespace AppTemplate.UseCases.Users.Get;

public record GetUserQuery(UserId UserId) : IQuery<Result<UserDto>>;

/// <summary>
/// Cache-aside through HybridCache: concurrent misses for the same user share one database
/// call (stampede protection), and "not found" is cached too, until the next user write
/// invalidates <see cref="Caching.CacheTags.Users"/>.
/// </summary>
public class GetUserHandler(IRepository<User> _repository, HybridCache _cache)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken
    )
    {
        var cached = await _cache.GetOrCreateAsync(
            CachedUser.KeyById(request.UserId),
            (_repository, request.UserId),
            static async (state, token) =>
            {
                var (repository, userId) = state;
                var entity = await repository.FirstOrDefaultAsync(
                    new UserByIdSpecification(userId),
                    token
                );
                return entity is null ? null : CachedUser.FromEntity(entity);
            },
            CachedUser.EntryOptions,
            CachedUser.Tags,
            cancellationToken
        );

        return cached is null ? Result.NotFound() : cached.ToUserDto();
    }
}
