using System.Globalization;
using AppTemplate.UseCases.Concurrency;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace AppTemplate.Web.Http;

/// <summary>
/// ETags for versioned resources: a quoted, strong tag holding the row version (<c>"1234"</c>),
/// and <c>If-Match</c> parsed per RFC 9110 rather than compared as a raw string.
/// </summary>
public static class EntityTagHeader
{
    public static string Format(uint version) =>
        new EntityTagHeaderValue(
            $"\"{version.ToString(CultureInfo.InvariantCulture)}\""
        ).ToString();

    /// <summary>
    /// <list type="bullet">
    /// <item>no header: <see cref="IfMatch.None"/> (no precondition)</item>
    /// <item><c>*</c>: met when the resource exists</item>
    /// <item>a list of tags: met when any <b>strong</b> tag equals the current version; a weak tag
    /// never matches, because <c>If-Match</c> uses the strong comparison</item>
    /// <item>anything unparsable: <see cref="IfMatch.Malformed"/> (400)</item>
    /// </list>
    /// </summary>
    public static IfMatch ParseIfMatch(StringValues header)
    {
        if (StringValues.IsNullOrEmpty(header))
        {
            return IfMatch.None;
        }
        if (!EntityTagHeaderValue.TryParseStrictList(header, out var tags) || tags.Count == 0)
        {
            return IfMatch.Malformed;
        }
        if (tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any)))
        {
            return new IfMatch(VersionPrecondition.Any);
        }

        var versions = tags.Where(tag => !tag.IsWeak)
            .Select(tag => tag.Tag.Value?.Trim('"'))
            .Select(value =>
                uint.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var version
                )
                    ? (uint?)version
                    : null
            )
            .OfType<uint>()
            .ToList();
        // A well-formed tag that isn't one of our versions just never matches.
        return new IfMatch(new VersionPrecondition(false, versions));
    }
}

/// <param name="Precondition">Null when the request had no <c>If-Match</c>.</param>
public sealed record IfMatch(VersionPrecondition? Precondition, bool IsMalformed = false)
{
    public static IfMatch None { get; } = new((VersionPrecondition?)null);

    public static IfMatch Malformed { get; } = new(null, IsMalformed: true);
}
