using System.Reflection;
using PackageBuilder.Contracts.Processes;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Processes;

namespace PackageBuilder.Infrastructure.Tests.Processes;

public sealed class ProcessExecutionContractTests
{
    [Fact]
    public void RequestSnapshotsArgumentsAndEnvironment()
    {
        var arguments = new List<string> { "one" };
        var environment = new List<ProcessEnvironmentVariable> { new("NAME", "value") };
        BuildJobId jobId = BuildJobId.Create("Job-1").Value!;
        var request = new ExternalProcessRequest(
            jobId,
            "project",
            "executable",
            "working",
            "temporary",
            "cache",
            "logs",
            arguments,
            environment,
            10);

        arguments.Add("two");
        environment.Add(new ProcessEnvironmentVariable("OTHER", "value"));

        Assert.Equal(["one"], request.Arguments);
        _ = Assert.Single(request.EnvironmentVariables);
        Assert.Equal(10, request.MaximumCapturedCharactersPerStream);
    }

    [Fact]
    public void ContractConstructorsRejectNullRequiredValues()
    {
        BuildJobId jobId = BuildJobId.Create("Job-1").Value!;
        _ = Assert.Throws<ArgumentNullException>(() => new ProcessEnvironmentVariable(null!, "value"));
        _ = Assert.Throws<ArgumentNullException>(() => new ProcessEnvironmentVariable("name", null!));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            null!, "p", "e", "w", "t", "c", "l", [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, null!, "e", "w", "t", "c", "l", [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", null!, "w", "t", "c", "l", [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", "e", null!, "t", "c", "l", [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", "e", "w", null!, "c", "l", [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", "e", "w", "t", null!, "l", [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", "e", "w", "t", "c", null!, [], [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", "e", "w", "t", "c", "l", null!, [], 1));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessRequest(
            jobId, "p", "e", "w", "t", "c", "l", [], null!, 1));

        string digest = new('a', 64);
        _ = Assert.Throws<ArgumentNullException>(() => new ExecutableMetadata(null!, 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("../tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("folder\\tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("C:/tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("folder:/tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("folder\n/tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("folder//tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("folder/./tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("folder/../tool.exe", 0, digest, null, null));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutableMetadata("tool.exe", -1, digest, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("tool.exe", 0, null!, null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("tool.exe", 0, "short", null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("tool.exe", 0, new string('A', 64), null, null));
        _ = Assert.Throws<ArgumentException>(() => new ExecutableMetadata("tool.exe", 0, new string('g', 64), null, null));
        _ = Assert.Throws<ArgumentNullException>(() => new ProcessStreamCapture(null!, false));
        var metadata = new ExecutableMetadata("path", 0, digest, null, null);
        var capture = new ProcessStreamCapture(string.Empty, false);
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessReceipt(null!, metadata, 0, capture, capture));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessReceipt(jobId, null!, 0, capture, capture));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessReceipt(jobId, metadata, 0, null!, capture));
        _ = Assert.Throws<ArgumentNullException>(() => new ExternalProcessReceipt(jobId, metadata, 0, capture, null!));
    }

    [Fact]
    public async Task ResultFactoriesAndRunnerNullGuardAreStrict()
    {
        _ = Assert.Throws<ArgumentException>(() => new ProcessExecutionFailure("", "location", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new ProcessExecutionFailure("CODE", "", "diagnostic"));
        _ = Assert.Throws<ArgumentException>(() => new ProcessExecutionFailure("CODE", "location", ""));
        _ = Assert.Throws<ArgumentNullException>(() => ProcessExecutionResult.Success(null!));
        _ = Assert.Throws<ArgumentException>(() => ProcessExecutionResult.Failed());
        _ = Assert.Throws<ArgumentNullException>(() => ProcessExecutionResult.Failed(null!));
        _ = Assert.Throws<ArgumentException>(() => ProcessExecutionResult.Failed([null!]));

        ProcessExecutionOperationResult failed = ProcessExecutionResult.Failed(
            new ProcessExecutionFailure("CODE", "location", "diagnostic"));
        Assert.False(failed.IsSuccess);
        _ = Assert.Single(failed.Failures);
        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await new StructuredExternalProcessRunner().RunAsync(null!));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void ExpectedProcessFailureClassifierHasAnExplicitBoundary(int caseId, bool expected)
    {
        Exception exception = caseId switch
        {
            0 => new IOException(),
            1 => new UnauthorizedAccessException(),
            2 => new InvalidOperationException(),
            3 => new System.ComponentModel.Win32Exception(),
            4 => new OverflowException(),
            _ => new ArgumentException("Unexpected process argument."),
        };
        MethodInfo method = typeof(StructuredExternalProcessRunner).GetMethod(
            "IsExpectedProcessFailure",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected failure classifier was not found.");

        bool result = (bool)(method.Invoke(null, [exception])
            ?? throw new InvalidOperationException("Expected failure classifier returned no value."));

        Assert.Equal(expected, result);
    }
}
