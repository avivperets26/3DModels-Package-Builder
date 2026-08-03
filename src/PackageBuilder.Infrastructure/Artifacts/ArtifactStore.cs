using System.Buffers;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>Persists contained job artifacts with deterministic paths and integrity-checked metadata.</summary>
public sealed class ArtifactStore(IArtifactHashService hashService) : IArtifactStore
{
    private const int BufferSize = 65_536;
    private const int MaximumMetadataBytes = 65_536;

    private readonly IArtifactHashService _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));

    public ArtifactStore()
        : this(new ArtifactHashService())
    {
    }

    public async Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> StageAsync(
        ArtifactStoreStageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ArtifactStoreOperationResult<ValidatedPaths> validation = ValidatePaths(
                request.ProjectRoot,
                request.ArtifactsRoot,
                request.Artifact.JobId,
                request.Artifact.Id,
                requireStore: false);
            if (!validation.IsSuccess)
            {
                return Failed(validation.Failures);
            }

            if (!request.Artifact.LifecycleState.Equals(BuildArtifactLifecycleState.Staged))
            {
                return Failed("ARTIFACT_STORE_INITIAL_STATE", "artifact.lifecycleState", "New artifacts must begin in the staged state.");
            }

            ArtifactStoreOperationResult<string> source = ValidateSource(
                request.SourceFilePath,
                validation.Value!);
            if (!source.IsSuccess)
            {
                return Failed(source.Failures);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ArtifactStoreLayout layout = validation.Value!.Layout;
            if (Directory.Exists(layout.ArtifactRoot) || File.Exists(layout.ArtifactRoot))
            {
                return Failed("ARTIFACT_STORE_COLLISION", "artifact", "The artifact already has a store entry.");
            }

            EnsureContainedDirectory(validation.Value, Path.GetDirectoryName(layout.ArtifactRoot)!);
            _ = Directory.CreateDirectory(layout.ArtifactRoot);
            EnsureNoReparse(layout.ArtifactRoot, "artifact");
            await CopyFileAsync(source.Value!, layout.PayloadPath, cancellationToken).ConfigureAwait(false);

            ArtifactHashOperationResult<ArtifactHashReceipt> hash = await _hashService.HashAsync(
                new ArtifactHashRequest(validation.Value.ProjectRoot, layout.PayloadPath, request.Artifact.Id),
                cancellationToken).ConfigureAwait(false);
            if (!hash.IsSuccess)
            {
                return Failed(hash.Failures.Select(static failure =>
                    new ArtifactStoreFailure(failure.Code, failure.Location, failure.Diagnostic)));
            }

            var record = new ArtifactStoreRecord(request.Artifact, hash.Value!.ContentIdentity, layout);
            await WriteNewMetadataAsync(layout.MetadataPath, record, cancellationToken).ConfigureAwait(false);
            return ArtifactStoreOperationResult.Success(record);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("ARTIFACT_STORE_CANCELLED", "$", "The artifact-store operation was cancelled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed("ARTIFACT_STORE_IO_FAILED", "$", "The artifact-store operation encountered a filesystem error.");
        }
    }

    public async Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> ReadAsync(
        ArtifactStoreReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ReadCoreAsync(
            request.ProjectRoot,
            request.ArtifactsRoot,
            request.JobId,
            request.ArtifactId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> TransitionAsync(
        ArtifactStoreTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArtifactStoreOperationResult<ArtifactStoreRecord> current = await ReadCoreAsync(
            request.ProjectRoot,
            request.ArtifactsRoot,
            request.JobId,
            request.ArtifactId,
            cancellationToken).ConfigureAwait(false);
        if (!current.IsSuccess)
        {
            return current;
        }

        if (!current.Value!.Artifact.LifecycleState.Equals(request.ExpectedState))
        {
            return Failed("ARTIFACT_STORE_STATE_CONFLICT", "expectedState", "The persisted lifecycle state does not match the expected state.");
        }

        if (!IsApprovedTransition(request.ExpectedState, request.TargetState))
        {
            return Failed("ARTIFACT_STORE_TRANSITION_INVALID", "targetState", "Only staged-to-validated and validated-to-promoted transitions are allowed.");
        }

        if (request.ChangedAtUtc.Offset != TimeSpan.Zero ||
            request.ChangedAtUtc < current.Value.Artifact.StateChangedAtUtc)
        {
            return Failed("ARTIFACT_STORE_TRANSITION_TIME_INVALID", "changedAtUtc", "The transition time must be UTC and must not precede the current state time.");
        }

        BuildArtifact previous = current.Value.Artifact;
        BuildModelValidationResult<BuildArtifact> changed = BuildArtifact.Create(
            previous.Id,
            previous.JobId,
            previous.StepId,
            previous.Role,
            previous.Target,
            request.TargetState,
            previous.LogicalReference,
            previous.CreatedAtUtc,
            request.ChangedAtUtc);
        var updated = new ArtifactStoreRecord(changed.Value!, current.Value.ContentIdentity, current.Value.Layout);
        try
        {
            await ReplaceMetadataAsync(updated.Layout.MetadataPath, updated, cancellationToken).ConfigureAwait(false);
            return ArtifactStoreOperationResult.Success(updated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("ARTIFACT_STORE_CANCELLED", "$", "The artifact-store operation was cancelled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed("ARTIFACT_STORE_IO_FAILED", "$", "The artifact-store operation encountered a filesystem error.");
        }
    }

    private async Task<ArtifactStoreOperationResult<ArtifactStoreRecord>> ReadCoreAsync(
        string projectRoot,
        string artifactsRoot,
        BuildJobId jobId,
        BuildArtifactId artifactId,
        CancellationToken cancellationToken)
    {
        try
        {
            ArtifactStoreOperationResult<ValidatedPaths> validation = ValidatePaths(
                projectRoot,
                artifactsRoot,
                jobId,
                artifactId,
                requireStore: true);
            if (!validation.IsSuccess)
            {
                return Failed(validation.Failures);
            }

            ArtifactStoreLayout layout = validation.Value!.Layout;
            if (!File.Exists(layout.PayloadPath) || !File.Exists(layout.MetadataPath))
            {
                return Failed("ARTIFACT_STORE_ENTRY_INCOMPLETE", "artifact", "The artifact payload and metadata must both exist.");
            }

            EnsureExistingPath(validation.Value.ProjectRoot, layout.PayloadPath, "payload");
            EnsureExistingPath(validation.Value.ProjectRoot, layout.MetadataPath, "metadata");
            byte[] metadata = await ReadMetadataAsync(layout.MetadataPath, cancellationToken).ConfigureAwait(false);
            ArtifactStoreOperationResult<ArtifactStoreRecord> decoded = ArtifactStoreMetadataCodec.Read(metadata, layout);
            if (!decoded.IsSuccess)
            {
                return decoded;
            }

            if (!decoded.Value!.Artifact.JobId.Equals(jobId) || !decoded.Value.Artifact.Id.Equals(artifactId))
            {
                return Failed("ARTIFACT_STORE_IDENTITY_MISMATCH", "metadata", "Artifact metadata does not match its typed store path.");
            }

            ArtifactHashOperationResult<ArtifactHashReceipt> hash = await _hashService.HashAsync(
                new ArtifactHashRequest(validation.Value.ProjectRoot, layout.PayloadPath, artifactId),
                cancellationToken).ConfigureAwait(false);
            return !hash.IsSuccess
                ? Failed(hash.Failures.Select(static failure =>
                    new ArtifactStoreFailure(failure.Code, failure.Location, failure.Diagnostic)))
                : !hash.Value!.ContentIdentity.Equals(decoded.Value.ContentIdentity)
                ? Failed("ARTIFACT_STORE_INTEGRITY_MISMATCH", "payload", "The payload no longer matches its recorded size and SHA-256.")
                : decoded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("ARTIFACT_STORE_CANCELLED", "$", "The artifact-store operation was cancelled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed("ARTIFACT_STORE_IO_FAILED", "$", "The artifact-store operation encountered a filesystem error.");
        }
    }

    private static ArtifactStoreOperationResult<ValidatedPaths> ValidatePaths(
        string projectRoot,
        string artifactsRoot,
        BuildJobId jobId,
        BuildArtifactId artifactId,
        bool requireStore)
    {
        ArtifactStoreOperationResult<string> project = Canonicalize(projectRoot, "projectRoot", trimEnd: true);
        ArtifactStoreOperationResult<string> store = Canonicalize(artifactsRoot, "artifactsRoot", trimEnd: true);
        if (!project.IsSuccess || !store.IsSuccess)
        {
            return ArtifactStoreOperationResult.Failed<ValidatedPaths>(
                [.. project.Failures, .. store.Failures]);
        }

        if (!Directory.Exists(project.Value!))
        {
            return PathFailed("ARTIFACT_STORE_PROJECT_MISSING", "projectRoot", "The trusted project root does not exist.");
        }

        if (!IsStrictDescendant(store.Value!, project.Value!))
        {
            return PathFailed("ARTIFACT_STORE_OUTSIDE_PROJECT", "artifactsRoot", "The artifact root must be contained by the project root.");
        }

        if (requireStore && !Directory.Exists(store.Value!))
        {
            return PathFailed("ARTIFACT_STORE_MISSING", "artifactsRoot", "The artifact store does not exist.");
        }

        EnsureExistingAncestors(project.Value!, store.Value!, "artifactsRoot");
        ArtifactStoreLayout layout = ArtifactStoreLayoutFactory.Create(store.Value!, jobId, artifactId).Value!;
        return ArtifactStoreOperationResult.Success(new ValidatedPaths(project.Value!, layout));
    }

    private static ArtifactStoreOperationResult<string> ValidateSource(
        string sourceFilePath,
        ValidatedPaths paths)
    {
        ArtifactStoreOperationResult<string> source = Canonicalize(sourceFilePath, "sourceFilePath", trimEnd: false);
        if (!source.IsSuccess)
        {
            return source;
        }

        if (!File.Exists(source.Value!))
        {
            return StringFailed("ARTIFACT_STORE_SOURCE_MISSING", "sourceFilePath", "The source artifact does not exist.");
        }

        if (!IsStrictDescendant(source.Value!, paths.ProjectRoot) ||
            IsStrictDescendant(source.Value!, paths.Layout.ArtifactsRoot))
        {
            return StringFailed("ARTIFACT_STORE_SOURCE_LOCATION", "sourceFilePath", "The source must be project-contained and outside the artifact store.");
        }

        try
        {
            EnsureExistingPath(paths.ProjectRoot, source.Value!, "sourceFilePath");
        }
        catch (IOException)
        {
            return StringFailed("ARTIFACT_STORE_REPARSE_BOUNDARY", "sourceFilePath", "The source path must not cross a reparse point.");
        }

        return source;
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
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
                int read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false);
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

    private static async Task<byte[]> ReadMetadataAsync(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        return info.Length is <= 0 or > MaximumMetadataBytes
            ? throw new IOException("Artifact metadata has an invalid size.")
            : await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteNewMetadataAsync(
        string path,
        ArtifactStoreRecord record,
        CancellationToken cancellationToken)
    {
        byte[] json = ArtifactStoreMetadataCodec.Write(record);
        await using var destination = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await destination.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReplaceMetadataAsync(
        string path,
        ArtifactStoreRecord record,
        CancellationToken cancellationToken)
    {
        string next = path + ".next";
        await WriteNewMetadataAsync(next, record, cancellationToken).ConfigureAwait(false);
        File.Move(next, path, overwrite: true);
    }

    private static bool IsApprovedTransition(
        BuildArtifactLifecycleState current,
        BuildArtifactLifecycleState target) =>
        current.Equals(BuildArtifactLifecycleState.Staged) && target.Equals(BuildArtifactLifecycleState.Validated) ||
        current.Equals(BuildArtifactLifecycleState.Validated) && target.Equals(BuildArtifactLifecycleState.Promoted);

    private static ArtifactStoreOperationResult<string> Canonicalize(string path, string location, bool trimEnd)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return StringFailed("ARTIFACT_STORE_PATH_INVALID", location, "A fully qualified path is required.");
        }

        try
        {
            string full = Path.GetFullPath(path);
            if (trimEnd)
            {
                full = Path.TrimEndingDirectorySeparator(full);
                path = Path.TrimEndingDirectorySeparator(path);
            }

            return StringComparer.OrdinalIgnoreCase.Equals(full, path)
                ? ArtifactStoreOperationResult.Success(full)
                : StringFailed("ARTIFACT_STORE_PATH_NOT_CANONICAL", location, "The path must already be canonical.");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return StringFailed("ARTIFACT_STORE_PATH_INVALID", location, "The path is invalid.");
        }
    }

    private static void EnsureContainedDirectory(ValidatedPaths paths, string directory)
    {
        _ = Directory.CreateDirectory(directory);
        EnsureExistingPath(paths.ProjectRoot, directory, "artifact");
    }

    private static void EnsureExistingAncestors(string boundary, string path, string location)
    {
        string relative = Path.GetRelativePath(boundary, path);
        string current = boundary;
        EnsureNoReparse(current, location);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
            {
                break;
            }

            EnsureNoReparse(current, location);
        }
    }

    private static void EnsureExistingPath(string boundary, string path, string location)
    {
        EnsureExistingAncestors(boundary, path, location);
        EnsureNoReparse(path, location);
    }

    private static void EnsureNoReparse(string path, string location)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"The {location} path crosses a reparse point.");
        }
    }

    private static bool IsStrictDescendant(string candidate, string boundary) =>
        !StringComparer.OrdinalIgnoreCase.Equals(candidate, boundary) &&
        candidate.StartsWith(EnsureTrailingSeparator(boundary), StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path) =>
        path + Path.DirectorySeparatorChar;

    private static ArtifactStoreOperationResult<ArtifactStoreRecord> Failed(
        IEnumerable<ArtifactStoreFailure> failures) =>
        ArtifactStoreOperationResult.Failed<ArtifactStoreRecord>([.. failures]);

    private static ArtifactStoreOperationResult<ArtifactStoreRecord> Failed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactStoreOperationResult.Failed<ArtifactStoreRecord>(
            new ArtifactStoreFailure(code, location, diagnostic));

    private static ArtifactStoreOperationResult<string> StringFailed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactStoreOperationResult.Failed<string>(
            new ArtifactStoreFailure(code, location, diagnostic));

    private static ArtifactStoreOperationResult<ValidatedPaths> PathFailed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactStoreOperationResult.Failed<ValidatedPaths>(
            new ArtifactStoreFailure(code, location, diagnostic));

    private sealed record ValidatedPaths(string ProjectRoot, ArtifactStoreLayout Layout);
}
