using System.Security.Claims;
using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.Core.ValueObjects;
using AppTemplate.UseCases.Auth;
using AppTemplate.UseCases.Auth.ExternalLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppTemplate.Web.Configurations.Auth;

/// <summary>
/// Wired as GoogleOptions.Events.OnTicketReceived (see ServiceConfigurations). Runs inside the
/// OAuth handler's own middleware pipeline, not a FastEndpoints request - there's no
/// endpoint-level DI here, so services are resolved directly off HttpContext.RequestServices.
/// context.HandleResponse() suppresses the default post-external-login sign-in: we mint our
/// own JWT pair instead and hand it to the browser via a redirect.
/// </summary>
public static class ExternalLoginCallbackHandler
{
    public static async Task HandleAsync(TicketReceivedContext context)
    {
        var services = context.HttpContext.RequestServices;
        var frontendBaseUrl = services
            .GetRequiredService<IOptions<FrontendOptions>>()
            .Value.BaseUrl;

        var principal = context.Principal!;
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? email;

        context.HandleResponse();

        if (providerKey is null || email is null || name is null)
        {
            context.Response.Redirect($"{frontendBaseUrl}/auth/login?error=external-login-failed");
            return;
        }

        var mediator = services.GetRequiredService<IMediator>();
        var command = new ExternalLoginCommand(
            GoogleDefaults.AuthenticationScheme,
            providerKey,
            new EmailAddress(email),
            UserName.From(name.Length > UserName.MaxLength ? name[..UserName.MaxLength] : name)
        );
        var result = await mediator.Send(command);

        if (!result.IsSuccess)
        {
            context.Response.Redirect($"{frontendBaseUrl}/auth/login?error=external-login-failed");
            return;
        }

        var accessToken = Uri.EscapeDataString(result.Value.AccessToken);
        var refreshToken = Uri.EscapeDataString(result.Value.RefreshToken);
        context.Response.Redirect(
            $"{frontendBaseUrl}/auth/oauth-callback#access_token={accessToken}&refresh_token={refreshToken}"
        );
    }
}
