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
        return validatedFiles.IsDefaultOrEmpty || validatedFiles.Any(f => f is null || !Allowed(projectName, f.Path))
            || !validatedFiles.Any(f => f.Path == projectName + ".uproject")
            || !validatedFiles.Any(f => f.Path == "Config/DefaultEngine.ini")
            || !validatedFiles.Any(f => f.Path == $"Content/{projectName}/Maps/L_Overview.umap")
            || !validatedFiles.Any(f => f.Path == $"Content/{projectName}/Documentation/README.md")
            ? throw new ArgumentException("A complete, validated content-only Unreal project is required.", nameof(validatedFiles))
            : writer.WriteAsync([.. validatedFiles.Select(f => f with { Path = projectName + "/" + f.Path })],
            destination, cancellationToken);
    }

    private static bool Allowed(string projectName, string path)
    {
        if (!DeliveryPath.IsValid(path))
        { return false; }
        if (path == projectName + ".uproject" || path == "Config/DefaultEngine.ini")
        { return true; }
        string root = "Content/" + projectName + "/";
        if (!path.StartsWith(root, StringComparison.Ordinal))
        { return false; }
        string relative = path[root.Length..];
        string[] segments = relative.Split('/');
        return segments.Length == 2 && segments[0] switch
        {
            "Maps" => segments[1] == "L_Overview.umap",
            "Documentation" => segments[1] == "README.md",
            "Meshes" => segments[1].StartsWith("SM_", StringComparison.Ordinal) && segments[1].EndsWith(".uasset", StringComparison.Ordinal),
            "Materials" or "Textures" or "Preview" => segments[1].EndsWith(".uasset", StringComparison.Ordinal),
            _ => false,
        };
    }
}
