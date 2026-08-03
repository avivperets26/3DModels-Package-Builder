using PackageBuilder.Contracts.Archives;

namespace PackageBuilder.Infrastructure.Tests.Archives;

public sealed class ArchiveSafetyPolicyTests
{
    [Fact]
    public void ValidPolicyNormalizesAndDeduplicatesExtensions()
    {
        ArchiveOperationResult<ArchiveSafetyPolicy> result = ArchiveSafetyPolicy.Create(
            1_000,
            10,
            5,
            500,
            900,
            20,
            [".FBX", ".png", ".fbx"],
            allowExtensionlessFiles: true);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Failures);
        Assert.Equal([".fbx", ".png"], result.Value!.AllowedExtensions);
        Assert.True(result.Value.AllowsFile("Model.FBX"));
        Assert.True(result.Value.AllowsFile("README"));
        Assert.False(result.Value.AllowsFile("script.exe"));
        _ = Assert.Throws<ArgumentException>(() => result.Value.AllowsFile(" "));
    }

    [Fact]
    public void InvalidPolicyReturnsEveryStructuredFailure()
    {
        ArchiveOperationResult<ArchiveSafetyPolicy> result = ArchiveSafetyPolicy.Create(
            0,
            0,
            0,
            20,
            10,
            double.NaN,
            null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_POSITIVE_LIMIT");
        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_TOTAL_LIMIT");
        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_RATIO");
        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_EXTENSIONS_NULL");
        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_EXTENSIONS_EMPTY");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidExpansionRatiosAreRejected(double ratio)
    {
        ArchiveOperationResult<ArchiveSafetyPolicy> result = ArchiveSafetyPolicy.Create(
            100,
            1,
            1,
            100,
            100,
            ratio,
            [".txt"]);

        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_RATIO");
    }

    [Theory]
    [InlineData("")]
    [InlineData("txt")]
    [InlineData(".bad/path")]
    [InlineData(".bad extension")]
    public void InvalidExtensionsAreRejected(string extension)
    {
        ArchiveOperationResult<ArchiveSafetyPolicy> result = ArchiveSafetyPolicy.Create(
            100,
            1,
            1,
            100,
            100,
            10,
            [extension]);

        Assert.Contains(result.Failures, failure => failure.Code == "ARCHIVE_POLICY_EXTENSION_INVALID");
    }

    [Fact]
    public void EmptyExtensionPolicyRequiresExplicitExtensionlessPermission()
    {
        ArchiveOperationResult<ArchiveSafetyPolicy> rejected = ArchiveSafetyPolicy.Create(
            100,
            1,
            1,
            100,
            100,
            10,
            []);
        ArchiveOperationResult<ArchiveSafetyPolicy> accepted = ArchiveSafetyPolicy.Create(
            100,
            1,
            1,
            100,
            100,
            10,
            [],
            allowExtensionlessFiles: true);

        Assert.Contains(rejected.Failures, failure => failure.Code == "ARCHIVE_POLICY_EXTENSIONS_EMPTY");
        Assert.True(accepted.IsSuccess);
    }

    [Fact]
    public void ResultAndFailureContractsEnforceTheirInvariants()
    {
        var failure = new ArchiveFailure("CODE", "location", "Diagnostic.");
        var failed =
            ArchiveOperationResult.Failed<ArchiveSafetyPolicy>(failure);

        Assert.False(failed.IsSuccess);
        Assert.Same(failure, Assert.Single(failed.Failures));
        _ = Assert.Throws<ArgumentException>(() => ArchiveOperationResult.Failed<ArchiveSafetyPolicy>());
        _ = Assert.Throws<ArgumentException>(
            () => ArchiveOperationResult.Failed<ArchiveSafetyPolicy>([null!]));
        _ = Assert.Throws<ArgumentException>(() => new ArchiveFailure("", "location", "Diagnostic."));
        _ = Assert.Throws<ArgumentException>(() => new ArchiveFailure("CODE", "", "Diagnostic."));
        _ = Assert.Throws<ArgumentException>(() => new ArchiveFailure("CODE", "location", ""));
    }
}
