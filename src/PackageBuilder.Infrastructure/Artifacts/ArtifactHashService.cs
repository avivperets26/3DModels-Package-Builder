using System.Buffers;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>Calculates SHA-256 identities without loading complete artifact files into memory.</summary>
public sealed class ArtifactHashService(IArtifactHashFileSystem fileSystem) : IArtifactHashService
{
    internal const int BufferSize = 65_536;

    private readonly IArtifactHashFileSystem _fileSystem = fileSystem
        ?? throw new ArgumentNullException(nameof(fileSystem));

    public ArtifactHashService()
        : this(new PhysicalArtifactHashFileSystem())
    {
    }

    public async Task<ArtifactHashOperationResult<ArtifactHashReceipt>> HashAsync(
        ArtifactHashRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ArtifactHashOperationResult<ValidatedRequest> validation = Validate(request);
            if (!validation.IsSuccess)
            {
                return ArtifactHashOperationResult.Failed<ArtifactHashReceipt>([.. validation.Failures]);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ValidatedRequest value = validation.Value!;
            long expectedBytes = _fileSystem.GetFileLength(value.FilePath);
            if (expectedBytes < 0)
            {
                return Failed("ARTIFACT_HASH_LENGTH_INVALID", "file", "The artifact reported an invalid byte length.");
            }

            if (IsReparsePoint(value.FilePath))
            {
                return Failed("ARTIFACT_HASH_FILE_REPARSE", "file", "The artifact file must not be a reparse point.");
            }

            using Stream source = _fileSystem.OpenReadLocked(value.FilePath);
            if (!source.CanRead || (source.CanSeek && source.Length != expectedBytes))
            {
                return Failed("ARTIFACT_HASH_SOURCE_CHANGED", "file", "The artifact changed before hashing began.");
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            long bytesRead = 0;
            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(
                            buffer.AsMemory(0, BufferSize),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    bytesRead = checked(bytesRead + read);
                    if (bytesRead > expectedBytes)
                    {
                        return Failed("ARTIFACT_HASH_SOURCE_CHANGED", "file", "The artifact changed while it was hashed.");
                    }

                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            if (bytesRead != expectedBytes)
            {
                return Failed("ARTIFACT_HASH_SOURCE_CHANGED", "file", "The artifact changed while it was hashed.");
            }

            string digestText = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            Sha256Digest digest = Sha256Digest.Create(digestText).Value!;
            ArtifactContentIdentity identity = ArtifactContentIdentity.Create(bytesRead, digest).Value!;
            return ArtifactHashOperationResult.Success(
                new ArtifactHashReceipt(value.ArtifactId, identity));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failed("ARTIFACT_HASH_CANCELLED", "$", "Artifact hashing was cancelled.");
        }
        catch (IOException)
        {
            return Failed("ARTIFACT_HASH_IO_FAILED", "$", "Artifact hashing encountered a filesystem error.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed("ARTIFACT_HASH_IO_FAILED", "$", "Artifact hashing encountered a filesystem error.");
        }
        catch (OverflowException)
        {
            return Failed("ARTIFACT_HASH_SIZE_OVERFLOW", "$", "Artifact byte accounting overflowed.");
        }
    }

    private ArtifactHashOperationResult<ValidatedRequest> Validate(ArtifactHashRequest request)
    {
        var failures = new List<ArtifactHashFailure>();
        string? projectRoot = CanonicalizeRoot(request.ProjectRoot, failures);
        string? filePath = CanonicalizeFile(request.FilePath, failures);
        if (failures.Count != 0)
        {
            return ArtifactHashOperationResult.Failed<ValidatedRequest>([.. failures]);
        }

        if (!_fileSystem.DirectoryExists(projectRoot!))
        {
            failures.Add(Failure("ARTIFACT_HASH_PROJECT_MISSING", "projectRoot", "The trusted project root does not exist."));
        }

        if (!_fileSystem.FileExists(filePath!))
        {
            failures.Add(Failure("ARTIFACT_HASH_FILE_MISSING", "file", "The artifact file does not exist."));
        }

        if (!IsStrictDescendant(filePath!, projectRoot!))
        {
            failures.Add(Failure("ARTIFACT_HASH_OUTSIDE_PROJECT", "file", "The artifact file must be contained by the project root."));
        }

        if (failures.Count == 0)
        {
            ValidateExistingPath(projectRoot!, filePath!, failures);
        }

        return failures.Count == 0
            ? ArtifactHashOperationResult.Success(new ValidatedRequest(projectRoot!, filePath!, request.ArtifactId))
            : ArtifactHashOperationResult.Failed<ValidatedRequest>([.. failures]);
    }

    private void ValidateExistingPath(string projectRoot, string filePath, List<ArtifactHashFailure> failures)
    {
        string relative = Path.GetRelativePath(projectRoot, filePath);
        string current = projectRoot;
        if (IsReparsePoint(current))
        {
            failures.Add(Failure("ARTIFACT_HASH_REPARSE_BOUNDARY", "projectRoot", "The trusted project root is a reparse point."));
            return;
        }

        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
            {
                failures.Add(Failure("ARTIFACT_HASH_REPARSE_BOUNDARY", "file", "The artifact path crosses a reparse point."));
                return;
            }
        }
    }

    private bool IsReparsePoint(string path) =>
        (_fileSystem.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static string? CanonicalizeRoot(string value, List<ArtifactHashFailure> failures)
    {
        string? full = Canonicalize(value, "projectRoot", failures);
        if (full is null)
        {
            return null;
        }

        full = Path.TrimEndingDirectorySeparator(full);
        if (!StringComparer.OrdinalIgnoreCase.Equals(full, Path.TrimEndingDirectorySeparator(value)))
        {
            failures.Add(Failure("ARTIFACT_HASH_PATH_NOT_CANONICAL", "projectRoot", "The path must already be canonical."));
            return null;
        }

        return full;
    }

    private static string? CanonicalizeFile(string value, List<ArtifactHashFailure> failures)
    {
        string? full = Canonicalize(value, "file", failures);
        if (full is not null && !StringComparer.OrdinalIgnoreCase.Equals(full, value))
        {
            failures.Add(Failure("ARTIFACT_HASH_PATH_NOT_CANONICAL", "file", "The path must already be canonical."));
            return null;
        }

        return full;
    }

    private static string? Canonicalize(string value, string location, List<ArtifactHashFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
        {
            failures.Add(Failure("ARTIFACT_HASH_PATH_INVALID", location, "A fully qualified path is required."));
            return null;
        }

        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            failures.Add(Failure("ARTIFACT_HASH_PATH_INVALID", location, "The path is invalid."));
            return null;
        }
    }

    private static bool IsStrictDescendant(string candidate, string boundary) =>
        !StringComparer.OrdinalIgnoreCase.Equals(candidate, boundary)
        && candidate.StartsWith(EnsureTrailingSeparator(boundary), StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private static ArtifactHashFailure Failure(string code, string location, string diagnostic) =>
        new(code, location, diagnostic);

    private static ArtifactHashOperationResult<ArtifactHashReceipt> Failed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactHashOperationResult.Failed<ArtifactHashReceipt>(Failure(code, location, diagnostic));

    private sealed record ValidatedRequest(
        string ProjectRoot,
        string FilePath,
        PackageBuilder.Domain.BuildJobs.BuildArtifactId ArtifactId);
}
