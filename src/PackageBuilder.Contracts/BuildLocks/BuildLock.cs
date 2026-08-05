using System.Text.RegularExpressions;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.BuildLocks;

public sealed record BuildLockWorker(string Id, string Version);

public sealed record BuildLockMarketplaceProfile(string Marketplace, string Profile, string Version);

/// <summary>All exact version inputs required to reproduce one completed build job.</summary>
public sealed record BuildLock(
    BuildJobId JobId,
    string PackageBuilderVersion,
    ToolVersion DotNetSdk,
    ToolVersion Blender,
    ToolVersion Unity,
    ToolVersion Unreal,
    int ManifestSchemaVersion,
    IReadOnlyList<BuildLockWorker> Workers,
    IReadOnlyList<BuildLockMarketplaceProfile> MarketplaceProfiles);

internal static partial class BuildLockValueValidator
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    public static bool IsIdentifier(string? value) =>
        value is { Length: > 0 } && IdentifierPattern().IsMatch(value);

    public static bool IsVersion(string? value) =>
        value is { Length: > 0 and <= 128 } && VersionPattern().IsMatch(value);
}
