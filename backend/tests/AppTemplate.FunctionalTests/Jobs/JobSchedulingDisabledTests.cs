using System.Net;
using System.Net.Http.Json;
using AppTemplate.Infrastructure.Jobs.Services;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Jobs;
using AppTemplate.Web.Features.JobFeatures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

/// <summary>JobScheduling:Enabled=false removes Hangfire without breaking the application.</summary>
public class JobSchedulingDisabledTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private WebApplicationFactory<Program> CreateDisabledApp() =>
        factory.WithWebHostBuilder(builder => builder.UseSetting("JobScheduling:Enabled", "false"));

    [Fact]
    public void WhenDisabled_BackgroundWorkIsSkippedInsteadOfEnqueued()
    {
        using var scope = CreateDisabledApp().Services.CreateScope();

        Assert.IsType<DisabledBackgroundJobScheduler>(
            scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>()
        );
    }

    [Fact]
    public async Task WhenDisabled_SignInStillProvisionsTheUser()
    {
        var client = CreateDisabledApp().CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"sub-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenDisabled_TheAdminApiReportsNoJobs()
    {
        var app = CreateDisabledApp();
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, $"sub-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.PermissionsHeader,
            $"{Permission.JobsRead},{Permission.JobsManage}"
        );
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.2.0.1");

        var jobs = await client.GetFromJsonAsync<RecurringJobResponse[]>("/admin/jobs");
        var restore = await client.PostAsync("/admin/jobs/restore", null);

        Assert.Empty(jobs!);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, restore.StatusCode);
    }
}
