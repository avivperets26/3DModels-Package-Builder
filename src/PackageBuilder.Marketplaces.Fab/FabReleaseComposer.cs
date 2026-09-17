using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Exact generated deliverable with a trusted stream opener. Sources must be frozen and contained;
/// the writer rehashes their bytes. Kind is fbx, glb, additional, unity, unreal or documentation.</summary>
public sealed record FabReleaseSource(BuildArtifactId ArtifactId, string Kind, string FileName,
    ArtifactContentIdentity Content, Func<CancellationToken, Task<Stream>> OpenReadAsync, FabTargetEvidence Evidence);

/// <summary>A complete job handoff; package observations come from trusted target execution, never supplier JSON.</summary>
public sealed record FabReleaseRequest(FabValidationContext Context, string Version, BuildLock Versions,
    FabListingDraft Listing, ImmutableArray<FabPortableDownload> Downloads, FabUnityPackageInspection? Unity,
    ImmutableArray<FabGalleryImage> Gallery, ImmutableArray<FabReleaseSource> Sources,
    ImmutableArray<FabTargetEvidence> MediaQualityEvidence, FabUnrealProjectInspection? Unreal = null);

/// <summary>Success identifies a private staged envelope. Atomic artifact-store promotion remains a separate gate.</summary>
public sealed record FabComposedRelease(string? FileName, ArtifactContentIdentity? Content, FabArtifactValidation Validation)
{
    /// <summary>A staged archive is successful only after content verification and all applicable validation gates.</summary>
    public bool IsSuccess => Content is not null && Validation.Passed;
}

/// <summary>Composes selected portable/Unity/Unreal outputs using approved pinned rules and existing target validators.
/// No upload, directory scanning, default-profile substitution or unvalidated output is allowed.</summary>
public sealed class FabReleaseComposer(FabRequirementsProfileUpdater profiles, FabPortableValidator portable,
    FabMediaGalleryValidator media, IReleaseArchiveWriter writer)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    /// <summary>Validates before opening output entries, then rehashes every streamed payload. The destination
    /// must be empty private staging; callers discard it on any error and atomically promote only success.</summary>
    public async Task<FabComposedRelease> ComposeAsync(FabReleaseRequest request, Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (!BuildLockJson.Serialize(request.Versions).IsSuccessful || request.Context is null
            || !Equals(request.Context.JobId, request.Versions.JobId))
        { return Failure("FAB_RELEASE_LOCK_INVALID", "Supply this job's complete build.lock."); }
        BuildLockMarketplaceProfile? pin = request.Versions.MarketplaceProfiles.SingleOrDefault(value => value.Marketplace == "fab");
        if (pin is null)
        { return Failure("FAB_RELEASE_PROFILE_MISSING", "Pin the approved Fab requirements profile in build.lock."); }
        RepositoryOperationResult<FabRequirementsProfile> loaded = await profiles.LoadPinnedAsync(pin, cancellationToken).ConfigureAwait(false);
        if (!loaded.IsSuccess)
        { return Failure("FAB_RELEASE_PROFILE_UNAPPROVED", "Load the exact approved profile; do not substitute the current candidate."); }
        FabRequirementsProfile profile = loaded.Value!;
        var validation = new FabValidation(profile);
        if (!validation.Context(request.Context))
        { return new(null, null, validation.Result()); }
        FabRequiredTargets targets = FabRequiredTargetResolver.Resolve(profile, request.Context.Listing).Value!;
        if (!DeliveryPath.IsValid(request.Context.ProductKey) || request.Context.ProductKey.Contains('/')
            || !DeliveryPath.IsValid(request.Version) || request.Version.Contains('/')
            || request.Sources.IsDefaultOrEmpty || request.Sources.Length > 512
            || request.Sources.Any(source => source is null || source.ArtifactId is null || source.Content is null
                || source.OpenReadAsync is null || !DeliveryPath.IsValid(source.FileName) || source.FileName.Contains('/'))
            || request.Downloads.IsDefault || request.Gallery.IsDefault || request.Gallery.Length > 256
            || request.Gallery.Sum(image => (long)(image?.Bytes.Length ?? 0)) > 32_000_000
            || request.MediaQualityEvidence.IsDefault || request.MediaQualityEvidence.Length > 256)
        { validation.Add("FAB_RELEASE_INPUT_INVALID", "Release identity or complete artifact inventory is invalid.", "Supply a safe version and the exact job inventory."); return new(null, null, validation.Result()); }
        FabListingChecklist listing = FabListingChecklistGenerator.Generate(profile, request.Context, request.Listing, request.Versions, cancellationToken);
        validation.Include(listing.Validation);
        if (request.Sources.Select(source => source.ArtifactId).Distinct().Count() != request.Sources.Length)
        { validation.Add("FAB_RELEASE_DUPLICATE", "Release artifact identities are duplicated.", "Use one entry per validated artifact."); }
        validation.Include(await portable.ValidateAsync(profile, request.Context, request.Downloads, cancellationToken).ConfigureAwait(false));
        bool needsUnity = targets.Required.Contains(FabOutput.Unity);
        if (needsUnity)
        {
            validation.Include(FabUnityPackageValidator.Validate(profile, request.Context, request.Unity, cancellationToken));
            if (request.Unity is not null && !request.Unity.ExternalDependencies.IsDefault && request.Listing is not null
                && !request.Listing.Dependencies.IsDefault && request.Unity.ExternalDependencies.Any(dependency => dependency is not null
                    && !request.Listing.Dependencies.Any(item => item is not null && item.Name == dependency.Root && item.Version == dependency.Version)))
            { validation.Add("FAB_RELEASE_DEPENDENCY_UNDISCLOSED", "Unity dependencies differ from listing disclosures.", "Include every installed external package and its exact version in the listing."); }
        }
        else if (request.Unity is not null)
        { validation.Add("FAB_RELEASE_UNREQUESTED", "Unity evidence was supplied for an unselected format.", "Remove unrequested outputs."); }
        bool needsUnreal = targets.Required.Contains(FabOutput.Unreal);
        if (needsUnreal)
        {
            validation.Include(FabUnrealProjectValidator.Validate(profile, request.Context, request.Unreal, cancellationToken));
            if (request.Unreal is not null && request.Unreal.EngineVersion != request.Versions.Unreal.Value)
            { validation.Add("FAB_UNREAL_VERSION_MISMATCH", "Unreal inspection differs from the pinned engine.", "Use the exact engine recorded in build.lock."); }
        }
        else if (request.Unreal is not null)
        { validation.Add("FAB_RELEASE_UNREQUESTED", "Unreal evidence was supplied for an unselected format.", "Remove unrequested outputs."); }
        // Own gallery data before validation and serialization so later caller mutation cannot alter it.
        ImmutableArray<FabGalleryImage> images = [.. request.Gallery.Select(image => image is null ? null! : image with { Bytes = image.Bytes.ToArray() })];
        if (images.Any(image => image is not null && request.Sources.Any(source => Equals(source.ArtifactId, image.ArtifactId))))
        { validation.Add("FAB_RELEASE_DUPLICATE", "A media artifact reuses a download or document identity.", "Use distinct job artifact identities across the complete release."); }
        if (targets.Required.Contains(FabOutput.Media))
        {
            validation.Include(media.Validate(profile, request.Context, images, cancellationToken));
            foreach (FabGalleryImage image in images.Where(image => image is not null && image.ArtifactId is not null && image.Content is not null))
            {
                FabTargetEvidence[] evidence = [.. request.MediaQualityEvidence.Where(item => item is not null && Equals(item.ArtifactId, image.ArtifactId))];
                _ = validation.Evidence(evidence.Length == 1 ? evidence[0] : null, request.Context, image.ArtifactId, image.Content, "media-quality");
            }
            if (request.MediaQualityEvidence.Length != images.Length)
            { validation.Add("FAB_MEDIA_EVIDENCE_INVALID", "Media quality observations do not match the complete gallery.", "Run final media quality validation for each delivery image."); }
        }
        else if (images.Length != 0)
        { validation.Add("FAB_RELEASE_UNREQUESTED", "Media was supplied but not requested.", "Select media or remove it."); }
        bool needsDocs = targets.Required.Contains(FabOutput.Documentation);
        int docs = 0;
        foreach (FabReleaseSource source in request.Sources)
        {
            if (source.Kind == "documentation")
            {
                docs++;
                _ = validation.Evidence(source.Evidence, request.Context, source.ArtifactId, source.Content, "documentation");
                if (!needsDocs || !new[] { ".md", ".txt", ".pdf", ".html", ".rtf" }.Contains(Path.GetExtension(source.FileName), StringComparer.OrdinalIgnoreCase))
                { validation.Add("FAB_RELEASE_DOCUMENT_INVALID", "An unrequested or unsupported document was supplied.", "Include only selected, validated customer documentation.", source.ArtifactId); }
            }
            else if (source.Kind == "unity")
            {
                if (!needsUnity || request.Unity is null || !Equals(request.Unity.ArtifactId, source.ArtifactId)
                    || !Equals(request.Unity.Content, source.Content) || request.Unity.FileName != source.FileName)
                { validation.Add("FAB_RELEASE_INVENTORY_MISMATCH", "Unity delivery differs from its inspected package.", "Use the inspected package bytes and filename.", source.ArtifactId); }
            }
            else if (source.Kind == "unreal")
            {
                if (!needsUnreal || request.Unreal is null || !Equals(request.Unreal.ArtifactId, source.ArtifactId)
                    || !Equals(request.Unreal.Content, source.Content) || request.Unreal.FileName != source.FileName)
                { validation.Add("FAB_RELEASE_INVENTORY_MISMATCH", "Unreal delivery differs from its inspected archive.", "Use the inspected project bytes and filename.", source.ArtifactId); }
            }
            else if (source.Kind is "fbx" or "glb" or "additional")
            {
                if (!request.Downloads.Any(download => download is not null && Equals(download.ArtifactId, source.ArtifactId)
                    && Equals(download.Content, source.Content) && (download.IsAdditional ? source.Kind == "additional" : download.Format == source.Kind))
                    || !source.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                { validation.Add("FAB_RELEASE_INVENTORY_MISMATCH", "Portable delivery differs from its validated download.", "Use only inspected ZIP deliveries.", source.ArtifactId); }
            }
            else
            { validation.Add("FAB_RELEASE_UNREQUESTED", "An unknown delivery kind was supplied.", "Remove source assets and unselected outputs.", source.ArtifactId); }
        }
        if (needsUnreal && request.Sources.Count(source => source.Kind == "unreal") != 1
            || needsDocs && docs == 0 || needsUnity && request.Sources.Count(source => source.Kind == "unity") != 1
            || request.Downloads.Any(download => download is not null && request.Sources.Count(source => Equals(source.ArtifactId, download.ArtifactId)
                && source.Kind == (download.IsAdditional ? "additional" : download.Format)) != 1))
        { validation.Add("FAB_RELEASE_DELIVERY_MISSING", "The selected release is missing a required delivery.", "Complete all requested outputs before composition."); }
        validation.Review(["assets", "archives"], targets.Required.Contains(FabOutput.Portable) ? null : "other-format-bytes");
        if (needsDocs)
        { validation.Review(["documentation"]); }
        if (needsUnity)
        { validation.Review(["unity"]); }
        if (targets.Required.Contains(FabOutput.Media))
        { validation.Review(["media"]); }
        if (!validation.Result().Passed)
        { return new(null, null, validation.Result()); }
        string root = request.Context.ProductKey + "/" + request.Version + "/";
        var entries = new List<ReleaseArchiveEntry>();
        entries.AddRange(request.Sources.Select(source => new ReleaseArchiveEntry(root + Folder(source.Kind) + "/" + source.FileName, source.Content, source.OpenReadAsync)));
        entries.AddRange(images.Select(image => Memory(root + "Media/" + image.FileName, image.Bytes.ToArray())));
        entries.Add(Memory(root + "listing.json", Encoding.UTF8.GetBytes(listing.ListingJson)));
        entries.Add(Memory(root + "upload-checklist.txt", Encoding.UTF8.GetBytes(listing.ChecklistText)));
        entries.Add(Memory(root + "requirements-profile.json", Encoding.UTF8.GetBytes(profile.CanonicalJson)));
        entries.Add(Memory(root + "build.lock.json", Encoding.UTF8.GetBytes(BuildLockJson.Serialize(request.Versions).Json!)));
        string manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            product = request.Context.ProductKey,
            version = request.Version,
            job = request.Context.JobId.Value,
            requirementsProfile = profile.BuildLockIdentity,
            thumbnail = images.SingleOrDefault(image => image.IsThumbnail)?.FileName,
            entries = entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).Select(entry => new { path = entry.Path, bytes = entry.Content.Bytes, sha256 = entry.Content.Sha256.Value }),
        }, _json) + "\n";
        entries.Add(Memory(root + "release-manifest.json", Encoding.UTF8.GetBytes(manifest)));
        try
        {
            ArtifactContentIdentity content = await writer.WriteAsync([.. entries], destination, cancellationToken).ConfigureAwait(false);
            return new(request.Context.ProductKey + "_" + request.Version + "_Fab.zip", content, validation.Result());
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            validation.Add("FAB_RELEASE_WRITE_FAILED", "Release staging failed or a payload changed after validation.", "Discard partial staging, repair the inputs and retry.");
            return new(null, null, validation.Result());
        }
    }

    private static ReleaseArchiveEntry Memory(string path, byte[] bytes) => new(path,
        ArtifactContentIdentity.Create(bytes.Length, Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(bytes))).Value).Value!,
        _ => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false)));

    private static string Folder(string kind) => kind switch { "documentation" => "Documentation", "unity" => "Unity", "unreal" => "Unreal", "additional" => "Additional", _ => "Portable" };

    private static FabComposedRelease Failure(string code, string action) => new(null, null, new("",
        [ValidationFinding.Create(FindingCode.Create(code).Value, FindingSeverity.Error,
            FindingExplanation.Create(action).Value, FindingSourceComponent.Create("fab-release").Value, null,
            CorrectiveAction.Create(action).Value, true).Value!]));
}
