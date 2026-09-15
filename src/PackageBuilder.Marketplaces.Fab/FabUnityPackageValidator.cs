using System.Collections.Immutable;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>A measured Unity asset from the complete post-import AssetDatabase inventory. Directories
/// are excluded; content hashes exclude .meta bytes. Dependencies are canonical AssetDatabase paths.</summary>
public sealed record FabUnityAsset(string Path, ArtifactContentIdentity Content,
    ImmutableArray<string> Dependencies, bool IsUsed);

/// <summary>A manifest-declared external package dependency, installed and disclosed to the customer.</summary>
public sealed record FabUnityDependency(string Root, string Version, bool Installed, bool Disclosed);

/// <summary>Trusted Unity worker/clean-import observation bound to package bytes, not supplier-authored JSON.
/// The caller maps the existing UnityPackageValidator and clean-reimport results without dropping findings.
/// ExpectedAssets comes from the build plan; Assets comes from complete clean-import inspection.</summary>
public sealed record FabUnityPackageInspection(BuildArtifactId ArtifactId, ArtifactContentIdentity Content,
    string FileName, string ProductRoot, ImmutableArray<string> ExpectedAssets,
    ImmutableArray<FabUnityAsset> Assets, ImmutableArray<FabUnityDependency> ExternalDependencies,
    FabTargetEvidence PackageValidation, FabTargetEvidence CleanImport);

/// <summary>Applies Fab's delegated Unity listing rules to existing engine validation evidence.
/// It performs no import or filesystem access, and cannot substitute for execution in the approved Editor.</summary>
public static class FabUnityPackageValidator
{
    /// <summary>Checks a complete trusted import observation against the independent plan and pinned rules.</summary>
    public static FabArtifactValidation Validate(FabRequirementsProfile profile, FabValidationContext? context,
        FabUnityPackageInspection? package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = new FabValidation(profile);
        if (!validation.Context(context))
        { return validation.Result(); }
        if (package?.ArtifactId is null || package.Content is null || package.Assets.IsDefaultOrEmpty
            || package.Assets.Length > 100_000 || package.Assets.Any(asset => asset is null || asset.Content is null)
            || package.ExpectedAssets.IsDefaultOrEmpty || package.ExpectedAssets.Any(string.IsNullOrWhiteSpace)
            || package.ExternalDependencies.IsDefault || package.ExternalDependencies.Length > 256
            || package.ExternalDependencies.Any(dependency => dependency is null))
        {
            validation.Add("FAB_UNITY_EVIDENCE_INVALID", "The complete Unity package inventory is missing or invalid.", "Run package validation and a complete clean import.");
            return validation.Result();
        }
        BuildArtifactId id = package.ArtifactId;
        if (!context!.Listing.Formats.Contains("unity") || !FabContentPath.Valid(package.FileName)
            || package.FileName.Contains('/') || !package.FileName.EndsWith(".unitypackage", StringComparison.Ordinal))
        { validation.Add("FAB_UNITY_FORMAT_INVALID", "This delivery is not a selected Unity package.", "Provide the selected .unitypackage artifact.", id); }
        _ = validation.Evidence(package.PackageValidation, context, id, package.Content, "unity-package");
        _ = validation.Evidence(package.CleanImport, context, id, package.Content, "unity-clean-import");
        _ = validation.Bound("unity-package-bytes", "unity", "bytes", package.Content.Bytes, id);
        validation.RequireRule("unity-delegation", "unity");
        validation.RequireRule("unity-structure", "unity");
        validation.RequireRule("unity-documentation", "documentation");
        validation.RequireRule("delivery-paths", "archives");
        validation.RequireRule("unity-redundancy", "unity");
        validation.RequireRule("unity-generated-content", "unity");
        string[] paths = [.. package.Assets.Select(asset => asset.Path)];
        bool validPaths = paths.All(FabContentPath.Valid);
        if (!validPaths || !FabContentPath.Valid(package.ProductRoot) || !package.ProductRoot.StartsWith("Assets/", StringComparison.Ordinal)
            || paths.Any(path => !path.StartsWith(package.ProductRoot + "/", StringComparison.Ordinal)))
        { validation.Add("FAB_UNITY_ROOT_INVALID", "Unity assets must stay within the planned product root with safe names.", "Regenerate the root organization and names from the product plan.", id); }
        if (!paths.Order(StringComparer.Ordinal).SequenceEqual(package.ExpectedAssets.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        { validation.Add("FAB_UNITY_INVENTORY_INVALID", "Imported assets differ from the complete planned delivery.", "Remove unrelated files and restore missing planned assets.", id); }
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length
            || package.Assets.GroupBy(asset => asset.Content).Any(group => group.Count() > 1))
        { validation.Add("FAB_UNITY_DUPLICATE", "The package contains duplicate paths or identical asset payloads.", "Consolidate redundant assets and update their references.", id); }
        HashSet<string> pathSet = paths.ToHashSet(StringComparer.Ordinal);
        foreach (FabUnityAsset asset in package.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (asset.Path is null)
            { continue; }
            if (new[] { ".zip", ".unitypackage", ".rar", ".7z", ".mp4", ".mov", ".webm" }
                .Contains(Path.GetExtension(asset.Path), StringComparer.OrdinalIgnoreCase))
            { validation.Add("FAB_UNITY_NESTED_CONTENT", "Generated Unity content contains an unsupported nested archive or video.", "Remove the payload or obtain a reviewed supported source/supplement exception.", id); }
            _ = validation.Bound("unity-path-length", "unity", "count", asset.Path.Length, id);
            if (!asset.IsUsed || asset.Content.Bytes == 0 || asset.Path.Split('/').Any(segment =>
                segment.Contains("AssetStoreTools", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("Library", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("Temp", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            { validation.Add("FAB_UNITY_REDUNDANT", "An asset is empty, unused or in an excluded generated/tool folder.", "Remove unused or generated content and rebuild references.", id); }
            if (asset.Dependencies.IsDefault || asset.Dependencies.Any(string.IsNullOrWhiteSpace)
                || asset.Dependencies.Any(dependency => !pathSet.Contains(dependency) && !External(dependency, package.ExternalDependencies)))
            { validation.Add("FAB_UNITY_DEPENDENCY_INVALID", "An asset dependency is unresolved or undisclosed.", "Include required assets or install and disclose the exact external dependency.", id); }
        }
        if (package.ExternalDependencies.Any(dependency => !ValidExternal(dependency))
            || package.ExternalDependencies.Select(dependency => dependency.Root).Distinct(StringComparer.Ordinal).Count() != package.ExternalDependencies.Length)
        { validation.Add("FAB_UNITY_DEPENDENCY_INVALID", "The external dependency inventory is incomplete or duplicated.", "Record exact installed versions and customer disclosures.", id); }
        if (!paths.Any(path => path?.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) == true))
        { validation.Add("FAB_UNITY_DEMO_MISSING", "The generated product has no demo or overview scene.", "Include the validated overview scene.", id); }
        if (!paths.Any(path => path is not null && new[] { ".md", ".txt", ".pdf", ".html", ".rtf" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
        { validation.Add("FAB_UNITY_DOCS_MISSING", "Supported customer documentation is missing.", "Include usage, dependencies and setup documentation.", id); }
        validation.Review(["assets", "unity", "documentation"]);
        return validation.Result();
    }

    private static bool External(string path, ImmutableArray<FabUnityDependency> dependencies) =>
        dependencies.Any(dependency => ValidExternal(dependency) && path.StartsWith(dependency.Root + "/", StringComparison.Ordinal)
            && FabContentPath.Valid(path));

    private static bool ValidExternal(FabUnityDependency dependency) => dependency.Installed && dependency.Disclosed
        && dependency.Root is { Length: > 9 and <= 128 } && dependency.Root.StartsWith("Packages/", StringComparison.Ordinal)
        && dependency.Root.Count(character => character == '/') == 1 && FabContentPath.Valid(dependency.Root)
        && !string.IsNullOrWhiteSpace(dependency.Version) && dependency.Version.Length <= 128;
}

/// <summary>Conservative delivery naming policy using the shared filesystem-segment validator.
/// This is Package Builder policy, not a claim that Fab forbids all names outside this subset.</summary>
internal static class FabContentPath
{
    /// <summary>Accepts bounded logical paths whose individual name tokens satisfy shared safe naming.</summary>
    internal static bool Valid(string? path) => DeliveryPath.IsValid(path);
}
