using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Avatar.Delete;

public sealed record DeleteAvatarCommand(UserId UserId) : ICommand<Result>;
