using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.Aggregates.UserAggregate.Specifications;
using AppTemplate.UseCases.Jobs;

namespace AppTemplate.UseCases.Users.SendWelcomeEmail;

/// <returns>How many welcome emails were enqueued.</returns>
public sealed record EnqueueMissedWelcomeEmailsCommand : ICommand<Result<int>>;

/// <summary>
/// Catches welcome emails whose enqueue was lost. Provisioning saves the user and then enqueues
/// the email on Hangfire's own connection, so a crash or a storage error in between leaves a
/// user who never gets one. This finds users still without the email once the original job
/// would have finished, including its retries, and enqueues it again.
/// <see cref="SendWelcomeEmailHandler"/> is idempotent, so a duplicate enqueue doesn't resend.
/// </summary>
public sealed class EnqueueMissedWelcomeEmailsHandler(
    IRepository<User> _repository,
    IBackgroundJobScheduler _jobs,
    TimeProvider _timeProvider
) : ICommandHandler<EnqueueMissedWelcomeEmailsCommand, Result<int>>
{
    /// <summary>
    /// Longer than the welcome email job's whole retry back-off (about 1 h 43 min), so the
    /// sweep doesn't race a job that is still retrying.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(2);

    /// <summary>Users older than this are left alone rather than retried forever.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>At most this many per run; the rest are picked up by the next run.</summary>
    public const int BatchSize = 500;

    public async ValueTask<Result<int>> Handle(
        EnqueueMissedWelcomeEmailsCommand command,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNow();
        var userIds = await _repository.ListAsync(
            new UsersAwaitingWelcomeEmailSpecification(
                createdAfter: now - MaxAge,
                createdBefore: now - GracePeriod,
                take: BatchSize
            ),
            cancellationToken
        );

        foreach (var userId in userIds)
        {
            _jobs.EnqueueWelcomeEmail(userId);
        }

        return userIds.Count;
    }
}
