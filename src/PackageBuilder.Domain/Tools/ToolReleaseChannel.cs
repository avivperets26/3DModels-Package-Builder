namespace PackageBuilder.Domain.Tools;

/// <summary>Distinguishes production releases from prerelease candidates.</summary>
public enum ToolReleaseChannel
{
    /// <summary>A production release with no prerelease marker.</summary>
    Stable = 0,

    /// <summary>An alpha, beta, preview, or release-candidate build.</summary>
    Preview = 1,
}
