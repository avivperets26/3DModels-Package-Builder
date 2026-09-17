using System.Collections.Immutable;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Products;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>One delivered file measured after clean extraction. Native inspection supplies usage and redirector state.</summary>
public sealed record FabUnrealFile(string Path, ArtifactContentIdentity Content, bool IsUsed, bool IsRedirector);

/// <summary>Trusted native observations bound to the exact archive, independent planned paths and clean reopen.
/// Project paths are relative to the project directory. Supplier-authored pass flags are not admissible evidence.</summary>
public sealed record FabUnrealProjectInspection(BuildArtifactId ArtifactId, ArtifactContentIdentity Content,
    string FileName, string ProjectName, string EngineVersion, ImmutableArray<string> ExpectedFiles,
    ImmutableArray<FabUnrealFile> Files, FabTargetEvidence ProjectValidation, FabTargetEvidence CleanReopen,
    FabTargetEvidence LogValidation);

/// <summary>Applies pinned Fab requirements to the existing native content-only validation and extraction evidence.</summary>
public static class FabUnrealProjectValidator
{
    /// <summary>Checks structure, usage, redirectors, documentation and native gates without starting an engine.</summary>
    public static FabArtifactValidation Validate(FabRequirementsProfile profile, FabValidationContext? context,
        FabUnrealProjectInspection? project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = new FabValidation(profile);
        if (!validation.Context(context))
        { return validation.Result(); }
        if (project?.ArtifactId is null || project.Content is null || project.Files.IsDefaultOrEmpty
            || project.Files.Length > 100_000 || project.Files.Any(file => file is null || file.Content is null)
            || project.ExpectedFiles.IsDefaultOrEmpty || project.ExpectedFiles.Length > 100_000
            || project.ExpectedFiles.Any(string.IsNullOrWhiteSpace)
            || !DeliveryPath.IsValid(project.ProjectName) || project.ProjectName.Contains('/')
            || string.IsNullOrWhiteSpace(project.EngineVersion))
        {
            validation.Add("FAB_UNREAL_EVIDENCE_INVALID", "Complete Unreal project observations are missing or invalid.", "Validate and reopen the exact generated ZIP.");
            return validation.Result();
        }
        BuildArtifactId id = project.ArtifactId;
        if (context!.Listing.ProductCase != ProductCase.Static || !context.Listing.Formats.Contains("unreal") || !DeliveryPath.IsValid(project.FileName)
            || project.FileName.Contains('/') || !project.FileName.EndsWith(".zip", StringComparison.Ordinal))
        { validation.Add("FAB_UNREAL_FORMAT_INVALID", "The artifact is not a selected Unreal ZIP.", "Supply the selected content-only project archive.", id); }
        _ = validation.Evidence(project.ProjectValidation, context, id, project.Content, "unreal-project");
        _ = validation.Evidence(project.CleanReopen, context, id, project.Content, "unreal-clean-reopen");
        _ = validation.Evidence(project.LogValidation, context, id, project.Content, "unreal-logs");
        validation.RequireRule("unreal-layout", "unreal");
        validation.RequireRule("unreal-overview", "unreal");
        validation.RequireRule("delivery-paths", "archives");
        // Fab's project limit applies before ZIP compression, not just to the uploaded archive.
        _ = validation.Bound("unreal-project-bytes", "unreal", "bytes", project.Files.Sum(file => (decimal)file.Content.Bytes) > long.MaxValue
            ? long.MaxValue : project.Files.Sum(file => file.Content.Bytes), id);
        string[] paths = [.. project.Files.Select(file => file.Path)];
        if (paths.Any(path => !UnrealContentDeliveryPolicy.Allows(project.ProjectName, path))
            || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length
            || !paths.Order(StringComparer.Ordinal).SequenceEqual(project.ExpectedFiles.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        { validation.Add("FAB_UNREAL_LAYOUT_INVALID", "Unreal content differs from the planned clean project layout.", "Remove extra projects, caches, code, plugins and unplanned or incorrectly named assets.", id); }
        if (!paths.Contains(project.ProjectName + ".uproject", StringComparer.Ordinal)
            || !paths.Contains("Config/DefaultEngine.ini", StringComparer.Ordinal))
        { validation.Add("FAB_UNREAL_PROJECT_MISSING", "The project descriptor or configuration is missing.", "Regenerate the complete project.", id); }
        if (!paths.Contains($"Content/{project.ProjectName}/Maps/L_Overview.umap", StringComparer.Ordinal))
        { validation.Add("FAB_UNREAL_OVERVIEW_MISSING", "The product overview map is missing.", "Include the validated overview map.", id); }
        if (!paths.Contains($"Content/{project.ProjectName}/Documentation/README.md", StringComparer.Ordinal))
        { validation.Add("FAB_UNREAL_DOCS_MISSING", "Customer documentation is missing.", "Include the generated usage instructions.", id); }
        foreach (FabUnrealFile file in project.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!file.IsUsed || file.IsRedirector || file.Content.Bytes == 0)
            { validation.Add("FAB_UNREAL_UNUSED_OR_REDIRECTOR", "An unused, empty or redirector file remains.", "Repair redirectors and remove unused content, then reopen and validate again.", id); }
        }
        validation.Review(["assets", "unreal", "documentation"]);
        return validation.Result();
    }
}
