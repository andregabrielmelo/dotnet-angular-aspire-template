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

builder
    .Services.AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.ShortSchemaNames = true;
    });

var app = builder.Build();

await app.UseAppMiddleware();
if (app.Environment.IsDevelopment())
{
    await app.StartDatabase();
}

app.MapDefaultEndpoints(); // Aspire health checks and metrics

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
