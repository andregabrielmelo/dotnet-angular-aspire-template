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
            app.UseDefaultExceptionHandler(); // from FastEndpoints
            app.UseHsts();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        // After authorization: a cached response is only served to callers who passed the
        // endpoint's policies (see AuthorizedSharedResponsePolicy).
        app.UseOutputCache();

        app.UseFastEndpoints();

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
            });
        }

        app.UseHttpsRedirection(); // Note this will drop Authorization headers

        return app;
    }
}
