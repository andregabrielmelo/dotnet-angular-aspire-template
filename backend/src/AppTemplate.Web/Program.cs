using AppTemplate.Infrastructure.Jobs.Extensions;
using AppTemplate.ServiceDefaults;
using AppTemplate.Web.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults() // This sets up OpenTelemetry logging
    .AddLoggerConfigurations(); // This adds Serilog for console formatting

using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

startupLogger.LogInformation("Starting web host");

builder.Services.AddOptionConfigurations(builder.Configuration, startupLogger, builder);
builder.Services.AddServiceConfigurations(startupLogger, builder);
builder.Services.AddAuthenticationConfigurations(startupLogger, builder);
builder.Services.AddAuthorizationConfigurations(startupLogger, builder);
builder.Services.AddCachingConfigurations(startupLogger, builder);

builder
    .Services.AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.ShortSchemaNames = true;
        o.DocumentSettings = s =>
        {
            s.Title = "AppTemplate API";
            s.Version = "v1";
            s.Description = "REST API for AppTemplate.";
        };
    });

var app = builder.Build();

await app.UseAppMiddleware();
if (app.Environment.IsDevelopment())
{
    await app.StartDatabase();
}

await app.UseJobSchedulingAsync();

app.MapDefaultEndpoints(); // Aspire health checks and metrics

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
