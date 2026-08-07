using System.Buffers;

namespace PackageBuilder.Targets.Unity.Projects;

/// <summary>Creates one reparse-safe Unity template clone while holding an exclusive job lock.</summary>
public sealed class UnityProjectCloneService
{
    private const int CopyBufferSize = 65_536;
    private static readonly string[] _requiredTemplateRoots = ["Assets", "Packages", "ProjectSettings"];
    private readonly int _copyBufferSize = CopyBufferSize;

    /// <summary>Creates an isolated Unity clone while holding its exclusive lease.</summary>
    public UnityProjectCloneResult TryCreate(UnityProjectCloneRequest request) =>
        TryCreate(request, CancellationToken.None);

    /// <summary>Creates an isolated Unity clone and observes cancellation during the durable copy.</summary>
    public UnityProjectCloneResult TryCreate(
        UnityProjectCloneRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        UnityProjectCloneFailure? validationFailure = Validate(request);
        if (validationFailure is not null)
        {
            return UnityProjectCloneResult.Failed(validationFailure.Code, validationFailure.Diagnostic);
        }

        string templateRoot = Canonical(request.TemplateDirectory);
        string jobRoot = Canonical(request.JobDirectory);
        string cloneRoot = Path.Combine(jobRoot, "unity-project");
        string lockPath = Path.Combine(jobRoot, ".packagebuilder-unity-clone.lock");
        FileStream? lockStream = null;
        string? stagingRoot = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            lockStream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);

            if (Directory.Exists(cloneRoot))
            {
                return FailAndRelease(
                    lockStream,
                    lockPath,
                    "UNITY_CLONE_ALREADY_EXISTS",
                    "The isolated Unity clone already exists.");
            }

            stagingRoot = Path.Combine(jobRoot, ".unity-project-staging-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(stagingRoot);
            CopyTemplate(templateRoot, stagingRoot, cancellationToken);
            Directory.Move(stagingRoot, cloneRoot);
            stagingRoot = null;

            var lease = new UnityProjectCloneLease(
                cloneRoot,
                lockPath,
                lockStream,
                request.RetentionPolicy);
            lockStream = null;
            return UnityProjectCloneResult.Success(lease);
        }
        catch (OperationCanceledException)
        {
            return UnityProjectCloneResult.Failed(
                "UNITY_CLONE_CANCELLED",
                "Unity template cloning was cancelled before the clone became available.");
        }
        catch (IOException)
        {
            return UnityProjectCloneResult.Failed(
                "UNITY_CLONE_IO_FAILED",
                "Unity template cloning could not acquire the job lock or copy the template safely.");
        }
        catch (UnauthorizedAccessException)
        {
            return UnityProjectCloneResult.Failed(
                "UNITY_CLONE_ACCESS_DENIED",
                "Unity template cloning was not authorized for the selected job directory.");
        }
        finally
        {
            if (lockStream is not null)
            {
                lockStream.Dispose();
                TryDeleteFile(lockPath);
            }

            if (stagingRoot is not null && Directory.Exists(stagingRoot))
            {
                TryDeleteDirectory(stagingRoot);
            }
        }
    }

    private static UnityProjectCloneFailure? Validate(UnityProjectCloneRequest request)
    {
        if (!Enum.IsDefined(request.RetentionPolicy))
        {
            return Failure("UNITY_CLONE_POLICY_INVALID", "The Unity clone retention policy is invalid.");
        }

        if (request.JobId is null)
        {
            return Failure("UNITY_CLONE_JOB_ID_MISSING", "A validated build-job identity is required.");
        }

        string projectRoot;
        string templateRoot;
        string jobRoot;
        try
        {
            projectRoot = Canonical(request.ProjectRoot);
            templateRoot = Canonical(request.TemplateDirectory);
            jobRoot = Canonical(request.JobDirectory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure("UNITY_CLONE_PATH_INVALID", "A Unity clone path is malformed.");
        }

        if (!Directory.Exists(projectRoot) || !Directory.Exists(templateRoot) || !Directory.Exists(jobRoot))
        {
            return Failure("UNITY_CLONE_DIRECTORY_MISSING", "Project, template, and job directories must exist.");
        }

        if (!IsStrictDescendant(projectRoot, templateRoot) || !IsStrictDescendant(projectRoot, jobRoot))
        {
            return Failure("UNITY_CLONE_PATH_OUTSIDE_PROJECT", "Template and job directories must be project-contained.");
        }

        if (IsSameOrDescendant(templateRoot, jobRoot) || IsSameOrDescendant(jobRoot, templateRoot))
        {
            return Failure("UNITY_CLONE_PATH_OVERLAP", "Template and job directories must not overlap.");
        }

        foreach (string path in EnumeratePathChain(projectRoot, templateRoot).Concat(
            EnumeratePathChain(projectRoot, jobRoot)))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return Failure("UNITY_CLONE_REPARSE_POINT", "Unity clone paths must not contain reparse points.");
            }
        }

        string[] actualRoots = Directory.GetDirectories(templateRoot)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        return !actualRoots.SequenceEqual(_requiredTemplateRoots, StringComparer.Ordinal) ||
            Directory.GetFiles(templateRoot).Length != 0
            ? Failure(
                "UNITY_CLONE_TEMPLATE_INVALID",
                "The Unity template must contain only Assets, Packages, and ProjectSettings roots.")
            : null;
    }

    private void CopyTemplate(string sourceRoot, string destinationRoot, CancellationToken cancellationToken)
    {
        foreach (string sourceDirectory in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(sourceDirectory);
            string relative = Path.GetRelativePath(sourceRoot, sourceDirectory);
            _ = Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(_copyBufferSize);
        try
        {
            foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                RejectReparsePoint(sourceFile);
                string relative = Path.GetRelativePath(sourceRoot, sourceFile);
                string destination = Path.Combine(destinationRoot, relative);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using FileStream source = new(
                    sourceFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    _copyBufferSize,
                    FileOptions.SequentialScan);
                using FileStream target = new(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    _copyBufferSize,
                    FileOptions.WriteThrough);
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    target.Write(buffer, 0, read);
                }

                target.Flush(flushToDisk: true);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static UnityProjectCloneResult FailAndRelease(
        FileStream lockStream,
        string lockPath,
        string code,
        string diagnostic)
    {
        lockStream.Dispose();
        TryDeleteFile(lockPath);
        return UnityProjectCloneResult.Failed(code, diagnostic);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("A reparse point was found while cloning the Unity template.");
        }
    }

    private static IEnumerable<string> EnumeratePathChain(string root, string descendant)
    {
        yield return root;
        string relative = Path.GetRelativePath(root, descendant);
        string current = root;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            yield return current;
        }
    }

    private static bool IsStrictDescendant(string root, string candidate) =>
        !PathComparer.Equals(root, candidate) && IsSameOrDescendant(root, candidate);

    private static bool IsSameOrDescendant(string root, string candidate)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return PathComparer.Equals(root, candidate) ||
            candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string Canonical(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static UnityProjectCloneFailure Failure(string code, string diagnostic) => new(code, diagnostic);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;
}
