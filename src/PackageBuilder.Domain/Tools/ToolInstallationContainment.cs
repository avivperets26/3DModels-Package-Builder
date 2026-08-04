namespace PackageBuilder.Domain.Tools;

/// <summary>Describes whether an installation is inside the configured project tool root.</summary>
public enum ToolInstallationContainment
{
    /// <summary>The executable is a strict descendant of the approved tool root.</summary>
    Contained = 0,

    /// <summary>The executable is outside the approved tool root and is informational only.</summary>
    External = 1,
}
