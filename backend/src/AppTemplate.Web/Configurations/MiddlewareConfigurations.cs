using Scalar.AspNetCore;

namespace AppTemplate.Web.Configurations;

public static class MiddlewareConfigurations
{
    public static async Task<IApplicationBuilder> UseAppMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // Unhandled exceptions become a 500 problem details response without exception
            // details; the middleware logs the exception with the request's trace id.
            app.UseExceptionHandler();
            app.UseHsts();
        }

        // Empty error responses (401/403 from authorization, 404 for unmatched routes) get a
        // problem details body too. See ProblemDetailsConfigurations.
        app.UseStatusCodePages();

        app.UseAuthentication();
        app.UseAuthorization();
        // After authorization: a cached response is only served to callers who passed the
        // endpoint's policies (see AuthorizedSharedResponsePolicy).
        app.UseOutputCache();

        app.UseFastEndpoints(config =>
        {
            config.Errors.ResponseBuilder =
                ProblemDetailsConfigurations.FastEndpointsValidationProblem;
            config.Errors.ProducesMetadataType = typeof(HttpValidationProblemDetails);
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerGen(
                options =>
                {
                    options.Path = "/openapi/{documentName}.json";
                },
                settings =>
                {
                    settings.Path = "/swagger";
                    settings.DocumentPath = "/openapi/{documentName}.json";
                }
            );

            app.MapScalarApiReference(options =>
                {
                    options.WithTitle("AppTemplate API");
                    options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
                })
                .AllowAnonymous(); // a browser page, not an API call: no bearer token
        }

        app.UseHttpsRedirection(); // Note this will drop Authorization headers

        return app;
    }
}
