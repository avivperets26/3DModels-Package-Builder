using System.Buffers;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>
/// Publishes a verified artifact through a hidden same-volume staging file and an atomic final rename.
/// </summary>
public sealed class AtomicArtifactPromotionService(
    IArtifactStore artifactStore,
    IArtifactHashService hashService) : IArtifactPromotionService
{
    private const int BufferSize = 65_536;
    private const int MaximumJournalBytes = 65_536;
    private const int MaximumCollisionVersions = 10_000;
    private const string JournalName = "promotion.json";
    private const string StagingDirectoryName = ".packagebuilder-promotion";

    private readonly IArtifactStore _artifactStore =
        artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
    private readonly IArtifactHashService _hashService =
        hashService ?? throw new ArgumentNullException(nameof(hashService));

    public AtomicArtifactPromotionService()
        : this(new ArtifactStore(), new ArtifactHashService())
    {
    }

    public async Task<ArtifactPromotionOperationResult> PromoteAsync(
        ArtifactPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ArtifactPromotionOperationResult? rootFailure = ValidateRoots(request, out PromotionRoots? roots);
            if (rootFailure is not null)
            {
                return rootFailure;
            }

            if (request.PromotedAtUtc.Offset != TimeSpan.Zero)
            {
                return Failed(
                    "ARTIFACT_PROMOTION_TIME_INVALID",
                    "promotedAtUtc",
                    "The promotion time must use the UTC offset.");
            }

            ArtifactStoreOperationResult<ArtifactStoreRecord> stored = await _artifactStore.ReadAsync(
                new ArtifactStoreReadRequest(
                    roots!.ProjectRoot,
                    roots.ArtifactsRoot,
                    request.JobId,
                    request.ArtifactId),
                cancellationToken).ConfigureAwait(false);
            if (!stored.IsSuccess)
            {
                return FromStore(stored.Failures);
            }

            ArtifactStoreRecord record = stored.Value!;
            bool alreadyPromoted = record.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Promoted);
            if (!alreadyPromoted &&
                !record.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Validated))
            {
                return Failed(
                    "ARTIFACT_PROMOTION_STATE_INVALID",
                    "artifact.lifecycleState",
                    "Only a validated artifact can begin release promotion.");
            }

            if (!alreadyPromoted && request.PromotedAtUtc < record.Artifact.StateChangedAtUtc)
            {
                return Failed(
                    "ARTIFACT_PROMOTION_TIME_INVALID",
                    "promotedAtUtc",
                    "The promotion time must not precede the artifact state time.");
            }

            ArtifactPromotionOperationResult? logicalFailure = ValidateLogicalReference(
                roots.BuildsRoot,
                record.Artifact.LogicalReference,
                out string? baseReleasePath);
            if (logicalFailure is not null)
            {
                return logicalFailure;
            }

            EnsureContainedDirectory(roots.ProjectRoot, roots.BuildsRoot, "buildsRoot");
            string stagingRoot = Path.Combine(roots.ArtifactsRoot, StagingDirectoryName);
            EnsureContainedDirectory(roots.ArtifactsRoot, stagingRoot, "staging");
            string jobStagingRoot = Path.Combine(stagingRoot, record.Layout.JobKey);
            EnsureContainedDirectory(stagingRoot, jobStagingRoot, "staging");
            string partialPath = Path.Combine(jobStagingRoot, record.Layout.ArtifactKey + ".partial");
            string journalPath = Path.Combine(record.Layout.ArtifactRoot, JournalName);

            JournalResolution journalResolution = await LoadOrCreateJournalAsync(
                roots,
                record,
                baseReleasePath!,
                journalPath,
                cancellationToken).ConfigureAwait(false);
            return journalResolution.Failure is not null
                ? journalResolution.Failure
                : await ResumeAsync(
                request,
                roots,
                record,
                journalResolution.Journal!,
                journalPath,
                partialPath,
                journalResolution.Recovered,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("ARTIFACT_PROMOTION_CANCELLED", "$", "Artifact promotion was cancelled safely.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            return Failed("ARTIFACT_PROMOTION_IO_FAILED", "$", "Artifact promotion encountered a filesystem error.");
        }
    }

    private async Task<ArtifactPromotionOperationResult> ResumeAsync(
        ArtifactPromotionRequest request,
        PromotionRoots roots,
        ArtifactStoreRecord record,
        ArtifactPromotionJournal journal,
        string journalPath,
        string partialPath,
        bool recovered,
        CancellationToken cancellationToken)
    {
        string releasePath = ResolveReleasePath(roots.BuildsRoot, journal.ReleaseRelativePath);
        if (Path.Exists(releasePath))
        {
            if (File.Exists(releasePath) &&
                await HasExpectedIdentityAsync(roots.ProjectRoot, releasePath, record, cancellationToken)
                    .ConfigureAwait(false))
            {
                return await CompleteAsync(
                    request,
                    record,
                    journal,
                    releasePath,
                    recovered: true,
                    cancellationToken).ConfigureAwait(false);
            }

            if (record.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Promoted))
            {
                return Failed(
                    "ARTIFACT_PROMOTION_RELEASE_INTEGRITY",
                    "release",
                    "The promoted release no longer matches its recorded content identity.");
            }

            JournalResolution next = await MoveJournalToNextCollisionAsync(
                roots,
                record,
                journal,
                journalPath,
                cancellationToken).ConfigureAwait(false);
            if (next.Failure is not null)
            {
                return next.Failure;
            }

            journal = next.Journal!;
            releasePath = next.ReleasePath!;
        }

        if (File.Exists(partialPath) &&
            !await HasExpectedIdentityAsync(roots.ProjectRoot, partialPath, record, cancellationToken)
                .ConfigureAwait(false))
        {
            EnsureExistingPath(roots.ArtifactsRoot, partialPath, "staging");
            File.Delete(partialPath);
        }

        if (!File.Exists(partialPath))
        {
            await CopyToPartialAsync(record.Layout.PayloadPath, partialPath, cancellationToken).ConfigureAwait(false);
        }

        if (!await HasExpectedIdentityAsync(roots.ProjectRoot, partialPath, record, cancellationToken)
            .ConfigureAwait(false))
        {
            return Failed(
                "ARTIFACT_PROMOTION_STAGING_INTEGRITY",
                "staging",
                "The complete staging file does not match the validated artifact identity.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureReleaseParent(roots, releasePath);
            try
            {
                File.Move(partialPath, releasePath);
                break;
            }
            catch (IOException) when (Path.Exists(releasePath))
            {
                JournalResolution next = await MoveJournalToNextCollisionAsync(
                    roots,
                    record,
                    journal,
                    journalPath,
                    cancellationToken).ConfigureAwait(false);
                if (next.Failure is not null)
                {
                    return next.Failure;
                }

                journal = next.Journal!;
                releasePath = next.ReleasePath!;
            }
        }

        return await CompleteAsync(
            request,
            record,
            journal,
            releasePath,
            recovered,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ArtifactPromotionOperationResult> CompleteAsync(
        ArtifactPromotionRequest request,
        ArtifactStoreRecord record,
        ArtifactPromotionJournal journal,
        string releasePath,
        bool recovered,
        CancellationToken cancellationToken)
    {
        ArtifactStoreRecord finalRecord = record;
        if (record.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Validated))
        {
            ArtifactStoreOperationResult<ArtifactStoreRecord> transition = await _artifactStore.TransitionAsync(
                new ArtifactStoreTransitionRequest(
                    request.ProjectRoot,
                    request.ArtifactsRoot,
                    request.JobId,
                    request.ArtifactId,
                    BuildArtifactLifecycleState.Validated,
                    BuildArtifactLifecycleState.Promoted,
                    request.PromotedAtUtc),
                cancellationToken).ConfigureAwait(false);
            if (!transition.IsSuccess)
            {
                return FromStore(transition.Failures);
            }

            finalRecord = transition.Value!;
        }

        return ArtifactPromotionResult.Success(
            new ArtifactPromotionReceipt(
                finalRecord,
                releasePath,
                journal.ReleaseRelativePath,
                journal.CollisionVersion,
                recovered));
    }

    private static async Task<JournalResolution> LoadOrCreateJournalAsync(
        PromotionRoots roots,
        ArtifactStoreRecord record,
        string baseReleasePath,
        string journalPath,
        CancellationToken cancellationToken)
    {
        bool recovered = File.Exists(journalPath);
        ArtifactPromotionJournal? journal;
        if (recovered)
        {
            byte[] bytes = await ReadJournalAsync(journalPath, cancellationToken).ConfigureAwait(false);
            return !ArtifactPromotionJournalCodec.TryRead(bytes, out journal) ||
                !JournalMatches(journal!, record, roots.BuildsRoot)
                ? JournalResolution.Failed(Failed(
                    "ARTIFACT_PROMOTION_JOURNAL_INVALID",
                    "journal",
                    "The persisted promotion journal is malformed or inconsistent with the artifact."))
                : JournalResolution.Success(journal!, releasePath: null, recovered: true);
        }

        if (record.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Promoted))
        {
            return JournalResolution.Failed(Failed(
                "ARTIFACT_PROMOTION_JOURNAL_MISSING",
                "journal",
                "A promoted artifact requires its persisted release journal."));
        }

        int version = FindAvailableVersion(baseReleasePath, 1, out string releasePath);
        if (version == 0)
        {
            return JournalResolution.Failed(CollisionExhausted());
        }

        journal = CreateJournal(record, roots.BuildsRoot, releasePath, version);
        await WriteJournalAsync(journalPath, journal, replace: false, cancellationToken).ConfigureAwait(false);
        return JournalResolution.Success(journal, releasePath, recovered: false);
    }

    private static async Task<JournalResolution> MoveJournalToNextCollisionAsync(
        PromotionRoots roots,
        ArtifactStoreRecord record,
        ArtifactPromotionJournal current,
        string journalPath,
        CancellationToken cancellationToken)
    {
        string baseReleasePath = ResolveBaseReleasePath(roots.BuildsRoot, record.Artifact.LogicalReference);
        int version = FindAvailableVersion(baseReleasePath, current.CollisionVersion + 1, out string releasePath);
        if (version == 0)
        {
            return JournalResolution.Failed(CollisionExhausted());
        }

        ArtifactPromotionJournal journal = CreateJournal(record, roots.BuildsRoot, releasePath, version);
        await WriteJournalAsync(journalPath, journal, replace: true, cancellationToken).ConfigureAwait(false);
        return JournalResolution.Success(journal, releasePath, recovered: true);
    }

    private async Task<bool> HasExpectedIdentityAsync(
        string projectRoot,
        string path,
        ArtifactStoreRecord record,
        CancellationToken cancellationToken)
    {
        ArtifactHashOperationResult<ArtifactHashReceipt> result = await _hashService.HashAsync(
            new ArtifactHashRequest(projectRoot, path, record.Artifact.Id),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess && result.Value!.ContentIdentity.Equals(record.ContentIdentity);
    }

    private static ArtifactPromotionOperationResult? ValidateRoots(
        ArtifactPromotionRequest request,
        out PromotionRoots? roots)
    {
        roots = null;
        if (!TryCanonical(request.ProjectRoot, out string? project) ||
            !TryCanonical(request.ArtifactsRoot, out string? artifacts) ||
            !TryCanonical(request.BuildsRoot, out string? builds))
        {
            return Failed(
                "ARTIFACT_PROMOTION_PATH_INVALID",
                "roots",
                "Project, artifacts, and Builds roots must be fully qualified canonical paths.");
        }

        if (!Directory.Exists(project))
        {
            return Failed(
                "ARTIFACT_PROMOTION_PROJECT_MISSING",
                "projectRoot",
                "The trusted project root does not exist.");
        }

        if (!Directory.Exists(artifacts) ||
            !IsStrictDescendant(artifacts, project) ||
            !IsStrictDescendant(builds!, artifacts))
        {
            return Failed(
                "ARTIFACT_PROMOTION_ROOT_CONTAINMENT",
                "roots",
                "Artifacts must be project-contained and Builds must be a dedicated artifact descendant.");
        }

        EnsureExistingAncestors(project, artifacts, "artifactsRoot");
        EnsureExistingAncestors(artifacts, builds!, "buildsRoot");
        roots = new PromotionRoots(project, artifacts, builds!);
        return null;
    }

    private static ArtifactPromotionOperationResult? ValidateLogicalReference(
        string buildsRoot,
        string logicalReference,
        out string? releasePath)
    {
        releasePath = null;
        string[] segments = logicalReference.Split('/');
        foreach (string segment in segments)
        {
            if (!IsPortableSegment(segment))
            {
                return Failed(
                    "ARTIFACT_PROMOTION_REFERENCE_UNSAFE",
                    "artifact.logicalReference",
                    "The release reference contains a non-portable filesystem segment.");
            }
        }

        releasePath = Path.GetFullPath(Path.Combine(buildsRoot, Path.Combine(segments)));
        return null;
    }

    private static bool IsPortableSegment(string segment)
    {
        if (segment.Length == 0 || segment[^1] is '.' or ' ' ||
            segment.Any(static value => value < 32 || "<>:\"/\\|?*".Contains(value, StringComparison.Ordinal)))
        {
            return false;
        }

        string deviceStem = segment.Split('.')[0].TrimEnd(' ', '.');
        return !deviceStem.Equals("CON", StringComparison.OrdinalIgnoreCase) &&
            !deviceStem.Equals("PRN", StringComparison.OrdinalIgnoreCase) &&
            !deviceStem.Equals("AUX", StringComparison.OrdinalIgnoreCase) &&
            !deviceStem.Equals("NUL", StringComparison.OrdinalIgnoreCase) &&
            !IsNumberedDevice(deviceStem, "COM") &&
            !IsNumberedDevice(deviceStem, "LPT");
    }

    private static bool IsNumberedDevice(string value, string prefix) =>
        value.Length == 4 &&
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        value[3] is >= '1' and <= '9';

    private static bool JournalMatches(
        ArtifactPromotionJournal journal,
        ArtifactStoreRecord record,
        string buildsRoot)
    {
        if (!string.Equals(journal.JobId, record.Artifact.JobId.Value, StringComparison.Ordinal) ||
            !string.Equals(journal.ArtifactId, record.Artifact.Id.Value, StringComparison.Ordinal) ||
            journal.CollisionVersion is < 1 or > MaximumCollisionVersions ||
            journal.Bytes != record.ContentIdentity.Bytes ||
            !string.Equals(journal.Sha256, record.ContentIdentity.Sha256.Value, StringComparison.Ordinal))
        {
            return false;
        }

        ArtifactPromotionOperationResult? result = ValidateLogicalReference(
            buildsRoot,
            journal.ReleaseRelativePath,
            out string? releasePath);
        return result is null &&
            string.Equals(
                releasePath,
                CollisionPath(
                    ResolveBaseReleasePath(buildsRoot, record.Artifact.LogicalReference),
                    journal.CollisionVersion),
                StringComparison.OrdinalIgnoreCase);
    }

    private static ArtifactPromotionJournal CreateJournal(
        ArtifactStoreRecord record,
        string buildsRoot,
        string releasePath,
        int version) =>
        new(
            record.Artifact.JobId.Value,
            record.Artifact.Id.Value,
            Path.GetRelativePath(buildsRoot, releasePath).Replace(Path.DirectorySeparatorChar, '/'),
            version,
            record.ContentIdentity.Bytes,
            record.ContentIdentity.Sha256.Value);

    private static int FindAvailableVersion(string basePath, int firstVersion, out string releasePath)
    {
        for (int version = Math.Max(1, firstVersion); version <= MaximumCollisionVersions; version++)
        {
            releasePath = CollisionPath(basePath, version);
            if (!Path.Exists(releasePath))
            {
                return version;
            }
        }

        releasePath = string.Empty;
        return 0;
    }

    private static string CollisionPath(string basePath, int version)
    {
        if (version == 1)
        {
            return basePath;
        }

        string? directory = Path.GetDirectoryName(basePath);
        string extension = Path.GetExtension(basePath);
        string name = Path.GetFileNameWithoutExtension(basePath);
        return Path.Combine(directory!, $"{name} ({version}){extension}");
    }

    private static string ResolveBaseReleasePath(string buildsRoot, string logicalReference) =>
        Path.GetFullPath(Path.Combine(buildsRoot, Path.Combine(logicalReference.Split('/'))));

    private static string ResolveReleasePath(string buildsRoot, string relativePath) =>
        Path.GetFullPath(Path.Combine(buildsRoot, Path.Combine(relativePath.Split('/'))));

    private static async Task CopyToPartialAsync(
        string sourcePath,
        string partialPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            partialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async Task<byte[]> ReadJournalAsync(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        return info.Length is <= 0 or > MaximumJournalBytes
            ? []
            : await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteJournalAsync(
        string path,
        ArtifactPromotionJournal journal,
        bool replace,
        CancellationToken cancellationToken)
    {
        byte[] json = ArtifactPromotionJournalCodec.Write(journal);
        string destination = path + ".next";
        if (File.Exists(destination))
        {
            EnsureNoReparse(destination);
            File.Delete(destination);
        }

        await using (var stream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(destination, path, overwrite: replace);
    }

    private static void EnsureReleaseParent(PromotionRoots roots, string releasePath)
    {
        string parent = Path.GetDirectoryName(releasePath)!;
        EnsureContainedDirectory(roots.BuildsRoot, parent, "release");
        if (Path.Exists(releasePath))
        {
            EnsureExistingPath(roots.BuildsRoot, releasePath, "release");
        }
    }

    private static void EnsureContainedDirectory(string boundary, string directory, string location)
    {
        string relative = Path.GetRelativePath(boundary, directory);
        string current = boundary;
        EnsureNoReparse(current);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                _ = Directory.CreateDirectory(current);
            }

            EnsureNoReparse(current);
        }

        _ = location;
    }

    private static void EnsureExistingAncestors(string boundary, string path, string location)
    {
        string relative = Path.GetRelativePath(boundary, path);
        string current = boundary;
        EnsureNoReparse(current);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Path.Exists(current))
            {
                break;
            }

            EnsureNoReparse(current);
        }

        _ = location;
    }

    private static void EnsureExistingPath(string boundary, string path, string location)
    {
        EnsureExistingAncestors(boundary, path, location);
        EnsureNoReparse(path);
    }

    private static void EnsureNoReparse(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("A promotion path crossed a reparse point.");
        }
    }

    private static bool TryCanonical(string path, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return string.Equals(
                canonical,
                Path.TrimEndingDirectorySeparator(path),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsStrictDescendant(string candidate, string boundary) =>
        !string.Equals(candidate, boundary, StringComparison.OrdinalIgnoreCase) &&
        candidate.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static ArtifactPromotionOperationResult FromStore(
        IEnumerable<ArtifactStoreFailure> failures) =>
        ArtifactPromotionResult.Failed(
            [.. failures.Select(static failure =>
                new ArtifactPromotionFailure(failure.Code, failure.Location, failure.Diagnostic))]);

    private static ArtifactPromotionOperationResult CollisionExhausted() =>
        Failed(
            "ARTIFACT_PROMOTION_COLLISIONS_EXHAUSTED",
            "release",
            "No collision-safe release version is available within the supported bound.");

    private static ArtifactPromotionOperationResult Failed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactPromotionResult.Failed(new ArtifactPromotionFailure(code, location, diagnostic));

    private sealed record PromotionRoots(string ProjectRoot, string ArtifactsRoot, string BuildsRoot);

    private sealed record JournalResolution(
        ArtifactPromotionOperationResult? Failure,
        ArtifactPromotionJournal? Journal,
        string? ReleasePath,
        bool Recovered)
    {
        public static JournalResolution Success(
            ArtifactPromotionJournal journal,
            string? releasePath,
            bool recovered) => new(null, journal, releasePath, recovered);

        public static JournalResolution Failed(ArtifactPromotionOperationResult failure) =>
            new(failure, null, null, Recovered: false);
    }
}
