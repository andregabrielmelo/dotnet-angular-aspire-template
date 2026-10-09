namespace AppTemplate.Web.Configurations;

/// <summary>
/// API versions. Every feature endpoint declares one with <c>Version(ApiVersions.V1)</c>, which
/// FastEndpoints prepends to its route (<c>/v1/users</c>). Evolve a breaking endpoint by adding
/// a V2 endpoint class next to the V1 one (ADR 015).
/// </summary>
public static class ApiVersions
{
    public const string Prefix = "v";

    public const int V1 = 1;

    /// <summary>The newest version; the OpenAPI document includes endpoints up to it.</summary>
    public const int Latest = V1;
}
