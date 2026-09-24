using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace AppTemplate.UseCases.Users.SendWelcomeEmail;

public sealed class WelcomeEmailOptions
{
    public const string SectionName = "WelcomeEmail";

    public string From { get; set; } = "no-reply@apptemplate.local";

    public string Subject { get; set; } = "Welcome to AppTemplate";
}

public sealed record SendWelcomeEmailCommand(UserId UserId) : ICommand<Result>;

/// <summary>
/// Runs as a background job, so it must be idempotent: retries (or a duplicate enqueue) find
/// <see cref="User.WelcomeEmailSentAtUtc"/> already set and do nothing. This is still
/// at-least-once delivery: a crash between sending and saving the marker sends the email again
/// on retry, which is the right trade-off for a welcome email.
/// </summary>
public sealed class SendWelcomeEmailHandler(
    IRepository<User> _repository,
    IEmailSender _emailSender,
    IOptions<WelcomeEmailOptions> _options,
    TimeProvider _timeProvider
) : ICommandHandler<SendWelcomeEmailCommand, Result>
{
    public async ValueTask<Result> Handle(
        SendWelcomeEmailCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _repository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.WelcomeEmailSentAtUtc is not null)
        {
            return Result.Success();
        }

        var options = _options.Value;
        await _emailSender.SendEmailAsync(
            user.Email.Value,
            options.From,
            options.Subject,
            $"Hi {user.Name.Value},\n\nYour account is ready. Welcome aboard!",
            cancellationToken
        );

        user.MarkWelcomeEmailSent(_timeProvider.GetUtcNow());
        await _repository.UpdateAsync(user, cancellationToken);

        return Result.Success();
    }
}
