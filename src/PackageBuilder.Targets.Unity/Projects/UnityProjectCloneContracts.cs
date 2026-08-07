using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Targets.Unity.Projects;

/// <summary>Controls whether a completed Unity job clone is deleted or retained for diagnosis.</summary>
public enum UnityCloneRetentionPolicy
{
    DeleteAlways = 0,
    RetainOnFailure,
    RetainAlways,
}

/// <summary>Describes the terminal outcome used to apply the clone retention policy.</summary>
public enum UnityCloneCompletionKind
{
    Success = 0,
    Failure,
    Cancelled,
}

/// <summary>Identifies the contained template and job roots for one exclusive Unity clone.</summary>
public sealed record UnityProjectCloneRequest(
    string ProjectRoot,
    string TemplateDirectory,
    string JobDirectory,
    BuildJobId JobId,
    UnityCloneRetentionPolicy RetentionPolicy);

/// <summary>Reports a stable clone failure without throwing for an expected invalid operation.</summary>
public sealed record UnityProjectCloneFailure(string Code, string Diagnostic);

/// <summary>Records clone retention and cleanup behavior after a lease completes.</summary>
public sealed record UnityProjectCloneCompletion(
    UnityCloneCompletionKind Kind,
    string CloneDirectory,
    bool Retained,
    bool CleanupSucceeded,
    string? Diagnostic);

/// <summary>Returns either the exclusive clone lease or one structured failure.</summary>
public sealed class UnityProjectCloneResult
{
    private UnityProjectCloneResult(UnityProjectCloneLease? lease, UnityProjectCloneFailure? failure)
    {
        Lease = lease;
        Failure = failure;
    }

    /// <summary>Gets whether an exclusive clone lease was created.</summary>
    public bool IsSuccessful => Lease is not null;

    /// <summary>Gets the exclusive clone lease when creation succeeded.</summary>
    public UnityProjectCloneLease? Lease { get; }

    /// <summary>Gets the structured failure when creation was rejected.</summary>
    public UnityProjectCloneFailure? Failure { get; }

    internal static UnityProjectCloneResult Success(UnityProjectCloneLease lease) => new(lease, null);

    internal static UnityProjectCloneResult Failed(string code, string diagnostic) =>
        new(null, new UnityProjectCloneFailure(code, diagnostic));
}
