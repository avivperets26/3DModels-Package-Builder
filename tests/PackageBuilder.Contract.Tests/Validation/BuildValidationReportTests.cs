using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contract.Tests.Validation;

[Trait("Task", "PB-0910")]
public sealed class BuildValidationReportTests
{
    [Fact]
    public void CanonicalUtf8RoundTripContainsVersionsMetricsHashesAndRedactedFindings()
    {
        BuildValidationReport report = Valid() with { Findings = [Finding("token=secret-value C:\\Users\\PrivatePerson\\asset 東京 café")] };
        BuildValidationReportResult result = BuildValidationReportJson.Serialize(report);
        Assert.True(result.IsSuccessful, result.Error);
        Assert.Equal(result.Json, BuildValidationReportJson.Deserialize(result.Json).Json);
        string json = result.Json!;
        Assert.DoesNotContain("secret-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivatePerson", json, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
        Assert.Equal("東京 café", result.Value!.Findings[0].Explanation.Value.Split("asset ")[1]);
        Assert.Equal("failed", JsonNode.Parse(json)!["finalStatus"]!.GetValue<string>());
        Assert.DoesNotContain('\n', json);
        Assert.False(Encoding.UTF8.GetBytes(json).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("unknown")]
    [InlineData("hash")]
    [InlineData("status")]
    [InlineData("job")]
    [InlineData("reference")]
    [InlineData("resource")]
    [InlineData("nested")]
    [InlineData("version")]
    public void RejectsMalformedOrContradictoryReports(string mutation)
    {
        JsonNode node = JsonNode.Parse(BuildValidationReportJson.Serialize(Valid()).Json!)!;
        switch (mutation)
        {
            case "schemaVersion":
                node["schemaVersion"] = 2;
                break;
            case "unknown":
                node["unknown"] = true;
                break;
            case "hash":
                node["artifacts"]![0]!["sha256"] = "bad";
                break;
            case "status":
                node["finalStatus"] = "failed";
                break;
            case "job":
                node["versions"]!["jobId"] = "different-job";
                break;
            case "reference":
                node["metrics"]![0]!["artifactId"] = "missing";
                break;
            case "resource":
                node["resources"]!["bytesRead"] = -1;
                break;
            case "nested":
                node["artifacts"]![0]!["unknown"] = true;
                break;
            case "version":
                node["versions"]!["unity"] = "not-unity";
                break;
        }
        Assert.False(BuildValidationReportJson.Deserialize(node.ToJsonString()).IsSuccessful);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    [InlineData("{\"a\":{\"b\":0,\"b\":1}}")]
    public void RejectsInvalidJson(string? json) => Assert.False(BuildValidationReportJson.Deserialize(json).IsSuccessful);

    [Fact]
    public void NoncanonicalOrOverflowingIntegerTokensFailWithoutThrowing()
    {
        string json = BuildValidationReportJson.Serialize(Valid()).Json!;
        Assert.False(BuildValidationReportJson.Deserialize(json.Replace("\"sizeBytes\":42", "\"sizeBytes\":42.0", StringComparison.Ordinal)).IsSuccessful);
        Assert.False(BuildValidationReportJson.Deserialize(json.Replace("\"sizeBytes\":42", "\"sizeBytes\":9223372036854775808", StringComparison.Ordinal)).IsSuccessful);
    }

    [Fact]
    public void BoundsUnknownMetricsAndCollectionIdentityAreEnforced()
    {
        BuildValidationReport valid = Valid();
        Assert.False(BuildValidationReportJson.Serialize(valid with { Metrics = [new("bad", "ratio", double.NaN)] }).IsSuccessful);
        Assert.False(BuildValidationReportJson.Serialize(valid with { Artifacts = [valid.Artifacts[0], valid.Artifacts[0]] }).IsSuccessful);
        Assert.False(BuildValidationReportJson.Serialize(valid with { Findings = [Finding("Missing", "unknown")] }).IsSuccessful);
        Assert.False(BuildValidationReportJson.Deserialize(new string('x', 1_048_577)).IsSuccessful);
        Assert.False(BuildValidationReportJson.Serialize(valid with { Resources = valid.Resources with { Stages = [new("build", 2)] } }).IsSuccessful);
        Assert.False(BuildValidationReportJson.Serialize(valid with { Metrics = Enumerable.Repeat(valid.Metrics[0], 4097).ToArray() }).IsSuccessful);
    }

    [Fact]
    public void StatusReflectsActiveCancelledAndFailedJobsIndependentlyOfWarnings()
    {
        foreach ((BuildJobState state, string status) in new[] { (BuildJobState.Validating, "incomplete"), (BuildJobState.Cancelled, "cancelled"),
            (BuildJobState.Failed, "failed"), (BuildJobState.Completed, "passed") })
        {
            string json = BuildValidationReportJson.Serialize(Valid() with { JobState = state }).Json!;
            Assert.Equal(status, JsonNode.Parse(json)!["finalStatus"]!.GetValue<string>());
        }
    }

    [Fact]
    public void CultureAndInputCollectionOrderDoNotChangeCanonicalBytes()
    {
        BuildValidationReport report = Valid() with { Metrics = [new("z", "ratio", .5), new("a", "count", 4)] };
        string json = BuildValidationReportJson.Serialize(report).Json!;
        CultureInfo prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(json, BuildValidationReportJson.Serialize(report with { Metrics = report.Metrics.Reverse().ToArray() }).Json);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    private static ValidationFinding Finding(string text, string artifact = "hero") => ValidationFinding.Create(
        FindingCode.Create("MEDIA_TEST").Value, FindingSeverity.Error, FindingExplanation.Create(text).Value,
        FindingSourceComponent.Create("media-validator").Value, BuildArtifactId.Create(artifact).Value,
        CorrectiveAction.Create("Review the image.").Value, true).Value!;

    private static BuildValidationReport Valid()
    {
        BuildJobId job = BuildJobId.Create("job-0910").Value!;
        var versions = new BuildLock(job, "1.0.0", ToolVersion.Create(ToolKind.DotNet, "10.0.302").Value!,
            ToolVersion.Create(ToolKind.Blender, "5.0.0").Value!, ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
            ToolVersion.Create(ToolKind.Unreal, "5.8.0").Value!, 1, [new("unity-worker", "1.0.0")], [new("fab", "default", "1.0.0")]);
        BuildArtifactId id = BuildArtifactId.Create("hero").Value!;
        return new(job, versions, BuildJobState.Completed, [new(id, "preview", Sha256Digest.Create(new string('a', 64)).Value!, 42)],
            new(1, [new("render", .5)], null, 42, 0, 42, 42), [new("width", "count", 1920, id)], []);
    }
}
