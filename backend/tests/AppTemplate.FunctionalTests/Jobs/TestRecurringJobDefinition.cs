using AppTemplate.Infrastructure.Jobs;
using Hangfire;

namespace AppTemplate.FunctionalTests.Jobs;

public sealed class TestRecurringJobProbe
{
    private int _runs;

    public int Runs => Volatile.Read(ref _runs);

    /// <summary>When set, the next runs throw it (to test failure handling).</summary>
    public Exception? FailWith { get; set; }

    /// <summary>When set, each run does this with the run's scoped services (to test what jobs change).</summary>
    public Func<IServiceProvider, Task>? Action { get; set; }

    internal void RecordRun() => Interlocked.Increment(ref _runs);
}

/// <summary>A harmless recurring job registered only in the test host.</summary>
public sealed class TestRecurringJobDefinition(
    TestRecurringJobProbe probe,
    IServiceProvider services
) : IRecurringJobDefinition
{
    public const string Id = "test-recurring-job";

    public string JobId => Id;

    public string CronExpression => Cron.Daily();

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        probe.RecordRun();
        if (probe.FailWith is { } exception)
        {
            throw exception;
        }
        if (probe.Action is { } action)
        {
            await action(services);
        }
    }
}
