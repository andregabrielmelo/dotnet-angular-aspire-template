using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Caching;

namespace AppTemplate.UseCases.Users.Delete;

public record DeleteUserCommand(UserId UserId) : Mediator.ICommand<Result>;

public class DeleteUserHandler(IRepository<User> _repository, ICache _cache)
    : Mediator.ICommandHandler<DeleteUserCommand, Result>
{
    public async ValueTask<Result> Handle(
        DeleteUserCommand request,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return Result.NotFound();

        await _repository.DeleteAsync(user, cancellationToken);
        await _cache.RemoveAsync(UserCacheKeys.ById(user.Id), cancellationToken);
        return Result.Success();
    }
}
