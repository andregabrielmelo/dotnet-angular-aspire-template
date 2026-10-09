namespace AppTemplate.UseCases.Users.SendWelcomeEmail;

/// <returns>How many welcome emails were enqueued.</returns>
public sealed record EnqueueMissedWelcomeEmailsCommand : ICommand<Result<int>>;
