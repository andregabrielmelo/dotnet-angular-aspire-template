using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Files;

/// <summary>
/// <see cref="IFileStorage"/> over the S3 API (AWSSDK.S3), with path-style addressing so a
/// single-host service like Garage works without per-bucket DNS. Network failures, timeouts
/// and server errors become <see cref="FileStorageUnavailableException"/>.
/// </summary>
internal sealed class S3FileStorage(IAmazonS3 client, IOptions<FileStorageOptions> options)
    : IFileStorage
{
    private string Bucket => options.Value.Bucket;

    public Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken
    ) =>
        GuardAsync(async () =>
        {
            await client.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = Bucket,
                    Key = key,
                    InputStream = new MemoryStream(content.ToArray(), writable: false),
                    ContentType = contentType,
                },
                cancellationToken
            );
            return true;
        });

    public Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            try
            {
                var response = await client.GetObjectAsync(Bucket, key, cancellationToken);
                return new StoredFile(
                    response.ResponseStream,
                    response.Headers.ContentType ?? "application/octet-stream",
                    response.ContentLength
                );
            }
            catch (AmazonS3Exception exception)
                when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return (StoredFile?)null;
            }
        });

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            await client.DeleteObjectAsync(Bucket, key, cancellationToken);
            return true;
        });

    private static async Task<T> GuardAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (AmazonServiceException exception)
            when (exception.StatusCode == 0 || (int)exception.StatusCode >= 500)
        {
            throw new FileStorageUnavailableException("File storage returned an error.", exception);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or AmazonClientException or TimeoutException
                || exception is TaskCanceledException { InnerException: TimeoutException }
            )
        {
            throw new FileStorageUnavailableException("File storage is unreachable.", exception);
        }
    }
}

/// <summary>Registered when <c>FileStorage:ServiceUrl</c> isn't set: every call is "unavailable".</summary>
internal sealed class UnconfiguredFileStorage : IFileStorage
{
    private static FileStorageUnavailableException NotConfigured() =>
        new("File storage isn't configured (FileStorage:ServiceUrl).");

    public Task PutAsync(
        string key,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken
    ) => throw NotConfigured();

    public Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken) =>
        throw NotConfigured();

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        throw NotConfigured();
}
