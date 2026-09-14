using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Validation;
using PackageBuilder.Marketplaces.Fab;

namespace PackageBuilder.App.Wpf.Tests;

internal static class FabValidatorFixtures
{
    internal static CancellationToken Token => TestContext.Current.CancellationToken;
    internal static FabValidationContext Context(string format = "unity") => new(
        BuildJobId.Create("Job-1004").Value!, "Product", new(ProductCase.Static, "3d-model", [format]), ["Item"]);

    internal static ArtifactContentIdentity Identity(ReadOnlySpan<byte> bytes) => ArtifactContentIdentity.Create(
        bytes.Length, Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(bytes))).Value).Value!;

    internal static BuildArtifactId Id(string value = "Artifact-1004") => BuildArtifactId.Create(value).Value!;

    internal static FabTargetEvidence Evidence(FabValidationContext context, BuildArtifactId id,
        ArtifactContentIdentity content, string stage) => new(context.JobId, id, content, context.ProductKey, stage, true, []);

    // Synthetic profiles exercise pass paths and boundary behavior without pretending that unresolved
    // official requirements have actually been clarified or an approved cached profile was changed.
    internal static FabRequirementsProfile Profile(Action<JsonObject>? mutate = null)
    {
        JsonObject root = JsonNode.Parse(FabRequirementsBaseline.ArtifactValidationProfile.CanonicalJson)!.AsObject();
        root["version"] = "2026-09-14.99";
        JsonArray rules = root["rules"]!.AsArray();
        foreach (JsonNode? rule in rules.Where(rule => rule!["status"]!.GetValue<string>() == "unresolved").ToArray())
        { _ = rules.Remove(rule); }
        rules.Add(new JsonObject
        {
            ["id"] = "other-format-bytes",
            ["section"] = "archives",
            ["origin"] = "package-builder-policy",
            ["source"] = "fab-formats",
            ["status"] = "verified",
            ["summary"] = "Synthetic test bound only.",
            ["limit"] = 6_000_000_000,
            ["unit"] = "bytes",
            ["comparison"] = "lte",
        });
        mutate?.Invoke(root);
        return FabRequirementsProfileJson.Load(root.ToJsonString()).Value!;
    }

    internal static void Limit(JsonObject root, string id, long limit) =>
        root["rules"]!.AsArray().Single(rule => rule!["id"]!.GetValue<string>() == id)!["limit"] = limit;

    internal static ImmutableArray<ValidationFinding> Failure() => [ValidationFinding.Create(
        FindingCode.Create("UNITY_TEST_ERROR").Value, FindingSeverity.Error, FindingExplanation.Create("Synthetic target failure.").Value,
        FindingSourceComponent.Create("unity-validator").Value, null, CorrectiveAction.Create("Repair the package.").Value, true).Value!];

    internal static void Has(FabArtifactValidation result, string code)
    {
        Assert.False(result.Passed);
        Assert.Contains(result.Findings, finding => finding.Code.Value == code && finding.BlocksRelease && finding.SuggestedAction is not null);
    }
}
