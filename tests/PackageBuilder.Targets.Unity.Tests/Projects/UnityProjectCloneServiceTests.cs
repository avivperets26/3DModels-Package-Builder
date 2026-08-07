using PackageBuilder.Targets.Unity.Projects;

namespace PackageBuilder.Targets.Unity.Tests.Projects;

[Trait("Task", "PB-0604")]
public sealed class UnityProjectCloneServiceTests
{
    [Fact]
    public void CreatesIsolatedCloneWithExactTemplateContentAndExclusiveLease()
    {
        using var workspace = new UnityCloneTestWorkspace();
        var service = new UnityProjectCloneService();

        UnityProjectCloneResult result = TryCreate(service, Request(workspace, UnityCloneRetentionPolicy.RetainAlways));

        Assert.True(result.IsSuccessful, result.Failure?.Diagnostic);
        using UnityProjectCloneLease lease = Assert.IsType<UnityProjectCloneLease>(result.Lease);
        Assert.Null(result.Failure);
        Assert.Equal(workspace.Clone, lease.CloneDirectory);
        Assert.Equal("template-content\n", File.ReadAllText(Path.Combine(workspace.Clone, "Assets", "marker.txt")));
        Assert.True(File.Exists(workspace.Lock));

        UnityProjectCloneResult concurrent = TryCreate(service, Request(workspace, UnityCloneRetentionPolicy.RetainAlways));
        Assert.False(concurrent.IsSuccessful);
        Assert.Equal("UNITY_CLONE_IO_FAILED", concurrent.Failure?.Code);

        UnityProjectCloneCompletion completion = lease.Complete(UnityCloneCompletionKind.Success);
        Assert.True(completion.Retained);
        Assert.True(completion.CleanupSucceeded);
        Assert.Null(completion.Diagnostic);
        Assert.True(Directory.Exists(workspace.Clone));
        Assert.False(File.Exists(workspace.Lock));
    }

    [Theory]
    [InlineData(UnityCloneRetentionPolicy.DeleteAlways, UnityCloneCompletionKind.Success, false)]
    [InlineData(UnityCloneRetentionPolicy.DeleteAlways, UnityCloneCompletionKind.Failure, false)]
    [InlineData(UnityCloneRetentionPolicy.DeleteAlways, UnityCloneCompletionKind.Cancelled, false)]
    [InlineData(UnityCloneRetentionPolicy.RetainOnFailure, UnityCloneCompletionKind.Success, false)]
    [InlineData(UnityCloneRetentionPolicy.RetainOnFailure, UnityCloneCompletionKind.Failure, true)]
    [InlineData(UnityCloneRetentionPolicy.RetainOnFailure, UnityCloneCompletionKind.Cancelled, true)]
    [InlineData(UnityCloneRetentionPolicy.RetainAlways, UnityCloneCompletionKind.Success, true)]
    public void CompletionAppliesExplicitRetentionPolicy(
        UnityCloneRetentionPolicy policy,
        UnityCloneCompletionKind kind,
        bool expectedRetained)
    {
        using var workspace = new UnityCloneTestWorkspace();
        var service = new UnityProjectCloneService();
        UnityProjectCloneLease lease = Assert.IsType<UnityProjectCloneLease>(TryCreate(service, Request(workspace, policy)).Lease);

        UnityProjectCloneCompletion completion = lease.Complete(kind);

        Assert.Equal(kind, completion.Kind);
        Assert.Equal(expectedRetained, completion.Retained);
        Assert.Equal(expectedRetained, Directory.Exists(workspace.Clone));
        Assert.True(completion.CleanupSucceeded);
        Assert.False(File.Exists(workspace.Lock));
    }

    [Fact]
    public void DisposeTreatsUnfinishedExecutionAsFailure()
    {
        using var workspace = new UnityCloneTestWorkspace();
        var service = new UnityProjectCloneService();
        UnityProjectCloneLease lease = Assert.IsType<UnityProjectCloneLease>(TryCreate(
            service,
            Request(workspace, UnityCloneRetentionPolicy.RetainOnFailure)).Lease);

        lease.Dispose();

        Assert.True(Directory.Exists(workspace.Clone));
        Assert.False(File.Exists(workspace.Lock));
        _ = Assert.Throws<ObjectDisposedException>(() => lease.Complete(UnityCloneCompletionKind.Success));
    }

    [Fact]
    public void ExistingCloneFailsWithoutModifyingRetainedContent()
    {
        using var workspace = new UnityCloneTestWorkspace();
        _ = Directory.CreateDirectory(workspace.Clone);
        string marker = Path.Combine(workspace.Clone, "retained.txt");
        File.WriteAllText(marker, "retained");
        var service = new UnityProjectCloneService();

        UnityProjectCloneResult result = TryCreate(service, Request(workspace, UnityCloneRetentionPolicy.RetainAlways));

        Assert.False(result.IsSuccessful);
        Assert.Equal("UNITY_CLONE_ALREADY_EXISTS", result.Failure?.Code);
        Assert.Equal("retained", File.ReadAllText(marker));
        Assert.False(File.Exists(workspace.Lock));
    }

    [Fact]
    public void PreCancelledCloneLeavesNoStagingOrLockState()
    {
        using var workspace = new UnityCloneTestWorkspace();
        var service = new UnityProjectCloneService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

#pragma warning disable xUnit1051 // This test deliberately supplies an already-cancelled token.
        UnityProjectCloneResult result = service.TryCreate(
            Request(workspace, UnityCloneRetentionPolicy.RetainAlways),
            cancellation.Token);
#pragma warning restore xUnit1051

        Assert.False(result.IsSuccessful);
        Assert.Equal("UNITY_CLONE_CANCELLED", result.Failure?.Code);
        Assert.False(Directory.Exists(workspace.Clone));
        Assert.False(File.Exists(workspace.Lock));
        Assert.Empty(Directory.GetDirectories(workspace.Job, ".unity-project-staging-*"));
    }

    [Fact]
    public void RejectsMissingOutsideOverlappingAndInvalidTemplatePaths()
    {
        using var workspace = new UnityCloneTestWorkspace();
        var service = new UnityProjectCloneService();
        string outside = Path.GetPathRoot(workspace.Root)!;
        string missing = Path.Combine(workspace.Root, "missing");

        AssertFailure(TryCreate(service, new(
            workspace.Root,
            missing,
            workspace.Job,
            workspace.JobId,
            UnityCloneRetentionPolicy.RetainAlways)), "UNITY_CLONE_DIRECTORY_MISSING");
        AssertFailure(TryCreate(service, new(
            workspace.Root,
            workspace.Template,
            outside,
            workspace.JobId,
            UnityCloneRetentionPolicy.RetainAlways)), "UNITY_CLONE_PATH_OUTSIDE_PROJECT");
        AssertFailure(TryCreate(service, new(
            workspace.Root,
            workspace.Template,
            Path.Combine(workspace.Template, "Assets"),
            workspace.JobId,
            UnityCloneRetentionPolicy.RetainAlways)), "UNITY_CLONE_PATH_OVERLAP");

        _ = Directory.CreateDirectory(Path.Combine(workspace.Template, "Unexpected"));
        AssertFailure(TryCreate(service, Request(workspace, UnityCloneRetentionPolicy.RetainAlways)),
            "UNITY_CLONE_TEMPLATE_INVALID");
    }

    [Fact]
    public void RejectsInvalidPolicyAndMissingJobIdentity()
    {
        using var workspace = new UnityCloneTestWorkspace();
        var service = new UnityProjectCloneService();

        AssertFailure(TryCreate(service, new(
            workspace.Root,
            workspace.Template,
            workspace.Job,
            workspace.JobId,
            (UnityCloneRetentionPolicy)999)), "UNITY_CLONE_POLICY_INVALID");
        AssertFailure(TryCreate(service, new(
            workspace.Root,
            workspace.Template,
            workspace.Job,
            null!,
            UnityCloneRetentionPolicy.RetainAlways)), "UNITY_CLONE_JOB_ID_MISSING");
    }

    private static UnityProjectCloneRequest Request(
        UnityCloneTestWorkspace workspace,
        UnityCloneRetentionPolicy retentionPolicy) =>
        new(workspace.Root, workspace.Template, workspace.Job, workspace.JobId, retentionPolicy);

    private static UnityProjectCloneResult TryCreate(
        UnityProjectCloneService service,
        UnityProjectCloneRequest request) =>
        service.TryCreate(request, TestContext.Current.CancellationToken);

    private static void AssertFailure(UnityProjectCloneResult result, string code)
    {
        Assert.False(result.IsSuccessful);
        Assert.Null(result.Lease);
        Assert.Equal(code, result.Failure?.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Failure?.Diagnostic));
    }
}
