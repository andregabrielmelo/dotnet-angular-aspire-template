using AppTemplate.BackendForFrontend.Configurations;
using AppTemplate.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthenticationConfigurations(builder);
builder.Services.AddReverseProxyConfigurations(builder);

// This host's own error responses (CSRF and session 401s, 403s, failures) use the same RFC 9457
// problem details contract as the Web API. Proxied API responses pass through untouched.
builder.Services.AddProblemDetails();

// Public, identical-for-everyone responses only (the default policy never caches requests
// from signed-in users or responses that set cookies).
builder.Services.AddOutputCache(options =>
    options.AddPolicy(
        BackendForFrontendEndpoints.ProvidersCachePolicy,
        policy => policy.Expire(TimeSpan.FromMinutes(5))
    )
);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// Gives empty 4xx/5xx responses a problem details body; a response that already has a body
// (anything proxied from the API or the SPA) is left alone.
app.UseStatusCodePages();

app.UseHttpsRedirection();

// Outside Development the built Angular app is served from wwwroot; in Development the
// "spa" YARP route forwards everything else to the Angular dev server instead.
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAntiforgeryHeaderCheck();

app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapBackendForFrontendEndpoints();
app.MapReverseProxyWithAccessTokens();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.MapDefaultEndpoints();

app.Run();

// Make the implicit Program class public so tests can host this app with WebApplicationFactory.
public partial class Program { }
