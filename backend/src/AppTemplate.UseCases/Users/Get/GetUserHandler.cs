using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.Core.ValueObjects;

namespace AppTemplate.UseCases.Users.Get;

public record GetUserQuery(UserId UserId) : IQuery<Result<UserDto>>;

public class GetUserHandler(IRepository<User> _repository)
    : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async ValueTask<Result<UserDto>> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken
    )
    {
        var specification = new UserByIdSpecification(request.UserId);
        var entity = await _repository.FirstOrDefaultAsync(specification, cancellationToken);
        if (entity == null)
            return Result.NotFound();

        return new UserDto(entity.Id, entity.Name, entity.PhoneNumber ?? PhoneNumber.Unknown);
    }
}
