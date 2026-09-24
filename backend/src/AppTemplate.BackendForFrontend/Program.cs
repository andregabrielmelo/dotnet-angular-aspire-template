using AppTemplate.BackendForFrontend.Configurations;
using AppTemplate.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddAuthenticationConfigurations(builder);
builder.Services.AddReverseProxyConfigurations(builder);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

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
