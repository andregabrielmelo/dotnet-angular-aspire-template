using AppTemplate.Web.Configurations;
using FastEndpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSwag.Generation;
using Xunit;

namespace AppTemplate.FunctionalTests.Versioning;

/// <summary>
/// Every feature endpoint is versioned (ADR 015), and the OpenAPI document describes exactly
/// those endpoints: none missing, and no operational or framework routes.
/// </summary>
[Trait(TestCategories.Name, TestCategories.RequiresDocker)]
public class ApiVersioningTests(AppTemplateWebApplicationFactory factory)
    : IClassFixture<AppTemplateWebApplicationFactory>
{
    private static readonly string VersionPrefix = $"{ApiVersions.Prefix}{ApiVersions.Latest}/";

    private List<(string Method, string Route)> FeatureEndpoints() =>
        factory
            .Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<EndpointDefinition>() is not null)
            .SelectMany(endpoint =>
                (
                    endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"]
                ).Select(method => (method, endpoint.RoutePattern.RawText!.TrimStart('/')))
            )
            .ToList();

    [Fact]
    public void EveryFeatureEndpoint_DeclaresAVersion()
    {
        var endpoints = FeatureEndpoints();
        Assert.NotEmpty(endpoints);

        var unversioned = endpoints
            .Where(endpoint => !endpoint.Route.StartsWith(VersionPrefix, StringComparison.Ordinal))
            .Select(endpoint => $"{endpoint.Method} /{endpoint.Route}")
            .ToList();

        Assert.True(
            unversioned.Count == 0,
            "Call Version(ApiVersions.V1) in Configure(): " + string.Join(", ", unversioned)
        );
    }

    [Fact]
    public async Task OpenApiDocument_ListsExactlyTheVersionedFeatureEndpoints()
    {
        var document = await factory
            .Services.GetRequiredService<IOpenApiDocumentGenerator>()
            .GenerateAsync("v1");

        var documented = document
            .Paths.SelectMany(path =>
                path.Value.Keys.Select(method =>
                    $"{method.ToUpperInvariant()} {NormalizeParameters(path.Key.TrimStart('/'))}"
                )
            )
            .Order()
            .ToList();
        var registered = FeatureEndpoints()
            .Select(endpoint => $"{endpoint.Method} {NormalizeParameters(endpoint.Route)}")
            .Order()
            .ToList();

        Assert.Equal(registered, documented);
    }

    // Route parameters without constraints, and lowercased: NSwag camel-cases some names.
    private static string NormalizeParameters(string route) =>
        System.Text.RegularExpressions.Regex.Replace(
            route,
            @"\{([^}:?]+)[^}]*\}",
            match => "{" + match.Groups[1].Value.ToLowerInvariant() + "}"
        );
}
