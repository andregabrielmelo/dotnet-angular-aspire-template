namespace AppTemplate.Infrastructure.Images;

public static class ImageProcessingServiceExtensions
{
    /// <summary>
    /// Untrusted-upload image processing with SkiaSharp (ADR 018). Independent of file storage:
    /// it needs no configuration, so it works whether or not storage is configured.
    /// </summary>
    public static IServiceCollection AddImageProcessing(this IServiceCollection services)
    {
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        return services;
    }
}
