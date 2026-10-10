using System.Diagnostics;
using System.Text;
using AppTemplate.Infrastructure.Images;
using AppTemplate.UseCases.Files;
using Ardalis.Result;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace AppTemplate.FunctionalTests.Images;

/// <summary>
/// The avatar image pipeline on its own (no Docker): size, signature and dimension checks in
/// that order, then a decode and re-encode that leaves the original bytes, metadata included,
/// behind.
/// </summary>
public class ImageProcessorTests
{
    private readonly IImageProcessor _processor = new ServiceCollection()
        .AddImageProcessing()
        .BuildServiceProvider()
        .GetRequiredService<IImageProcessor>();

    private Task<Result<ProcessedImage>> ProcessAsync(byte[] bytes) =>
        _processor.ProcessAvatarAsync(new MemoryStream(bytes), CancellationToken.None);

    private static string ReasonOf(Result<ProcessedImage> result) =>
        Assert.Single(result.ValidationErrors).ErrorCode;

    [Fact]
    public async Task AcceptedImage_IsReEncodedAsWebPWithinTheOutputSize()
    {
        var result = await ProcessAsync(TestImages.Png(1200, 800));

        Assert.True(result.IsSuccess);
        Assert.Equal("image/webp", result.Value.ContentType);
        Assert.Equal((512, 341), (result.Value.Width, result.Value.Height));
        using var decoded = SKBitmap.Decode(result.Value.Content);
        Assert.Equal(512, decoded.Width);
    }

    /// <summary>
    /// The stored image is 40 x 20 with a red top-left quarter. After applying its EXIF
    /// orientation the red quarter must sit in the corner a viewer would see it, and the
    /// rotated orientations (5-8) swap width and height.
    /// </summary>
    [Theory]
    [InlineData(1, 40, 20, "top-left")]
    [InlineData(2, 40, 20, "top-right")] // mirrored horizontally
    [InlineData(3, 40, 20, "bottom-right")] // rotated 180°
    [InlineData(4, 40, 20, "bottom-left")] // mirrored vertically
    [InlineData(5, 20, 40, "top-left")] // transposed
    [InlineData(6, 20, 40, "top-right")] // rotated 90° clockwise
    [InlineData(7, 20, 40, "bottom-right")] // transversed
    [InlineData(8, 20, 40, "bottom-left")] // rotated 90° counter-clockwise
    public async Task ExifOrientation_IsAppliedBeforeReEncoding(
        int orientation,
        int width,
        int height,
        string redCorner
    )
    {
        var result = await ProcessAsync(TestImages.JpegWithOrientation(40, 20, orientation));

        Assert.True(result.IsSuccess);
        Assert.Equal((width, height), (result.Value.Width, result.Value.Height));
        using var decoded = SKBitmap.Decode(result.Value.Content);
        var corners = new Dictionary<string, SKColor>
        {
            ["top-left"] = decoded.GetPixel(width / 4, height / 4),
            ["top-right"] = decoded.GetPixel(width * 3 / 4, height / 4),
            ["bottom-left"] = decoded.GetPixel(width / 4, height * 3 / 4),
            ["bottom-right"] = decoded.GetPixel(width * 3 / 4, height * 3 / 4),
        };
        // Lossy WebP shifts colors slightly, so compare by dominant channel.
        static bool IsRed(SKColor c) => c.Red > 180 && c.Green < 90 && c.Blue < 90;
        Assert.Equal([redCorner], corners.Where(c => IsRed(c.Value)).Select(c => c.Key));
    }

    [Fact]
    public async Task ReEncoding_DropsTheExifData()
    {
        var upload = TestImages.JpegWithExif(300, 300);
        Assert.Contains(TestImages.ExifMarker, Encoding.ASCII.GetString(upload));

        var result = await ProcessAsync(upload);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(
            TestImages.ExifMarker,
            Encoding.ASCII.GetString(result.Value.Content)
        );
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(result.Value.Content));
    }

    [Fact]
    public async Task OversizedUpload_IsRejectedBeforeAnythingElse()
    {
        var tooBig = new byte[AvatarLimits.MaxBytes + 1];
        TestImages.Png(10, 10).CopyTo(tooBig, 0);

        Assert.Equal("too_large", ReasonOf(await ProcessAsync(tooBig)));
    }

    [Theory]
    [InlineData("GIF89a............")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData("%PDF-1.7 pretending to be an image")]
    public async Task WrongSignature_IsRejected(string content)
    {
        Assert.Equal(
            "unsupported_type",
            ReasonOf(await ProcessAsync(Encoding.ASCII.GetBytes(content)))
        );
    }

    [Fact]
    public async Task DecompressionBomb_IsRejectedFromItsHeaderWithoutDecoding()
    {
        var bomb = TestImages.PngBomb(20_000, 20_000); // 400 megapixels, 1.6 GB decoded
        Assert.True(bomb.Length < 100);
        var stopwatch = Stopwatch.StartNew();

        var result = await ProcessAsync(bomb);

        Assert.Equal("too_many_pixels", ReasonOf(result));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task ImageJustOverTheDimensionLimit_IsRejected()
    {
        var wide = TestImages.PngBomb(AvatarLimits.MaxDimension + 1, 10);

        Assert.Equal("too_many_pixels", ReasonOf(await ProcessAsync(wide)));
    }
}
