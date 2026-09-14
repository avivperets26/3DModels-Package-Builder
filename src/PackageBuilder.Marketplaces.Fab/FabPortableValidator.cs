using System.Collections.Immutable;
using PackageBuilder.Contracts.Archives;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>One ZIP delivery and the exact manifest-derived relevant file set. Archive paths are trusted
/// orchestration inputs, checked by the contained archive/hash services. Source payloads are never extracted.</summary>
public sealed record FabPortableDownload(BuildArtifactId ArtifactId, ArtifactContentIdentity Content,
    string ProductKey, string Format, bool IsAdditional, ArchiveOperationRequest Archive,
    ImmutableArray<string> ExpectedFiles, FabTargetEvidence TargetEvidence);

/// <summary>Fab rules layered on the existing portable validator, archive safety preflight and streamed hash service.</summary>
public sealed class FabPortableValidator(ISafeArchiveService archives, IArtifactHashService hashes)
{
    /// <summary>Read-only validation of frozen build artifacts. Hashes before and after inspection bind the
    /// result to the target receipt; callers must prevent concurrent mutation of the artifact store.</summary>
    public async Task<FabArtifactValidation> ValidateAsync(FabRequirementsProfile profile,
        FabValidationContext? context, ImmutableArray<FabPortableDownload> downloads,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = new FabValidation(profile);
        if (!validation.Context(context))
        { return validation.Result(); }
        if (downloads.IsDefault || downloads.Length > 256 || downloads.Any(download => download is null))
        {
            validation.Add("FAB_DOWNLOADS_INVALID", "The download inventory is missing or oversized.", "Provide a bounded complete download inventory.");
            return validation.Result();
        }
        validation.RequireRule("asset-formats", "assets");
        validation.RequireRule("delivery-paths", "archives");
        _ = validation.Bound("additional-count", "archives", "count", downloads.Count(download => download.IsAdditional));
        foreach (string format in context!.Listing.Formats.Where(format => format is "fbx" or "glb"))
        {
            if (downloads.Count(download => !download.IsAdditional && download.Format == format) != 1)
            { validation.Add("FAB_FORMAT_MISSING", "Each selected portable format requires exactly one primary delivery.", "Supply one artifact for every selected portable format."); }
        }
        if (downloads.Select(download => download.ArtifactId).Distinct().Count() != downloads.Length)
        { validation.Add("FAB_DOWNLOAD_DUPLICATE", "Download artifact identities are duplicated.", "Remove duplicated downloads."); }
        foreach (FabPortableDownload download in downloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (download.ArtifactId is null || download.Content is null || download.Archive is null
                || download.ExpectedFiles.IsDefaultOrEmpty || download.ExpectedFiles.Length > 100_000
                || download.ExpectedFiles.Any(string.IsNullOrWhiteSpace))
            { validation.Add("FAB_DOWNLOAD_INVALID", "A download is missing required content or manifest evidence.", "Regenerate the complete artifact receipt."); continue; }
            BuildArtifactId id = download.ArtifactId;
            if (download.ProductKey != context.ProductKey)
            { validation.Add("FAB_FILE_IRRELEVANT", "A download belongs to a different product.", "Include only files relevant to this listing.", id); }
            if (!download.IsAdditional && (download.Format is not ("fbx" or "glb") || !context.Listing.Formats.Contains(download.Format)))
            { validation.Add("FAB_FORMAT_INVALID", "The primary format was not selected or is unsupported.", "Select a profile-supported portable format.", id); }
            _ = validation.Evidence(download.TargetEvidence, context, id, download.Content, "portable-target");
            _ = download.IsAdditional
                ? validation.Bound("additional-bytes", "archives", "bytes", download.Content.Bytes, id)
                : validation.Bound("other-format-bytes", "archives", "bytes", download.Content.Bytes, id);
            var hashRequest = new ArtifactHashRequest(download.Archive.ProjectRoot, download.Archive.SourceArchivePath, id);
            ArtifactHashOperationResult<ArtifactHashReceipt> before = await hashes.HashAsync(hashRequest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!before.IsSuccess || !Equals(before.Value!.ContentIdentity, download.Content) || !Equals(before.Value.ArtifactId, id))
            { validation.Add("FAB_CONTENT_CHANGED", "Download bytes do not match the validated artifact.", "Rebuild and validate the exact download bytes.", id); continue; }
            ArchiveOperationResult<ArchiveInspection> inspection = await archives.InspectAsync(download.Archive, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!inspection.IsSuccess)
            { validation.Add("FAB_ARCHIVE_INVALID", "Archive safety inspection failed.", "Repair archive paths, entries, formats or safety quotas.", id); continue; }
            // Archive inspection reports OS-relative paths; marketplace inventory uses logical '/' paths.
            string[] files = [.. inspection.Value!.Entries.Where(entry => !entry.IsDirectory)
                .Select(entry => entry.RelativePath.Replace('\\', '/')).Order(StringComparer.Ordinal)];
            if (!files.All(FabContentPath.Valid))
            { validation.Add("FAB_NAMING_INVALID", "A delivery path violates the generated naming policy.", "Use safe manifest-planned folder and file names.", id); }
            if (inspection.Value.ArchiveBytes != download.Content.Bytes || files.Length == 0
                || !files.SequenceEqual(download.ExpectedFiles.Order(StringComparer.Ordinal), StringComparer.Ordinal)
                || files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
            { validation.Add("FAB_ARCHIVE_CONTENT_INVALID", "Archive contents do not match the relevant product file set.", "Regenerate the exact manifest-planned archive without extra or missing files.", id); }
            if (!download.IsAdditional && !files.Any(path => path.EndsWith("." + download.Format, StringComparison.OrdinalIgnoreCase)))
            { validation.Add("FAB_FORMAT_CONTENT_MISSING", "The archive contains no asset in its declared format.", "Include the selected model format.", id); }
            ArtifactHashOperationResult<ArtifactHashReceipt> after = await hashes.HashAsync(hashRequest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!after.IsSuccess || !Equals(after.Value!.ContentIdentity, download.Content) || !Equals(after.Value.ArtifactId, id))
            { validation.Add("FAB_CONTENT_CHANGED", "Download bytes changed during inspection.", "Freeze the artifact and validate again.", id); }
        }
        validation.Review(["assets", "archives", "documentation"],
            downloads.Any(download => !download.IsAdditional) ? null : "other-format-bytes");
        return validation.Result();
    }
}
