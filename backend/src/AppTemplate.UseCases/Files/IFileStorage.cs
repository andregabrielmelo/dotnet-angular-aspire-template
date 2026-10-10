namespace AppTemplate.UseCases.Files;

/// <summary>
/// Object storage (S3-compatible: Garage locally, implemented in Infrastructure). Keys are
/// opaque; callers never derive them from user input. An outage throws
/// <see cref="FileStorageUnavailableException"/>, which use cases turn into
/// <c>Result.Unavailable</c> (503): file storage is an optional dependency, and everything
/// else keeps working without it.
/// </summary>
public interface IFileStorage
{
    Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken
    );

    /// <summary>Null when no object has the key.</summary>
    Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deleting a key that doesn't exist succeeds, so retries are safe.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>An object's content; dispose it to release the connection.</summary>
public sealed record StoredFile(Stream Content, string ContentType, long Length) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed class FileStorageUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
