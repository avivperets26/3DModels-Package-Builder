using System.Buffers;
using System.IO.Compression;
using PackageBuilder.Contracts.Archives;

namespace PackageBuilder.Infrastructure.Archives;

/// <summary>Performs streaming ZIP preflight and extraction inside a caller-provided trusted boundary.</summary>
public sealed class SafeZipArchiveService(IArchiveFileSystem fileSystem) : ISafeArchiveService
{
    private const int CopyBufferSize = 65_536;
    private const int UnixFileTypeMask = 0xF000;
    private const int UnixRegularFile = 0x8000;
    private const int UnixDirectory = 0x4000;
    private const int UnixSymbolicLink = 0xA000;

    private static readonly SearchValues<char> _unsafeSegmentCharacters = SearchValues.Create("<>:\"|?*");
    private static readonly HashSet<string> _reservedWindowsNames = new(
        [
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9",
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly IArchiveFileSystem _fileSystem = fileSystem
        ?? throw new ArgumentNullException(nameof(fileSystem));

    public SafeZipArchiveService()
        : this(new PhysicalArchiveFileSystem())
    {
    }

    public async Task<ArchiveOperationResult<ArchiveInspection>> InspectAsync(
        ArchiveOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ArchiveOperationResult<ValidatedArchiveRequest> validated = ValidateRequest(request);
        if (!validated.IsSuccess)
        {
            return ArchiveOperationResult.Failed<ArchiveInspection>([.. validated.Failures]);
        }

        try
        {
            using Stream source = _fileSystem.OpenReadLocked(validated.Value!.SourceArchivePath);
            return await InspectLockedAsync(validated.Value, source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FailedInspection("ARCHIVE_CANCELLED", "$", "Archive inspection was cancelled.");
        }
        catch (InvalidDataException)
        {
            return FailedInspection("ARCHIVE_INVALID", "archive", "Archive structure is invalid or unsupported.");
        }
        catch (IOException)
        {
            return FailedInspection("ARCHIVE_READ_FAILED", "archive", "Archive could not be read safely.");
        }
        catch (UnauthorizedAccessException)
        {
            return FailedInspection("ARCHIVE_READ_FAILED", "archive", "Archive could not be read safely.");
        }
    }

    public async Task<ArchiveOperationResult<ArchiveExtractionReceipt>> ExtractAsync(
        ArchiveOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ArchiveOperationResult<ValidatedArchiveRequest> validated = ValidateRequest(request);
        if (!validated.IsSuccess)
        {
            return ArchiveOperationResult.Failed<ArchiveExtractionReceipt>([.. validated.Failures]);
        }

        try
        {
            using Stream source = _fileSystem.OpenReadLocked(validated.Value!.SourceArchivePath);
            if (!source.CanSeek)
            {
                return FailedExtraction(
                    "ARCHIVE_STREAM_NOT_SEEKABLE",
                    "archive",
                    "Archive source must support deterministic seek operations.");
            }

            long archiveBytes = source.Length;
            ArchiveFailure? archiveSizeFailure = ValidateArchiveSize(archiveBytes, request.Policy);
            if (archiveSizeFailure is not null)
            {
                return ArchiveOperationResult.Failed<ArchiveExtractionReceipt>(archiveSizeFailure);
            }

            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
            ArchiveOperationResult<ArchiveInspection> inspection =
                await InspectArchiveAsync(
                        validated.Value,
                        archive,
                        archiveBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            if (!inspection.IsSuccess)
            {
                return ArchiveOperationResult.Failed<ArchiveExtractionReceipt>([.. inspection.Failures]);
            }

            ArchiveFailure? destinationFailure = ValidateDestinationStillUnused(validated.Value);
            if (destinationFailure is not null)
            {
                return ArchiveOperationResult.Failed<ArchiveExtractionReceipt>(destinationFailure);
            }

            ArchiveFailure? createRootFailure = EnsureDirectoryTree(
                validated.Value.DestinationContainmentRoot,
                validated.Value.DestinationRoot);
            if (createRootFailure is not null)
            {
                return ArchiveOperationResult.Failed<ArchiveExtractionReceipt>(createRootFailure);
            }

            long extractedBytes = 0;
            foreach (ArchiveEntryPlan plan in inspection.Value!.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArchiveFailure? extractionFailure = await ExtractEntryAsync(
                        archive.Entries[plan.Index],
                        plan,
                        validated.Value,
                        extractedBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (extractionFailure is not null)
                {
                    return ArchiveOperationResult.Failed<ArchiveExtractionReceipt>(extractionFailure);
                }

                extractedBytes = checked(extractedBytes + plan.UncompressedBytes);
            }

            return ArchiveOperationResult.Success<ArchiveExtractionReceipt>(
                new ArchiveExtractionReceipt(
                    inspection.Value,
                    validated.Value.DestinationRoot,
                    extractedBytes));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FailedExtraction("ARCHIVE_CANCELLED", "$", "Archive extraction was cancelled.");
        }
        catch (InvalidDataException)
        {
            return FailedExtraction("ARCHIVE_INVALID", "archive", "Archive structure is invalid or unsupported.");
        }
        catch (IOException)
        {
            return FailedExtraction(
                "ARCHIVE_EXTRACTION_IO_FAILED",
                "$",
                "Archive extraction encountered a filesystem error; the dedicated destination must be discarded.");
        }
        catch (UnauthorizedAccessException)
        {
            return FailedExtraction(
                "ARCHIVE_EXTRACTION_IO_FAILED",
                "$",
                "Archive extraction encountered a filesystem error; the dedicated destination must be discarded.");
        }
    }

    private static async Task<ArchiveOperationResult<ArchiveInspection>> InspectLockedAsync(
        ValidatedArchiveRequest request,
        Stream source,
        CancellationToken cancellationToken)
    {
        if (!source.CanSeek)
        {
            return FailedInspection(
                "ARCHIVE_STREAM_NOT_SEEKABLE",
                "archive",
                "Archive source must support deterministic seek operations.");
        }

        long archiveBytes = source.Length;
        ArchiveFailure? sizeFailure = ValidateArchiveSize(archiveBytes, request.Policy);
        if (sizeFailure is not null)
        {
            return ArchiveOperationResult.Failed<ArchiveInspection>(sizeFailure);
        }

        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        return await InspectArchiveAsync(request, archive, archiveBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ArchiveOperationResult<ArchiveInspection>> InspectArchiveAsync(
        ValidatedArchiveRequest request,
        ZipArchive archive,
        long archiveBytes,
        CancellationToken cancellationToken)
    {
        if (archive.Entries.Count > request.Policy.MaximumEntryCount)
        {
            return FailedInspection(
                "ARCHIVE_ENTRY_COUNT_LIMIT",
                "archive",
                "Archive entry count exceeds the configured limit.");
        }

        var plans = new List<ArchiveEntryPlan>(archive.Entries.Count);
        var targets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        long totalCompressed = 0;
        long totalUncompressed = 0;

        for (int index = 0; index < archive.Entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ZipArchiveEntry entry = archive.Entries[index];
            ArchiveOperationResult<ArchiveEntryPlan> planned = PlanEntry(
                entry,
                index,
                request,
                targets);
            if (!planned.IsSuccess)
            {
                return ArchiveOperationResult.Failed<ArchiveInspection>([.. planned.Failures]);
            }

            ArchiveEntryPlan plan = planned.Value!;
            plans.Add(plan);
            if (plan.UncompressedBytes > request.Policy.MaximumTotalUncompressedBytes - totalUncompressed)
            {
                return FailedInspection(
                    "ARCHIVE_TOTAL_SIZE_LIMIT",
                    "archive",
                    "Projected extracted size exceeds the configured total limit.");
            }

            if (plan.CompressedBytes > archiveBytes - totalCompressed)
            {
                return FailedInspection(
                    "ARCHIVE_COMPRESSED_SIZE_INVALID",
                    "archive",
                    "Projected compressed entry data exceeds the archive file size.");
            }

            totalCompressed += plan.CompressedBytes;
            totalUncompressed += plan.UncompressedBytes;
        }

        if (ExpansionRatio(totalUncompressed, totalCompressed) > request.Policy.MaximumExpansionRatio)
        {
            return FailedInspection(
                "ARCHIVE_EXPANSION_RATIO_LIMIT",
                "archive",
                "Projected archive expansion ratio exceeds the configured limit.");
        }

        long verifiedBytes = 0;
        foreach (ArchiveEntryPlan plan in plans.Where(static plan => !plan.IsDirectory))
        {
            ArchiveFailure? contentFailure = await VerifyEntryContentAsync(
                    archive.Entries[plan.Index],
                    plan,
                    request.Policy,
                    verifiedBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            if (contentFailure is not null)
            {
                return ArchiveOperationResult.Failed<ArchiveInspection>(contentFailure);
            }

            verifiedBytes = checked(verifiedBytes + plan.UncompressedBytes);
        }

        return ArchiveOperationResult.Success<ArchiveInspection>(
            new ArchiveInspection(plans, archiveBytes, totalCompressed, totalUncompressed));
    }

    private static ArchiveOperationResult<ArchiveEntryPlan> PlanEntry(
        ZipArchiveEntry entry,
        int index,
        ValidatedArchiveRequest request,
        Dictionary<string, bool> targets)
    {
        string location = $"entries[{index}]";
        ArchiveFailure? typeFailure = ValidateEntryType(entry, location, out bool isDirectory);
        if (typeFailure is not null)
        {
            return ArchiveOperationResult.Failed<ArchiveEntryPlan>(typeFailure);
        }

        ArchiveOperationResult<string[]> segmentsResult = NormalizeEntrySegments(entry.FullName, isDirectory, location);
        if (!segmentsResult.IsSuccess)
        {
            return ArchiveOperationResult.Failed<ArchiveEntryPlan>([.. segmentsResult.Failures]);
        }

        string[] segments = segmentsResult.Value!;
        if (segments.Length > request.Policy.MaximumPathDepth)
        {
            return FailedPlan(
                "ARCHIVE_PATH_DEPTH_LIMIT",
                location,
                "Archive entry path depth exceeds the configured limit.");
        }

        string relativePath = string.Join(Path.DirectorySeparatorChar, segments);
        if (!isDirectory && !request.Policy.AllowsFile(relativePath))
        {
            return FailedPlan(
                "ARCHIVE_EXTENSION_NOT_ALLOWED",
                location,
                "Archive entry extension is not allowed by the active policy.");
        }

        ArchiveOperationResult<string> destination = CanonicalizeDestination(
            request.DestinationRoot,
            relativePath,
            location);
        if (!destination.IsSuccess)
        {
            return ArchiveOperationResult.Failed<ArchiveEntryPlan>([.. destination.Failures]);
        }

        string destinationPath = destination.Value!;

        ArchiveFailure? collisionFailure = ValidateTargetCollision(destinationPath, isDirectory, location, targets);
        if (collisionFailure is not null)
        {
            return ArchiveOperationResult.Failed<ArchiveEntryPlan>(collisionFailure);
        }

        // ZipArchive rejects negative size metadata before exposing an entry.
        long compressedBytes = entry.CompressedLength;
        long uncompressedBytes = entry.Length;

        if (isDirectory && (compressedBytes != 0 || uncompressedBytes != 0))
        {
            return FailedPlan(
                "ARCHIVE_DIRECTORY_HAS_DATA",
                location,
                "Archive directory entries must not contain file data.");
        }

        if (uncompressedBytes > request.Policy.MaximumEntryUncompressedBytes)
        {
            return FailedPlan(
                "ARCHIVE_ENTRY_SIZE_LIMIT",
                location,
                "Archive entry exceeds the configured extracted-size limit.");
        }

        targets.Add(destinationPath, isDirectory);
        return ArchiveOperationResult.Success<ArchiveEntryPlan>(
            new ArchiveEntryPlan(
                index,
                relativePath,
                destinationPath,
                isDirectory,
                compressedBytes,
                uncompressedBytes));
    }

    private static ArchiveFailure? ValidateEntryType(
        ZipArchiveEntry entry,
        string location,
        out bool isDirectory)
    {
        uint attributes = unchecked((uint)entry.ExternalAttributes);
        int unixType = (int)((attributes >> 16) & UnixFileTypeMask);
        var windowsAttributes = (FileAttributes)(attributes & ushort.MaxValue);
        bool nameIsDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
        bool attributesAreDirectory = unixType == UnixDirectory
            || (windowsAttributes & FileAttributes.Directory) != 0;
        isDirectory = nameIsDirectory || attributesAreDirectory;

        return unixType == UnixSymbolicLink
            || (windowsAttributes & FileAttributes.ReparsePoint) != 0
            || (unixType != 0 && unixType != UnixRegularFile && unixType != UnixDirectory)
            ? Failure(
                "ARCHIVE_ENTRY_LINK_OR_SPECIAL",
                location,
                "Archive links, reparse points, and special files are not allowed.")
            : (unixType == UnixRegularFile && nameIsDirectory)
            || (unixType == UnixDirectory && !nameIsDirectory)
            ? Failure(
                "ARCHIVE_ENTRY_TYPE_CONFLICT",
                location,
                "Archive entry type metadata conflicts with its path form.")
            : null;
    }

    private static ArchiveOperationResult<string[]> NormalizeEntrySegments(
        string entryName,
        bool isDirectory,
        string location)
    {
        // ZipArchive never exposes an entry with an empty FullName.
        string normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/')
            || (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':'))
        {
            return FailedSegments(
                "ARCHIVE_ENTRY_ABSOLUTE_PATH",
                location,
                "Archive entry uses an absolute or rooted path.");
        }

        if (isDirectory)
        {
            normalized = normalized.TrimEnd('/');
        }

        string[] segments = normalized.Split('/');
        if (segments.Any(static segment => segment.Length == 0))
        {
            return FailedSegments(
                "ARCHIVE_ENTRY_EMPTY_SEGMENT",
                location,
                "Archive entry contains an empty path segment.");
        }

        foreach (string segment in segments)
        {
            if (segment is "." or "..")
            {
                return FailedSegments(
                    "ARCHIVE_ENTRY_TRAVERSAL",
                    location,
                    "Archive entry contains a traversal segment.");
            }

            if (segment.EndsWith(' ') || segment.EndsWith('.'))
            {
                return FailedSegments(
                    "ARCHIVE_ENTRY_WINDOWS_ALIAS",
                    location,
                    "Archive entry uses a Windows-ambiguous trailing character.");
            }

            if (segment.Any(static character => char.IsControl(character))
                || segment.AsSpan().IndexOfAny(_unsafeSegmentCharacters) >= 0)
            {
                return FailedSegments(
                    "ARCHIVE_ENTRY_INVALID_CHARACTER",
                    location,
                    "Archive entry contains a character that is unsafe on Windows.");
            }

            string deviceCandidate = segment.Split('.', 2)[0];
            if (_reservedWindowsNames.Contains(deviceCandidate))
            {
                return FailedSegments(
                    "ARCHIVE_ENTRY_RESERVED_NAME",
                    location,
                    "Archive entry uses a reserved Windows device name.");
            }
        }

        return ArchiveOperationResult.Success<string[]>(segments);
    }

    private static ArchiveFailure? ValidateTargetCollision(
        string target,
        bool isDirectory,
        string location,
        Dictionary<string, bool> targets)
    {
        if (targets.ContainsKey(target))
        {
            return Failure(
                "ARCHIVE_DUPLICATE_TARGET",
                location,
                "Archive entries resolve to the same case-insensitive destination.");
        }

        string? parent = Path.GetDirectoryName(target);
        while (parent is not null)
        {
            if (targets.TryGetValue(parent, out bool parentIsDirectory) && !parentIsDirectory)
            {
                return Failure(
                    "ARCHIVE_FILE_DIRECTORY_COLLISION",
                    location,
                    "Archive file and directory targets conflict.");
            }

            parent = Path.GetDirectoryName(parent);
        }

        if (!isDirectory)
        {
            string prefix = string.Concat(target, Path.DirectorySeparatorChar);
            if (targets.Keys.Any(existing => existing.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return Failure(
                    "ARCHIVE_FILE_DIRECTORY_COLLISION",
                    location,
                    "Archive file and directory targets conflict.");
            }
        }

        return null;
    }

    private static async Task<ArchiveFailure?> VerifyEntryContentAsync(
        ZipArchiveEntry entry,
        ArchiveEntryPlan plan,
        ArchiveSafetyPolicy policy,
        long verifiedBeforeEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            using Stream input = entry.Open();
            long actual = await ReadBoundedAsync(
                    input,
                    plan,
                    policy,
                    verifiedBeforeEntry,
                    output: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return actual == plan.UncompressedBytes
                ? null
                : Failure(
                    "ARCHIVE_CONTENT_SIZE_MISMATCH",
                    $"entries[{plan.Index}]",
                    "Archive entry content size does not match its metadata.");
        }
        catch (InvalidDataException)
        {
            return Failure(
                "ARCHIVE_ENTRY_INVALID_OR_ENCRYPTED",
                $"entries[{plan.Index}]",
                "Archive entry is corrupt, encrypted, or uses unsupported compression.");
        }
        catch (IOException)
        {
            return Failure(
                "ARCHIVE_ENTRY_READ_FAILED",
                $"entries[{plan.Index}]",
                "Archive entry could not be read safely.");
        }
    }

    private async Task<ArchiveFailure?> ExtractEntryAsync(
        ZipArchiveEntry entry,
        ArchiveEntryPlan plan,
        ValidatedArchiveRequest request,
        long extractedBeforeEntry,
        CancellationToken cancellationToken)
    {
        string directory = plan.IsDirectory
            ? plan.DestinationPath
            : Path.GetDirectoryName(plan.DestinationPath)!;
        ArchiveFailure? directoryFailure = EnsureDirectoryTree(request.DestinationRoot, directory);
        if (directoryFailure is not null)
        {
            return directoryFailure;
        }

        if (plan.IsDirectory)
        {
            return null;
        }

        ArchiveFailure? targetFailure = InspectExistingChain(
            request.DestinationRoot,
            plan.DestinationPath,
            allowFileLeaf: false,
            requireLeaf: false,
            "destination");
        if (targetFailure is not null)
        {
            return targetFailure;
        }

        try
        {
            using Stream input = entry.Open();
            await using Stream output = _fileSystem.CreateNewFile(plan.DestinationPath);
            long actual = await ReadBoundedAsync(
                    input,
                    plan,
                    request.Policy,
                    extractedBeforeEntry,
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (InvalidDataException)
        {
            return Failure(
                "ARCHIVE_ENTRY_CHANGED",
                $"entries[{plan.Index}]",
                "Archive entry changed or became unreadable after preflight.");
        }
        catch (IOException)
        {
            return Failure(
                "ARCHIVE_ENTRY_WRITE_FAILED",
                $"entries[{plan.Index}]",
                "Archive entry could not be written; the dedicated destination must be discarded.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(
                "ARCHIVE_ENTRY_WRITE_FAILED",
                $"entries[{plan.Index}]",
                "Archive entry could not be written; the dedicated destination must be discarded.");
        }
    }

    private static async Task<long> ReadBoundedAsync(
        Stream input,
        ArchiveEntryPlan plan,
        ArchiveSafetyPolicy policy,
        long totalBeforeEntry,
        Stream? output,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            long actual = 0;
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return actual;
                }

                if (read > plan.UncompressedBytes - actual
                    || read > policy.MaximumEntryUncompressedBytes - actual
                    || read > policy.MaximumTotalUncompressedBytes - totalBeforeEntry - actual)
                {
                    throw new InvalidDataException("Archive content exceeded its verified size bounds.");
                }

                actual += read;

                if (output is not null)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private ArchiveOperationResult<ValidatedArchiveRequest> ValidateRequest(ArchiveOperationRequest request)
    {
        ArchiveOperationResult<string> project = NormalizeAbsolutePath(request.ProjectRoot, "projectRoot");
        ArchiveOperationResult<string> source = NormalizeAbsolutePath(request.SourceArchivePath, "sourceArchivePath");
        ArchiveOperationResult<string> containment = NormalizeAbsolutePath(
            request.DestinationContainmentRoot,
            "destinationContainmentRoot");
        ArchiveOperationResult<string> destination = NormalizeAbsolutePath(request.DestinationRoot, "destinationRoot");

        ArchiveFailure[] pathFailures =
        [
            .. project.Failures,
            .. source.Failures,
            .. containment.Failures,
            .. destination.Failures,
        ];
        if (pathFailures.Length != 0)
        {
            return ArchiveOperationResult.Failed<ValidatedArchiveRequest>(pathFailures);
        }

        string projectRoot = project.Value!;
        string sourcePath = source.Value!;
        string containmentRoot = containment.Value!;
        string destinationRoot = destination.Value!;

        if (Path.GetPathRoot(projectRoot)!.Equals(projectRoot, StringComparison.OrdinalIgnoreCase)
            || !_fileSystem.DirectoryExists(projectRoot))
        {
            return FailedRequest(
                "ARCHIVE_PROJECT_ROOT_INVALID",
                "projectRoot",
                "Project root must be an existing dedicated directory.");
        }

        if (!IsStrictDescendant(projectRoot, sourcePath))
        {
            return FailedRequest(
                "ARCHIVE_SOURCE_OUTSIDE_PROJECT",
                "sourceArchivePath",
                "Source archive must be contained beneath the project root.");
        }

        if (!_fileSystem.FileExists(sourcePath))
        {
            return FailedRequest(
                "ARCHIVE_SOURCE_MISSING",
                "sourceArchivePath",
                "Source archive does not exist as a regular file.");
        }

        if (!Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return FailedRequest(
                "ARCHIVE_SOURCE_EXTENSION",
                "sourceArchivePath",
                "Source archive must use the .zip extension.");
        }

        if (!IsStrictDescendant(projectRoot, containmentRoot)
            || !_fileSystem.DirectoryExists(containmentRoot))
        {
            return FailedRequest(
                "ARCHIVE_CONTAINMENT_ROOT_INVALID",
                "destinationContainmentRoot",
                "Destination containment root must be an existing project descendant.");
        }

        if (!IsStrictDescendant(containmentRoot, destinationRoot))
        {
            return FailedRequest(
                "ARCHIVE_DESTINATION_OUTSIDE_CONTAINMENT",
                "destinationRoot",
                "Destination must be a dedicated descendant of its containment root.");
        }

        ArchiveFailure? destinationFailure = ValidateDestinationStillUnused(
            new ValidatedArchiveRequest(
                projectRoot,
                sourcePath,
                containmentRoot,
                destinationRoot,
                request.Policy));
        if (destinationFailure is not null)
        {
            return ArchiveOperationResult.Failed<ValidatedArchiveRequest>(destinationFailure);
        }

        foreach ((string root, string target, bool allowFileLeaf, bool requireLeaf, string location) in new[]
                 {
                     (projectRoot, projectRoot, false, true, "projectRoot"),
                     (projectRoot, sourcePath, true, true, "sourceArchivePath"),
                     (projectRoot, containmentRoot, false, true, "destinationContainmentRoot"),
                     (containmentRoot, destinationRoot, false, false, "destinationRoot"),
                 })
        {
            ArchiveFailure? inspectionFailure = InspectExistingChain(
                root,
                target,
                allowFileLeaf,
                requireLeaf,
                location);
            if (inspectionFailure is not null)
            {
                return ArchiveOperationResult.Failed<ValidatedArchiveRequest>(inspectionFailure);
            }
        }

        return ArchiveOperationResult.Success<ValidatedArchiveRequest>(
            new ValidatedArchiveRequest(
                projectRoot,
                sourcePath,
                containmentRoot,
                destinationRoot,
                request.Policy));
    }

    private ArchiveFailure? ValidateDestinationStillUnused(ValidatedArchiveRequest request)
    {
        return _fileSystem.FileExists(request.DestinationRoot)
            || _fileSystem.DirectoryExists(request.DestinationRoot)
                ? Failure(
                    "ARCHIVE_DESTINATION_EXISTS",
                    "destinationRoot",
                    "Destination must be a new dedicated directory.")
                : null;
    }

    private ArchiveFailure? EnsureDirectoryTree(string root, string target)
    {
        string relative = Path.GetRelativePath(root, target);
        string current = root;
        foreach (string segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                if (_fileSystem.FileExists(current))
                {
                    return Failure(
                        "ARCHIVE_DESTINATION_COLLISION",
                        "destination",
                        "Extraction directory collides with an existing file.");
                }

                if (!_fileSystem.DirectoryExists(current))
                {
                    _fileSystem.CreateDirectory(current);
                }

                if ((_fileSystem.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return Failure(
                        "ARCHIVE_DESTINATION_REPARSE_POINT",
                        "destination",
                        "Extraction directory crosses a reparse point.");
                }
            }
            catch (IOException)
            {
                return Failure(
                    "ARCHIVE_DESTINATION_CREATE_FAILED",
                    "destination",
                    "Extraction directory could not be created or inspected safely.");
            }
            catch (UnauthorizedAccessException)
            {
                return Failure(
                    "ARCHIVE_DESTINATION_CREATE_FAILED",
                    "destination",
                    "Extraction directory could not be created or inspected safely.");
            }
        }

        return null;
    }

    private ArchiveFailure? InspectExistingChain(
        string root,
        string target,
        bool allowFileLeaf,
        bool requireLeaf,
        string location)
    {
        string current = root;
        string[] segments = StringComparer.OrdinalIgnoreCase.Equals(root, target)
            ? []
            : Path.GetRelativePath(root, target).Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries);

        for (int index = -1; index < segments.Length; index++)
        {
            if (index >= 0)
            {
                current = Path.Combine(current, segments[index]);
            }

            bool isLeaf = index == segments.Length - 1;
            bool fileExists = _fileSystem.FileExists(current);
            bool directoryExists = _fileSystem.DirectoryExists(current);
            if (!fileExists && !directoryExists)
            {
                return requireLeaf && isLeaf
                    ? Failure(
                        "ARCHIVE_PATH_MISSING",
                        location,
                        "Required archive operation path does not exist.")
                    : null;
            }

            if (fileExists && !(allowFileLeaf && isLeaf))
            {
                return Failure(
                    "ARCHIVE_PATH_FILE_COLLISION",
                    location,
                    "Archive operation path crosses an existing file.");
            }

            try
            {
                if ((_fileSystem.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return Failure(
                        "ARCHIVE_PATH_REPARSE_POINT",
                        location,
                        "Archive operation path crosses a reparse point.");
                }
            }
            catch (IOException)
            {
                return Failure(
                    "ARCHIVE_PATH_INSPECTION_FAILED",
                    location,
                    "Archive operation path could not be inspected safely.");
            }
            catch (UnauthorizedAccessException)
            {
                return Failure(
                    "ARCHIVE_PATH_INSPECTION_FAILED",
                    location,
                    "Archive operation path could not be inspected safely.");
            }
        }

        return null;
    }

    private static ArchiveOperationResult<string> NormalizeAbsolutePath(string value, string location)
    {
        if (string.IsNullOrWhiteSpace(value) || !StringComparer.Ordinal.Equals(value, value.Trim()))
        {
            return ArchiveOperationResult.Failed<string>(Failure(
                "ARCHIVE_PATH_EMPTY_OR_WHITESPACE",
                location,
                "Archive operation path must be a non-empty absolute value without surrounding whitespace."));
        }

        if (value.StartsWith("\\\\", StringComparison.Ordinal)
            || value.StartsWith("\\??\\", StringComparison.Ordinal)
            || value.StartsWith("\\\\?\\", StringComparison.Ordinal)
            || value.StartsWith("\\\\.\\", StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(value))
        {
            return ArchiveOperationResult.Failed<string>(Failure(
                "ARCHIVE_PATH_NOT_LOCAL_ABSOLUTE",
                location,
                "Archive operation path must be a fully qualified local path without device or UNC syntax."));
        }

        try
        {
            string fullPath = Path.GetFullPath(value);
            string pathRoot = Path.GetPathRoot(fullPath)!;
            if (!StringComparer.OrdinalIgnoreCase.Equals(fullPath, pathRoot))
            {
                fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return ArchiveOperationResult.Success<string>(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ArchiveOperationResult.Failed<string>(Failure(
                "ARCHIVE_PATH_INVALID",
                location,
                "Archive operation path could not be canonicalized safely."));
        }
    }

    private static bool IsStrictDescendant(string root, string candidate)
    {
        string prefix = string.Concat(root.TrimEnd(Path.DirectorySeparatorChar), Path.DirectorySeparatorChar);
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static ArchiveOperationResult<string> CanonicalizeDestination(
        string destinationRoot,
        string relativePath,
        string location)
    {
        string destinationPath;
        try
        {
            destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ArchiveOperationResult.Failed<string>(Failure(
                "ARCHIVE_ENTRY_PATH_INVALID",
                location,
                "Archive entry path cannot be canonicalized safely."));
        }

        return IsStrictDescendant(destinationRoot, destinationPath)
            ? ArchiveOperationResult.Success<string>(destinationPath)
            : ArchiveOperationResult.Failed<string>(Failure(
                "ARCHIVE_ENTRY_OUTSIDE_DESTINATION",
                location,
                "Archive entry resolves outside the dedicated destination."));
    }

    private static double ExpansionRatio(long uncompressedBytes, long compressedBytes) =>
        uncompressedBytes == 0
            ? 1d
            : compressedBytes == 0
                ? double.PositiveInfinity
                : uncompressedBytes / (double)compressedBytes;

    private static ArchiveFailure? ValidateArchiveSize(long archiveBytes, ArchiveSafetyPolicy policy) =>
        archiveBytes > policy.MaximumArchiveBytes
            ? Failure(
                "ARCHIVE_FILE_SIZE_LIMIT",
                "archive",
                "Archive file size exceeds the configured limit.")
            : null;

    private static ArchiveFailure Failure(string code, string location, string diagnostic) =>
        new(code, location, diagnostic);

    private static ArchiveOperationResult<ArchiveInspection> FailedInspection(
        string code,
        string location,
        string diagnostic) =>
        ArchiveOperationResult.Failed<ArchiveInspection>(Failure(code, location, diagnostic));

    private static ArchiveOperationResult<ArchiveExtractionReceipt> FailedExtraction(
        string code,
        string location,
        string diagnostic) =>
        ArchiveOperationResult.Failed<ArchiveExtractionReceipt>(Failure(code, location, diagnostic));

    private static ArchiveOperationResult<ArchiveEntryPlan> FailedPlan(
        string code,
        string location,
        string diagnostic) =>
        ArchiveOperationResult.Failed<ArchiveEntryPlan>(Failure(code, location, diagnostic));

    private static ArchiveOperationResult<string[]> FailedSegments(
        string code,
        string location,
        string diagnostic) =>
        ArchiveOperationResult.Failed<string[]>(Failure(code, location, diagnostic));

    private static ArchiveOperationResult<ValidatedArchiveRequest> FailedRequest(
        string code,
        string location,
        string diagnostic) =>
        ArchiveOperationResult.Failed<ValidatedArchiveRequest>(Failure(code, location, diagnostic));

    private sealed record ValidatedArchiveRequest(
        string ProjectRoot,
        string SourceArchivePath,
        string DestinationContainmentRoot,
        string DestinationRoot,
        ArchiveSafetyPolicy Policy);
}
