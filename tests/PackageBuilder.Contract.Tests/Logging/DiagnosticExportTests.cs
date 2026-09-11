using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Contract.Tests.Logging;

[Trait("Task", "PB-0912")]
public sealed class DiagnosticExportTests
{
    private const string Log = """{"timestampUtc":"2026-09-11T00:00:00Z","jobId":"job-support","correlationId":"correlation","component":"worker","severity":"warning","message":"Check material count","properties":{}}""";

    [Theory]
    [InlineData("password=\"secret with spaces\"", "spaces")]
    [InlineData("{\"password\":\"private-secret with spaces\"}", "private-secret")]
    [InlineData("{'api_key':'private-secret'}", "private-secret")]
    [InlineData("password=[REDACTED] quoted-tail\"", "quoted-tail")]
    [InlineData("Bearer hidden-token", "hidden-token")]
    [InlineData("C:\\Users\\Private Person\\work\\asset", "Private")]
    [InlineData("/home" + "/private/person/work", "private")]
    [InlineData("Source models/private-item.step", "private-item")]
    [InlineData("Source ..\\private-item\\data.bin", "private-item")]
    [InlineData("~/private-person/data", "private-person")]
    [InlineData("\\\\server\\private\\asset", "private")]
    [InlineData("%USERPROFILE%\\PrivateFolder", "PrivateFolder")]
    [InlineData("https://private.example/x?signature=hidden", "hidden")]
    [InlineData("person@private.example", "person")]
    [InlineData("ghp_privatefixturetoken", "privatefixturetoken")]
    [InlineData("models/private-item.fbx", "private-item")]
    [InlineData("-----BEGIN " + "PRIVATE KEY-----hidden-----END " + "PRIVATE KEY-----", "hidden")]
    public void ExportRemovesSensitiveProseIncludingPreviouslyRedactedTails(string value, string absent) => Assert.DoesNotContain(absent, SensitiveDiagnosticValueRedactor.RedactForExport("message", value), StringComparison.Ordinal);

    [Theory]
    [InlineData("sourcePath")]
    [InlineData("api_token")]
    [InlineData("supportEmail")]
    [InlineData("username")]
    public void SensitivePropertyNamesOmitWholeValue(string key) =>
        Assert.Equal("[REDACTED]", SensitiveDiagnosticValueRedactor.RedactForExport(key, "private-value"));

    [Theory]
    [InlineData("jobId", "another-job")]
    [InlineData("severity", "42")]
    [InlineData("severity", "Warning")]
    [InlineData("timestampUtc", "2026-09-11T01:00:00+01:00")]
    [InlineData("component", "bad component")]
    [InlineData("message", "")]
    [InlineData("extra", "unexpected")]
    public void InvalidOrCrossJobRecordRejectsEntireExport(string property, string value)
    {
        JsonNode node = JsonNode.Parse(Log)!;
        node[property] = value;
        Assert.False(Export(Log + "\n" + node.ToJsonString()).IsSuccess);
    }

    [Theory]
    [InlineData("{\"message\":0,\"message\":1}")]
    [InlineData("{\"properties\":{\"token\":\"a\",\"token\":\"b\"}}")]
    [InlineData("[]")]
    [InlineData("{\"message\":")]
    [InlineData("\n")]
    [InlineData(null)]
    public void MalformedLogsFailWithoutPartialOutput(string? text)
    {
        StructuredLogResult<string> result = Export(text);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void SafeUnicodeLogsCanonicalizeLineEndingsAndPreserveUsefulDiagnostics()
    {
        JsonNode node = JsonNode.Parse(Log)!;
        node["message"] = "Review café 東京";
        node["properties"]!["sourcePath"] = "private relative location";
        string result = Export(node.ToJsonString() + "\r\n").Value!;
        Assert.EndsWith("\n", result, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', result);
        Assert.Equal("Review café 東京", JsonNode.Parse(result)!["message"]!.GetValue<string>());
        Assert.Equal("[REDACTED]", JsonNode.Parse(result)!["properties"]!["sourcePath"]!.GetValue<string>());
        Assert.Equal("", Export("").Value);
    }

    [Fact]
    public void LogLimitsAndCancellationApplyBeforeReturningAnyOutput()
    {
        Assert.False(Export(new string('x', 4_000_001)).IsSuccess);
        Assert.False(Export(string.Join('\n', Enumerable.Repeat(Log, 2049))).IsSuccess);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = Assert.Throws<OperationCanceledException>(() => StructuredJobLogExport.Create("", BuildJobId.Create("job-support").Value!, cancelled.Token));
    }

    private static StructuredLogResult<string> Export(string? text) => StructuredJobLogExport.Create(text,
        BuildJobId.Create("job-support").Value!, TestContext.Current.CancellationToken);
}
