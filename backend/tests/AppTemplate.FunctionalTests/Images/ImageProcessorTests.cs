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
