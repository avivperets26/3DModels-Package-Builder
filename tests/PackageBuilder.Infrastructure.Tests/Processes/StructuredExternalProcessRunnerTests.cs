using System.Security.Cryptography;
using System.Text.Json;
using PackageBuilder.Contracts.Processes;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Processes;

namespace PackageBuilder.Infrastructure.Tests.Processes;

public sealed class StructuredExternalProcessRunnerTests(ProcessRunnerTestWorkspace workspace)
    : IClassFixture<ProcessRunnerTestWorkspace>
{
    [Fact]
    public async Task RunsContainedExecutableWithLiteralArgumentsExplicitEnvironmentAndSeparateStreams()
    {
        string[] arguments =
        [
            "plain",
            "contains spaces",
            "\"quoted\"",
            "semi;colon",
            "amp&ersand",
            "$(whoami)",
            "%PATH%",
            "ãƒ¦ãƒ‹ã‚³ãƒ¼ãƒ‰",
            string.Empty,
            "--exit-code=7",
        ];
        ProcessEnvironmentVariable[] environment =
        [
            new("DOTNET_ROOT", workspace.DotnetRoot),
            new("PROBE_VALUE", "literal value & ; $(ignored)"),
        ];

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(arguments: arguments, environment: environment));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Failures);
        ExternalProcessReceipt receipt = result.Value!;
        Assert.Equal("Job-PB0207", receipt.JobId.Value);
        Assert.Equal(7, receipt.ExitCode);
        Assert.Equal("probe-stderr", receipt.StandardError.Text);
        Assert.False(receipt.StandardOutput.WasTruncated);
        Assert.False(receipt.StandardError.WasTruncated);

        using var observation = JsonDocument.Parse(receipt.StandardOutput.Text);
        Assert.Equal(arguments, observation.RootElement.GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(workspace.WorkingDirectory, observation.RootElement.GetProperty("workingDirectory").GetString());
        Assert.Equal("literal value & ; $(ignored)", observation.RootElement.GetProperty("probeValue").GetString());
        Assert.Equal(JsonValueKind.Null, observation.RootElement.GetProperty("path").ValueKind);
        AssertManagedEnvironment(observation.RootElement);

        byte[] executableBytes = await File.ReadAllBytesAsync(
            workspace.ProbeExecutablePath,
            TestContext.Current.CancellationToken);
        Assert.Equal(executableBytes.LongLength, receipt.Executable.ByteCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(executableBytes)).ToLowerInvariant(), receipt.Executable.Sha256);
        Assert.Equal(
            Path.GetRelativePath(workspace.ProjectRoot, workspace.ProbeExecutablePath).Replace('\\', '/'),
            receipt.Executable.ProjectRelativePath);
    }

    [Fact]
    public async Task BoundedCaptureDrainsBothStreamsAndReportsTruncation()
    {
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(
                arguments: ["--stdout-count=5000", "--stderr-count=6000"],
                captureLimit: 101));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.ExitCode);
        Assert.Equal(new string('O', 101), result.Value.StandardOutput.Text);
        Assert.Equal(new string('E', 101), result.Value.StandardError.Text);
        Assert.True(result.Value.StandardOutput.WasTruncated);
        Assert.True(result.Value.StandardError.WasTruncated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16_777_217)]
    public async Task RejectsInvalidCaptureLimits(int limit)
    {
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(captureLimit: limit));

        AssertFailure(result, "PROCESS_CAPTURE_LIMIT_INVALID", "maximumCapturedCharactersPerStream");
    }

    [Fact]
    public async Task RejectsNullAndNullContainingArgumentsWithoutInterpretingOtherText()
    {
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(arguments: [null!, "bad\0value", "safe & literal"]));

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Failures.Count(failure => failure.Code == "PROCESS_ARGUMENT_INVALID"));
    }

    [Theory]
    [InlineData("null-entry", "PROCESS_ENVIRONMENT_INVALID")]
    [InlineData("empty-name", "PROCESS_ENVIRONMENT_NAME_INVALID")]
    [InlineData("edge-space", "PROCESS_ENVIRONMENT_NAME_INVALID")]
    [InlineData("equals", "PROCESS_ENVIRONMENT_NAME_INVALID")]
    [InlineData("control", "PROCESS_ENVIRONMENT_NAME_INVALID")]
    [InlineData("duplicate", "PROCESS_ENVIRONMENT_DUPLICATE")]
    [InlineData("reserved", "PROCESS_ENVIRONMENT_RESERVED")]
    [InlineData("null-value", "PROCESS_ENVIRONMENT_VALUE_INVALID")]
    public async Task RejectsInvalidEnvironment(string caseId, string code)
    {
        ProcessEnvironmentVariable[] environment = caseId switch
        {
            "null-entry" => [null!],
            "empty-name" => [new ProcessEnvironmentVariable("", "value")],
            "edge-space" => [new ProcessEnvironmentVariable(" NAME", "value")],
            "equals" => [new ProcessEnvironmentVariable("BAD=NAME", "value")],
            "control" => [new ProcessEnvironmentVariable("BAD\nNAME", "value")],
            "duplicate" => [new ProcessEnvironmentVariable("Name", "one"), new ProcessEnvironmentVariable("NAME", "two")],
            "reserved" => [new ProcessEnvironmentVariable("TEMP", "value")],
            _ => [new ProcessEnvironmentVariable("SAFE", "bad\0value")],
        };
        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(environment: environment));

        Assert.Contains(result.Failures, failure => failure.Code == code);
    }

    [Fact]
    public async Task RejectsMissingAndOutsidePaths()
    {
        string missingExecutable = Path.Combine(workspace.Root, "missing.exe");
        string outsideDirectory = Path.GetPathRoot(workspace.ProjectRoot)!;

        ProcessExecutionOperationResult missing = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(executablePath: missingExecutable));
        ProcessExecutionOperationResult outside = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(workingDirectory: outsideDirectory));
        ProcessExecutionOperationResult rootAlias = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(cacheDirectory: workspace.ProjectRoot));

        AssertFailure(missing, "PROCESS_EXECUTABLE_MISSING", "executable");
        AssertFailure(outside, "PROCESS_PATH_OUTSIDE_PROJECT", "workingDirectory");
        AssertFailure(rootAlias, "PROCESS_PATH_OUTSIDE_PROJECT", "cacheDirectory");
    }

    [Theory]
    [InlineData("executable")]
    [InlineData("workingDirectory")]
    [InlineData("temporaryDirectory")]
    [InlineData("cacheDirectory")]
    [InlineData("logDirectory")]
    public async Task EveryLaunchPathMustBeContained(string location)
    {
        string outsideDirectory = Environment.SystemDirectory;
        string outsideExecutable = Path.Combine(outsideDirectory, "where.exe");
        ExternalProcessRequest request = location switch
        {
            "executable" => workspace.Request(executablePath: outsideExecutable),
            "workingDirectory" => workspace.Request(workingDirectory: outsideDirectory),
            "temporaryDirectory" => workspace.Request(temporaryDirectory: outsideDirectory),
            "cacheDirectory" => workspace.Request(cacheDirectory: outsideDirectory),
            "logDirectory" => workspace.Request(logDirectory: outsideDirectory),
            _ => throw new InvalidOperationException("Unknown test location."),
        };

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(request);

        AssertFailure(result, "PROCESS_PATH_OUTSIDE_PROJECT", location);
    }

    [Fact]
    public async Task RejectsInvalidNonCanonicalAndMissingDirectoryPaths()
    {
        string nonCanonical = Path.Combine(workspace.Root, "working", "..", "working");
        string missingDirectory = Path.Combine(workspace.Root, "missing-directory");

        ProcessExecutionOperationResult relative = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(projectRoot: "relative"));
        ProcessExecutionOperationResult canonical = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(temporaryDirectory: nonCanonical));
        ProcessExecutionOperationResult missing = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(logDirectory: missingDirectory));

        AssertFailure(relative, "PROCESS_PATH_INVALID", "projectRoot");
        AssertFailure(canonical, "PROCESS_PATH_NOT_CANONICAL", "temporaryDirectory");
        AssertFailure(missing, "PROCESS_DIRECTORY_MISSING", "logDirectory");
    }

    [Fact]
    public async Task RejectsNonCanonicalAndSyntacticallyInvalidExecutablePaths()
    {
        string nonCanonical = workspace.ProbeExecutablePath.Replace('\\', '/');
        string invalid = string.Concat(workspace.Root, Path.DirectorySeparatorChar, "bad", '\0', ".exe");

        ProcessExecutionOperationResult canonical = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(executablePath: nonCanonical));
        ProcessExecutionOperationResult malformed = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(executablePath: invalid));

        AssertFailure(canonical, "PROCESS_PATH_NOT_CANONICAL", "executable");
        AssertFailure(malformed, "PROCESS_PATH_INVALID", "executable");
    }

    [Fact]
    public async Task RejectsDirectoryAndExecutableReparseBoundaries()
    {
        string physicalDirectory = workspace.CreateDirectory("physical-temporary");
        string linkedDirectory = Path.Combine(workspace.Root, "linked-temporary");
        _ = Directory.CreateSymbolicLink(linkedDirectory, physicalDirectory);
        string linkedExecutable = Path.Combine(workspace.Root, "linked-probe.exe");
        _ = File.CreateSymbolicLink(linkedExecutable, workspace.ProbeExecutablePath);

        ProcessExecutionOperationResult directoryResult = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(temporaryDirectory: linkedDirectory));
        ProcessExecutionOperationResult executableResult = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(executablePath: linkedExecutable));

        AssertFailure(directoryResult, "PROCESS_REPARSE_BOUNDARY", "temporaryDirectory");
        AssertFailure(executableResult, "PROCESS_REPARSE_BOUNDARY", "executable");
    }

    [Fact]
    public async Task RejectsReparsePointProjectRoot()
    {
        string physicalRoot = workspace.CreateDirectory("physical-project");
        string physicalExecutable = Path.Combine(physicalRoot, "probe.exe");
        File.Copy(workspace.ProbeExecutablePath, physicalExecutable);
        foreach (string child in new[] { "working", "temporary", "cache", "logs" })
        {
            _ = Directory.CreateDirectory(Path.Combine(physicalRoot, child));
        }

        string linkedRoot = Path.Combine(workspace.Root, "linked-project");
        _ = Directory.CreateSymbolicLink(linkedRoot, physicalRoot);
        var request = new ExternalProcessRequest(
            BuildJobId.Create("Job-PB0207").Value!,
            linkedRoot,
            Path.Combine(linkedRoot, "probe.exe"),
            Path.Combine(linkedRoot, "working"),
            Path.Combine(linkedRoot, "temporary"),
            Path.Combine(linkedRoot, "cache"),
            Path.Combine(linkedRoot, "logs"),
            [],
            [new ProcessEnvironmentVariable("DOTNET_ROOT", workspace.DotnetRoot)],
            100);

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(request);

        AssertFailure(result, "PROCESS_REPARSE_BOUNDARY", "projectRoot");
    }

    [Fact]
    public async Task InvalidExecutableReturnsSanitizedStructuredFailure()
    {
        string invalidExecutable = workspace.CreateFile("invalid/not-an-executable.exe", "not executable");

        ProcessExecutionOperationResult result = await ProcessRunnerTestWorkspace.RunAsync(
            workspace.Request(executablePath: invalidExecutable));

        AssertFailure(result, "PROCESS_EXECUTION_FAILED", "$");
        Assert.DoesNotContain(invalidExecutable, result.Failures[0].Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private void AssertManagedEnvironment(JsonElement observation)
    {
        foreach (string property in new[]
                 {
                     "temporary", "temporaryAlternative", "temporaryPortable", "home", "userProfile",
                     "applicationData", "localApplicationData",
                 })
        {
            Assert.Equal(workspace.TemporaryDirectory, observation.GetProperty(property).GetString());
        }

        foreach (string property in new[] { "cache", "xdgCache", "dotnetHome", "nugetPackages" })
        {
            Assert.Equal(workspace.CacheDirectory, observation.GetProperty(property).GetString());
        }

        Assert.Equal(workspace.LogDirectory, observation.GetProperty("logs").GetString());
    }

    private static void AssertFailure(ProcessExecutionOperationResult result, string code, string location)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Contains(result.Failures, failure => failure.Code == code && failure.Location == location);
    }
}
