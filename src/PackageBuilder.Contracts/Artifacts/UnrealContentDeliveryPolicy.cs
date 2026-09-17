namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Closed content-only static Unreal delivery layout shared by the target writer and marketplace validator.</summary>
public static class UnrealContentDeliveryPolicy
{
    /// <summary>Accepts only planned static product assets; plugins, source, caches and nested archives are excluded.</summary>
    public static bool Allows(string projectName, string path)
    {
        if (!DeliveryPath.IsValid(projectName) || projectName.Contains('/') || !DeliveryPath.IsValid(path))
        { return false; }
        if (path == projectName + ".uproject" || path == "Config/DefaultEngine.ini")
        { return true; }
        string root = "Content/" + projectName + "/";
        if (!path.StartsWith(root, StringComparison.Ordinal))
        { return false; }
        string[] segments = path[root.Length..].Split('/');
        return segments.Length == 2 && segments[0] switch
        {
            "Maps" => segments[1] == "L_Overview.umap",
            "Documentation" => segments[1] == "README.md",
            "Meshes" => Asset(segments[1], "SM_"),
            "Materials" => Asset(segments[1], "M_") || Asset(segments[1], "MI_"),
            "Textures" => Asset(segments[1], "T_"),
            "Preview" => Asset(segments[1], "BP_") || Asset(segments[1], "WBP_"),
            _ => false,
        };
    }

    private static bool Asset(string name, string prefix) => name.StartsWith(prefix, StringComparison.Ordinal)
        && name.EndsWith(".uasset", StringComparison.Ordinal) && name.Length > prefix.Length + 7;
}
