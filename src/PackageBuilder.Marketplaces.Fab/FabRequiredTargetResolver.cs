using System.Collections.Immutable;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Companions and formats are not extra engine families.</summary>
public enum FabOutput { Portable, Unity, Unreal, Glb, Documentation, Media }

/// <summary>Explicit listing choices; an animated model is not automatically an Animation-category listing.</summary>
public sealed record FabListingConfiguration(
    ProductCase ProductCase, string Category, ImmutableArray<string> Formats,
    bool IncludeDocumentation = false, bool IncludeMedia = false);

/// <summary>Deterministic requirements preview. Unresolved rules require review, never a compliance pass.</summary>
public sealed record FabRequiredTargets(
    ImmutableArray<FabOutput> Required, ImmutableArray<FabOutput> Optional,
    ImmutableArray<BuildTarget> BuildTargets, ImmutableArray<FabRequirementRule> UnresolvedRules,
    string ProfileSha256);

/// <summary>Resolves selected profile-supported downloads and local companion defaults without starting builds.</summary>
public static class FabRequiredTargetResolver
{
    public static RepositoryOperationResult<FabRequiredTargets> Resolve(
        FabRequirementsProfile profile, FabListingConfiguration? listing)
    {
        ArgumentNullException.ThrowIfNull(profile);
        FabListingRequirements? rules = profile.Document.Listings.SingleOrDefault(value => value.Category == listing?.Category);
        if (listing?.ProductCase is null || rules is null || listing.Formats.IsDefaultOrEmpty
            || listing.Formats.Length > 4 || listing.Formats.Distinct(StringComparer.Ordinal).Count() != listing.Formats.Length
            || listing.Formats.Any(format => !rules.Formats.Contains(format, StringComparer.Ordinal)))
        {
            return RepositoryOperationResult.Failure<FabRequiredTargets>(
                "FAB_LISTING_INVALID", "Select a supported category and one or more distinct allowed download formats.");
        }

        // A scoped review must not silently certify product cases or delivery types it did not cover.
        if (profile.Document.Rules.Any(rule => rule.Id == "static-review-scope")
            && (listing.ProductCase != ProductCase.Static || listing.Category != "3d-model"
                || listing.Formats.Any(format => format is not ("fbx" or "unity")
                    && !(format == "unreal" && profile.Document.Rules.Any(rule => rule.Id == "static-unreal-review-scope" && rule.Status == "verified")))))
        {
            return RepositoryOperationResult.Failure<FabRequiredTargets>(
                "FAB_REVIEW_SCOPE_UNSUPPORTED", "This reviewed profile does not cover the selected product case or format.");
        }

        var required = new HashSet<FabOutput>();
        var available = new HashSet<FabOutput>();
        foreach (string format in listing.Formats)
        { AddFormat(required, format); }
        foreach (string format in rules.Formats)
        { AddFormat(available, format); }
        if (rules.DocumentationRequired || listing.IncludeDocumentation)
        { _ = required.Add(FabOutput.Documentation); }
        if (rules.MediaRequired || listing.IncludeMedia)
        { _ = required.Add(FabOutput.Media); }
        available.UnionWith([FabOutput.Documentation, FabOutput.Media]);
        available.ExceptWith(required);
        var sections = new HashSet<string>(StringComparer.Ordinal) { "assets", "archives" };
        if (required.Contains(FabOutput.Unity))
        { _ = sections.Add("unity"); }
        if (required.Contains(FabOutput.Unreal))
        { _ = sections.Add("unreal"); }
        if (required.Contains(FabOutput.Media))
        { _ = sections.Add("media"); }
        if (required.Contains(FabOutput.Documentation))
        { _ = sections.Add("documentation"); }
        // The ambiguous exchange-format size applies only to portable downloads.
        ImmutableArray<FabRequirementRule> unresolved = [.. profile.Document.Rules.Where(rule =>
            rule.Status == "unresolved" && sections.Contains(rule.Section)
            && (rule.Id != "other-format-bytes" || required.Contains(FabOutput.Portable)))];
        ImmutableArray<BuildTarget> targets = [.. BuildTarget.All.Where(target => required.Contains(target.CanonicalIdentifier switch
        {
            "portable" => FabOutput.Portable, "unity" => FabOutput.Unity, "unreal" => FabOutput.Unreal,
            _ => throw new InvalidOperationException("Unknown build target family."),
        }))];
        return RepositoryOperationResult.Success(new FabRequiredTargets(
            [.. required.Order()], [.. available.Order()], targets, unresolved, profile.Sha256));
    }

    private static void AddFormat(HashSet<FabOutput> outputs, string format)
    {
        switch (format)
        {
            case "fbx":
                _ = outputs.Add(FabOutput.Portable);
                break;
            case "glb":
                _ = outputs.Add(FabOutput.Portable);
                _ = outputs.Add(FabOutput.Glb);
                break;
            case "unity":
                _ = outputs.Add(FabOutput.Unity);
                break;
            case "unreal":
                _ = outputs.Add(FabOutput.Unreal);
                break;
            default:
                throw new InvalidOperationException("The validated profile contains an unsupported format.");
        }
    }
}
