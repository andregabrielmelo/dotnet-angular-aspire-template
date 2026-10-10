using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.Avatar.Set;

/// <param name="Upload">The uploaded bytes, already capped at <c>AvatarLimits.MaxBytes</c> by the endpoint.</param>
public sealed record SetAvatarCommand(UserId UserId, Stream Upload) : ICommand<Result>;
