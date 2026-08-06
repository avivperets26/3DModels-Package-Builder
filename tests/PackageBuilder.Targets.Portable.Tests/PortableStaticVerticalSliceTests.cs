using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PackageBuilder.Application.BuildLocks;
using PackageBuilder.Application.Orchestration;
using PackageBuilder.Application.Tools;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Contracts.Persistence;
using PackageBuilder.Contracts.Profiles;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Tools;
using PackageBuilder.Infrastructure.Artifacts;
using PackageBuilder.Infrastructure.Logging;
using PackageBuilder.Infrastructure.Persistence;

namespace PackageBuilder.Targets.Portable.Tests;

[Collection("PB-0507 portable static vertical slice")]
public sealed class PortableStaticVerticalSliceTests
{
    [Fact]
    public async Task CompleteStaticManifestBuildsValidatedAtomicallyPromotedRelease()
    {
        using var workspace = new PortableStaticTestWorkspace();
        using var logWriter = new ContainedJsonLinesLogWriter(workspace.Root);
        SqliteBuildMetadataRepository repository = workspace.CreateRepository();
        var worker = new PortableStaticStageWorker(workspace, forceReimportFailure: false);
        var promoter = new PortableStaticReleasePromoter(workspace, worker);
        var orchestrator = new PersistedBuildJobOrchestrator(
            repository,
            worker,
            promoter,
            logWriter,
            new IncrementingUtcClock());

        Assert.False(Directory.Exists(workspace.BuildsRoot));
        BuildJobOrchestrationResult result = await orchestrator.StartAsync(
            new StartBuildJobRequest(workspace.JobId, workspace.CorrelationId),
            TestContext.Current.CancellationToken);

        RepositoryOperationResult<PersistedBuildJob?> diagnosticJob = await repository.GetAsync(
            workspace.JobId,
            TestContext.Current.CancellationToken);
        Assert.True(
            result.IsSuccess,
            $"{result.Error?.Code}: {result.Error?.Message}; persisted={diagnosticJob.Value?.State.CanonicalIdentifier ?? "missing"}");
        Assert.Equal(BuildJobState.Completed, result.Value!.State);
        Assert.True(result.Value.ReleasePromoted);
        Assert.Equal(BuildJobState.All.Where(static state => state.IsExecutionStage), result.Value.ExecutedStages);
        ArtifactPromotionReceipt promotion = Assert.IsType<ArtifactPromotionReceipt>(promoter.Receipt);
        Assert.True(File.Exists(promotion.ReleasePath));
        Assert.Equal("Stone_Arch/Stone_Arch_FBX.zip", promotion.ReleaseRelativePath);
        Assert.Equal(worker.ArchiveReceipt!.ArchiveIdentity, promotion.ArtifactRecord.ContentIdentity);
        Assert.Empty(Directory.EnumerateFiles(workspace.ArtifactsRoot, "*.partial", SearchOption.AllDirectories));

        using (ZipArchive archive = ZipFile.OpenRead(promotion.ReleasePath))
        {
            Assert.Equal(
                [
                    "Stone_Arch_fbx/README_StoneArch.txt",
                    "Stone_Arch_fbx/StoneArch.fbx",
                ],
                archive.Entries.Select(static entry => entry.FullName));
        }

        Assert.True(File.Exists(worker.ReportPath));
        using (var report = JsonDocument.Parse(await File.ReadAllTextAsync(
                   worker.ReportPath,
                   TestContext.Current.CancellationToken)))
        {
            Assert.Equal("passed", report.RootElement.GetProperty("status").GetString());
            Assert.Equal("StoneArch", report.RootElement.GetProperty("productId").GetString());
            Assert.Equal(0, report.RootElement.GetProperty("findings").GetArrayLength());
            Assert.Equal(
                worker.ArchiveReceipt.ArchiveIdentity.Sha256.Value,
                report.RootElement.GetProperty("releaseArtifact").GetProperty("sha256").GetString());
        }

        Assert.True(File.Exists(workspace.JobLogPath));
        string[] logLines = await File.ReadAllLinesAsync(
            workspace.JobLogPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(result.Value.ExecutedStages.Count, logLines.Length);
        Assert.All(logLines, static line => Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(line).RootElement.ValueKind));

        Assert.True(diagnosticJob.IsSuccess);
        Assert.Equal(BuildJobState.Completed, diagnosticJob.Value!.State);
        workspace.PublishManualPointerIfRequested(promotion.ReleasePath, worker.ReportPath);
    }

    [Fact]
    public async Task BlockingCleanReimportEvidenceWritesFailedReportAndNeverPromotes()
    {
        using var workspace = new PortableStaticTestWorkspace(retainOverride: false);
        using var logWriter = new ContainedJsonLinesLogWriter(workspace.Root);
        SqliteBuildMetadataRepository repository = workspace.CreateRepository();
        var worker = new PortableStaticStageWorker(workspace, forceReimportFailure: true);
        var promoter = new PortableStaticReleasePromoter(workspace, worker);
        var orchestrator = new PersistedBuildJobOrchestrator(
            repository,
            worker,
            promoter,
            logWriter,
            new IncrementingUtcClock());

        BuildJobOrchestrationResult result = await orchestrator.StartAsync(
            new StartBuildJobRequest(workspace.JobId, workspace.CorrelationId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(BuildJobState.Failed, result.Value!.State);
        Assert.False(result.Value.ReleasePromoted);
        Assert.Equal(0, promoter.CallCount);
        Assert.False(Directory.Exists(workspace.BuildsRoot));
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(
            worker.ReportPath,
            TestContext.Current.CancellationToken));
        Assert.Equal("failed", report.RootElement.GetProperty("status").GetString());
        Assert.Contains(
            report.RootElement.GetProperty("findings").EnumerateArray(),
            static finding => finding.GetProperty("code").GetString() == "PORTABLE_REIMPORT_FAILED");
    }
}

[CollectionDefinition("PB-0507 portable static vertical slice", DisableParallelization = true)]
public sealed class PortableStaticVerticalSliceFixture;

/// <summary>Composes existing production boundaries for the PB-0507 target-level E2E test.</summary>
internal sealed class PortableStaticStageWorker(
    PortableStaticTestWorkspace workspace,
    bool forceReimportFailure) : IBuildJobStageWorker
{
    private readonly ArtifactStore _artifactStore = new();
    private ProductManifest? _manifest;
    private PublisherProfile? _publisher;
    private GeneratedBuildLock? _buildLock;
    private PortableNamingProfile? _naming;
    private ArtifactStoreRecord? _fbxRecord;
    private ArtifactStoreRecord? _flatReadmeRecord;
    private ArtifactStoreRecord? _productReadmeRecord;
    private PortableFolderLayout? _layout;
    private PortableReadmeDocument? _readme;

    public PortableFbxArchiveReceipt? ArchiveReceipt { get; private set; }

    public ArtifactStoreRecord? ArchiveRecord { get; private set; }

    public string ReportPath => workspace.ReportPath;

    public async Task<BuildStageExecutionResult> ExecuteAsync(
        BuildJobId jobId,
        BuildJobState stage,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!jobId.Equals(workspace.JobId) || !string.Equals(correlationId, workspace.CorrelationId, StringComparison.Ordinal))
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_IDENTITY_MISMATCH");
        }

        try
        {
            return stage.Equals(BuildJobState.Preflight)
                ? Preflight()
                : stage.Equals(BuildJobState.Inspecting)
                ? Inspect()
                : stage.Equals(BuildJobState.Normalizing)
                ? await NormalizeAsync(cancellationToken)
                : stage.Equals(BuildJobState.BuildingTargets)
                ? await BuildTargetAsync(cancellationToken)
                : stage.Equals(BuildJobState.RenderingPreviews)
                ? BuildStageExecutionResult.Success()
                : stage.Equals(BuildJobState.Validating)
                ? await ValidateAsync(cancellationToken)
                : stage.Equals(BuildJobState.PackagingMarketplace)
                ? BuildStageExecutionResult.Success()
                : stage.Equals(BuildJobState.CleanReimport)
                ? VerifyCleanReimportEvidence()
                : BuildStageExecutionResult.Failure("PORTABLE_STATIC_STAGE_UNSUPPORTED");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_STAGE_FAILED");
        }
    }

    private BuildStageExecutionResult Preflight()
    {
        ProductManifestDeserializationResult manifestResult = ProductManifestJson.Deserialize(
            File.ReadAllText(workspace.ManifestPath, Encoding.UTF8));
        ProfileJsonDeserializationResult<PublisherProfile> publisherResult = PublisherProfileJson.Deserialize(
            File.ReadAllText(workspace.PublisherProfilePath, Encoding.UTF8));
        if (!manifestResult.IsSuccessful || !publisherResult.IsSuccessful ||
            !File.Exists(workspace.SourceFbxPath))
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_PREFLIGHT_FAILED");
        }

        _manifest = manifestResult.Value;
        _publisher = publisherResult.Value;
        BuildLockGenerationResult lockResult = DeterministicBuildLockGenerator.Generate(
            new BuildLockGenerationRequest(
                workspace.JobId,
                "0.1.0",
                Version(ToolKind.DotNet, "10.0.302"),
                [
                    Approval(ToolKind.Blender, "5.0.0"),
                    Approval(ToolKind.Unity, "6000.3.10f1"),
                    Approval(ToolKind.Unreal, "5.7.2"),
                ],
                _manifest!.SchemaVersion,
                [new BuildLockWorker("portable-worker", "1.0.0")],
                [new BuildLockMarketplaceProfile("fab", "default", "1.0.0")]));
        _buildLock = lockResult.Value;
        return lockResult.IsSuccess
            ? BuildStageExecutionResult.Success()
            : BuildStageExecutionResult.Failure("PORTABLE_STATIC_BUILD_LOCK_FAILED");
    }

    private BuildStageExecutionResult Inspect() =>
        _manifest is not null && _publisher is not null &&
        _manifest.ProductCase.Equals(ProductCase.Static) &&
        _manifest.Rig is null && _manifest.Animations.Count == 0 &&
        _manifest.Targets.Any(static target => target.Equals(BuildTarget.Portable)) &&
        _manifest.PublisherProfileReference.Equals(_publisher.Root)
            ? BuildStageExecutionResult.Success()
            : BuildStageExecutionResult.Failure("PORTABLE_STATIC_INSPECTION_FAILED");

    private async Task<BuildStageExecutionResult> NormalizeAsync(CancellationToken cancellationToken)
    {
        _fbxRecord = await StageAndValidateAsync(
            "Artifact-PB0507-Fbx",
            "normalized-fbx",
            "work/normalized/StoneArch.fbx",
            workspace.SourceFbxPath,
            cancellationToken);
        return _fbxRecord is null
            ? BuildStageExecutionResult.Failure("PORTABLE_STATIC_NORMALIZATION_FAILED")
            : BuildStageExecutionResult.Success();
    }

    private async Task<BuildStageExecutionResult> BuildTargetAsync(CancellationToken cancellationToken)
    {
        _naming = PortableNamingProfile.Create(_manifest!.AssetId, _manifest.FolderName).Value;
        PortableReadmeResult<PortableReadmeRequest> readmeRequest = PortableReadmeRequest.Create(
            _manifest,
            _publisher,
            _naming,
            PortableModelDimensions.Create(2, 2, 2).Value,
            null,
            [],
            ["Import StoneArch.fbx and assign materials for the destination renderer."]);
        PortableReadmeResult<PortableReadmeDocument> readmeResult = PortableReadmeGenerator.Generate(readmeRequest.Value);
        if (!readmeRequest.IsValid || !readmeResult.IsValid)
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_README_FAILED");
        }

        _readme = readmeResult.Value;
        await File.WriteAllBytesAsync(workspace.ReadmePath, [.. _readme!.Utf8Bytes], cancellationToken);
        _flatReadmeRecord = await StageAndValidateAsync(
            "Artifact-PB0507-FlatReadme",
            "portable-readme",
            "work/portable/README_StoneArch.txt",
            workspace.ReadmePath,
            cancellationToken);
        _productReadmeRecord = await StageAndValidateAsync(
            "Artifact-PB0507-ProductReadme",
            "portable-readme",
            "work/portable/README_Stone_Arch.txt",
            workspace.ReadmePath,
            cancellationToken);
        if (_flatReadmeRecord is null || _productReadmeRecord is null)
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_README_STAGE_FAILED");
        }

        ArtifactStoreRecord provisionalArchive = ProvisionalArchiveRecord();
        _layout = Compose(provisionalArchive);
        await using (var destination = new FileStream(
                         workspace.ArchivePath,
                         FileMode.CreateNew,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         65_536,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (FileStream fbx = File.OpenRead(_fbxRecord!.Layout.PayloadPath))
        await using (FileStream readme = File.OpenRead(_flatReadmeRecord.Layout.PayloadPath))
        {
            PortableFbxArchiveResult archiveResult = await PortableFbxArchiveBuilder.CreateAsync(
                _layout,
                [
                    new PortableFbxArchiveSource(_fbxRecord, fbx),
                    new PortableFbxArchiveSource(_flatReadmeRecord, readme),
                ],
                destination,
                cancellationToken);
            if (!archiveResult.IsSuccess)
            {
                return BuildStageExecutionResult.Failure("PORTABLE_STATIC_ARCHIVE_FAILED");
            }

            ArchiveReceipt = archiveResult.Receipt;
        }

        ArchiveRecord = await StageAndValidateAsync(
            "Artifact-PB0507-Release",
            "portable-fbx-archive",
            $"{_naming!.ProductFolderName}/{_naming.FbxArchiveFileName}",
            workspace.ArchivePath,
            cancellationToken);
        if (ArchiveRecord is null || !ArchiveRecord.ContentIdentity.Equals(ArchiveReceipt!.ArchiveIdentity))
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_ARCHIVE_STAGE_FAILED");
        }

        _layout = Compose(ArchiveRecord);
        return BuildStageExecutionResult.Success();
    }

    private async Task<BuildStageExecutionResult> ValidateAsync(CancellationToken cancellationToken)
    {
        await using FileStream archive = File.OpenRead(workspace.ArchivePath);
        PortableTargetValidationReport validation = await PortableTargetValidator.ValidateAsync(
            _layout,
            _naming,
            ArchiveReceipt,
            archive,
            [],
            _readme,
            [new PortableAssetReferenceEvidence(_naming!.FbxFileName, [])],
            [new PortableReimportEvidence(_naming.FbxFileName, !forceReimportFailure)],
            cancellationToken);
        PortableStaticValidationReportResult report = PortableStaticValidationReportJson.Serialize(
            workspace.JobId,
            _manifest,
            _buildLock!.Lock,
            ArchiveReceipt,
            validation,
            workspace.JobLogReference);
        if (!report.IsSuccess)
        {
            return BuildStageExecutionResult.Failure("PORTABLE_STATIC_REPORT_FAILED");
        }

        await File.WriteAllBytesAsync(workspace.ReportPath, [.. report.Value!.Utf8Bytes], cancellationToken);
        ArtifactStoreRecord? reportRecord = await StageAndValidateAsync(
            "Artifact-PB0507-Report",
            "validation-report",
            "reports/Job-PB0507/validation-report.json",
            workspace.ReportPath,
            cancellationToken);
        return reportRecord is null
            ? BuildStageExecutionResult.Failure("PORTABLE_STATIC_REPORT_STAGE_FAILED")
            : validation.Passed
            ? BuildStageExecutionResult.Success()
            : BuildStageExecutionResult.Failure("PORTABLE_STATIC_VALIDATION_FAILED");
    }

    private BuildStageExecutionResult VerifyCleanReimportEvidence()
    {
        using var evidence = JsonDocument.Parse(File.ReadAllText(workspace.ReimportEvidencePath, Encoding.UTF8));
        JsonElement root = evidence.RootElement;
        string measuredHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(workspace.SourceFbxPath)));
        return root.GetProperty("passed").GetBoolean() &&
            root.GetProperty("meshCount").GetInt32() == 1 &&
            root.GetProperty("armatureCount").GetInt32() == 0 &&
            root.GetProperty("actionCount").GetInt32() == 0 &&
            string.Equals(root.GetProperty("sha256").GetString(), measuredHash, StringComparison.Ordinal)
            ? BuildStageExecutionResult.Success()
            : BuildStageExecutionResult.Failure("PORTABLE_STATIC_REIMPORT_FAILED");
    }

    private PortableFolderLayout Compose(ArtifactStoreRecord archive) =>
        PortableFolderComposer.Compose(
            _naming,
            [
                PortableCompositionArtifact.Create(_fbxRecord, PortableArtifactPurpose.Fbx).Value,
                PortableCompositionArtifact.Create(archive, PortableArtifactPurpose.FbxArchive).Value,
                PortableCompositionArtifact.Create(_flatReadmeRecord, PortableArtifactPurpose.FlatReadme).Value,
                PortableCompositionArtifact.Create(_productReadmeRecord, PortableArtifactPurpose.ProductReadme).Value,
            ]).Value!;

    private ArtifactStoreRecord ProvisionalArchiveRecord()
    {
        byte[] placeholder = [0];
        BuildArtifact artifact = Artifact(
            "Artifact-PB0507-ProvisionalArchive",
            "portable-fbx-archive",
            $"{_naming!.ProductFolderName}/{_naming.FbxArchiveFileName}",
            BuildArtifactLifecycleState.Validated);
        ArtifactContentIdentity identity = ArtifactContentIdentity.Create(
            placeholder.LongLength,
            Sha256Digest.Create(Convert.ToHexStringLower(SHA256.HashData(placeholder))).Value).Value!;
        ArtifactStoreLayout layout = ArtifactStoreLayoutFactory.Create(
            workspace.ArtifactsRoot,
            workspace.JobId,
            artifact.Id).Value!;
        return new ArtifactStoreRecord(artifact, identity, layout);
    }

    private async Task<ArtifactStoreRecord?> StageAndValidateAsync(
        string id,
        string role,
        string logicalReference,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        BuildArtifact artifact = Artifact(id, role, logicalReference, BuildArtifactLifecycleState.Staged);
        ArtifactStoreOperationResult<ArtifactStoreRecord> staged = await _artifactStore.StageAsync(
            new ArtifactStoreStageRequest(workspace.Root, workspace.ArtifactsRoot, sourcePath, artifact),
            cancellationToken);
        if (!staged.IsSuccess)
        {
            return null;
        }

        ArtifactStoreOperationResult<ArtifactStoreRecord> validated = await _artifactStore.TransitionAsync(
            new ArtifactStoreTransitionRequest(
                workspace.Root,
                workspace.ArtifactsRoot,
                workspace.JobId,
                artifact.Id,
                BuildArtifactLifecycleState.Staged,
                BuildArtifactLifecycleState.Validated,
                workspace.ArtifactValidatedAtUtc),
            cancellationToken);
        return validated.Value;
    }

    private BuildArtifact Artifact(
        string id,
        string role,
        string logicalReference,
        BuildArtifactLifecycleState state) =>
        BuildArtifact.Create(
            BuildArtifactId.Create(id).Value,
            workspace.JobId,
            BuildStepId.Create("Step-PB0507-Portable").Value,
            BuildArtifactRole.Create(role).Value,
            BuildTarget.Portable,
            state,
            logicalReference,
            workspace.ArtifactCreatedAtUtc,
            state.Equals(BuildArtifactLifecycleState.Staged)
                ? workspace.ArtifactCreatedAtUtc
                : workspace.ArtifactValidatedAtUtc).Value!;

    private static ToolVersion Version(ToolKind tool, string version) =>
        ToolVersion.Create(tool, version).Value!;

    private static ToolVersionApproval Approval(ToolKind tool, string version) =>
        new(Version(tool, version), ToolVersionApprovalState.ApprovedLatest, []);
}

/// <summary>Adapts PB-0206 atomic promotion to PB-0213's release-promotion boundary.</summary>
internal sealed class PortableStaticReleasePromoter(
    PortableStaticTestWorkspace workspace,
    PortableStaticStageWorker worker) : IBuildJobReleasePromoter
{
    public int CallCount { get; private set; }

    public ArtifactPromotionReceipt? Receipt { get; private set; }

    public async Task<BuildJobPromotionResult> PromoteAsync(
        BuildJobId jobId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (!jobId.Equals(workspace.JobId) ||
            !string.Equals(correlationId, workspace.CorrelationId, StringComparison.Ordinal) ||
            worker.ArchiveRecord is null)
        {
            return BuildJobPromotionResult.Failure("PORTABLE_STATIC_PROMOTION_INPUT_INVALID");
        }

        ArtifactPromotionOperationResult result = await new AtomicArtifactPromotionService().PromoteAsync(
            new ArtifactPromotionRequest(
                workspace.Root,
                workspace.ArtifactsRoot,
                workspace.BuildsRoot,
                workspace.JobId,
                worker.ArchiveRecord.Artifact.Id,
                workspace.PromotedAtUtc),
            cancellationToken);
        Receipt = result.Value;
        return result.IsSuccess
            ? BuildJobPromotionResult.Success()
            : BuildJobPromotionResult.Failure(
                result.Failures.Count == 0
                    ? "PORTABLE_STATIC_PROMOTION_FAILED"
                    : result.Failures[0].Code);
    }
}

/// <summary>Owns one contained, disposable PB-0507 filesystem and persisted-job fixture.</summary>
internal sealed class PortableStaticTestWorkspace : IDisposable
{
    private readonly bool _retain;

    public PortableStaticTestWorkspace(bool? retainOverride = null)
    {
        RepositoryRoot = FindRepositoryRoot();
        _retain = retainOverride ?? string.Equals(
            Environment.GetEnvironmentVariable("PACKAGEBUILDER_PB0507_RETAIN_OUTPUT"),
            "1",
            StringComparison.Ordinal);
        string category = _retain ? "manual" : "tests";
        Root = Path.GetFullPath(Path.Combine(
            RepositoryRoot,
            "artifacts",
            "PB-0507",
            category,
            $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        _ = Directory.CreateDirectory(Root);
        InputRoot = Create("input");
        WorkRoot = Create("work");
        ArtifactsRoot = Create("artifacts");
        _ = Create("runtime-data");
        DatabaseBackupRoot = Create("runtime-data/backups");
        ReportsRoot = Create("reports");
        BuildsRoot = Path.Combine(ArtifactsRoot, "Builds");
        ManifestPath = CopyFixture("product-manifest.json", "input/product-manifest.json");
        SourceFbxPath = CopyFixture("source/StoneArch.fbx", "input/source/StoneArch.fbx");
        ReimportEvidencePath = CopyFixture("clean-reimport-evidence.json", "input/clean-reimport-evidence.json");
        PublisherProfilePath = Path.Combine(RepositoryRoot, "profiles", "publishers", "AvivPeretsFBX.example.json");
        ReadmePath = Path.Combine(WorkRoot, "README_StoneArch.txt");
        ArchivePath = Path.Combine(WorkRoot, "Stone_Arch_FBX.zip");
        ReportPath = Path.Combine(ReportsRoot, "validation-report.json");
        DatabasePath = Path.Combine(Root, "runtime-data", "jobs.db");
    }

    public string RepositoryRoot { get; }

    public string Root { get; }

    public string InputRoot { get; }

    public string WorkRoot { get; }

    public string ArtifactsRoot { get; }

    public string BuildsRoot { get; }

    public string ReportsRoot { get; }

    public string DatabaseBackupRoot { get; }

    public string ManifestPath { get; }

    public string SourceFbxPath { get; }

    public string ReimportEvidencePath { get; }

    public string PublisherProfilePath { get; }

    public string ReadmePath { get; }

    public string ArchivePath { get; }

    public string ReportPath { get; }

    public string DatabasePath { get; }

    public BuildJobId JobId { get; } = BuildJobId.Create("Job-PB0507").Value!;

    public string CorrelationId { get; } = "Correlation-PB0507";

    public DateTimeOffset ArtifactCreatedAtUtc { get; } =
        new(2026, 8, 6, 18, 0, 0, TimeSpan.Zero);

    public DateTimeOffset ArtifactValidatedAtUtc { get; } =
        new(2026, 8, 6, 18, 1, 0, TimeSpan.Zero);

    public DateTimeOffset PromotedAtUtc { get; } =
        new(2026, 8, 6, 18, 2, 0, TimeSpan.Zero);

    public string JobLogReference => $"logs/jobs/{JobDirectoryName}/job.log";

    public string JobLogPath => Path.Combine(Root, Path.Combine(JobLogReference.Split('/')));

    private string JobDirectoryName =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JobId.Value)));

    public SqliteBuildMetadataRepository CreateRepository()
    {
        SqliteMigrationResult migration = new SqliteDatabaseMigrator().Migrate(
            Root,
            DatabasePath,
            DatabaseBackupRoot,
            TestContext.Current.CancellationToken);
        Assert.True(migration.IsSuccess, migration.Error?.Code);
        RepositoryOperationResult<SqliteBuildMetadataRepository> result =
            SqliteBuildMetadataRepository.Create(Root, DatabasePath);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value!;
    }

    public void PublishManualPointerIfRequested(string releasePath, string reportPath)
    {
        if (!_retain)
        {
            return;
        }

        string manualRoot = Path.Combine(RepositoryRoot, "artifacts", "PB-0507", "manual");
        File.WriteAllLines(
            Path.Combine(manualRoot, "latest.txt"),
            [
                $"workspace={Root}",
                $"release={releasePath}",
                $"report={reportPath}",
                $"log={JobLogPath}",
            ],
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Dispose()
    {
        if (_retain || !Directory.Exists(Root))
        {
            return;
        }

        string approvedRoot = Path.GetFullPath(Path.Combine(RepositoryRoot, "artifacts", "PB-0507", "tests"));
        if (!Root.StartsWith(approvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The disposable PB-0507 workspace escaped its approved test root.");
        }

        Directory.Delete(Root, recursive: true);
    }

    private string Create(string relativePath)
    {
        string path = Path.Combine(Root, Path.Combine(relativePath.Split('/')));
        _ = Directory.CreateDirectory(path);
        return path;
    }

    private string CopyFixture(string sourceReference, string destinationReference)
    {
        string fixtureRoot = Path.Combine(RepositoryRoot, "tests", "fixtures", "portable", "static-vertical-slice");
        string source = Path.Combine(fixtureRoot, Path.Combine(sourceReference.Split('/')));
        string destination = Path.Combine(Root, Path.Combine(destinationReference.Split('/')));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
        return destination;
    }

    private static string FindRepositoryRoot()
    {
        string? environmentRoot = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(environmentRoot) && File.Exists(Path.Combine(environmentRoot, "AGENTS.md")))
        {
            return Path.GetFullPath(environmentRoot);
        }

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("The Package Builder repository root was not found.");
    }
}

internal sealed class IncrementingUtcClock : IBuildJobClock
{
    private DateTimeOffset _current = new(2026, 8, 6, 17, 0, 0, TimeSpan.Zero);

    public DateTimeOffset GetUtcNow()
    {
        _current = _current.AddSeconds(1);
        return _current;
    }
}
