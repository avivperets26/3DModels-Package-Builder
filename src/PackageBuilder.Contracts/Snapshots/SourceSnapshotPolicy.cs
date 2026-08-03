namespace PackageBuilder.Contracts.Snapshots;

/// <summary>Defines caller-approved limits for one immutable source snapshot.</summary>
public sealed class SourceSnapshotPolicy
{
    private SourceSnapshotPolicy(int maximumFileCount, long maximumFileBytes, long maximumTotalBytes)
    {
        MaximumFileCount = maximumFileCount;
        MaximumFileBytes = maximumFileBytes;
        MaximumTotalBytes = maximumTotalBytes;
    }

    public int MaximumFileCount { get; }

    public long MaximumFileBytes { get; }

    public long MaximumTotalBytes { get; }

    /// <summary>Creates explicit limits; the infrastructure never silently chooses production quotas.</summary>
    public static SnapshotOperationResult<SourceSnapshotPolicy> Create(
        int maximumFileCount,
        long maximumFileBytes,
        long maximumTotalBytes)
    {
        var failures = new List<SnapshotFailure>();
        if (maximumFileCount <= 0)
        {
            failures.Add(Failure("SNAPSHOT_POLICY_FILE_COUNT", "maximumFileCount"));
        }

        if (maximumFileBytes <= 0)
        {
            failures.Add(Failure("SNAPSHOT_POLICY_FILE_BYTES", "maximumFileBytes"));
        }

        if (maximumTotalBytes <= 0)
        {
            failures.Add(Failure("SNAPSHOT_POLICY_TOTAL_BYTES", "maximumTotalBytes"));
        }

        if (maximumTotalBytes < maximumFileBytes)
        {
            failures.Add(new SnapshotFailure(
                "SNAPSHOT_POLICY_TOTAL_TOO_SMALL",
                "maximumTotalBytes",
                "The total-byte limit must be at least the per-file limit."));
        }

        return failures.Count == 0
            ? SnapshotOperationResult.Success(new SourceSnapshotPolicy(
                maximumFileCount,
                maximumFileBytes,
                maximumTotalBytes))
            : SnapshotOperationResult.Failed<SourceSnapshotPolicy>([.. failures]);
    }

    private static SnapshotFailure Failure(string code, string location) =>
        new(code, location, "Snapshot limits must be greater than zero.");
}
