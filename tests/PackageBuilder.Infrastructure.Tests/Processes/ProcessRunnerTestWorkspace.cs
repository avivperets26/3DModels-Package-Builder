using System.Diagnostics;
using PackageBuilder.Contracts.Processes;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Infrastructure.Processes;

namespace PackageBuilder.Infrastructure.Tests.Processes;

public sealed class ProcessRunnerTestWorkspace : IDisposable
{
    public ProcessRunnerTestWorkspace()
    {
        ProjectRoot = FindProjectRoot();
        Root = Path.Combine(ProjectRoot, "artifacts", "PB-0208", "tests", Guid.NewGuid().ToString("N"));
        WorkingDirectory = CreateDirectory("working");
        TemporaryDirectory = CreateDirectory("temporary");
        CacheDirectory = CreateDirectory("cache");
        LogDirectory = CreateDirectory("logs");
        ProbeProjectDirectory = CreateDirectory("probe");
        DotnetRoot = LocateDotnetRoot(ProjectRoot);
        BuildProbe();
    }

    public string ProjectRoot { get; }

    public string Root { get; }

    public string WorkingDirectory { get; }

    public string TemporaryDirectory { get; }

    public string CacheDirectory { get; }

    public string LogDirectory { get; }

    public string ProbeProjectDirectory { get; }

    public string DotnetRoot { get; }

    public string ProbeExecutablePath =>
        Path.Combine(ProbeProjectDirectory, "bin", "Release", "net10.0", "PackageBuilder.ProcessProbe.exe");

    public ExternalProcessRequest Request(
        string? projectRoot = null,
        string? executablePath = null,
        string? workingDirectory = null,
        string? temporaryDirectory = null,
        string? cacheDirectory = null,
        string? logDirectory = null,
        IEnumerable<string>? arguments = null,
        IEnumerable<ProcessEnvironmentVariable>? environment = null,
        int captureLimit = 65_536,
        ProcessExecutionPolicy? executionPolicy = null) =>
        new(
            BuildJobId.Create("Job-PB0207").Value!,
            projectRoot ?? ProjectRoot,
            executablePath ?? ProbeExecutablePath,
            workingDirectory ?? WorkingDirectory,
            temporaryDirectory ?? TemporaryDirectory,
            cacheDirectory ?? CacheDirectory,
            logDirectory ?? LogDirectory,
            arguments ?? [],
            environment ?? [new ProcessEnvironmentVariable("DOTNET_ROOT", DotnetRoot)],
            captureLimit,
            executionPolicy);

    public string ResolveReference(string reference) =>
        Path.Combine(ProjectRoot, reference.Replace('/', Path.DirectorySeparatorChar));

    public static Task<ProcessExecutionOperationResult> RunAsync(ExternalProcessRequest request) =>
        new StructuredExternalProcessRunner().RunAsync(request, TestContext.Current.CancellationToken);

    public static Task<ProcessExecutionOperationResult> RunWithCancellationAsync(
        ExternalProcessRequest request,
        CancellationTokenSource cancellation) =>
        new StructuredExternalProcessRunner().RunAsync(request, cancellation.Token);

    public string CreateDirectory(string relativePath)
    {
        string path = Path.Combine(Root, relativePath);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string relativePath, string contents)
    {
        string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>Builds a project-contained probe so integration tests never select a system executable.</summary>
    private void BuildProbe()
    {
        string source = Path.Combine(ProjectRoot, "tests", "fixtures", "processes", "ProcessProbe.cs");
        File.Copy(source, Path.Combine(ProbeProjectDirectory, "Program.cs"));
        File.WriteAllText(
            Path.Combine(ProbeProjectDirectory, "ProcessProbe.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>PackageBuilder.ProcessProbe</AssemblyName>
                <RootNamespace>PackageBuilder.ProcessProbe</RootNamespace>
              </PropertyGroup>
            </Project>
            """);

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(DotnetRoot, "dotnet.exe"),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = ProbeProjectDirectory,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add("ProcessProbe.csproj");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        startInfo.Environment["DOTNET_CLI_HOME"] = CacheDirectory;
        startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(CacheDirectory, "packages");
        startInfo.Environment["NUGET_HTTP_CACHE_PATH"] = Path.Combine(CacheDirectory, "http");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The process probe build did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(ProbeExecutablePath))
        {
            throw new InvalidOperationException($"The process probe build failed. {output} {error}");
        }
    }

    private static string LocateDotnetRoot(string projectRoot)
    {
        string repositoryRoot = Path.Combine(projectRoot, "tools", "dotnet", "10.0.302");
        if (File.Exists(Path.Combine(repositoryRoot, "dotnet.exe")))
        {
            return repositoryRoot;
        }

        string? environmentRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        return !string.IsNullOrWhiteSpace(environmentRoot)
            && File.Exists(Path.Combine(environmentRoot, "dotnet.exe"))
            ? Path.GetFullPath(environmentRoot)
            : throw new InvalidOperationException("An exact .NET 10 SDK root is required to build the process probe.");
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
