using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Application.Tools;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Application.BuildLocks;

public sealed record BuildLockGenerationRequest(
    BuildJobId JobId,
    string PackageBuilderVersion,
    ToolVersion DotNetSdk,
    IReadOnlyCollection<ToolVersionApproval> EngineApprovals,
    int ManifestSchemaVersion,
    IReadOnlyCollection<BuildLockWorker> Workers,
    IReadOnlyCollection<BuildLockMarketplaceProfile> MarketplaceProfiles);

public sealed record GeneratedBuildLock(BuildLock Lock, string CanonicalJson, string Sha256);

public sealed record BuildLockGenerationError(string Code, string Message);

public sealed record BuildLockGenerationResult(GeneratedBuildLock? Value, BuildLockGenerationError? Error)
{
    public bool IsSuccess => Error is null;
}

/// <summary>Snapshots approved exact build inputs into canonical JSON with a content digest.</summary>
public static class DeterministicBuildLockGenerator
{
    public static BuildLockGenerationResult Generate(BuildLockGenerationRequest? request)
    {
        if (request is null || request.JobId is null || request.DotNetSdk is null
            || request.EngineApprovals is null || request.Workers is null || request.MarketplaceProfiles is null)
        {
            return Failure("BUILD_LOCK_INPUT_INVALID", "All build-lock inputs are required.");
        }

        if (request.DotNetSdk.Tool != ToolKind.DotNet || request.DotNetSdk.Channel != ToolReleaseChannel.Stable)
        {
            return Failure("BUILD_LOCK_DOTNET_INVALID", "The build lock requires an exact stable .NET SDK version.");
        }

        var engines = new Dictionary<ToolKind, ToolVersion>();
        foreach (ToolVersionApproval? approval in request.EngineApprovals)
        {
            if (approval?.Version is null
                || approval.Version.Tool is ToolKind.DotNet
                || approval.Version.Channel != ToolReleaseChannel.Stable
                || approval.State is not (ToolVersionApprovalState.ApprovedLatest or ToolVersionApprovalState.LastKnownGood)
                || !engines.TryAdd(approval.Version.Tool, approval.Version))
            {
                return Failure("BUILD_LOCK_ENGINE_APPROVAL_INVALID", "Engine inputs must be unique approved stable Blender, Unity, and Unreal versions.");
            }
        }

        if (engines.Count != 3
            || !engines.TryGetValue(ToolKind.Blender, out ToolVersion? blender)
            || !engines.TryGetValue(ToolKind.Unity, out ToolVersion? unity)
            || !engines.TryGetValue(ToolKind.Unreal, out ToolVersion? unreal))
        {
            return Failure("BUILD_LOCK_ENGINE_SET_INCOMPLETE", "Exactly one approved Blender, Unity, and Unreal version is required.");
        }

        var buildLock = new BuildLock(
            request.JobId,
            request.PackageBuilderVersion,
            request.DotNetSdk,
            blender,
            unity,
            unreal,
            request.ManifestSchemaVersion,
            request.Workers.ToArray(),
            request.MarketplaceProfiles.ToArray());
        BuildLockJsonResult serialized = BuildLockJson.Serialize(buildLock);
        if (!serialized.IsSuccessful)
        {
            return Failure("BUILD_LOCK_CONTRACT_INVALID", "Build-lock component identities, versions, and schema version must satisfy the versioned contract.");
        }

        string canonicalJson = serialized.Json!;
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
        return new(new GeneratedBuildLock(buildLock, canonicalJson, digest), null);
    }

    private static BuildLockGenerationResult Failure(string code, string message) =>
        new(null, new BuildLockGenerationError(code, message));
}
