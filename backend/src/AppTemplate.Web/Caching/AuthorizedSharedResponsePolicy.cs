using Microsoft.AspNetCore.OutputCaching;

namespace AppTemplate.Web.Caching;

/// <summary>
/// Output-cache policy for authenticated endpoints whose response is the same for every
/// caller who is allowed to see it (e.g. the user list, gated by users:read).
///
/// The built-in default policy refuses to cache any request carrying an Authorization header,
/// which is the safe default. Every API call carries a bearer token, so this policy opts in
/// explicitly, under these conditions:
/// - the output-cache middleware runs after UseAuthorization, so a cached response is only ever
///   served to a caller who already passed the endpoint's policy
/// - only GET/HEAD 200 responses that set no cookies are stored
/// - it never varies by user, so never use it on an endpoint whose body depends on the caller
/// </summary>
public sealed class AuthorizedSharedResponsePolicy : IOutputCachePolicy
{
    ValueTask IOutputCachePolicy.CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        var request = context.HttpContext.Request;
        var cacheable =
            (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && context.HttpContext.User.Identity?.IsAuthenticated == true;

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;
        // Different query strings are different pages - never serve one for another.
        context.CacheVaryByRules.QueryKeys = "*";

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    ) => ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken
    )
    {
        var response = context.HttpContext.Response;
        if (
            response.StatusCode != StatusCodes.Status200OK
            || !StringValuesIsEmpty(response.Headers.SetCookie)
        )
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }

    private static bool StringValuesIsEmpty(Microsoft.Extensions.Primitives.StringValues values) =>
        values.Count == 0;
}
