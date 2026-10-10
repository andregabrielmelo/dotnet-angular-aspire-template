using System.IO.Hashing;
using System.Text;
using SkiaSharp;

namespace AppTemplate.FunctionalTests.Images;

/// <summary>Image fixtures, generated rather than committed.</summary>
public static class TestImages
{
    public const string ExifMarker = "SECRET-GPS-48.8584N-2.2945E";

    public static byte[] Png(int width, int height) =>
        Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width, int height) =>
        Encode(width, height, SKEncodedImageFormat.Jpeg);

    /// <summary>A JPEG with an APP1 (EXIF) segment holding <see cref="ExifMarker"/>, like a phone photo's GPS data.</summary>
    public static byte[] JpegWithExif(int width, int height)
    {
        var jpeg = Jpeg(width, height);
        var payload = Encoding.ASCII.GetBytes("Exif\0\0" + ExifMarker);
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray(); // right after SOI
    }

    /// <summary>
    /// A <paramref name="width"/> x <paramref name="height"/> JPEG, white with a red block in its
    /// stored top-left quarter, whose EXIF says to display it with <paramref name="orientation"/>
    /// (1-8, the TIFF Orientation tag), as a phone camera writes it.
    /// </summary>
    public static byte[] JpegWithOrientation(int width, int height, int orientation)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawRect(0, 0, width / 2f, height / 2f, new SKPaint { Color = SKColors.Red });
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        var jpeg = data.ToArray();

        // "Exif\0\0", then a little-endian TIFF header and one IFD holding only Orientation.
        byte[] payload =
        [
            .. "Exif\0\0"u8,
            .. "II"u8,
            0x2A,
            0x00, // TIFF magic
            0x08,
            0x00,
            0x00,
            0x00, // offset of the first IFD
            0x01,
            0x00, // one entry
            0x12,
            0x01, // tag 0x0112 Orientation
            0x03,
            0x00, // type SHORT
            0x01,
            0x00,
            0x00,
            0x00, // count 1
            (byte)orientation,
            0x00,
            0x00,
            0x00, // value
            0x00,
            0x00,
            0x00,
            0x00, // no next IFD
        ];
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg[..2].Concat(segment).Concat(jpeg[2..]).ToArray(); // right after SOI
    }

    /// <summary>
    /// A valid PNG header claiming <paramref name="width"/> x <paramref name="height"/>, with no
    /// real pixel data: tiny on disk, gigantic once decoded.
    /// </summary>
    public static byte[] PngBomb(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(stream, "IHDR", header);
        WriteChunk(stream, "IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
            canvas.DrawCircle(
                width / 2f,
                height / 2f,
                Math.Min(width, height) / 3f,
                new SKPaint { Color = SKColors.Gold }
            );
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typeAndData);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32.HashToUInt32(typeAndData));
        stream.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
