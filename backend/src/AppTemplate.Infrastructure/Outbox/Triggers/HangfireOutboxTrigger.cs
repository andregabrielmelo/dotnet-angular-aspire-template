using AppTemplate.Infrastructure.Jobs;
using Hangfire;

namespace AppTemplate.Infrastructure.Outbox.Triggers;

/// <summary>
/// Resolves Hangfire's client only when there's something to deliver: every DbContext gets
/// this trigger through its interceptor, and most never write an outbox message.
/// </summary>
internal sealed class HangfireOutboxTrigger(IServiceProvider services) : IOutboxTrigger
{
    public void MessagesWritten() =>
        services
            .GetRequiredService<IBackgroundJobClient>()
            .Enqueue<ProcessOutboxJob>(
                JobQueues.Critical,
                job => job.ExecuteAsync(CancellationToken.None)
            );
}
