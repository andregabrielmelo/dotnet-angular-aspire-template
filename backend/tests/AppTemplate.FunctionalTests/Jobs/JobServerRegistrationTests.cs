using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AppTemplate.FunctionalTests.Jobs;

/// <summary>
/// <c>JobScheduling:RunServer</c> is read from the registered options when hosted services are
/// built, not at registration. The test host turns it off, so Hangfire's server must be absent.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class JobServerRegistrationTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    [Fact]
    public void WithRunServerOff_HangfiresServerIsNotHosted()
    {
        var hosted = factory
            .Services.GetServices<IHostedService>()
            .Select(service => service.GetType().Name)
            .ToList();

        Assert.DoesNotContain("BackgroundJobServerHostedService", hosted);
        Assert.Contains("DisabledJobServer", hosted);
    }
}
