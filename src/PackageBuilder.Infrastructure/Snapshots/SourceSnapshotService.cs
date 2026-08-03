using System.Buffers;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Snapshots;

namespace PackageBuilder.Infrastructure.Snapshots;

/// <summary>Creates contained, copied source snapshots without ever writing through to accepted inputs.</summary>
public sealed class SourceSnapshotService(ISourceSnapshotFileSystem fileSystem) : ISourceSnapshotService
{
    private const int CopyBufferSize = 65_536;
    private static readonly SearchValues<char> _unsafeSegmentCharacters = SearchValues.Create("<>:\"/\\|?*");
    private static readonly HashSet<string> _reservedWindowsNames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly ISourceSnapshotFileSystem _fileSystem = fileSystem
        ?? throw new ArgumentNullException(nameof(fileSystem));

    public SourceSnapshotService()
        : this(new PhysicalSourceSnapshotFileSystem())
    {
    }

    public async Task<SnapshotOperationResult<SourceSnapshotReceipt>> CreateAsync(
        SourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SnapshotOperationResult<ValidatedRequest> validation = ValidateRequest(request);
        if (!validation.IsSuccess)
        {
            return SnapshotOperationResult.Failed<SourceSnapshotReceipt>([.. validation.Failures]);
        }

        try
        {
            SnapshotOperationResult<IReadOnlyList<PlannedFile>> plan = BuildPlan(validation.Value!);
            if (!plan.IsSuccess)
            {
                return SnapshotOperationResult.Failed<SourceSnapshotReceipt>([.. plan.Failures]);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_fileSystem.FileExists(validation.Value!.SnapshotRoot)
                || _fileSystem.DirectoryExists(validation.Value.SnapshotRoot))
            {
                return Failed("SNAPSHOT_DESTINATION_IN_USE", "snapshotRoot", "The dedicated snapshot destination is already in use.");
            }

            _fileSystem.CreateDirectory(validation.Value.SnapshotRoot);
            if (IsReparsePoint(validation.Value.SnapshotRoot))
            {
                return Failed("SNAPSHOT_DESTINATION_REPARSE", "snapshotRoot", "The snapshot destination resolved through a reparse point.");
            }

            var copied = new List<SourceSnapshotFile>(plan.Value!.Count);
            long totalBytes = 0;
            foreach (PlannedFile file in plan.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SnapshotOperationResult<SourceSnapshotFile> result = await CopyFileAsync(
                        validation.Value,
                        file,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return SnapshotOperationResult.Failed<SourceSnapshotReceipt>([.. result.Failures]);
                }

                copied.Add(result.Value!);
                totalBytes = checked(totalBytes + result.Value!.Bytes);
            }

            return SnapshotOperationResult.Success(
                new SourceSnapshotReceipt(validation.Value.SnapshotRoot, copied, totalBytes));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("SNAPSHOT_CANCELLED", "$", "Source snapshot creation was cancelled; discard the dedicated destination.");
        }
        catch (IOException)
        {
            return Failed("SNAPSHOT_IO_FAILED", "$", "Source snapshot creation encountered a filesystem error; discard the dedicated destination.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed("SNAPSHOT_IO_FAILED", "$", "Source snapshot creation encountered a filesystem error; discard the dedicated destination.");
        }
        catch (OverflowException)
        {
            return Failed("SNAPSHOT_SIZE_OVERFLOW", "$", "Source snapshot size accounting overflowed.");
        }
    }

    private SnapshotOperationResult<ValidatedRequest> ValidateRequest(SourceSnapshotRequest request)
    {
        var failures = new List<SnapshotFailure>();
        string? projectRoot = CanonicalizeRoot(request.ProjectRoot, "projectRoot", failures);
        string? sourceRoot = CanonicalizeRoot(request.SourceRoot, "sourceRoot", failures);
        string? jobRoot = CanonicalizeRoot(request.JobRoot, "jobRoot", failures);
        string? snapshotRoot = CanonicalizeRoot(request.SnapshotRoot, "snapshotRoot", failures);
        if (failures.Count != 0)
        {
            return SnapshotOperationResult.Failed<ValidatedRequest>([.. failures]);
        }

        if (!_fileSystem.DirectoryExists(projectRoot!))
        {
            failures.Add(Failure("SNAPSHOT_PROJECT_MISSING", "projectRoot", "The trusted project root does not exist."));
        }

        if (!_fileSystem.DirectoryExists(sourceRoot!))
        {
            failures.Add(Failure("SNAPSHOT_SOURCE_MISSING", "sourceRoot", "The accepted source directory does not exist."));
        }

        if (!_fileSystem.DirectoryExists(jobRoot!))
        {
            failures.Add(Failure("SNAPSHOT_JOB_MISSING", "jobRoot", "The dedicated job root does not exist."));
        }

        if (!IsStrictDescendant(sourceRoot!, projectRoot!))
        {
            failures.Add(Failure("SNAPSHOT_SOURCE_OUTSIDE_PROJECT", "sourceRoot", "The source directory must be contained by the project root."));
        }

        if (!IsStrictDescendant(jobRoot!, projectRoot!))
        {
            failures.Add(Failure("SNAPSHOT_JOB_OUTSIDE_PROJECT", "jobRoot", "The job directory must be contained by the project root."));
        }

        if (!IsStrictDescendant(snapshotRoot!, jobRoot!))
        {
            failures.Add(Failure("SNAPSHOT_DESTINATION_OUTSIDE_JOB", "snapshotRoot", "The snapshot destination must be contained by the job root."));
        }

        if (PathsOverlap(sourceRoot!, jobRoot!) || PathsOverlap(sourceRoot!, snapshotRoot!))
        {
            failures.Add(Failure("SNAPSHOT_SOURCE_DESTINATION_OVERLAP", "$", "Source and job-owned destinations must not overlap."));
        }

        if (_fileSystem.FileExists(snapshotRoot!) || _fileSystem.DirectoryExists(snapshotRoot!))
        {
            failures.Add(Failure("SNAPSHOT_DESTINATION_IN_USE", "snapshotRoot", "The dedicated snapshot destination must not already exist."));
        }

        if (failures.Count == 0)
        {
            ValidateExistingPath(projectRoot!, projectRoot!, "projectRoot", failures);
            ValidateExistingPath(projectRoot!, sourceRoot!, "sourceRoot", failures);
            ValidateExistingPath(projectRoot!, jobRoot!, "jobRoot", failures);
            ValidateExistingAncestors(jobRoot!, snapshotRoot!, failures);
        }

        return failures.Count == 0
            ? SnapshotOperationResult.Success(new ValidatedRequest(
                projectRoot!, sourceRoot!, jobRoot!, snapshotRoot!, request.Policy))
            : SnapshotOperationResult.Failed<ValidatedRequest>([.. failures]);
    }

    private SnapshotOperationResult<IReadOnlyList<PlannedFile>> BuildPlan(ValidatedRequest request)
    {
        var files = new List<PlannedFile>();
        var pending = new Stack<string>();
        pending.Push(request.SourceRoot);
        long totalBytes = 0;

        while (pending.Count != 0)
        {
            string directory = pending.Pop();
            if (IsReparsePoint(directory))
            {
                return PlanFailed("SNAPSHOT_SOURCE_REPARSE", "source", "Source directories must not contain reparse points.");
            }

            string[] entries = [.. _fileSystem.EnumerateEntries(directory).Order(StringComparer.OrdinalIgnoreCase)];
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                string entry = Path.GetFullPath(entries[index]);
                if (!IsStrictDescendant(entry, request.SourceRoot))
                {
                    return PlanFailed("SNAPSHOT_SOURCE_ESCAPE", "source", "A source entry escaped the accepted source root.");
                }

                FileAttributes attributes = _fileSystem.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return PlanFailed("SNAPSHOT_SOURCE_REPARSE", "source", "Source entries must not be reparse points.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }

                string relative = Path.GetRelativePath(request.SourceRoot, entry).Replace('\\', '/');
                SnapshotFailure? nameFailure = ValidateRelativePath(relative);
                if (nameFailure is not null)
                {
                    return SnapshotOperationResult.Failed<IReadOnlyList<PlannedFile>>(nameFailure);
                }

                long bytes = _fileSystem.GetFileLength(entry);
                if (bytes < 0 || bytes > request.Policy.MaximumFileBytes)
                {
                    return PlanFailed("SNAPSHOT_FILE_LIMIT", "source", "A source file exceeds the approved per-file byte limit.");
                }

                if (files.Count == request.Policy.MaximumFileCount)
                {
                    return PlanFailed("SNAPSHOT_FILE_COUNT_LIMIT", "source", "The source exceeds the approved file-count limit.");
                }

                totalBytes = checked(totalBytes + bytes);
                if (totalBytes > request.Policy.MaximumTotalBytes)
                {
                    return PlanFailed("SNAPSHOT_TOTAL_LIMIT", "source", "The source exceeds the approved total-byte limit.");
                }

                string destination = Path.GetFullPath(Path.Combine(request.SnapshotRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                files.Add(new PlannedFile(relative, entry, destination, bytes));
            }
        }

        files.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return files.Count == 0
            ? PlanFailed("SNAPSHOT_SOURCE_EMPTY", "sourceRoot", "The accepted source directory contains no files.")
            : SnapshotOperationResult.Success<IReadOnlyList<PlannedFile>>(files);
    }

    private async Task<SnapshotOperationResult<SourceSnapshotFile>> CopyFileAsync(
        ValidatedRequest request,
        PlannedFile file,
        CancellationToken cancellationToken)
    {
        if (IsReparsePoint(file.SourcePath))
        {
            return FileFailed("SNAPSHOT_SOURCE_CHANGED", file.RelativePath, "A source entry changed after preflight.");
        }

        EnsureDestinationDirectory(request, Path.GetDirectoryName(file.DestinationPath)!);
        using Stream source = _fileSystem.OpenReadLocked(file.SourcePath);
        if (!source.CanRead || (source.CanSeek && source.Length != file.Bytes))
        {
            return FileFailed("SNAPSHOT_SOURCE_CHANGED", file.RelativePath, "A source file changed after preflight.");
        }

        await using Stream destination = _fileSystem.CreateNewFile(file.DestinationPath);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        long copied = 0;
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                copied = checked(copied + read);
                if (copied > file.Bytes || copied > request.Policy.MaximumFileBytes)
                {
                    return FileFailed("SNAPSHOT_SOURCE_CHANGED", file.RelativePath, "A source file changed after preflight.");
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        if (copied != file.Bytes)
        {
            return FileFailed("SNAPSHOT_SOURCE_CHANGED", file.RelativePath, "A source file changed after preflight.");
        }

        string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        destination.Close();
        _fileSystem.MarkFileReadOnly(file.DestinationPath);
        return SnapshotOperationResult.Success(new SourceSnapshotFile(file.RelativePath, copied, digest));
    }

    private void EnsureDestinationDirectory(ValidatedRequest request, string destinationDirectory)
    {
        string relative = Path.GetRelativePath(request.SnapshotRoot, destinationDirectory);
        string current = request.SnapshotRoot;
        if (relative == ".")
        {
            return;
        }

        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!_fileSystem.DirectoryExists(current))
            {
                _fileSystem.CreateDirectory(current);
            }

            if (IsReparsePoint(current))
            {
                throw new IOException("Snapshot destination contains a reparse point.");
            }
        }
    }

    private void ValidateExistingPath(string boundary, string path, string location, List<SnapshotFailure> failures)
    {
        string relative = Path.GetRelativePath(boundary, path);
        string current = boundary;
        if (IsReparsePoint(current))
        {
            failures.Add(Failure("SNAPSHOT_REPARSE_BOUNDARY", location, "A trusted path boundary is a reparse point."));
            return;
        }

        if (relative == ".")
        {
            return;
        }

        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                failures.Add(Failure("SNAPSHOT_REPARSE_BOUNDARY", location, "A trusted path contains a reparse point."));
                return;
            }
        }
    }

    private void ValidateExistingAncestors(string jobRoot, string snapshotRoot, List<SnapshotFailure> failures)
    {
        string relative = Path.GetRelativePath(jobRoot, snapshotRoot);
        string current = jobRoot;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!_fileSystem.FileExists(current) && !_fileSystem.DirectoryExists(current))
            {
                return;
            }

            if (IsReparsePoint(current))
            {
                failures.Add(Failure("SNAPSHOT_REPARSE_BOUNDARY", "snapshotRoot", "The destination contains a reparse point."));
                return;
            }
        }
    }

    private bool IsReparsePoint(string path) =>
        (_fileSystem.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string? CanonicalizeRoot(string value, string location, List<SnapshotFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
        {
            failures.Add(Failure("SNAPSHOT_PATH_INVALID", location, "A fully qualified path is required."));
            return null;
        }

        try
        {
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
            if (!StringComparer.OrdinalIgnoreCase.Equals(full, Path.TrimEndingDirectorySeparator(value)))
            {
                failures.Add(Failure("SNAPSHOT_PATH_NOT_CANONICAL", location, "The path must already be canonical."));
                return null;
            }

            return full;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            failures.Add(Failure("SNAPSHOT_PATH_INVALID", location, "The path is invalid."));
            return null;
        }
    }

    private static SnapshotFailure? ValidateRelativePath(string relativePath)
    {
        string[] segments = relativePath.Split('/');
        foreach (string segment in segments)
        {
            string stem = segment.Split('.')[0];
            if (segment.EndsWith(' ')
                || segment.EndsWith('.')
                || segment.AsSpan().IndexOfAny(_unsafeSegmentCharacters) >= 0
                || segment.Any(char.IsControl)
                || _reservedWindowsNames.Contains(stem))
            {
                return Failure("SNAPSHOT_NAME_UNSAFE", "source", "A source name is not portable and safe for a snapshot.");
            }
        }

        return null;
    }

    private static bool IsStrictDescendant(string candidate, string boundary) =>
        !StringComparer.OrdinalIgnoreCase.Equals(candidate, boundary)
        && candidate.StartsWith(EnsureTrailingSeparator(boundary), StringComparison.OrdinalIgnoreCase);

    private static bool PathsOverlap(string left, string right) =>
        StringComparer.OrdinalIgnoreCase.Equals(left, right)
        || left.StartsWith(EnsureTrailingSeparator(right), StringComparison.OrdinalIgnoreCase)
        || right.StartsWith(EnsureTrailingSeparator(left), StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private static SnapshotFailure Failure(string code, string location, string diagnostic) =>
        new(code, location, diagnostic);

    private static SnapshotOperationResult<SourceSnapshotReceipt> Failed(string code, string location, string diagnostic) =>
        SnapshotOperationResult.Failed<SourceSnapshotReceipt>(Failure(code, location, diagnostic));

    private static SnapshotOperationResult<IReadOnlyList<PlannedFile>> PlanFailed(string code, string location, string diagnostic) =>
        SnapshotOperationResult.Failed<IReadOnlyList<PlannedFile>>(Failure(code, location, diagnostic));

    private static SnapshotOperationResult<SourceSnapshotFile> FileFailed(string code, string location, string diagnostic) =>
        SnapshotOperationResult.Failed<SourceSnapshotFile>(Failure(code, location, diagnostic));

    private sealed record ValidatedRequest(
        string ProjectRoot,
        string SourceRoot,
        string JobRoot,
        string SnapshotRoot,
        SourceSnapshotPolicy Policy);

    private sealed record PlannedFile(string RelativePath, string SourcePath, string DestinationPath, long Bytes);
}
