using AppTemplate.Infrastructure.Jobs;
using AppTemplate.UseCases.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class RecurringJobRunnerTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private async Task RunAsync(string jobId)
    {
        // Like Hangfire: a fresh DI scope per execution.
        await using var scope = factory.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<RecurringJobRunner>()
            .ExecuteAsync(jobId, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_RunsTheDefinitionWithThatId()
    {
        factory.TestRecurringJob.FailWith = null;
        var before = factory.TestRecurringJob.Runs;
        using var runs = factory.CollectMetric<long>("jobs.runs");

        await RunAsync(TestRecurringJobDefinition.Id);

        Assert.Equal(before + 1, factory.TestRecurringJob.Runs);
        AssertOneRun(runs, "succeeded");
    }

    [Fact]
    public async Task ExecuteAsync_RethrowsFailuresSoHangfireCanRetry()
    {
        factory.TestRecurringJob.FailWith = new InvalidOperationException("boom");
        using var runs = factory.CollectMetric<long>("jobs.runs");
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunAsync(TestRecurringJobDefinition.Id)
            );
            Assert.Equal("boom", exception.Message);
            AssertOneRun(runs, "failed");
        }
        finally
        {
            factory.TestRecurringJob.FailWith = null;
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownId_CompletesWithoutRunningAnything()
    {
        var before = factory.TestRecurringJob.Runs;
        using var runs = factory.CollectMetric<long>("jobs.runs");

        await RunAsync("no-such-job");

        Assert.Equal(before, factory.TestRecurringJob.Runs);
        // An unknown id is input from storage: it must never become a metric tag.
        Assert.Empty(runs.GetMeasurementSnapshot());
    }

    private static void AssertOneRun(MetricCollector<long> runs, string outcome)
    {
        var run = Assert.Single(runs.GetMeasurementSnapshot());
        Assert.Equal(1, run.Value);
        Assert.Equal(TestRecurringJobDefinition.Id, run.Tags[ApplicationMetrics.JobTag]);
        Assert.Equal(outcome, run.Tags[ApplicationMetrics.OutcomeTag]);
    }
}
