using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AppTemplate.BackendForFrontend.Configurations;

public static class AuthenticationConfigurations
{
    public const string CookieName = "__Host-apptemplate";

    /// <summary>AuthenticationProperties item carrying the brokered provider's alias.</summary>
    public const string IdentityProviderHintItem = "kc_idp_hint";

    /// <summary>
    /// OpenID Connect (authorization code + PKCE) against Keycloak for login, and an HTTP-only
    /// cookie for the browser session. The tokens Keycloak issues are kept inside that encrypted
    /// cookie ticket - never readable by JavaScript - and the access token is attached to proxied
    /// API calls by <see cref="ReverseProxyConfigurations"/>.
    /// </summary>
    public static IServiceCollection AddAuthenticationConfigurations(
        this IServiceCollection services,
        WebApplicationBuilder builder
    )
    {
        var keycloak = builder.Configuration.GetSection("Keycloak");

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    options.Cookie.Name = CookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;

                    // The SPA talks to this host with fetch/XHR - answer with status codes
                    // instead of redirecting API calls to a login page.
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                }
            )
            .AddKeycloakOpenIdConnect(
                serviceName: "keycloak",
                realm: keycloak["Realm"] ?? "apptemplate",
                options =>
                {
                    options.ClientId = keycloak["ClientId"] ?? "apptemplate-backend-for-frontend";
                    options.ClientSecret = keycloak["ClientSecret"];
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.SaveTokens = true;
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.NameClaimType = "name";
                    // Keycloak runs over plain HTTP inside Aspire locally.
                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    // The Aspire service-discovery address ("https+http://keycloak/...") is for
                    // local development. Elsewhere, set Keycloak:Authority to the realm's public
                    // HTTPS URL, which is what issued tokens name as their issuer.
                    if (keycloak["Authority"] is { Length: > 0 } authority)
                    {
                        options.Authority = authority;
                    }

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    // Refresh tokens, so the access token can be renewed without a new login.
                    options.Scope.Add("offline_access");

                    // Keycloak-specific: skip Keycloak's own login form and go straight to a
                    // brokered provider (Google, GitHub, ...) chosen in the SPA.
                    var redirectToIdentityProvider = options.Events.OnRedirectToIdentityProvider;
                    options.Events.OnRedirectToIdentityProvider = async context =>
                    {
                        if (
                            context.Properties.Items.TryGetValue(
                                IdentityProviderHintItem,
                                out var alias
                            ) && !string.IsNullOrEmpty(alias)
                        )
                        {
                            context.ProtocolMessage.SetParameter("kc_idp_hint", alias);
                        }

                        await redirectToIdentityProvider(context);
                    };
                }
            );

        services
            .AddOptions<ExternalIdentityProvidersOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                    configuration
                        .GetSection(ExternalIdentityProvidersOptions.SectionName)
                        .Bind(options.Providers)
            )
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<ExternalIdentityProvidersOptions>,
            ExternalIdentityProvidersOptionsValidator
        >();

        // Refreshes the access token from the stored refresh token shortly before it expires.
        services.AddOpenIdConnectAccessTokenManagement();

        services.AddAuthorization();

        return services;
    }
}
