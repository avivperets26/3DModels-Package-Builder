namespace PackageBuilder.Domain.Tools;

/// <summary>Identifies a supported build tool or engine without coupling the domain to a locator.</summary>
public enum ToolKind
{
    /// <summary>The repository-approved .NET SDK.</summary>
    DotNet = 0,

    /// <summary>The Blender content-processing engine.</summary>
    Blender = 1,

    /// <summary>The Unity editor.</summary>
    Unity = 2,

    /// <summary>The Unreal editor.</summary>
    Unreal = 3,
}
