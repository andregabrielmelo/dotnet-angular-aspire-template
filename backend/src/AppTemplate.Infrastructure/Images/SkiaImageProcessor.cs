using AppTemplate.UseCases.Files;
using Ardalis.Result;
using SkiaSharp;

namespace AppTemplate.Infrastructure.Images;

/// <summary>
/// <see cref="IImageProcessor"/> with SkiaSharp (MIT). Checks happen cheapest first: size,
/// file signature, then the dimensions from the header, so a decompression bomb (a tiny file
/// that decodes to gigapixels) is refused before a single pixel is allocated. Only then is it
/// decoded, within a timeout, scaled to fit <see cref="AvatarLimits.OutputSize"/> and
/// re-encoded as WebP, which leaves every byte of the original (metadata included) behind.
/// </summary>
internal sealed class SkiaImageProcessor : IImageProcessor
{
    public const string OutputContentType = "image/webp";

    public async Task<Result<ProcessedImage>> ProcessAvatarAsync(
        Stream upload,
        CancellationToken cancellationToken
    )
    {
        var bytes = await ReadAtMostAsync(upload, AvatarLimits.MaxBytes + 1, cancellationToken);
        if (bytes.Length > AvatarLimits.MaxBytes)
        {
            return Invalid(
                "too_large",
                $"The image must be at most {AvatarLimits.MaxBytes / 1024 / 1024} MB."
            );
        }
        if (!HasSupportedSignature(bytes))
        {
            // Whatever Content-Type the client claimed: only the bytes count.
            return Invalid("unsupported_type", "The file must be a JPEG, PNG or WebP image.");
        }

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return Invalid("unreadable", "The image couldn't be read.");
        }
        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (
            width > AvatarLimits.MaxDimension
            || height > AvatarLimits.MaxDimension
            || (long)width * height > AvatarLimits.MaxPixels
        )
        {
            return Invalid(
                "too_many_pixels",
                $"The image may be at most {AvatarLimits.MaxDimension} pixels wide and high."
            );
        }

        SKBitmap? decoded;
        try
        {
            decoded = await Task.Run(() => Decode(codec), cancellationToken)
                .WaitAsync(AvatarLimits.DecodeTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return Invalid("unreadable", "The image took too long to read.");
        }
        if (decoded is null)
        {
            return Invalid("unreadable", "The image couldn't be read.");
        }

        using (decoded)
        using (var oriented = Orient(decoded, codec.EncodedOrigin))
        using (var resized = FitWithin(oriented, AvatarLimits.OutputSize))
        using (var image = SKImage.FromBitmap(resized))
        using (var encoded = image.Encode(SKEncodedImageFormat.Webp, 85))
        {
            return new ProcessedImage(
                encoded.ToArray(),
                OutputContentType,
                resized.Width,
                resized.Height
            );
        }
    }

    private static SKBitmap? Decode(SKCodec codec)
    {
        var info = new SKImageInfo(
            codec.Info.Width,
            codec.Info.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }
        bitmap.Dispose();
        return null;
    }

    /// <summary>Applies the EXIF orientation, which is about to be dropped with the rest of the metadata.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source.Copy();
        }
        var swap =
            origin
            is SKEncodedOrigin.LeftTop
                or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom
                or SKEncodedOrigin.LeftBottom;
        var (width, height) = swap ? (source.Height, source.Width) : (source.Width, source.Height);
        var target = new SKBitmap(
            new SKImageInfo(width, height, source.ColorType, source.AlphaType)
        );
        using var canvas = new SKCanvas(target);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1, width / 2f, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180, width / 2f, height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1, 0, height / 2f);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1, 0, source.Height / 2f);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                canvas.Scale(1, -1, 0, source.Height / 2f);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
        }
        // A 1:1 draw (only 90° rotations and flips), so sampling never blends pixels.
        canvas.DrawBitmap(source, 0, 0, SKSamplingOptions.Default);
        return target;
    }

    private static SKBitmap FitWithin(SKBitmap source, int size)
    {
        var scale = Math.Min(1.0, (double)size / Math.Max(source.Width, source.Height));
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(source.Height * scale))
        );
        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? source.Copy();
    }

    private static bool HasSupportedSignature(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) // JPEG
        || bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) // PNG
        || (
            bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes[8..12].SequenceEqual("WEBP"u8)
        );

    private static async Task<byte[]> ReadAtMostAsync(
        Stream stream,
        int limit,
        CancellationToken cancellationToken
    )
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, Math.Min(read, limit - (int)buffer.Length));
            if (buffer.Length >= limit)
            {
                break;
            }
        }
        return buffer.ToArray();
    }

    private static Result<ProcessedImage> Invalid(string code, string message) =>
        Result.Invalid(new ValidationError("file", message, code, ValidationSeverity.Error));
}
