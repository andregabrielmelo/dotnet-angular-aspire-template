using AppTemplate.Core.Aggregates.UserAggregate;
using AppTemplate.UseCases.Users.Avatar.Delete;
using AppTemplate.UseCases.Users.Avatar.Get;
using AppTemplate.UseCases.Users.Avatar.Set;
using AppTemplate.Web.Configurations;
using AppTemplate.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AppTemplate.Web.Features.UserFeatures.Avatar;

public sealed class AvatarRequest
{
    public int Id { get; set; }
}

public sealed class UploadAvatarRequest
{
    public int Id { get; set; }

    /// <summary>A JPEG, PNG or WebP image, at most 2 MB and 4096 pixels wide and high.</summary>
    public IFormFile? File { get; set; }
}

public sealed class UploadAvatarValidator : Validator<UploadAvatarRequest>
{
    public UploadAvatarValidator()
    {
        RuleFor(request => request.Id).GreaterThan(0);
        RuleFor(request => request.File).NotNull().WithMessage("Choose an image to upload.");
    }
}

internal static class AvatarEndpoints
{
    public const string Route = "/users/{id}/avatar";

    /// <summary>A multipart body around a full-size upload: the request limit Kestrel enforces.</summary>
    public const long MaxRequestBytes = AvatarLimits.MaxBytes + 64 * 1024;
}

/// <summary>
/// Replaces your avatar. The image is checked (size, signature, dimensions before decoding),
/// re-encoded without metadata and stored under a new opaque key; the old one is deleted via
/// the outbox. Only the user themselves may do this, whatever their permissions.
/// </summary>
public sealed class UploadAvatarEndpoint(IMediator _mediator)
    : Endpoint<UploadAvatarRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Put(AvatarEndpoints.Route);
        Version(ApiVersions.V1);
        AllowFileUploads();
        Options(x =>
            x.RequireRateLimiting(RateLimitPolicies.AvatarUploads)
                .WithMetadata(new RequestSizeLimitAttribute(AvatarEndpoints.MaxRequestBytes))
        );

        Summary(s =>
        {
            s.Summary = "Upload your avatar";
            s.Description =
                "Multipart upload of a JPEG, PNG or WebP image (field 'file', at most 2 MB and 4096x4096). It's re-encoded as WebP, at most 512x512, without metadata.";
            s.Responses[204] = "Avatar replaced";
            s.Responses[400] = "Not an acceptable image";
            s.Responses[403] = "Not your profile";
            s.Responses[404] = "No such user";
            s.Responses[413] = "Request too large";
            s.Responses[429] = "Too many requests";
            s.Responses[503] = "File storage is unavailable";
        });

        Tags("Users");
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        UploadAvatarRequest request,
        CancellationToken cancellationToken
    )
    {
        // The client's Content-Type and file name are ignored: the image processor reads the bytes.
        await using var upload = request.File!.OpenReadStream();
        var result = await _mediator.Send(
            new SetAvatarCommand(UserId.From(request.Id), upload),
            cancellationToken
        );
        return result.ToNoContentResult();
    }
}

public sealed class DeleteAvatarEndpoint(IMediator _mediator)
    : Endpoint<AvatarRequest, Results<NoContent, ProblemHttpResult>>
{
    public override void Configure()
    {
        Delete(AvatarEndpoints.Route);
        Version(ApiVersions.V1);

        Summary(s =>
        {
            s.Summary = "Remove your avatar";
            s.Responses[204] = "Avatar removed (or there was none)";
            s.Responses[403] = "Not your profile";
            s.Responses[404] = "No such user";
        });

        Tags("Users");
    }

    public override async Task<Results<NoContent, ProblemHttpResult>> ExecuteAsync(
        AvatarRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _mediator.Send(
            new DeleteAvatarCommand(UserId.From(request.Id)),
            cancellationToken
        );
        return result.ToNoContentResult();
    }
}

/// <summary>
/// Streams a user's avatar: your own, or anyone's with users:read. The ETag is the object key,
/// which changes with every upload, so <c>If-None-Match</c> answers 304 without touching storage,
/// and <c>Cache-Control: private, no-cache</c> keeps shared caches out and makes browsers revalidate.
/// </summary>
public sealed class GetAvatarEndpoint(IMediator _mediator) : Endpoint<AvatarRequest>
{
    public override void Configure()
    {
        Get(AvatarEndpoints.Route);
        Version(ApiVersions.V1);

        Summary(s =>
        {
            s.Summary = "Get a user's avatar";
            s.Responses[200] = "The image";
            s.Responses[304] = "Not modified since the ETag in If-None-Match";
            s.Responses[403] = "Requires users:read for someone else's avatar";
            s.Responses[404] = "No such user, or no avatar";
            s.Responses[503] = "File storage is unavailable";
        });

        Tags("Users");

        Description(builder =>
            builder
                .Produces(200, contentType: "image/webp")
                .Produces(304)
                .ProducesProblem(403)
                .ProducesProblem(404)
                .ProducesProblem(503)
        );
    }

    public override async Task HandleAsync(
        AvatarRequest request,
        CancellationToken cancellationToken
    )
    {
        var cachedKey = EntityTagHeaderValue.TryParseList(
            HttpContext.Request.Headers.IfNoneMatch,
            out var tags
        )
            ? tags.FirstOrDefault(tag => !tag.IsWeak)?.Tag.Value?.Trim('"')
            : null;

        var result = await _mediator.Send(
            new GetAvatarQuery(UserId.From(request.Id), cachedKey),
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            await Send.ResultAsync(result.ToProblem());
            return;
        }

        var avatar = result.Value;
        HttpContext.Response.Headers.ETag = new EntityTagHeaderValue(
            $"\"{avatar.Key}\""
        ).ToString();
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        if (avatar.File is null)
        {
            // Sent explicitly: a handler that writes nothing gets FastEndpoints' default 204.
            await Send.ResultAsync(TypedResults.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        await using var file = avatar.File;
        await Send.StreamAsync(
            file.Content,
            fileName: null,
            fileLengthBytes: file.Length,
            contentType: file.ContentType,
            cancellation: cancellationToken
        );
    }
}
