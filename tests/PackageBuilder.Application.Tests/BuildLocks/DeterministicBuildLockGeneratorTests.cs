using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Application.BuildLocks;
using PackageBuilder.Application.Tools;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.Tests.BuildLocks;

[Trait("Task", "PB-0308")]
public sealed class DeterministicBuildLockGeneratorTests
{
    [Fact]
    public void GeneratesIdenticalCanonicalLockAndDigestRegardlessOfInputOrder()
    {
        BuildLockGenerationRequest first = ValidRequest();
        BuildLockGenerationRequest second = first with
        {
            EngineApprovals = first.EngineApprovals.Reverse().ToArray(),
            Workers = first.Workers.Reverse().ToArray(),
            MarketplaceProfiles = first.MarketplaceProfiles.Reverse().ToArray(),
        };

        BuildLockGenerationResult a = DeterministicBuildLockGenerator.Generate(first);
        BuildLockGenerationResult b = DeterministicBuildLockGenerator.Generate(second);

        Assert.True(a.IsSuccess);
        Assert.Equal(a.Value!.CanonicalJson, b.Value!.CanonicalJson);
        Assert.Equal(a.Value.Sha256, b.Value.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(a.Value.CanonicalJson))), a.Value.Sha256);
        Assert.Equal(64, a.Value.Sha256.Length);
    }

    [Theory]
    [InlineData(ToolVersionApprovalState.Candidate, "BUILD_LOCK_ENGINE_APPROVAL_INVALID")]
    [InlineData(ToolVersionApprovalState.Rejected, "BUILD_LOCK_ENGINE_APPROVAL_INVALID")]
    public void RejectsUnapprovedEngineVersions(ToolVersionApprovalState state, string code)
    {
        BuildLockGenerationRequest request = ValidRequest();
        request = request with { EngineApprovals = [Approval(ToolKind.Blender, "5.0.0", state), .. request.EngineApprovals.Skip(1)] };
        Assert.Equal(code, DeterministicBuildLockGenerator.Generate(request).Error!.Code);
    }

    [Fact]
    public void RejectsMissingOrDuplicateEngineSet()
    {
        BuildLockGenerationRequest request = ValidRequest();
        Assert.Equal("BUILD_LOCK_ENGINE_SET_INCOMPLETE", DeterministicBuildLockGenerator.Generate(request with { EngineApprovals = request.EngineApprovals.Take(2).ToArray() }).Error!.Code);
        Assert.Equal("BUILD_LOCK_ENGINE_APPROVAL_INVALID", DeterministicBuildLockGenerator.Generate(request with { EngineApprovals = [.. request.EngineApprovals, request.EngineApprovals.First()] }).Error!.Code);
    }

    [Fact]
    public void RejectsPrereleaseSdkAndInvalidComponentContract()
    {
        BuildLockGenerationRequest request = ValidRequest();
        Assert.Equal("BUILD_LOCK_DOTNET_INVALID", DeterministicBuildLockGenerator.Generate(request with { DotNetSdk = Version(ToolKind.DotNet, "10.0.0-preview.1") }).Error!.Code);
        Assert.Equal("BUILD_LOCK_CONTRACT_INVALID", DeterministicBuildLockGenerator.Generate(request with { Workers = [] }).Error!.Code);
    }

    private static BuildLockGenerationRequest ValidRequest() => new(
        BuildJobId.Create("Job-0308").Value!,
        "1.2.3",
        Version(ToolKind.DotNet, "10.0.302"),
        [Approval(ToolKind.Blender, "5.0.0"), Approval(ToolKind.Unity, "6000.3.10f1"), Approval(ToolKind.Unreal, "5.8.0", ToolVersionApprovalState.LastKnownGood)],
        1,
        [new("unity-worker", "2.0.0"), new("blender-worker", "1.0.0")],
        [new("unity-asset-store", "default", "2.0.0"), new("fab", "default", "1.0.0")]);

    private static ToolVersionApproval Approval(ToolKind tool, string value, ToolVersionApprovalState state = ToolVersionApprovalState.ApprovedLatest) => new(Version(tool, value), state, []);
    private static ToolVersion Version(ToolKind tool, string value) => ToolVersion.Create(tool, value).Value!;
}
