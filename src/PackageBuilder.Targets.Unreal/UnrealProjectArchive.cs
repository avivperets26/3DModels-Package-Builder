using System.Collections.Immutable;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Builds a content-only Unreal delivery from an explicit, native-validated file inventory.
/// Callers must supply the exact identities returned after import-path sanitization and validation.</summary>
public static class UnrealProjectArchive
{
    /// <summary>Applies the Unreal allowlist, then delegates deterministic, hash-checked ZIP writing.
    /// No recursive discovery, generated caches, plugins, binaries or source code enter the archive.</summary>
    public static Task<ArtifactContentIdentity> CreateAsync(string projectName,
        ImmutableArray<ReleaseArchiveEntry> validatedFiles, IReleaseArchiveWriter writer, Stream destination,
        CancellationToken cancellationToken = default)
    {
        UnrealImportPlan.ValidateName(projectName);
        ArgumentNullException.ThrowIfNull(writer);
        return validatedFiles.IsDefaultOrEmpty || validatedFiles.Any(f => f is null || !UnrealContentDeliveryPolicy.Allows(projectName, f.Path))
            || !validatedFiles.Any(f => f.Path == projectName + ".uproject")
            || !validatedFiles.Any(f => f.Path == "Config/DefaultEngine.ini")
            || !validatedFiles.Any(f => f.Path == $"Content/{projectName}/Maps/L_Overview.umap")
            || !validatedFiles.Any(f => f.Path == $"Content/{projectName}/Documentation/README.md")
            ? throw new ArgumentException("A complete, validated content-only Unreal project is required.", nameof(validatedFiles))
            : writer.WriteAsync([.. validatedFiles.Select(f => f with { Path = projectName + "/" + f.Path })],
            destination, cancellationToken);
    }

}
