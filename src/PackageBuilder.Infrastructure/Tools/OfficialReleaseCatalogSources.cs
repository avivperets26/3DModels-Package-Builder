using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tools;

internal interface IOfficialReleaseCatalogSource
{
    ToolKind Tool { get; }

    Uri Source { get; }

    ReleaseCatalogResult<OfficialReleaseCatalog> Parse(
        ReadOnlyMemory<byte> contents,
        DateTimeOffset refreshedAtUtc);
}

internal static class OfficialReleaseCatalogSources
{
    private static readonly Dictionary<ToolKind, IOfficialReleaseCatalogSource> _sources =
        new()
        {
            [ToolKind.DotNet] = new DotNetReleaseCatalogSource(),
            [ToolKind.Blender] = new BlenderReleaseCatalogSource(),
            [ToolKind.Unity] = new UnityReleaseCatalogSource(),
            [ToolKind.Unreal] = new UnrealReleaseCatalogSource(),
        };

    public static bool TryGet(ToolKind tool, out IOfficialReleaseCatalogSource? source) =>
        _sources.TryGetValue(tool, out source);
}

internal abstract class JsonReleaseCatalogSource : IOfficialReleaseCatalogSource
{
    internal const int MaximumReleaseCount = 512;

    public abstract ToolKind Tool { get; }

    public abstract Uri Source { get; }

    public ReleaseCatalogResult<OfficialReleaseCatalog> Parse(
        ReadOnlyMemory<byte> contents,
        DateTimeOffset refreshedAtUtc)
    {
        try
        {
            using var document = JsonDocument.Parse(
                contents,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            return !HasUniqueProperties(document.RootElement)
                ? Failure("Official release metadata contains duplicate JSON properties.")
                : ParseDocument(document.RootElement, refreshedAtUtc);
        }
        catch (JsonException)
        {
            return Failure("Official release metadata is not valid bounded JSON.");
        }
    }

    protected abstract ReleaseCatalogResult<OfficialReleaseCatalog> ParseDocument(
        JsonElement root,
        DateTimeOffset refreshedAtUtc);

    protected ReleaseCatalogResult<OfficialReleaseCatalog> Create(
        DateTimeOffset refreshedAtUtc,
        IEnumerable<OfficialToolRelease> releases) =>
        OfficialReleaseCatalog.Create(Tool, Source, refreshedAtUtc, releases);

    internal static bool TryVersion(
        ToolKind tool,
        string? value,
        out ToolVersion? version)
    {
        ToolModelValidationResult<ToolVersion> result = ToolVersion.Create(tool, value);
        version = result.Value;
        return result.IsValid;
    }

    protected static DateTimeOffset? ParseUtcDate(JsonElement element, string propertyName)
    {
        return !element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(
                property.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset value)
            ? null
            : value;
    }

    protected static ReleaseCatalogResult<OfficialReleaseCatalog> Failure(string message) =>
        ReleaseCatalogResult.Failure<OfficialReleaseCatalog>("RELEASE_SOURCE_METADATA_INVALID", message);

    private static bool HasUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || !HasUniqueProperties(property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!HasUniqueProperties(item))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

internal sealed class DotNetReleaseCatalogSource : JsonReleaseCatalogSource
{
    public override ToolKind Tool => ToolKind.DotNet;

    public override Uri Source { get; } = new(
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json");

    protected override ReleaseCatalogResult<OfficialReleaseCatalog> ParseDocument(
        JsonElement root,
        DateTimeOffset refreshedAtUtc)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("releases-index", out JsonElement entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            return Failure("The .NET release index does not contain a releases-index array.");
        }

        var releases = new List<OfficialToolRelease>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (releases.Count >= MaximumReleaseCount)
            {
                return Failure("The .NET release index exceeds the bounded release count.");
            }

            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("support-phase", out JsonElement supportPhase)
                || supportPhase.ValueKind != JsonValueKind.String
                || string.Equals(supportPhase.GetString(), "eol", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!entry.TryGetProperty("latest-sdk", out JsonElement latestSdk)
                || latestSdk.ValueKind != JsonValueKind.String
                || !TryVersion(Tool, latestSdk.GetString(), out ToolVersion? version))
            {
                return Failure("A current .NET channel contains an invalid latest-sdk version.");
            }

            ToolReleaseSupport support = entry.TryGetProperty("release-type", out JsonElement releaseType)
                && releaseType.ValueKind == JsonValueKind.String
                && string.Equals(releaseType.GetString(), "lts", StringComparison.OrdinalIgnoreCase)
                    ? ToolReleaseSupport.LongTermSupport
                    : ToolReleaseSupport.Standard;
            releases.Add(new OfficialToolRelease(
                version!,
                support,
                ParseUtcDate(entry, "latest-release-date")));
        }

        return Create(refreshedAtUtc, releases);
    }
}

internal sealed class UnityReleaseCatalogSource : JsonReleaseCatalogSource
{
    public override ToolKind Tool => ToolKind.Unity;

    public override Uri Source { get; } = new(
        "https://services.api.unity.com/unity/editor/release/v1/releases?limit=100&offset=0&order=RELEASE_DATE_DESC");

    protected override ReleaseCatalogResult<OfficialReleaseCatalog> ParseDocument(
        JsonElement root,
        DateTimeOffset refreshedAtUtc)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("results", out JsonElement entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            return Failure("The Unity Release API response does not contain a results array.");
        }

        var releases = new List<OfficialToolRelease>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (releases.Count >= MaximumReleaseCount)
            {
                return Failure("The Unity Release API response exceeds the bounded release count.");
            }

            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("version", out JsonElement versionElement)
                || versionElement.ValueKind != JsonValueKind.String
                || !TryVersion(Tool, versionElement.GetString(), out ToolVersion? version))
            {
                return Failure("A Unity release contains an invalid canonical version.");
            }

            ToolReleaseSupport support = entry.TryGetProperty("stream", out JsonElement stream)
                && stream.ValueKind == JsonValueKind.String
                && string.Equals(stream.GetString(), "LTS", StringComparison.OrdinalIgnoreCase)
                    ? ToolReleaseSupport.LongTermSupport
                    : ToolReleaseSupport.Standard;
            releases.Add(new OfficialToolRelease(
                version!,
                support,
                ParseUtcDate(entry, "releaseDate")));
        }

        return Create(refreshedAtUtc, releases);
    }
}

internal abstract partial class HtmlReleaseCatalogSource : IOfficialReleaseCatalogSource
{
    private const int MaximumTextCharacters = 4_194_304;

    public abstract ToolKind Tool { get; }

    public abstract Uri Source { get; }

    protected abstract Regex ReleasePattern { get; }

    public ReleaseCatalogResult<OfficialReleaseCatalog> Parse(
        ReadOnlyMemory<byte> contents,
        DateTimeOffset refreshedAtUtc)
    {
        string html;
        try
        {
            html = new UTF8Encoding(false, true).GetString(contents.Span);
        }
        catch (DecoderFallbackException)
        {
            return Failure("Official release metadata is not valid UTF-8.");
        }

        if (html.Length > MaximumTextCharacters)
        {
            return Failure("Official release metadata exceeds the bounded text size.");
        }

        try
        {
            string text = WebUtility.HtmlDecode(TagPattern().Replace(html, " "));
            var releases = new Dictionary<string, OfficialToolRelease>(StringComparer.Ordinal);
            foreach (Match match in ReleasePattern.Matches(text))
            {
                if (releases.Count >= JsonReleaseCatalogSource.MaximumReleaseCount)
                {
                    return Failure("Official release metadata exceeds the bounded release count.");
                }

                if (!TryNormalizeVersion(match, out string normalized)
                    || !JsonReleaseCatalogSource.TryVersion(Tool, normalized, out ToolVersion? version))
                {
                    continue;
                }

                ToolReleaseSupport support = match.Groups["lts"].Success
                    ? ToolReleaseSupport.LongTermSupport
                    : ToolReleaseSupport.Standard;
                _ = releases.TryAdd(version!.Value, new OfficialToolRelease(version, support, ReleasedAtUtc: null));
            }

            return OfficialReleaseCatalog.Create(Tool, Source, refreshedAtUtc, releases.Values);
        }
        catch (RegexMatchTimeoutException)
        {
            return Failure("Official release metadata could not be parsed within the bounded time.");
        }
    }

    protected virtual bool TryNormalizeVersion(Match match, out string normalized)
    {
        normalized = match.Groups["version"].Value;
        if (normalized.Count(static value => value == '.') == 1)
        {
            normalized += ".0";
        }

        if (match.Groups["preview"].Success)
        {
            string label = match.Groups["preview"].Value.ToLowerInvariant() switch
            {
                "alpha" => "alpha",
                "beta" => "beta",
                "rc" or "release candidate" => "rc",
                _ => "preview",
            };
            string number = match.Groups["previewNumber"].Success
                ? match.Groups["previewNumber"].Value
                : "1";
            normalized += $"-{label}.{number}";
        }

        return true;
    }

    protected static ReleaseCatalogResult<OfficialReleaseCatalog> Failure(string message) =>
        ReleaseCatalogResult.Failure<OfficialReleaseCatalog>("RELEASE_SOURCE_METADATA_INVALID", message);

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex TagPattern();
}

internal sealed partial class BlenderReleaseCatalogSource : HtmlReleaseCatalogSource
{
    public override ToolKind Tool => ToolKind.Blender;

    public override Uri Source { get; } = new("https://www.blender.org/download/releases/");

    protected override Regex ReleasePattern => BlenderReleasePattern();

    [GeneratedRegex(
        @"\bBlender\s+(?<version>\d+\.\d+(?:\.\d+)?)\s*(?<lts>LTS)?(?:\s+(?<preview>Alpha|Beta|RC|Release Candidate)\s*(?<previewNumber>\d+)?)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex BlenderReleasePattern();
}

internal sealed partial class UnrealReleaseCatalogSource : HtmlReleaseCatalogSource
{
    public override ToolKind Tool => ToolKind.Unreal;

    public override Uri Source { get; } = new(
        "https://dev.epicgames.com/documentation/unreal-engine/whats-new?lang=en-US");

    protected override Regex ReleasePattern => UnrealReleasePattern();

    [GeneratedRegex(
        @"\bUnreal Engine\s+(?<version>\d+\.\d+(?:\.\d+)?)(?:\s+(?<preview>Preview)\s*(?<previewNumber>\d+))?(?:\s+Release Notes)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex UnrealReleasePattern();
}
