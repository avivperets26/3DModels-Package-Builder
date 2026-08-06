using System.Text.Json;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableStaticValidationReportJsonTests
{
    [Fact]
    public async Task SerializesDeterministicPassedStaticReceipt()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync(includeGlb: false);
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        {
            PortableTargetValidationReport validation = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                receipt,
                archive,
                [scenario.TextureReceipt],
                scenario.Readme,
                scenario.References(),
                scenario.Reimports(),
                TestContext.Current.CancellationToken);
            BuildJobId jobId = JobId();

            PortableStaticValidationReportResult result = PortableStaticValidationReportJson.Serialize(
                jobId,
                Manifest(),
                BuildLock(jobId),
                receipt,
                validation,
                "logs/jobs/job-pb0507/job.log");

            Assert.True(result.IsSuccess);
            Assert.Equal(PortableStaticValidationReportError.None, result.Error);
            Assert.NotNull(result.Value);
            Assert.DoesNotContain('\r', result.Value.Json);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(result.Value.Json), result.Value.Utf8Bytes);
            using var document = JsonDocument.Parse(result.Value.Json);
            JsonElement root = document.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("Job-PB0507", root.GetProperty("jobId").GetString());
            Assert.Equal("SilverwingTalonbow", root.GetProperty("productId").GetString());
            Assert.Equal("static", root.GetProperty("productCase").GetString());
            Assert.Equal("portable", root.GetProperty("target").GetString());
            Assert.Equal("passed", root.GetProperty("status").GetString());
            Assert.Equal(receipt.ArchiveIdentity.Sha256.Value,
                root.GetProperty("releaseArtifact").GetProperty("sha256").GetString());
            Assert.Equal(0, root.GetProperty("findings").GetArrayLength());
            Assert.Equal(result.Value.Json, PortableStaticValidationReportJson.Serialize(
                jobId,
                Manifest(),
                BuildLock(jobId),
                receipt,
                validation,
                "logs/jobs/job-pb0507/job.log").Value!.Json);
        }
    }

    [Fact]
    public async Task SerializesBlockingFindingsInStableOrder()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync(includeGlb: false);
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        {
            PortableTargetValidationReport failed = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                receipt,
                archive,
                [],
                scenario.Readme,
                [],
                [],
                TestContext.Current.CancellationToken);
            BuildJobId jobId = JobId();

            PortableStaticValidationReportResult result = PortableStaticValidationReportJson.Serialize(
                jobId,
                Manifest(),
                BuildLock(jobId),
                receipt,
                failed,
                "logs/jobs/job-pb0507/job.log");

            Assert.True(result.IsSuccess);
            using var document = JsonDocument.Parse(result.Value!.Json);
            Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
            string[] codes =
            [
                .. document.RootElement.GetProperty("findings").EnumerateArray()
                    .Select(static finding => finding.GetProperty("code").GetString()!),
            ];
            Assert.NotEmpty(codes);
            Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        }
    }

    [Theory]
    [InlineData("null-job", PortableStaticValidationReportError.NullJobId)]
    [InlineData("null-manifest", PortableStaticValidationReportError.NullManifest)]
    [InlineData("non-static", PortableStaticValidationReportError.NonStaticManifest)]
    [InlineData("missing-portable", PortableStaticValidationReportError.PortableTargetMissing)]
    [InlineData("null-lock", PortableStaticValidationReportError.NullBuildLock)]
    [InlineData("mismatched-lock", PortableStaticValidationReportError.BuildLockJobMismatch)]
    [InlineData("invalid-lock", PortableStaticValidationReportError.InvalidBuildLock)]
    [InlineData("null-receipt", PortableStaticValidationReportError.NullArchiveReceipt)]
    [InlineData("null-report", PortableStaticValidationReportError.NullValidationReport)]
    [InlineData("invalid-log", PortableStaticValidationReportError.InvalidLogReference)]
    public async Task RejectsContradictoryOrIncompleteInputs(
        string scenarioName,
        PortableStaticValidationReportError expected)
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync(includeGlb: false);
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        {
            PortableTargetValidationReport validation = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                receipt,
                archive,
                [scenario.TextureReceipt],
                scenario.Readme,
                scenario.References(),
                scenario.Reimports(),
                TestContext.Current.CancellationToken);
            BuildJobId jobId = JobId();
            ProductManifest manifest = scenarioName switch
            {
                "non-static" => PortableReadmeTestValues.Manifest(ProductCase.Rigged),
                "missing-portable" => ManifestWithoutPortable(),
                _ => Manifest(),
            };
            BuildLock buildLock = scenarioName switch
            {
                "mismatched-lock" => BuildLock(BuildJobId.Create("Job-Other").Value!),
                "invalid-lock" => BuildLock(jobId) with { PackageBuilderVersion = "bad version" },
                _ => BuildLock(jobId),
            };

            PortableStaticValidationReportResult result = PortableStaticValidationReportJson.Serialize(
                scenarioName == "null-job" ? null : jobId,
                scenarioName == "null-manifest" ? null : manifest,
                scenarioName == "null-lock" ? null : buildLock,
                scenarioName == "null-receipt" ? null : receipt,
                scenarioName == "null-report" ? null : validation,
                scenarioName == "invalid-log" ? "../private.log" : "logs/jobs/job-pb0507/job.log");

            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
            Assert.Equal(expected, result.Error);
        }
    }

    private static ProductManifest Manifest() =>
        PortableReadmeTestValues.Manifest(ProductCase.Static, withTexture: true);

    private static ProductManifest ManifestWithoutPortable()
    {
        ProductManifest source = Manifest();
        return ProductManifest.Create(
            source.SchemaVersion,
            source.PublisherProfileReference,
            source.DisplayName,
            source.AssetId,
            source.FolderName,
            source.ProductCase,
            source.Version,
            [PackageBuilder.Domain.Targets.BuildTarget.Unity],
            source.SourceAssets,
            source.Materials,
            source.Rig,
            source.Animations,
            source.ItemSet,
            source.ItemCollection,
            source.MarketplaceProfileReference).Value!;
    }

    private static BuildJobId JobId() => BuildJobId.Create("Job-PB0507").Value!;

    private static BuildLock BuildLock(BuildJobId jobId) =>
        new(
            jobId,
            "0.1.0",
            ToolVersion.Create(ToolKind.DotNet, "10.0.302").Value!,
            ToolVersion.Create(ToolKind.Blender, "5.0.0").Value!,
            ToolVersion.Create(ToolKind.Unity, "6000.3.10f1").Value!,
            ToolVersion.Create(ToolKind.Unreal, "5.7.2").Value!,
            1,
            [new BuildLockWorker("portable-worker", "1.0.0")],
            [new BuildLockMarketplaceProfile("fab", "default", "1.0.0")]);
}
