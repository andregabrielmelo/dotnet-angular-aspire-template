using Microsoft.AspNetCore.Diagnostics;

namespace AppTemplate.Web.Http;

/// <summary>
/// A concurrency conflict no use case handled (two requests changed the same row at once) is a
/// 409 problem details response, not a 500: retrying with fresh data is the client's remedy.
/// </summary>
public sealed class ConcurrencyConflictExceptionHandler(IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is not ConcurrencyConflictException conflict)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails =
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = conflict.Message,
                },
            }
        );
    }
}
