using System.Diagnostics;
using System.Reflection;
using PackageBuilder.Contracts.Processes;
using PackageBuilder.Infrastructure.Processes;

namespace PackageBuilder.Infrastructure.Tests.Processes;

public sealed class StructuredExternalProcessLifecycleTests(ProcessRunnerTestWorkspace workspace)
    : IClassFixture<ProcessRunnerTestWorkspace>
{
    [Fact]
    public async Task ExternalCancellationIsAcknowledgedGracefullyAndPreservesCompleteLogs()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(250));

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunWithCancellationAsync(
            workspace.Request(
                arguments: ["--mode=graceful"],
                executionPolicy: Policy(grace: 1_000)),
            cancellation);

        Assert.True(result.IsSuccess);
        ExternalProcessReceipt receipt = result.Value!;
        Assert.Equal(ProcessCompletionKind.Cancelled, receipt.Completion.Kind);
        Assert.True(receipt.Completion.CancellationSignalCreated);
        Assert.True(receipt.Completion.GracefulTerminationAcknowledged);
        Assert.False(receipt.Completion.ForcedTermination);
        Assert.True(receipt.Completion.ControlFileCleanupSucceeded);
        Assert.Equal(42, receipt.ExitCode);
        Assert.Contains("probe-started", receipt.StandardOutput.Text, StringComparison.Ordinal);
        Assert.Equal("probe-cancelled", receipt.StandardError.Text);
        AssertPreservedLogs(receipt);
        AssertNoControlFiles();
    }

    [Fact]
    public async Task StartupTimeoutEscalatesAndPreservesEmptyLogs()
    {
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(
                arguments: ["--mode=startup-delay"],
                executionPolicy: Policy(startup: 100, grace: 50)));

        AssertForced(result.Value!, ProcessCompletionKind.StartupTimedOut);
        Assert.Equal(string.Empty, result.Value!.StandardOutput.Text);
        Assert.Equal(string.Empty, result.Value.StandardError.Text);
        AssertPreservedLogs(result.Value);
        AssertNoControlFiles();
    }

    [Fact]
    public async Task IdleTimeoutEscalatesAfterObservedActivity()
    {
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(
                arguments: ["--mode=ignore"],
                executionPolicy: Policy(idle: 100, grace: 50)));

        AssertForced(result.Value!, ProcessCompletionKind.IdleTimedOut);
        Assert.Contains("probe-started", result.Value!.StandardOutput.Text, StringComparison.Ordinal);
        AssertPreservedLogs(result.Value);
        AssertNoControlFiles();
    }

    [Fact]
    public async Task TotalTimeoutWinsWhileHeartbeatPreventsIdleTimeout()
    {
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(
                arguments: ["--mode=heartbeat"],
                executionPolicy: Policy(startup: 250, idle: 150, total: 500, grace: 50)));

        Assert.True(result.IsSuccess);
        AssertForced(result.Value!, ProcessCompletionKind.TotalTimedOut);
        Assert.Contains("heartbeat", result.Value!.StandardOutput.Text, StringComparison.Ordinal);
        AssertPreservedLogs(result.Value);
        AssertNoControlFiles();
    }

    [Fact]
    public async Task ForcedCancellationTerminatesChildTreeAndReleasesProcessHandles()
    {
        string pidFile = Path.Combine(workspace.WorkingDirectory, $"child-{Guid.NewGuid():N}.pid");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(500));

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunWithCancellationAsync(
            workspace.Request(
                arguments: ["--mode=ignore", "--spawn-child", $"--pid-file={pidFile}"],
                executionPolicy: Policy(idle: 5_000, total: 5_000, grace: 50)),
            cancellation);

        AssertForced(result.Value!, ProcessCompletionKind.Cancelled);
        Assert.True(File.Exists(pidFile));
        int childPid = int.Parse(
            await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(IsProcessAlive(childPid));
        AssertPreservedLogs(result.Value!);
        AssertNoControlFiles();
    }

    [Theory]
    [InlineData("startup-zero", "executionPolicy.startupTimeout")]
    [InlineData("idle-negative", "executionPolicy.idleTimeout")]
    [InlineData("total-too-large", "executionPolicy.totalTimeout")]
    [InlineData("grace-zero", "executionPolicy.gracefulTerminationTimeout")]
    [InlineData("startup-after-total", "executionPolicy.startupTimeout")]
    [InlineData("idle-after-total", "executionPolicy.idleTimeout")]
    public async Task InvalidLifecyclePoliciesFailBeforeLaunch(string caseId, string location)
    {
        int logDirectoryCount = Directory.GetDirectories(workspace.LogDirectory, "*", SearchOption.AllDirectories).Length;
        ProcessExecutionPolicy policy = caseId switch
        {
            "startup-zero" => new(TimeSpan.Zero, Seconds(1), Seconds(2), Seconds(1)),
            "idle-negative" => new(Seconds(1), TimeSpan.FromMilliseconds(-1), Seconds(2), Seconds(1)),
            "total-too-large" => new(Seconds(1), Seconds(1), TimeSpan.FromDays(8), Seconds(1)),
            "grace-zero" => new(Seconds(1), Seconds(1), Seconds(2), TimeSpan.Zero),
            "startup-after-total" => new(Seconds(3), Seconds(1), Seconds(2), Seconds(1)),
            _ => new(Seconds(1), Seconds(3), Seconds(2), Seconds(1)),
        };

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(executionPolicy: policy));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Failures, failure => failure.Location == location);
        Assert.Equal(
            logDirectoryCount,
            Directory.GetDirectories(workspace.LogDirectory, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task CancellationBeforeLaunchCreatesNoProcessState()
    {
        int logDirectoryCount = Directory.GetDirectories(workspace.LogDirectory, "*", SearchOption.AllDirectories).Length;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        cancellation.Cancel();

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunWithCancellationAsync(
            workspace.Request(),
            cancellation);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Failures, failure => failure.Code == "PROCESS_CANCELLED_BEFORE_START");
        Assert.Equal(
            logDirectoryCount,
            Directory.GetDirectories(workspace.LogDirectory, "*", SearchOption.AllDirectories).Length);
        AssertNoControlFiles();
    }

    [Fact]
    public void CancellationSignalOperationsReportExistingAndLockedFiles()
    {
        string controlFile = Path.Combine(workspace.TemporaryDirectory, $"signal-{Guid.NewGuid():N}.request");
        File.WriteAllText(controlFile, string.Empty);

        Assert.False(InvokePrivate<bool>("TryCreateCancellationSignal", controlFile));
        using (var locked = new FileStream(controlFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(InvokePrivate<bool>("TryDeleteCancellationSignal", controlFile));
        }

        File.Delete(controlFile);
    }

    [Fact]
    public async Task TerminationPreservesUnownedSignalWhenExitPrecedesEscalation()
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = workspace.ProbeExecutablePath,
            CreateNoWindow = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("The process probe did not start.");
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        string controlFile = Path.Combine(workspace.TemporaryDirectory, $"race-{Guid.NewGuid():N}.request");
        File.WriteAllText(controlFile, "unowned");
        var delayedExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task completion = CompleteAfterDelayAsync(delayedExit);
        MethodInfo method = typeof(StructuredExternalProcessRunner).GetMethod(
            "TerminateAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Termination helper was not found.");
        var termination = (Task<ProcessCompletionMetadata>)(method.Invoke(
            null,
            [process, delayedExit.Task, ProcessCompletionKind.Cancelled, controlFile, TimeSpan.FromMilliseconds(1)])
            ?? throw new InvalidOperationException("Termination helper returned no task."));

        ProcessCompletionMetadata metadata = await termination;
        await completion;

        Assert.False(metadata.CancellationSignalCreated);
        Assert.False(metadata.GracefulTerminationAcknowledged);
        Assert.False(metadata.ForcedTermination);
        Assert.True(metadata.ControlFileCleanupSucceeded);
        Assert.Equal("unowned", File.ReadAllText(controlFile));
        File.Delete(controlFile);
    }

    private static ProcessExecutionPolicy Policy(
        int startup = 1_000,
        int idle = 2_000,
        int total = 4_000,
        int grace = 100) =>
        new(
            TimeSpan.FromMilliseconds(startup),
            TimeSpan.FromMilliseconds(idle),
            TimeSpan.FromMilliseconds(total),
            TimeSpan.FromMilliseconds(grace));

    private static TimeSpan Seconds(int value) => TimeSpan.FromSeconds(value);

    private static T InvokePrivate<T>(string name, params object[] arguments)
    {
        MethodInfo method = typeof(StructuredExternalProcessRunner).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Process helper '{name}' was not found.");
        return (T)(method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"Process helper '{name}' returned no value."));
    }

    private static async Task CompleteAfterDelayAsync(TaskCompletionSource completion)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        _ = completion.TrySetResult();
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void AssertForced(ExternalProcessReceipt receipt, ProcessCompletionKind kind)
    {
        Assert.Equal(kind, receipt.Completion.Kind);
        Assert.True(receipt.Completion.CancellationSignalCreated);
        Assert.False(receipt.Completion.GracefulTerminationAcknowledged);
        Assert.True(receipt.Completion.ForcedTermination);
        Assert.True(receipt.Completion.ControlFileCleanupSucceeded);
    }

    private void AssertPreservedLogs(ExternalProcessReceipt receipt)
    {
        Assert.NotNull(receipt.Logs);
        string output = workspace.ResolveReference(receipt.Logs.StandardOutputReference);
        string error = workspace.ResolveReference(receipt.Logs.StandardErrorReference);
        Assert.True(File.Exists(output));
        Assert.True(File.Exists(error));
        Assert.Equal(receipt.StandardOutput.Text, File.ReadAllText(output)[..receipt.StandardOutput.Text.Length]);
        Assert.Equal(receipt.StandardError.Text, File.ReadAllText(error)[..receipt.StandardError.Text.Length]);
    }

    private void AssertNoControlFiles() =>
        Assert.Empty(Directory.GetFiles(workspace.TemporaryDirectory, ".packagebuilder-cancel-*"));
}
