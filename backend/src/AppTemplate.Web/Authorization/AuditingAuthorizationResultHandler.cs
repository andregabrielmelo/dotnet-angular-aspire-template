using AppTemplate.UseCases.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AppTemplate.Web.Authorization;

/// <summary>
/// Records a signed-in caller being refused an admin endpoint (403): a probe for privileges
/// the caller doesn't have is a security event. The target is the endpoint's route pattern,
/// never the raw path, so it stays bounded. Then the default handler writes the response.
/// </summary>
public sealed class AuditingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private const string AdminPrefix = "/v1/admin";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult
    )
    {
        if (authorizeResult.Forbidden && context.Request.Path.StartsWithSegments(AdminPrefix))
        {
            var route =
                (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                ?? context.Request.Path.Value!;
            await context
                .RequestServices.GetRequiredService<IAuditLog>()
                .RecordAsync(
                    AuditActions.AuthorizationDenied,
                    AuditTarget.Endpoint($"{context.Request.Method} /{route.TrimStart('/')}"),
                    AuditOutcome.Denied,
                    context.RequestAborted
                );
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
