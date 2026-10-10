using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppTemplate.UseCases.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.UseCases.Idempotency;

/// <summary>
/// Atomic execute and replay (ADR 019). For an <see cref="IIdempotentCommand"/> with a key,
/// from a signed-in caller, inside one database transaction:
/// <list type="number">
/// <item>Claim: insert the record (key, fingerprint, no result) <b>before</b> the handler runs.
/// A concurrent duplicate's insert blocks on the unique index until this transaction ends.</item>
/// <item>If a committed record exists: same fingerprint replays its result; a different one is
/// 422 (the key was reused for another request).</item>
/// <item>Otherwise run the handler. On success, store the result and commit it together with
/// the business changes; on failure, roll back, leaving no record so a retry runs again.</item>
/// </list>
/// So for each committed key the handler ran once, and a blocked duplicate either replays (the
/// first committed) or runs (the first rolled back): never a 409 for "in progress". The stored
/// result is the use case's <c>Result</c>, so a replay goes through the same HTTP mapping.
/// </summary>
public sealed class IdempotencyBehavior<TMessage, TResponse>(IServiceProvider services)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage, IIdempotentCommand
    where TResponse : Ardalis.Result.IResult
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var subject = services.GetService<ICurrentUser>()?.ExternalId;
        var store = services.GetService<IIdempotencyStore>();
        var unitOfWork = services.GetService<IUnitOfWork>();
        if (
            string.IsNullOrEmpty(message.IdempotencyKey)
            || string.IsNullOrEmpty(subject)
            || store is null
            || unitOfWork is null
        )
        {
            return await next(message, cancellationToken);
        }

        // Before any replay: a caller who lost access since the first request is refused.
        if (
            services.GetService<ICommandAuthorizer<TMessage>>() is { } authorizer
            && !await authorizer.IsAllowedAsync(message, cancellationToken)
        )
        {
            return Failure("Forbidden");
        }

        var scope = new IdempotencyScope(subject, message.Operation, message.IdempotencyKey);
        var fingerprint = Fingerprint(message);

        // Two passes at most: if the record we collided with was cleaned up meanwhile, claim again.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
            if (!await store.TryClaimAsync(scope, fingerprint, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                var existing = await store.FindAsync(scope, cancellationToken);
                if (existing is null)
                {
                    continue;
                }
                return existing.Fingerprint == fingerprint
                    ? Replay(existing.ResultPayload)
                    : Failure("Conflict", IdempotencyResults.KeyReused);
            }

            var response = await next(message, cancellationToken);
            if (!response.IsOk())
            {
                await transaction.RollbackAsync(cancellationToken);
                return response;
            }

            await store.CompleteAsync(Serialize(response), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }

        return await next(message, cancellationToken);
    }

    /// <summary>
    /// SHA-256 of the operation and the command's semantic inputs (route values included), as
    /// canonical JSON without the key itself. Not the raw body: reordered JSON is the same request.
    /// </summary>
    public static string Fingerprint(TMessage message)
    {
        var node = JsonSerializer.SerializeToNode(message, message.GetType(), Json)!.AsObject();
        node.Remove(
            JsonNamingPolicy.CamelCase.ConvertName(nameof(IIdempotentCommand.IdempotencyKey))
        );
        var canonical = $"{message.Operation}\n{node.ToJsonString(Json)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Serialize(TResponse response)
    {
        var envelope = new JsonObject { ["status"] = response.Status.ToString() };
        if (response.ValueType is { } valueType && valueType != typeof(Ardalis.Result.Result))
        {
            envelope["value"] = JsonSerializer.SerializeToNode(
                response.GetValue(),
                valueType,
                Json
            );
        }
        return envelope.ToJsonString(Json);
    }

    private static TResponse Replay(string? payload)
    {
        var envelope = JsonNode.Parse(payload ?? "{}")!.AsObject();
        var status = envelope["status"]?.GetValue<string>() ?? nameof(ResultStatus.Ok);
        if (typeof(TResponse) == typeof(Ardalis.Result.Result))
        {
            return (TResponse)
                (object)(
                    status == nameof(ResultStatus.NoContent) ? Result.NoContent() : Result.Success()
                );
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var value = envelope["value"].Deserialize(valueType, Json);
        var success = typeof(TResponse).GetMethod(
            nameof(Result<object>.Success),
            BindingFlags.Public | BindingFlags.Static,
            [valueType]
        )!;
        return (TResponse)success.Invoke(null, [value])!;
    }

    /// <summary>A failed <typeparamref name="TResponse"/> through its static factory (Forbidden, Conflict, ...).</summary>
    private static TResponse Failure(string factory, params string[] errors)
    {
        var withErrors = typeof(TResponse).GetMethod(
            factory,
            BindingFlags.Public | BindingFlags.Static,
            [typeof(string[])]
        );
        if (withErrors is not null)
        {
            return (TResponse)withErrors.Invoke(null, [errors])!;
        }
        return (TResponse)
            typeof(TResponse)
                .GetMethod(factory, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)!
                .Invoke(null, null)!;
    }
}

internal static class ResultStatusExtensions
{
    public static bool IsOk(this Ardalis.Result.IResult result) =>
        result.Status is ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent;
}
