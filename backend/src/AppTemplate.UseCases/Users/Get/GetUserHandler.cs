using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Get;

public class GetUserHandler(IRepository<User> _repository, ICache _cache)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken
    )
    {
        // Cache-aside: a miss (null) is cached too, so CreateUserHandler invalidates the new id's key
        var dto = await _cache.GetOrCreateAsync<UserDto?>(
            UserCacheKeys.ById(request.UserId),
            async ct =>
            {
                var specification = new UserByIdSpecification(request.UserId);
                var entity = await _repository.FirstOrDefaultAsync(specification, ct);
                return entity == null
                    ? null
                    : new UserDto(
                        entity.Id,
                        entity.Name,
                        entity.PhoneNumber ?? PhoneNumber.Unknown
                    );
            },
            cancellationToken
        );
        if (dto == null)
            return Result.NotFound();

        return dto;
    }
}
