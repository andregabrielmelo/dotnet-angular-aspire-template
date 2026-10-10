namespace AppTemplate.UseCases.Files;

/// <summary>
/// Turns an untrusted upload into a safe image (implemented in Infrastructure): it checks the
/// file signature, reads the dimensions <b>before</b> decoding, rejects anything over the
/// limits, then decodes and re-encodes it, which drops all metadata (EXIF, GPS) and any
/// payload hidden in the original bytes.
/// </summary>
public interface IImageProcessor
{
    /// <returns><c>Result.Invalid</c> with a reason the client may see, or the re-encoded image.</returns>
    Task<Result<ProcessedImage>> ProcessAvatarAsync(
        Stream upload,
        CancellationToken cancellationToken
    );
}

public sealed record ProcessedImage(byte[] Content, string ContentType, int Width, int Height);

/// <summary>Limits for avatar uploads, enforced in order: bytes, signature, header dimensions, decode.</summary>
public static class AvatarLimits
{
    /// <summary>Largest accepted upload, in bytes (also the request size limit).</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>Largest accepted width or height, read from the header before decoding.</summary>
    public const int MaxDimension = 4096;

    /// <summary>Largest accepted pixel count, so a decode never needs more than ~64 MB.</summary>
    public const long MaxPixels = 16_000_000;

    /// <summary>Stored avatars are scaled down to fit this square.</summary>
    public const int OutputSize = 512;

    public static readonly TimeSpan DecodeTimeout = TimeSpan.FromSeconds(10);
}
