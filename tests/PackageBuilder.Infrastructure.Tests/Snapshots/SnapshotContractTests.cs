using PackageBuilder.Contracts.Snapshots;

namespace PackageBuilder.Infrastructure.Tests.Snapshots;

public sealed class SnapshotContractTests
{
    [Fact]
    public void ServiceRejectsNullFileSystem() =>
        Assert.Throws<ArgumentNullException>(() => new PackageBuilder.Infrastructure.Snapshots.SourceSnapshotService(null!));

    [Fact]
    public void RequestAndReceiptRejectNullRequiredValues()
    {
        SourceSnapshotPolicy policy = SourceSnapshotPolicy.Create(1, 1, 1).Value!;
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotRequest(null!, "source", "job", "snapshot", policy));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotRequest("project", null!, "job", "snapshot", policy));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotRequest("project", "source", null!, "snapshot", policy));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotRequest("project", "source", "job", null!, policy));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotRequest("project", "source", "job", "snapshot", null!));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotFile(null!, 0, "hash"));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotFile("file", 0, null!));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotReceipt(null!, [], 0));
        _ = Assert.Throws<ArgumentNullException>(() => new SourceSnapshotReceipt("snapshot", null!, 0));
    }

    [Fact]
    public void FailureRejectsMissingSafeFields()
    {
        _ = Assert.Throws<ArgumentException>(() => new SnapshotFailure("", "location", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new SnapshotFailure("CODE", "", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new SnapshotFailure("CODE", "location", ""));
    }

    [Fact]
    public void ResultFactoriesRejectInvalidCallsAndCopyCollections()
    {
        _ = Assert.Throws<ArgumentNullException>(() => SnapshotOperationResult.Success<string>(null!));
        _ = Assert.Throws<ArgumentNullException>(() => SnapshotOperationResult.Failed<string>(null!));
        _ = Assert.Throws<ArgumentException>(() => SnapshotOperationResult.Failed<string>());
        _ = Assert.Throws<ArgumentException>(() => SnapshotOperationResult.Failed<string>([null!]));

        SnapshotFailure[] failures = [new SnapshotFailure("CODE", "$", "diagnostic")];
        var result = SnapshotOperationResult.Failed<string>(failures);
        failures[0] = new SnapshotFailure("OTHER", "$", "other");

        Assert.Equal("CODE", result.Failures[0].Code);
    }

    [Fact]
    public void ReceiptCopiesInputCollection()
    {
        var files = new List<SourceSnapshotFile> { new("A.fbx", 1, new string('0', 64)) };
        var receipt = new SourceSnapshotReceipt("snapshot", files, 1);
        files.Clear();

        _ = Assert.Single(receipt.Files);
        Assert.Equal(1, receipt.FileCount);
    }
}
