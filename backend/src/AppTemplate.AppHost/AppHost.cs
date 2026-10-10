using AppTemplate.AppHost.Garage;

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

// Service account the Web API uses for Keycloak's Admin API (password reset emails).
var userAdminSecret = builder.AddParameter("keycloak-user-admin-secret", secret: true);

// Development SMTP catcher: Keycloak sends its emails (e.g. password reset links) here, and
// they can be read in Mailpit's web UI (the "mailpit" resource's http endpoint). The registry is
// explicit because Podman, unlike Docker, does not assume docker.io for unqualified image names.
var mailpit = builder
    .AddContainer("mailpit", "axllent/mailpit", "v1.31.2")
    .WithImageRegistry("docker.io")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");

var keycloak = builder
    .AddKeycloak("keycloak", port: 8080)
    .WithDataVolume()
    .WithRealmImport("./Realms")
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(mailpit);

// Garage: S3-compatible object storage for uploaded files (avatars). It's an optional
// dependency: the API starts and serves without it, and only file endpoints answer 503. The
// default key and bucket are created on first start; there's no admin API (see garage.toml).
var garageRpcSecret = builder.AddParameter("garage-rpc-secret", secret: true);
var garageAccessKey = builder.AddParameter("garage-access-key", secret: true);
var garageSecretKey = builder.AddParameter("garage-secret-key", secret: true);
var garage = builder
    .AddContainer("garage", GarageDefaults.Image, GarageDefaults.Tag)
    .WithImageRegistry(GarageDefaults.Registry)
    .WithEntrypoint(GarageDefaults.Entrypoint)
    .WithArgs(GarageDefaults.ServerArguments)
    .WithBindMount("./Garage/garage.toml", GarageDefaults.ConfigPath, isReadOnly: true)
    .WithVolume("apptemplate-garage-data", GarageDefaults.DataPath)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEnvironment("GARAGE_RPC_SECRET", garageRpcSecret)
    .WithEnvironment("GARAGE_DEFAULT_ACCESS_KEY", garageAccessKey)
    .WithEnvironment("GARAGE_DEFAULT_SECRET_KEY", garageSecretKey)
    .WithEnvironment("GARAGE_DEFAULT_BUCKET", GarageDefaults.Bucket)
    .WithHttpEndpoint(targetPort: GarageDefaults.S3Port, name: "s3");

// Redis: HybridCache's distributed (L2) cache and the output cache store, shared by every
// API instance so cache invalidations reach all of them.
var cache = builder.AddRedis("cache");

// register the API project and link the DB
var api = builder
    .AddProject<Projects.AppTemplate_Web>("web")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithReference(applicationDatabase)
    .WaitFor(applicationDatabase)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(cache)
    .WaitFor(cache)
    .WithEnvironment("Keycloak__Admin__ClientSecret", userAdminSecret)
    // Background jobs (welcome emails) send through Mailpit too.
    .WithEnvironment(
        "Mailserver__Hostname",
        mailpit.GetEndpoint("smtp").Property(EndpointProperty.Host)
    )
    .WithEnvironment(
        "Mailserver__Port",
        mailpit.GetEndpoint("smtp").Property(EndpointProperty.Port)
    )
    .WaitFor(mailpit)
    // File storage: only the S3 endpoint, key and bucket - never Garage's RPC secret.
    .WithEnvironment("FileStorage__ServiceUrl", garage.GetEndpoint("s3"))
    .WithEnvironment("FileStorage__AccessKey", garageAccessKey)
    .WithEnvironment("FileStorage__SecretKey", garageSecretKey)
    .WithEnvironment("FileStorage__Bucket", GarageDefaults.Bucket)
    .WithEnvironment("FileStorage__Region", GarageDefaults.Region)
    .WithHttpEndpoint(name: "api-http")
    // Readiness (Postgres reachable): resources that WaitFor(api) start only once it's ready.
    .WithHttpHealthCheck("/health")
    // Hangfire dashboard (Development only, local requests only).
    .WithUrlForEndpoint(
        "http",
        _ => new ResourceUrlAnnotation { Url = "/hangfire", DisplayText = "Jobs dashboard" }
    );

// Angular dev server - only reached through the backend for frontend, never directly.
var frontend = builder
    .AddJavaScriptApp("angular", "../../../frontend", runScriptName: "start")
    .WithNpm(installCommand: "ci")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/")
    .PublishAsDockerFile();

// The browser's single entry point: owns the session cookie, runs the OIDC flow against
// Keycloak, and proxies /api to the Web API (adding the access token) and everything else to
// the Angular dev server. Its port is pinned because the realm's redirect URIs point at it.
var backendForFrontend = builder
    .AddProject<Projects.AppTemplate_BackendForFrontend>(
        "backend-for-frontend",
        launchProfileName: null
    )
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithHttpsEndpoint(port: 7100, name: "https")
    .WithHttpHealthCheck("/health", endpointName: "https")
    .WithEnvironment("Keycloak__ClientSecret", backendForFrontendSecret)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(api)
    .WaitFor(api)
    .WithReference(frontend.GetEndpoint("http"))
    .WaitFor(frontend)
    .WithExternalHttpEndpoints();

ExternalIdentityProviders.Configure(builder, keycloak, backendForFrontend);

builder.Build().Run();
