using System.Reflection;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactStoreContractTests
{
    [Fact]
    public void RequestsAndRecordsRejectNullRequiredValues()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        BuildArtifact artifact = ArtifactStoreTestWorkspace.Artifact("Job", "Artifact");
        BuildJobId jobId = artifact.JobId;
        BuildArtifactId artifactId = artifact.Id;
        ArtifactStoreLayout layout = ArtifactStoreLayoutFactory.Create(workspace.StoreRoot, jobId, artifactId).Value!;
        ArtifactContentIdentity identity = ArtifactContentIdentity.Create(
            0,
            Sha256Digest.Create(new string('0', 64)).Value).Value!;

        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreStageRequest(null!, "store", "source", artifact));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreStageRequest("project", null!, "source", artifact));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreStageRequest("project", "store", null!, artifact));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreStageRequest("project", "store", "source", null!));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreReadRequest(null!, "store", jobId, artifactId));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreReadRequest("project", null!, jobId, artifactId));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreReadRequest("project", "store", null!, artifactId));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreReadRequest("project", "store", jobId, null!));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreRecord(null!, identity, layout));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreRecord(artifact, null!, layout));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreRecord(artifact, identity, null!));
    }

    [Fact]
    public void TransitionRequestRejectsNullRequiredValues()
    {
        BuildJobId jobId = ArtifactStoreTestWorkspace.JobId("Job");
        BuildArtifactId artifactId = ArtifactStoreTestWorkspace.ArtifactId("Artifact");
        DateTimeOffset changed = DateTimeOffset.UnixEpoch;

        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreTransitionRequest(
            null!, "store", jobId, artifactId, BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated, changed));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreTransitionRequest(
            "project", null!, jobId, artifactId, BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated, changed));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreTransitionRequest(
            "project", "store", null!, artifactId, BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated, changed));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreTransitionRequest(
            "project", "store", jobId, null!, BuildArtifactLifecycleState.Staged, BuildArtifactLifecycleState.Validated, changed));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreTransitionRequest(
            "project", "store", jobId, artifactId, null!, BuildArtifactLifecycleState.Validated, changed));
        _ = Assert.Throws<ArgumentNullException>(() => new ArtifactStoreTransitionRequest(
            "project", "store", jobId, artifactId, BuildArtifactLifecycleState.Staged, null!, changed));
    }

    [Fact]
    public void ResultFactoriesRequireValidSuccessAndFailureValues()
    {
        var failure = new ArtifactStoreFailure("CODE", "location", "diagnostic");
        var failed = ArtifactStoreOperationResult.Failed<object>(failure);

        Assert.False(failed.IsSuccess);
        Assert.Null(failed.Value);
        Assert.Same(failure, Assert.Single(failed.Failures));
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactStoreOperationResult.Success<object>(null!));
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactStoreOperationResult.Failed<object>(null!));
        _ = Assert.Throws<ArgumentException>(() => ArtifactStoreOperationResult.Failed<object>());
        _ = Assert.Throws<ArgumentException>(() => ArtifactStoreOperationResult.Failed<object>(failure, null!));
    }

    [Fact]
    public void LayoutFactoryRejectsNullIdentities()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        BuildJobId jobId = ArtifactStoreTestWorkspace.JobId("Job");
        BuildArtifactId artifactId = ArtifactStoreTestWorkspace.ArtifactId("Artifact");

        _ = Assert.Throws<ArgumentNullException>(() => ArtifactStoreLayoutFactory.Create(workspace.StoreRoot, null!, artifactId));
        _ = Assert.Throws<ArgumentNullException>(() => ArtifactStoreLayoutFactory.Create(workspace.StoreRoot, jobId, null!));
    }

    [Fact]
    public void PublicStoreSurfaceDoesNotExposeMutableCollectionsOrDeletion()
    {
        MethodInfo[] methods = typeof(IArtifactStore).GetMethods();

        Assert.Equal(["ReadAsync", "StageAsync", "TransitionAsync"], methods.Select(static method => method.Name).Order());
        Assert.DoesNotContain(methods, static method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.All(typeof(ArtifactStoreLayout).GetProperties(), static property => Assert.False(property.CanWrite));
        Assert.All(typeof(ArtifactStoreRecord).GetProperties(), static property => Assert.False(property.CanWrite));
    }

    [Fact]
    public void StoreRejectsNullHashDependency() =>
        _ = Assert.Throws<ArgumentNullException>(() => new PackageBuilder.Infrastructure.Artifacts.ArtifactStore(null!));
}
