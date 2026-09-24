var builder = DistributedApplication.CreateBuilder(args);

// Add Postgre SQL Server container
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// Add the database
var applicationDatabase = postgres.AddDatabase("apptemplate");

// OpenID Connect provider: hosts the login, registration and logout pages. The realm (clients,
// audience mapper, registration enabled) is imported from Realms/ on first start. The port is
// pinned so the issuer URL the browser sees stays stable across runs.
var backendForFrontendSecret = builder.AddParameter(
    "keycloak-backend-for-frontend-secret",
    secret: true
);

var keycloak = builder
    .AddKeycloak("keycloak", port: 8080)
    .WithDataVolume()
    .WithRealmImport("./Realms")
    .WithLifetime(ContainerLifetime.Persistent);

// register the API project and link the DB
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithHttpEndpoint(name: "api-http");

// Angular dev server - only reached through the backend for frontend, never directly.
var frontend = builder
    .AddJavaScriptApp("angular", "../../../frontend", runScriptName: "start")
    .WithNpm(installCommand: "ci")
    .WithHttpEndpoint(env: "PORT")
    .PublishAsDockerFile();

// The browser's single entry point: owns the session cookie, runs the OIDC flow against
// Keycloak, and proxies /api to the Web API (adding the access token) and everything else to
// the Angular dev server. Its port is pinned because the realm's redirect URIs point at it.
builder
    .AddProject<Projects.AppTemplate_BackendForFrontend>(
        "backend-for-frontend",
        launchProfileName: null
    )
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithHttpsEndpoint(port: 7100, name: "https")
    .WithEnvironment("Keycloak__ClientSecret", backendForFrontendSecret)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(api)
    .WaitFor(api)
    .WithReference(frontend.GetEndpoint("http"))
    .WaitFor(frontend)
    .WithExternalHttpEndpoints();

builder.Build().Run();
