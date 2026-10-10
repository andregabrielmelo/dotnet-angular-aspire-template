using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Events;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;

namespace AppTemplate.UseCases.Users.SendWelcomeEmail;

/// <summary>
/// Delivered by the outbox relay, at least once. <see cref="SendWelcomeEmailHandler"/> checks
/// <see cref="User.WelcomeEmailSentAtUtc"/> first, so a redelivery doesn't email twice. A
/// failure throws, so the outbox retries it with back-off.
/// </summary>
public sealed class SendWelcomeEmailWhenUserProvisioned(
    IRepository<User> _repository,
    IMediator _mediator
) : INotificationHandler<UserProvisioned>
{
    public async ValueTask Handle(UserProvisioned notification, CancellationToken cancellationToken)
    {
        var user = await _repository.FirstOrDefaultAsync(
            new UserByExternalIdSpecification(notification.ExternalId),
            cancellationToken
        );
        if (user is null)
        {
            return; // deleted before delivery: nobody to welcome
        }

        var result = await _mediator.Send(new SendWelcomeEmailCommand(user.Id), cancellationToken);
        if (!result.IsSuccess && result.Status != ResultStatus.NotFound)
        {
            throw new InvalidOperationException(
                $"Sending the welcome email failed: {string.Join("; ", result.Errors)}"
            );
        }
    }
}
