using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Infrastructure.Artifacts;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

internal sealed class ArtifactPromotionTestWorkspace : IDisposable
{
    public ArtifactPromotionTestWorkspace()
    {
        ProjectRoot = FindProjectRoot();
        Root = Path.Combine(ProjectRoot, "artifacts", "PB-0206", "tests", Guid.NewGuid().ToString("N"));
        SourceRoot = Path.Combine(Root, "source");
        ArtifactsRoot = Path.Combine(Root, "artifacts");
        BuildsRoot = Path.Combine(ArtifactsRoot, "Builds");
        _ = Directory.CreateDirectory(SourceRoot);
        _ = Directory.CreateDirectory(ArtifactsRoot);
        Store = new ArtifactStore();
    }

    public string ProjectRoot { get; }

    public string Root { get; }

    public string SourceRoot { get; }

    public string ArtifactsRoot { get; }

    public string BuildsRoot { get; }

    public ArtifactStore Store { get; }

    public async Task<ArtifactStoreRecord> StageAndValidateAsync(
        string logicalReference = "packages/Silverwing.zip",
        string artifactId = "Artifact-01")
    {
        string source = Path.Combine(SourceRoot, artifactId + ".bin");
        await File.WriteAllBytesAsync(
            source,
            "validated-package-content"u8.ToArray(),
            TestContext.Current.CancellationToken);
        ArtifactStoreRecord staged = (await Store.StageAsync(
            new ArtifactStoreStageRequest(
                ProjectRoot,
                ArtifactsRoot,
                source,
                Artifact(logicalReference, artifactId)),
            TestContext.Current.CancellationToken)).Value!;
        return (await Store.TransitionAsync(
            new ArtifactStoreTransitionRequest(
                ProjectRoot,
                ArtifactsRoot,
                staged.Artifact.JobId,
                staged.Artifact.Id,
                BuildArtifactLifecycleState.Staged,
                BuildArtifactLifecycleState.Validated,
                Utc(12, 1)),
            TestContext.Current.CancellationToken)).Value!;
    }

    public async Task<ArtifactStoreRecord> StageOnlyAsync(
        string logicalReference = "packages/Silverwing.zip",
        string artifactId = "Artifact-01")
    {
        string source = Path.Combine(SourceRoot, artifactId + ".bin");
        await File.WriteAllBytesAsync(
            source,
            "validated-package-content"u8.ToArray(),
            TestContext.Current.CancellationToken);
        return (await Store.StageAsync(
            new ArtifactStoreStageRequest(
                ProjectRoot,
                ArtifactsRoot,
                source,
                Artifact(logicalReference, artifactId)),
            TestContext.Current.CancellationToken)).Value!;
    }

    public ArtifactPromotionRequest Request(
        string artifactId = "Artifact-01",
        DateTimeOffset? promotedAtUtc = null,
        string? projectRoot = null,
        string? artifactsRoot = null,
        string? buildsRoot = null) =>
        new(
            projectRoot ?? ProjectRoot,
            artifactsRoot ?? ArtifactsRoot,
            buildsRoot ?? BuildsRoot,
            ArtifactStoreTestWorkspace.JobId("Job-01"),
            ArtifactStoreTestWorkspace.ArtifactId(artifactId),
            promotedAtUtc ?? Utc(12, 2));

    public static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static BuildArtifact Artifact(string logicalReference, string artifactId) =>
        BuildArtifact.Create(
            ArtifactStoreTestWorkspace.ArtifactId(artifactId),
            ArtifactStoreTestWorkspace.JobId("Job-01"),
            BuildStepId.Create("Step-01").Value!,
            BuildArtifactRole.Create("portable-package").Value!,
            BuildTarget.Portable,
            BuildArtifactLifecycleState.Staged,
            logicalReference,
            Utc(12, 0),
            Utc(12, 0)).Value!;

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PackageBuilder.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Package Builder project root could not be located.");
    }
}
