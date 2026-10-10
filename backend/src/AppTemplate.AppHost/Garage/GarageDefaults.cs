// Shared with the functional tests (linked into AppTemplate.FunctionalTests), so the
// Testcontainers round trip runs exactly the image and bootstrap the AppHost runs.
namespace AppTemplate.AppHost.Garage;

/// <summary>Garage, the S3-compatible object store used for file storage in development.</summary>
public static class GarageDefaults
{
    public const string Registry = "docker.io";
    public const string Image = "dxflrs/garage";

    /// <summary>
    /// v2.3+ is needed for <c>--single-node --default-bucket</c>. garage.toml lists this
    /// version's options and defaults: re-check it when bumping the tag.
    /// </summary>
    public const string Tag = "v2.4.1";

    public const string Entrypoint = "/garage";

    /// <summary>Configures a one-node layout and creates the default key and bucket on first start (idempotent).</summary>
    public static readonly string[] ServerArguments =
    [
        "server",
        "--single-node",
        "--default-bucket",
    ];

    public const string ConfigPath = "/etc/garage.toml";
    public const string DataPath = "/var/lib/garage";
    public const int S3Port = 3900;
    public const string Region = "garage";
    public const string Bucket = "apptemplate";
}
