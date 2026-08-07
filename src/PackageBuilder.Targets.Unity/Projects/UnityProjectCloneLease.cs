namespace PackageBuilder.Targets.Unity.Projects;

/// <summary>Owns the exclusive writer lock for one isolated Unity project clone.</summary>
public sealed class UnityProjectCloneLease : IDisposable
{
    private readonly FileStream _lockStream;
    private readonly string _lockPath;
    private readonly UnityCloneRetentionPolicy _retentionPolicy;
    private bool _completed;

    internal UnityProjectCloneLease(
        string cloneDirectory,
        string lockPath,
        FileStream lockStream,
        UnityCloneRetentionPolicy retentionPolicy)
    {
        CloneDirectory = cloneDirectory;
        _lockPath = lockPath;
        _lockStream = lockStream;
        _retentionPolicy = retentionPolicy;
    }

    /// <summary>Gets the isolated Unity project directory protected by this lease.</summary>
    public string CloneDirectory { get; }

    /// <summary>Releases the writer lock and applies the configured retention policy once.</summary>
    public UnityProjectCloneCompletion Complete(UnityCloneCompletionKind kind)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;
        _lockStream.Dispose();

        bool retained = _retentionPolicy == UnityCloneRetentionPolicy.RetainAlways ||
            (_retentionPolicy == UnityCloneRetentionPolicy.RetainOnFailure &&
                kind != UnityCloneCompletionKind.Success);
        string? diagnostic = null;
        bool cleanupSucceeded = true;
        if (!retained && Directory.Exists(CloneDirectory))
        {
            try
            {
                Directory.Delete(CloneDirectory, recursive: true);
            }
            catch (IOException)
            {
                cleanupSucceeded = false;
                diagnostic = "Unity clone cleanup could not be completed.";
            }
            catch (UnauthorizedAccessException)
            {
                cleanupSucceeded = false;
                diagnostic = "Unity clone cleanup was not authorized.";
            }
        }

        try
        {
            if (File.Exists(_lockPath))
            {
                File.Delete(_lockPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            cleanupSucceeded = false;
            diagnostic ??= "Unity clone lock cleanup could not be completed.";
        }

        return new UnityProjectCloneCompletion(
            kind,
            CloneDirectory,
            retained,
            cleanupSucceeded,
            diagnostic);
    }

    /// <summary>Completes an unfinished lease as a failure so diagnostic retention remains explicit.</summary>
    public void Dispose()
    {
        if (!_completed)
        {
            _ = Complete(UnityCloneCompletionKind.Failure);
        }
    }
}
