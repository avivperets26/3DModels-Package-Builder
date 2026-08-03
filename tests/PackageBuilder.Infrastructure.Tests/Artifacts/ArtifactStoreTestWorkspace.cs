using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Infrastructure.Tests.Artifacts;

internal sealed class ArtifactStoreTestWorkspace : IDisposable
{
    public ArtifactStoreTestWorkspace()
    {
        ProjectRoot = FindProjectRoot();
        Root = Path.Combine(ProjectRoot, "artifacts", "PB-0205", "tests", Guid.NewGuid().ToString("N"));
        SourceRoot = Path.Combine(Root, "source");
        StoreRoot = Path.Combine(Root, "store");
        _ = Directory.CreateDirectory(SourceRoot);
    }

    public string ProjectRoot { get; }

    public string Root { get; }

    public string SourceRoot { get; }

    public string StoreRoot { get; }

    public string AddSource(string name, ReadOnlySpan<byte> content)
    {
        string path = Path.Combine(SourceRoot, name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content.ToArray());
        return path;
    }

    public ArtifactStoreStageRequest StageRequest(
        string source,
        string jobId = "Job-01",
        string artifactId = "Artifact-01",
        BuildArtifactLifecycleState? state = null,
        bool includeTarget = true) =>
        new(ProjectRoot, StoreRoot, source, Artifact(jobId, artifactId, state, includeTarget));

    public ArtifactStoreReadRequest ReadRequest(
        string jobId = "Job-01",
        string artifactId = "Artifact-01") =>
        new(ProjectRoot, StoreRoot, JobId(jobId), ArtifactId(artifactId));

    public ArtifactStoreTransitionRequest TransitionRequest(
        BuildArtifactLifecycleState expected,
        BuildArtifactLifecycleState target,
        DateTimeOffset? changedAtUtc = null,
        string jobId = "Job-01",
        string artifactId = "Artifact-01") =>
        new(
            ProjectRoot,
            StoreRoot,
            JobId(jobId),
            ArtifactId(artifactId),
            expected,
            target,
            changedAtUtc ?? new DateTimeOffset(2026, 8, 3, 12, 1, 0, TimeSpan.Zero));

    public static BuildArtifact Artifact(
        string jobId,
        string artifactId,
        BuildArtifactLifecycleState? state = null,
        bool includeTarget = true) =>
        BuildArtifact.Create(
            ArtifactId(artifactId),
            JobId(jobId),
            BuildStepId.Create("Step-01").Value!,
            BuildArtifactRole.Create("portable-package").Value!,
            includeTarget ? BuildTarget.Portable : null,
            state ?? BuildArtifactLifecycleState.Staged,
            "packages/Silverwing.zip",
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)).Value!;

    public static BuildJobId JobId(string value) => BuildJobId.Create(value).Value!;

    public static BuildArtifactId ArtifactId(string value) => BuildArtifactId.Create(value).Value!;

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

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
