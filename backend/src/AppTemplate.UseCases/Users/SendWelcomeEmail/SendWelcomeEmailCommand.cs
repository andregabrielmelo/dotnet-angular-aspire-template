using AppTemplate.Core.Aggregates.UserAggregate;

namespace AppTemplate.UseCases.Users.SendWelcomeEmail;

public sealed record SendWelcomeEmailCommand(UserId UserId) : ICommand<Result>;
