using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Contracts.Persistence;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Offline, bounded Fab schema-v1 loader; rejects unknown fields and ambiguous or unsourced rules.</summary>
public static class FabRequirementsProfileJson
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = JsonInputSafeguards.MaximumDepth,
    };

    private static readonly string[] _sections = ["assets", "unity", "unreal", "archives", "media", "documentation"];
    private static readonly string[] _formats = ["fbx", "glb", "unity", "unreal"];

    public static RepositoryOperationResult<FabRequirementsProfile> Load(string? json)
    {
        JsonInputError error = JsonInputSafeguards.TryParseObject(json, 262_144, out JsonDocument? parsed);
        if (error != JsonInputError.None)
        {
            return Invalid(error.ToString());
        }

        using (parsed!)
        {
            try
            {
                FabRequirementsDocument? document = parsed!.Deserialize<FabRequirementsDocument>(_options);
                if (document is null || !IsValid(document))
                {
                    return Invalid("Invalid profile identity, dates, sources, rules or listing requirements.");
                }

                FabRequirementsDocument canonical = document with
                {
                    Sources = [.. document.Sources.OrderBy(source => source.Id, StringComparer.Ordinal)],
                    Rules = [.. document.Rules.OrderBy(rule => rule.Id, StringComparer.Ordinal)],
                    Listings = [.. document.Listings.OrderBy(listing => listing.Category, StringComparer.Ordinal)
                        .Select(listing => listing with { Formats = [.. listing.Formats.Order(StringComparer.Ordinal)] })],
                };
                return RepositoryOperationResult.Success(new FabRequirementsProfile(
                    canonical, JsonSerializer.Serialize(canonical, _options)));
            }
            catch (JsonException)
            {
                return Invalid("Profile JSON does not match schema version 1.");
            }
        }
    }

    private static bool IsValid(FabRequirementsDocument document)
    {
        if (document.SchemaVersion != 1 || document.Marketplace != "fab" || document.Profile != "asset-listing"
            || !IsVersion(document.Version) || document.EffectiveOn == default
            || document.EffectiveDateBasis != "local-reviewed-adoption"
            || document.Sources.IsDefaultOrEmpty || document.Sources.Length > 16
            || document.Rules.IsDefaultOrEmpty || document.Rules.Length > 256
            || document.Listings.IsDefaultOrEmpty || document.Listings.Length != 2)
        {
            return false;
        }

        var sources = new HashSet<string>(StringComparer.Ordinal);
        if (!document.Sources.All(source => source is not null && IsId(source.Id) && sources.Add(source.Id)
                && IsSource(source.Url) && source.ReviewedOn != default && source.ReviewedOn <= document.EffectiveOn
                && (source.UpdatedOn is null || source.UpdatedOn != default(DateOnly) && source.UpdatedOn <= source.ReviewedOn)))
        {
            return false;
        }

        var rules = new HashSet<string>(StringComparer.Ordinal);
        if (!document.Rules.All(rule => rule is not null && IsId(rule.Id) && rules.Add(rule.Id)
                && _sections.Contains(rule.Section, StringComparer.Ordinal)
                && rule.Origin is "official" or "package-builder-policy" && sources.Contains(rule.Source)
                && rule.Status is "verified" or "unresolved" && Text(rule.Summary, 512)
                && (rule.Limit is null
                    ? rule.Unit is null && rule.Comparison is null
                    : rule.Limit is > 0 and <= 1_000_000_000_000 && rule.Status == "verified"
                        && rule.Unit is "bytes" or "pixels" or "count" && rule.Comparison is "lt" or "lte" or "gte"))
            || !_sections.All(section => document.Rules.Any(rule => rule.Section == section)))
        {
            return false;
        }

        var categories = new HashSet<string>(StringComparer.Ordinal);
        return document.Listings.All(listing => listing is not null
            && listing.Category is "3d-model" or "animation" && categories.Add(listing.Category)
            && !listing.Formats.IsDefaultOrEmpty && listing.Formats.Length <= _formats.Length
            && listing.Formats.All(format => _formats.Contains(format, StringComparer.Ordinal))
            && listing.Formats.Distinct(StringComparer.Ordinal).Count() == listing.Formats.Length
            && sources.Contains(listing.Source) && listing.CompanionPolicy == "package-builder-policy")
            && HasBound(document, "image-width", "pixels", "gte")
            && HasBound(document, "image-height", "pixels", "gte")
            && HasBound(document, "image-bytes", "bytes", "lt")
            && HasBound(document, "gallery-bytes", "bytes", "lt")
            && document.Rules.Single(rule => rule.Id == "image-bytes").Limit
                <= document.Rules.Single(rule => rule.Id == "gallery-bytes").Limit;
    }

    private static bool HasBound(FabRequirementsDocument document, string id, string unit, string comparison) =>
        document.Rules.Any(rule => rule.Id == id && rule.Section == "media" && rule.Status == "verified"
            && rule.Limit is not null && rule.Unit == unit && rule.Comparison == comparison);

    internal static bool IsVersion(string? value) => value is { Length: >= 12 and <= 20 }
        && value[10] == '.' && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _) && int.TryParse(value[11..], NumberStyles.None,
            CultureInfo.InvariantCulture, out int revision) && revision > 0 && value[11] != '0';

    private static bool IsId(string? value) => value is { Length: > 0 and <= 64 }
        && value[0] is >= 'a' and <= 'z' && value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');

    private static bool Text(string? value, int maximum) => CompatibilityEvidenceValidation.IsText(value, maximum);

    private static bool IsSource(string? value) => value is { Length: <= 512 }
        && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && (uri.Host == "dev.epicgames.com" && uri.AbsolutePath.StartsWith("/documentation/en-us/fab/", StringComparison.Ordinal)
            || uri.Host == "www.fab.com" && uri.AbsolutePath == "/o/technical-requirements"
            || uri.Host == "assetstore.unity.com" && uri.AbsolutePath == "/publishing/submission-guidelines"
            || uri.Host == "forums.unrealengine.com" && uri.AbsolutePath == "/t/request-for-uploading-large-file-package-on-fab-store/2539119");

    private static RepositoryOperationResult<FabRequirementsProfile> Invalid(string message) =>
        RepositoryOperationResult.Failure<FabRequirementsProfile>("FAB_PROFILE_INVALID", message);
}
