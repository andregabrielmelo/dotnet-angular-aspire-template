var builder = DistributedApplication.CreateBuilder(args);

// Add Postgre SQL Server container
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Add the database
var applicationDatabase = postgres.AddDatabase("apptemplate");

// Add Redis container, used as HybridCache's distributed (L2) cache. No data volume: it's disposable
var cache = builder.AddRedis("cache").WithLifetime(ContainerLifetime.Persistent);

// register the API project and link the DB and cache
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpEndpoint(name: "api-http");

// Add frontend service and reference the API
var frontend = builder
    .AddJavaScriptApp("angular", "../../../frontend", runScriptName: "start")
    .WithNpm(installCommand: "ci")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(name: "frontend-http", env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
