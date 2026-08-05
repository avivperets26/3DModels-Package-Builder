using System.Collections.ObjectModel;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Contracts.Tools;

/// <summary>Vendor support classification attached to a published tool release.</summary>
public enum ToolReleaseSupport
{
    Standard = 0,
    LongTermSupport = 1,
}

/// <summary>Identifies where a returned catalog was obtained.</summary>
public enum ReleaseCatalogOrigin
{
    Cache = 0,
    FreshNetwork = 1,
    LastKnownCache = 2,
}

/// <summary>One normalized release from an official vendor source.</summary>
public sealed record OfficialToolRelease(
    ToolVersion Version,
    ToolReleaseSupport Support,
    DateTimeOffset? ReleasedAtUtc);

/// <summary>Immutable, validated release metadata for one supported tool.</summary>
public sealed class OfficialReleaseCatalog
{
    private OfficialReleaseCatalog(
        ToolKind tool,
        Uri source,
        DateTimeOffset refreshedAtUtc,
        ReadOnlyCollection<OfficialToolRelease> releases)
    {
        Tool = tool;
        Source = source;
        RefreshedAtUtc = refreshedAtUtc;
        Releases = releases;
    }

    public ToolKind Tool { get; }

    public Uri Source { get; }

    public DateTimeOffset RefreshedAtUtc { get; }

    public IReadOnlyList<OfficialToolRelease> Releases { get; }

    public static ReleaseCatalogResult<OfficialReleaseCatalog> Create(
        ToolKind tool,
        Uri? source,
        DateTimeOffset refreshedAtUtc,
        IEnumerable<OfficialToolRelease?>? releases)
    {
        if (!Enum.IsDefined(tool))
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                "RELEASE_CATALOG_TOOL_INVALID",
                "The release catalog tool is not supported.");
        }

        if (source is null || !source.IsAbsoluteUri || source.Scheme != Uri.UriSchemeHttps)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                "RELEASE_CATALOG_SOURCE_INVALID",
                "The official release source must be an absolute HTTPS URI.");
        }

        if (refreshedAtUtc.Offset != TimeSpan.Zero)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                "RELEASE_CATALOG_REFRESH_INVALID",
                "The release refresh timestamp must use the UTC offset.");
        }

        if (releases is null)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                "RELEASE_CATALOG_RELEASES_NULL",
                "The release collection is required.");
        }

        var snapshot = new List<OfficialToolRelease>();
        var versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (OfficialToolRelease? release in releases)
        {
            if (release is null
                || release.Version is null
                || release.Version.Tool != tool
                || !Enum.IsDefined(release.Support)
                || release.ReleasedAtUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            {
                return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                    "RELEASE_CATALOG_RELEASE_INVALID",
                    "Every release must contain a matching canonical tool version and valid UTC metadata.");
            }

            if (!versions.Add(release.Version.Value))
            {
                return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                    "RELEASE_CATALOG_DUPLICATE_VERSION",
                    "A release catalog cannot contain duplicate canonical versions.");
            }

            snapshot.Add(release);
        }

        if (snapshot.Count == 0)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                "RELEASE_CATALOG_EMPTY",
                "An official release catalog must contain at least one release.");
        }

        snapshot.Sort(static (left, right) => right.Version.CompareTo(left.Version));
        return ReleaseCatalogResult.Success(
            new OfficialReleaseCatalog(tool, source, refreshedAtUtc, snapshot.AsReadOnly()));
    }
}

/// <summary>Contained paths and explicit network consent for one catalog lookup.</summary>
public sealed record OfficialReleaseCatalogRequest(
    ToolKind Tool,
    string ProjectRoot,
    string DownloadsRoot,
    string CacheRoot,
    bool AllowNetworkRefresh = false);

/// <summary>A catalog plus provenance and contained persistence locations.</summary>
public sealed record OfficialReleaseCatalogSnapshot(
    OfficialReleaseCatalog Catalog,
    ReleaseCatalogOrigin Origin,
    string RawMetadataPath,
    string CacheMetadataPath,
    string? RefreshFailureCode);

/// <summary>Reads cached metadata or explicitly refreshes it from an official source.</summary>
public interface IOfficialReleaseCatalogProvider
{
    Task<ReleaseCatalogResult<OfficialReleaseCatalogSnapshot>> GetAsync(
        OfficialReleaseCatalogRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>A bounded response returned by a release metadata transport.</summary>
public sealed record OfficialReleaseMetadataPayload(Uri EffectiveSource, ReadOnlyMemory<byte> Contents);

/// <summary>Transport boundary used only after the caller explicitly allows a refresh.</summary>
public interface IOfficialReleaseMetadataTransport
{
    Task<ReleaseCatalogResult<OfficialReleaseMetadataPayload>> FetchAsync(
        Uri source,
        int maximumBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>Sanitized failure safe to expose without leaking paths or response content.</summary>
public sealed record ReleaseCatalogError(string Code, string Message);

/// <summary>Non-throwing result for expected catalog validation, I/O, and network failures.</summary>
public sealed class ReleaseCatalogResult<T>
{
    internal ReleaseCatalogResult(T? value, ReleaseCatalogError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public ReleaseCatalogError? Error { get; }
}

/// <summary>Factory helpers for typed release-catalog results.</summary>
public static class ReleaseCatalogResult
{
    public static ReleaseCatalogResult<T> Success<T>(T value) => new(value, null);

    public static ReleaseCatalogResult<T> Failure<T>(string code, string message) =>
        new(default, new ReleaseCatalogError(code, message));
}
