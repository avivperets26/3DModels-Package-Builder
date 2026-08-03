using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

public sealed class ArtifactStoreLayoutTests
{
    [Fact]
    public void LayoutUsesDeterministicSafeKeysInsteadOfRawIdentities()
    {
        using var workspace = new ArtifactStoreTestWorkspace();

        ArtifactStoreLayout layout = ArtifactStoreLayoutFactory.Create(
            workspace.StoreRoot,
            ArtifactStoreTestWorkspace.JobId("../Job/Ω"),
            ArtifactStoreTestWorkspace.ArtifactId("CON:Artifact/One")).Value!;

        Assert.Matches("^[0-9a-f]{64}$", layout.JobKey);
        Assert.Matches("^[0-9a-f]{64}$", layout.ArtifactKey);
        Assert.DoesNotContain("../Job/Ω", layout.ArtifactRoot, StringComparison.Ordinal);
        Assert.DoesNotContain("CON:Artifact/One", layout.ArtifactRoot, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(layout.ArtifactRoot, "payload"), layout.PayloadPath);
        Assert.Equal(Path.Combine(layout.ArtifactRoot, "artifact.json"), layout.MetadataPath);
        Assert.Equal($"Jobs/{layout.JobKey}/{layout.ArtifactKey}/payload", layout.PayloadRelativePath);
        Assert.Equal($"Jobs/{layout.JobKey}/{layout.ArtifactKey}/artifact.json", layout.MetadataRelativePath);
    }

    [Fact]
    public void OrdinallyDifferentIdentitiesCannotCollideOnWindows()
    {
        using var workspace = new ArtifactStoreTestWorkspace();

        ArtifactStoreLayout upper = ArtifactStoreLayoutFactory.Create(
            workspace.StoreRoot,
            ArtifactStoreTestWorkspace.JobId("JOB"),
            ArtifactStoreTestWorkspace.ArtifactId("ARTIFACT")).Value!;
        ArtifactStoreLayout lower = ArtifactStoreLayoutFactory.Create(
            workspace.StoreRoot,
            ArtifactStoreTestWorkspace.JobId("job"),
            ArtifactStoreTestWorkspace.ArtifactId("artifact")).Value!;

        Assert.NotEqual(upper.JobKey, lower.JobKey);
        Assert.NotEqual(upper.ArtifactKey, lower.ArtifactKey);
        Assert.NotEqual(upper.ArtifactRoot, lower.ArtifactRoot);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public void LayoutRejectsInvalidRoots(string root)
    {
        ArtifactStoreOperationResult<ArtifactStoreLayout> result = ArtifactStoreLayoutFactory.Create(
            root,
            ArtifactStoreTestWorkspace.JobId("Job"),
            ArtifactStoreTestWorkspace.ArtifactId("Artifact"));

        Assert.False(result.IsSuccess);
        Assert.Equal("ARTIFACT_STORE_PATH_INVALID", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void LayoutRejectsNonCanonicalRoot()
    {
        using var workspace = new ArtifactStoreTestWorkspace();
        string nonCanonical = Path.Combine(workspace.Root, "folder", "..", "store");

        ArtifactStoreOperationResult<ArtifactStoreLayout> result = ArtifactStoreLayoutFactory.Create(
            nonCanonical,
            ArtifactStoreTestWorkspace.JobId("Job"),
            ArtifactStoreTestWorkspace.ArtifactId("Artifact"));

        Assert.False(result.IsSuccess);
        Assert.Equal("ARTIFACT_STORE_PATH_NOT_CANONICAL", Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void LayoutRejectsPathTooLongRoot()
    {
        string path = Path.GetPathRoot(Environment.SystemDirectory)! + new string('a', 40_000);

        ArtifactStoreOperationResult<ArtifactStoreLayout> result = ArtifactStoreLayoutFactory.Create(
            path,
            ArtifactStoreTestWorkspace.JobId("Job"),
            ArtifactStoreTestWorkspace.ArtifactId("Artifact"));

        Assert.False(result.IsSuccess);
        Assert.Equal("ARTIFACT_STORE_PATH_INVALID", Assert.Single(result.Failures).Code);
    }
}
