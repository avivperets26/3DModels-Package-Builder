using System.Text.Json;
using PackageBuilder.Contracts.Tools;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Tools;

namespace PackageBuilder.Infrastructure.Tests.Tools;

/// <summary>Reuses the production locator; a separate explicit environment input enables live preflight.</summary>
public sealed class UnrealFoundationInstallationTests
{
    [Fact]
    public void ExternalMetadataPreflightUsesBoundedSharedParsing()
    {
        Assert.False(UnrealInstallationLocator.TryParseBuildVersion(new byte[65_537], out _));
        Assert.False(UnrealInstallationLocator.TryParseBuildVersion(
            "{\"MajorVersion\":5,\"MinorVersion\":8,\"PatchVersion\":2,\"PatchVersion\":3}"u8.ToArray(), out _));
        Assert.True(UnrealInstallationLocator.TryParseBuildVersion(
            "{\"MajorVersion\":5,\"MinorVersion\":8,\"PatchVersion\":2}"u8.ToArray(), out ToolVersion? version));
        Assert.Equal("5.8.2", version!.Value);
    }

    [Fact]
    public async Task FoundationCandidateUsesVerifiedContainedDiscovery()
    {
        using var fixture = new UnrealDiscoveryTestWorkspace();
        string synthetic = fixture.CreateEngine("project/tools/unreal/5.8.2", 5, 8, 2);
        var locator = new UnrealInstallationLocator();
        UnrealInstallationDiscoveryReport report = await locator.DiscoverAsync(fixture.Request(configuredRoots: [synthetic]), TestContext.Current.CancellationToken);
        Assert.Empty(report.Failures);
        Assert.Contains(report.Detections, d => d.Status == UnrealInstallationDiscoveryStatus.Verified);

        string? engine = Environment.GetEnvironmentVariable("PB_UNREAL_ENGINE_ROOT");
        if (engine is null)
        { return; }
        var request = new UnrealInstallationDiscoveryRequest(BuildJobId.Create("Job-Unreal-Foundation").Value!,
            fixture.RepositoryRoot, Path.Combine(fixture.RepositoryRoot, "tools"), [engine], [], [], [], []);
        report = await locator.DiscoverAsync(request, TestContext.Current.CancellationToken);
        Assert.Empty(report.Failures);
        UnrealInstallationDetection detected = Assert.Single(report.Detections, d => string.Equals(d.InstallationRoot, engine, StringComparison.OrdinalIgnoreCase));
        if (detected.Status == UnrealInstallationDiscoveryStatus.ExternalInformational)
        {
            // User-approved development harness exception; never select it through production discovery.
            Assert.Equal(@"C:\Program Files\Epic Games\UE_5.8", engine);
            Assert.Null(detected.Installation);
            Assert.True(File.Exists(detected.EditorExecutablePath));
            Assert.InRange(new FileInfo(detected.BuildVersionPath).Length, 1, 65_536);
            Assert.True(UnrealInstallationLocator.TryParseBuildVersion(
                await File.ReadAllBytesAsync(detected.BuildVersionPath, TestContext.Current.CancellationToken),
                out ToolVersion? version));
            Assert.Equal("5.8.2", version!.Value);
        }
        else
        {
            Assert.Equal(UnrealInstallationDiscoveryStatus.Verified, detected.Status);
            Assert.Equal("5.8.2", detected.Installation!.Version.Value);
        }
        string evidence = Path.Combine(fixture.RepositoryRoot, "artifacts", "PB-1101", "installation-discovery.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);
        await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(new
        {
            realInstallation = true,
            version = "5.8.2",
            executable = detected.EditorExecutablePath,
            status = detected.Status.ToString(),
            approval = "candidate-only"
        }), TestContext.Current.CancellationToken);
    }
}
