using PackageBuilder.Contracts.Snapshots;

namespace PackageBuilder.Infrastructure.Tests.Snapshots;

public sealed class SourceSnapshotPolicyTests
{
    [Fact]
    public void ValidLimitsAreRetained()
    {
        SnapshotOperationResult<SourceSnapshotPolicy> result = SourceSnapshotPolicy.Create(12, 100, 500);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.MaximumFileCount);
        Assert.Equal(100, result.Value.MaximumFileBytes);
        Assert.Equal(500, result.Value.MaximumTotalBytes);
        Assert.Empty(result.Failures);
    }

    [Theory]
    [InlineData(0, 1, 1, "SNAPSHOT_POLICY_FILE_COUNT")]
    [InlineData(1, 0, 1, "SNAPSHOT_POLICY_FILE_BYTES")]
    [InlineData(1, 1, 0, "SNAPSHOT_POLICY_TOTAL_BYTES")]
    [InlineData(-1, -1, -1, "SNAPSHOT_POLICY_FILE_COUNT")]
    [InlineData(1, 10, 9, "SNAPSHOT_POLICY_TOTAL_TOO_SMALL")]
    public void InvalidLimitsReturnStructuredFailures(int count, long fileBytes, long totalBytes, string code)
    {
        SnapshotOperationResult<SourceSnapshotPolicy> result = SourceSnapshotPolicy.Create(count, fileBytes, totalBytes);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Failures, failure => failure.Code == code);
    }
}
