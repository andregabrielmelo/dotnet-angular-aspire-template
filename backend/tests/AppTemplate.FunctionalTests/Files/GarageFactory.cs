using AppTemplate.AppHost.Garage;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;

namespace AppTemplate.FunctionalTests.Files;

/// <summary>
/// The API with real object storage: a Garage container started from the AppHost's own image,
/// command and garage.toml (<see cref="GarageDefaults"/>, linked from the AppHost project), so
/// the bootstrap tested here is the one developers run.
/// </summary>
public class GarageFactory : AppTemplateWebApplicationFactory
{
    // Deliberately low-entropy development values (Garage wants hex), as in the AppHost.
    public const string AccessKey = "GKdededededededededededede";
    public const string SecretKey =
        "dededededededededededededededededededededededededededededededede";

    public IContainer Garage { get; } =
        new ContainerBuilder(
            $"{GarageDefaults.Registry}/{GarageDefaults.Image}:{GarageDefaults.Tag}"
        )
            .WithEntrypoint(GarageDefaults.Entrypoint)
            .WithCommand(GarageDefaults.ServerArguments)
            .WithResourceMapping(
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Garage", "garage.toml")),
                GarageDefaults.ConfigPath
            )
            .WithEnvironment("GARAGE_RPC_SECRET", SecretKey)
            .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", AccessKey)
            .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", SecretKey)
            .WithEnvironment("GARAGE_DEFAULT_BUCKET", GarageDefaults.Bucket)
            .WithPortBinding(GarageDefaults.S3Port, assignRandomHostPort: true)
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilMessageIsLogged("S3 API server listening")
            )
            .Build();

    public string ServiceUrl =>
        $"http://{Garage.Hostname}:{Garage.GetMappedPublicPort(GarageDefaults.S3Port)}";

    public override async Task InitializeAsync()
    {
        await Garage.StartAsync();
        await base.InitializeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("FileStorage:ServiceUrl", ServiceUrl);
        builder.UseSetting("FileStorage:AccessKey", AccessKey);
        builder.UseSetting("FileStorage:SecretKey", SecretKey);
        builder.UseSetting("FileStorage:Bucket", GarageDefaults.Bucket);
        builder.UseSetting("FileStorage:Region", GarageDefaults.Region);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Garage.DisposeAsync();
    }
}
