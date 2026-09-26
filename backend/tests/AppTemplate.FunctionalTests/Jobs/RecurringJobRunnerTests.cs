using AppTemplate.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

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

        await RunAsync(TestRecurringJobDefinition.Id);

        Assert.Equal(before + 1, factory.TestRecurringJob.Runs);
    }

    [Fact]
    public async Task ExecuteAsync_RethrowsFailuresSoHangfireCanRetry()
    {
        factory.TestRecurringJob.FailWith = new InvalidOperationException("boom");
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunAsync(TestRecurringJobDefinition.Id)
            );
            Assert.Equal("boom", exception.Message);
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

        await RunAsync("no-such-job");

        Assert.Equal(before, factory.TestRecurringJob.Runs);
    }
}
