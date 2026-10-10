using System.ComponentModel.DataAnnotations;

namespace AppTemplate.Infrastructure.Files;

/// <summary>
/// An S3-compatible endpoint (Garage locally, wired by the AppHost; any S3 service elsewhere).
/// Without <see cref="ServiceUrl"/>, file storage is simply off: uploads answer 503 and
/// everything else works.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string? ServiceUrl { get; set; }

    [Required]
    public string Bucket { get; set; } = "apptemplate";

    /// <summary>Garage's default region is "garage"; AWS needs the bucket's real region.</summary>
    [Required]
    public string Region { get; set; } = "garage";

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>Per request, so a stalled storage service fails fast instead of holding the request.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUrl);
}
