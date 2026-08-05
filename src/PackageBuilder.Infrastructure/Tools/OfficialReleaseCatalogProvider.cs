using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Infrastructure.Tools;

/// <summary>Contained cache-first provider for the supported vendors' official release metadata.</summary>
public sealed class OfficialReleaseCatalogProvider(
    IOfficialReleaseMetadataTransport transport,
    TimeProvider? timeProvider = null) : IOfficialReleaseCatalogProvider
{
    private const int CacheSchemaVersion = 1;
    private const int MaximumMetadataBytes = 4_194_304;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheLocks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly IOfficialReleaseMetadataTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<ReleaseCatalogResult<OfficialReleaseCatalogSnapshot>> GetAsync(
        OfficialReleaseCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ReleaseCatalogResult<CatalogPaths> pathsResult = ValidateRequest(request);
        if (!pathsResult.IsSuccess)
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalogSnapshot>(
                pathsResult.Error!.Code,
                pathsResult.Error.Message);
        }

        CatalogPaths paths = pathsResult.Value!;
        SemaphoreSlim gate = _cacheLocks.GetOrAdd(paths.CacheMetadataPath, static _ => new SemaphoreSlim(1, 1));
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }

        try
        {
            if (!request.AllowNetworkRefresh)
            {
                ReleaseCatalogResult<OfficialReleaseCatalog> cached = LoadCache(paths.Source, paths.CacheMetadataPath);
                return cached.IsSuccess
                    ? Snapshot(cached.Value!, ReleaseCatalogOrigin.Cache, paths, refreshFailureCode: null)
                    : ReleaseCatalogResult.Failure<OfficialReleaseCatalogSnapshot>(
                        "RELEASE_CATALOG_CACHE_UNAVAILABLE",
                        "No valid cached official release metadata is available while network refresh is disabled.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Cancelled();
            }

            ReleaseCatalogResult<OfficialReleaseMetadataPayload> fetched = await _transport.FetchAsync(
                paths.Source.Source,
                MaximumMetadataBytes,
                cancellationToken).ConfigureAwait(false);
            if (!fetched.IsSuccess)
            {
                return fetched.Error!.Code == "RELEASE_CATALOG_CANCELLED" ? Cancelled() : Fallback(paths, fetched.Error.Code);
            }

            OfficialReleaseMetadataPayload payload = fetched.Value!;
            if (!HasSameHttpsAuthority(paths.Source.Source, payload.EffectiveSource)
                || payload.Contents.IsEmpty
                || payload.Contents.Length > MaximumMetadataBytes)
            {
                return Fallback(paths, "RELEASE_TRANSPORT_RESPONSE_INVALID");
            }

            DateTimeOffset refreshedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();
            ReleaseCatalogResult<OfficialReleaseCatalog> parsed = paths.Source.Parse(
                payload.Contents,
                refreshedAtUtc);
            if (!parsed.IsSuccess)
            {
                return Fallback(paths, parsed.Error!.Code);
            }

            ReleaseCatalogResult<OfficialReleaseCatalog> persisted = Persist(
                parsed.Value!,
                payload.Contents,
                paths);
            return persisted.IsSuccess
                ? Snapshot(persisted.Value!, ReleaseCatalogOrigin.FreshNetwork, paths, refreshFailureCode: null)
                : Fallback(paths, persisted.Error!.Code);
        }
        finally
        {
            _ = gate.Release();
        }
    }

    private static ReleaseCatalogResult<CatalogPaths> ValidateRequest(OfficialReleaseCatalogRequest? request)
    {
        if (request is null || !OfficialReleaseCatalogSources.TryGet(request.Tool, out IOfficialReleaseCatalogSource? source))
        {
            return ReleaseCatalogResult.Failure<CatalogPaths>(
                "RELEASE_CATALOG_REQUEST_INVALID",
                "The release catalog request is missing or identifies an unsupported tool.");
        }

        if (!TryCanonicalize(request.ProjectRoot, out string projectRoot)
            || !TryCanonicalize(request.DownloadsRoot, out string downloadsRoot)
            || !TryCanonicalize(request.CacheRoot, out string cacheRoot)
            || !IsStrictDescendant(downloadsRoot, projectRoot)
            || !IsStrictDescendant(cacheRoot, projectRoot)
            || !string.Equals(Path.GetFileName(downloadsRoot), "downloads", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(cacheRoot), "cache", StringComparison.OrdinalIgnoreCase)
            || HasReparseBoundary(projectRoot, downloadsRoot)
            || HasReparseBoundary(projectRoot, cacheRoot))
        {
            return ReleaseCatalogResult.Failure<CatalogPaths>(
                "RELEASE_CATALOG_PATHS_UNSAFE",
                "Release metadata paths must be canonical, contained, non-reparse project downloads and cache roots.");
        }

        string toolName = request.Tool.ToString().ToLowerInvariant();
        string rawMetadataPath = Path.Combine(downloadsRoot, "release-catalogs", toolName, "official-metadata.source");
        string cacheMetadataPath = Path.Combine(cacheRoot, "release-catalogs", "v1", $"{toolName}.json");
        return ReleaseCatalogResult.Success(
            new CatalogPaths(source!, rawMetadataPath, cacheMetadataPath));
    }

    private static ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> Fallback(
        CatalogPaths paths,
        string refreshFailureCode)
    {
        ReleaseCatalogResult<OfficialReleaseCatalog> cached = LoadCache(paths.Source, paths.CacheMetadataPath);
        return cached.IsSuccess
            ? Snapshot(cached.Value!, ReleaseCatalogOrigin.LastKnownCache, paths, refreshFailureCode)
            : ReleaseCatalogResult.Failure<OfficialReleaseCatalogSnapshot>(
                "RELEASE_CATALOG_REFRESH_AND_CACHE_UNAVAILABLE",
                "The official metadata refresh failed and no valid last-known cache is available.");
    }

    private static ReleaseCatalogResult<OfficialReleaseCatalog> Persist(
        OfficialReleaseCatalog catalog,
        ReadOnlyMemory<byte> rawMetadata,
        CatalogPaths paths)
    {
        try
        {
            WriteAtomic(paths.RawMetadataPath, rawMetadata.Span);
            var envelope = new CatalogCacheEnvelope(
                CacheSchemaVersion,
                catalog.Tool.ToString(),
                catalog.Source.AbsoluteUri,
                catalog.RefreshedAtUtc,
                [.. catalog.Releases.Select(static release => new CatalogCacheRelease(
                    release.Version.Value,
                    release.Support.ToString(),
                    release.ReleasedAtUtc))]);
            byte[] cacheBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, _jsonOptions);
            WriteAtomic(paths.CacheMetadataPath, cacheBytes);
            return ReleaseCatalogResult.Success(catalog);
        }
        catch (Exception exception) when (IsExpectedFileFailure(exception))
        {
            return ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
                "RELEASE_CATALOG_CACHE_WRITE_FAILED",
                "Official release metadata could not be persisted beneath the approved roots.");
        }
    }

    private static ReleaseCatalogResult<OfficialReleaseCatalog> LoadCache(
        IOfficialReleaseCatalogSource source,
        string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath))
            {
                return CacheInvalid();
            }

            var file = new FileInfo(cachePath);
            if (file.Length <= 0 || file.Length > MaximumMetadataBytes)
            {
                return CacheInvalid();
            }

            byte[] bytes = File.ReadAllBytes(cachePath);
            using var document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            if (!HasUniqueProperties(document.RootElement))
            {
                return CacheInvalid();
            }

            CatalogCacheEnvelope? envelope = JsonSerializer.Deserialize<CatalogCacheEnvelope>(bytes, _jsonOptions);
            if (envelope is null
                || envelope.SchemaVersion != CacheSchemaVersion
                || !string.Equals(envelope.Tool, source.Tool.ToString(), StringComparison.Ordinal)
                || !Uri.TryCreate(envelope.Source, UriKind.Absolute, out Uri? cachedSource)
                || cachedSource != source.Source
                || envelope.Releases is null
                || envelope.Releases.Length == 0
                || envelope.Releases.Length > JsonReleaseCatalogSource.MaximumReleaseCount)
            {
                return CacheInvalid();
            }

            var releases = new List<OfficialToolRelease>(envelope.Releases.Length);
            foreach (CatalogCacheRelease release in envelope.Releases)
            {
                ToolModelValidationResult<ToolVersion> versionResult = ToolVersion.Create(source.Tool, release.Version);
                if (!versionResult.IsValid
                    || !Enum.TryParse(release.Support, ignoreCase: false, out ToolReleaseSupport support)
                    || !Enum.IsDefined(support))
                {
                    return CacheInvalid();
                }

                releases.Add(new OfficialToolRelease(versionResult.Value!, support, release.ReleasedAtUtc));
            }

            return OfficialReleaseCatalog.Create(
                source.Tool,
                cachedSource,
                envelope.RefreshedAtUtc,
                releases);
        }
        catch (Exception exception) when (exception is JsonException || IsExpectedFileFailure(exception))
        {
            return CacheInvalid();
        }
    }

    private static ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> Snapshot(
        OfficialReleaseCatalog catalog,
        ReleaseCatalogOrigin origin,
        CatalogPaths paths,
        string? refreshFailureCode) =>
        ReleaseCatalogResult.Success(
            new OfficialReleaseCatalogSnapshot(
                catalog,
                origin,
                paths.RawMetadataPath,
                paths.CacheMetadataPath,
                refreshFailureCode));

    private static ReleaseCatalogResult<OfficialReleaseCatalog> CacheInvalid() =>
        ReleaseCatalogResult.Failure<OfficialReleaseCatalog>(
            "RELEASE_CATALOG_CACHE_INVALID",
            "The cached official release metadata is missing, corrupt, incompatible, or stale in structure.");

    private static ReleaseCatalogResult<OfficialReleaseCatalogSnapshot> Cancelled() =>
        ReleaseCatalogResult.Failure<OfficialReleaseCatalogSnapshot>(
            "RELEASE_CATALOG_CANCELLED",
            "The release catalog operation was cancelled.");

    private static void WriteAtomic(string path, ReadOnlySpan<byte> contents)
    {
        string directory = Path.GetDirectoryName(path)!;
        _ = Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.WriteThrough))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static bool TryCanonicalize(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains('/')
            || !Path.IsPathFullyQualified(value)
            || value.EndsWith(Path.DirectorySeparatorChar))
        {
            return false;
        }

        try
        {
            path = Path.GetFullPath(value);
            return string.Equals(path, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            path = string.Empty;
            return false;
        }
    }

    private static bool IsStrictDescendant(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool HasReparseBoundary(string root, string path)
    {
        string current = root;
        if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }

        foreach (string segment in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSameHttpsAuthority(Uri expected, Uri actual) =>
        actual.IsAbsoluteUri
        && actual.Scheme == Uri.UriSchemeHttps
        && string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase)
        && expected.Port == actual.Port;

    private static bool HasUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || !HasUniqueProperties(property.Value))
                {
                    return false;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!HasUniqueProperties(item))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsExpectedFileFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private sealed record CatalogPaths(
        IOfficialReleaseCatalogSource Source,
        string RawMetadataPath,
        string CacheMetadataPath);

    private sealed record CatalogCacheEnvelope(
        int SchemaVersion,
        string Tool,
        string Source,
        DateTimeOffset RefreshedAtUtc,
        CatalogCacheRelease[] Releases);

    private sealed record CatalogCacheRelease(
        string Version,
        string Support,
        DateTimeOffset? ReleasedAtUtc);
}
