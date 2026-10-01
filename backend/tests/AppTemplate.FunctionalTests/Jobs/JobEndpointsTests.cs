using System.Net;
using System.Net.Http.Json;
using AppTemplate.UseCases.Authorization;
using AppTemplate.Web.Features.JobFeatures;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

public class JobEndpointsTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>,
        IAsyncLifetime
{
    private const string JobId = TestRecurringJobDefinition.Id;

    private HttpClient Reader() =>
        factory.CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}", Permission.JobsRead);

    private HttpClient Manager() =>
        factory.CreateAuthenticatedClient(
            $"sub-{Guid.NewGuid():N}",
            Permission.JobsRead,
            Permission.JobsManage
        );

    // Every test starts with the test job registered and not paused.
    public async Task InitializeAsync()
    {
        await Manager().PostAsync($"/admin/jobs/{JobId}/resume", null);
        await Manager().PostAsync("/admin/jobs/restore", null);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_RequiresAuthenticationAndJobsRead()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/admin/jobs")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await factory
                    .CreateAuthenticatedClient($"sub-{Guid.NewGuid():N}")
                    .GetAsync("/admin/jobs")
            ).StatusCode
        );
    }

    [Fact]
    public async Task List_WithJobsRead_ReturnsTheRecurringJobs()
    {
        var jobs = await Reader().GetFromJsonAsync<RecurringJobResponse[]>("/admin/jobs");

        var job = Assert.Single(jobs!, j => j.Id == JobId);
        Assert.Equal("0 0 * * *", job.Cron);
        Assert.False(job.IsPaused);
    }

    [Fact]
    public async Task Get_ReturnsTheJobWithItsRecentRuns()
    {
        var detail = await Reader()
            .GetFromJsonAsync<RecurringJobDetailResponse>($"/admin/jobs/{JobId}");

        Assert.Equal(JobId, detail!.Job.Id);
        Assert.NotNull(detail.RecentExecutions);
    }

    [Theory]
    [InlineData("/admin/jobs/no-such-job", HttpStatusCode.NotFound)]
    [InlineData("/admin/jobs/Not_A_Valid_Id", HttpStatusCode.BadRequest)]
    public async Task Get_WithUnknownOrInvalidId_IsRejected(string path, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await Reader().GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("POST", "/admin/jobs/test-recurring-job/trigger")]
    [InlineData("POST", "/admin/jobs/test-recurring-job/pause")]
    [InlineData("POST", "/admin/jobs/test-recurring-job/resume")]
    [InlineData("DELETE", "/admin/jobs/test-recurring-job")]
    [InlineData("POST", "/admin/jobs/restore")]
    public async Task Mutations_RequireJobsManage(string method, string path)
    {
        var response = await Reader()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PauseAndResume_AreReflectedInTheList()
    {
        var manager = Manager();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await manager.PostAsync($"/admin/jobs/{JobId}/pause", null)).StatusCode
        );
        var paused = (
            await manager.GetFromJsonAsync<RecurringJobResponse[]>("/admin/jobs")
        )!.Single(j => j.Id == JobId);
        Assert.True(paused.IsPaused);
        Assert.Null(paused.NextExecution);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await manager.PostAsync($"/admin/jobs/{JobId}/resume", null)).StatusCode
        );
        var resumed = (
            await manager.GetFromJsonAsync<RecurringJobResponse[]>("/admin/jobs")
        )!.Single(j => j.Id == JobId);
        Assert.False(resumed.IsPaused);
    }

    [Fact]
    public async Task Trigger_RemoveAndRestore_Succeed()
    {
        var manager = Manager();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await manager.PostAsync($"/admin/jobs/{JobId}/trigger", null)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await manager.DeleteAsync($"/admin/jobs/{JobId}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await manager.GetAsync($"/admin/jobs/{JobId}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await manager.PostAsync("/admin/jobs/restore", null)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await manager.GetAsync($"/admin/jobs/{JobId}")).StatusCode
        );
    }

    [Fact]
    public async Task Mutations_OnAnUnknownJob_AreNotFound()
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Manager().PostAsync("/admin/jobs/no-such-job/pause", null)).StatusCode
        );
    }
}
