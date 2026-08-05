namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>Defines the current metadata schema version and its approved table inventory.</summary>
public static class SqliteSchema
{
    public const int CurrentVersion = 2;

    public static IReadOnlyList<string> VersionOneTables { get; } = Array.AsReadOnly<string>(
    [
        "Products",
        "ProductVersions",
        "PublisherProfiles",
        "BuildJobs",
        "BuildSteps",
        "Artifacts",
        "ValidationFindings",
        "ToolInstallations",
        "EngineVersions",
        "RequirementsProfiles",
        "Settings",
    ]);

    public static IReadOnlyList<string> CurrentTables { get; } = Array.AsReadOnly<string>(
    [
        .. VersionOneTables,
        "EngineVersionModules",
        "EngineVersionTransitions",
    ]);

    internal const string CreateVersionOne = """
        CREATE TABLE Products (
            ProductId TEXT PRIMARY KEY NOT NULL,
            DisplayName TEXT NOT NULL,
            AssetId TEXT NOT NULL UNIQUE,
            FolderName TEXT NOT NULL UNIQUE,
            ProductCase TEXT NOT NULL CHECK (ProductCase IN ('static','rigged','rigged-animated','item-set','item-collection')),
            CreatedAtUtc TEXT NOT NULL
        ) STRICT;

        CREATE TABLE ProductVersions (
            ProductVersionId TEXT PRIMARY KEY NOT NULL,
            ProductId TEXT NOT NULL REFERENCES Products(ProductId) ON DELETE CASCADE,
            Version TEXT NOT NULL,
            ManifestReference TEXT NOT NULL,
            ManifestSha256 TEXT NOT NULL CHECK (length(ManifestSha256) = 64 AND ManifestSha256 NOT GLOB '*[^0-9a-f]*'),
            CreatedAtUtc TEXT NOT NULL,
            UNIQUE (ProductId, Version)
        ) STRICT;

        CREATE TABLE PublisherProfiles (
            PublisherProfileId TEXT PRIMARY KEY NOT NULL,
            PublisherRoot TEXT NOT NULL UNIQUE,
            DisplayName TEXT NOT NULL,
            SchemaVersion INTEGER NOT NULL CHECK (SchemaVersion > 0),
            ProfileJson TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        ) STRICT;

        CREATE TABLE BuildJobs (
            BuildJobId TEXT PRIMARY KEY NOT NULL,
            ProductVersionId TEXT REFERENCES ProductVersions(ProductVersionId) ON DELETE RESTRICT,
            State TEXT NOT NULL CHECK (State IN ('queued','preflight','inspecting','awaiting-review','normalizing','building-targets','rendering-previews','validating','packaging-marketplace','clean-reimport','completed','failed','cancelled')),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            FailureCode TEXT
        ) STRICT;

        CREATE TABLE BuildSteps (
            BuildStepId TEXT PRIMARY KEY NOT NULL,
            BuildJobId TEXT NOT NULL REFERENCES BuildJobs(BuildJobId) ON DELETE CASCADE,
            StepOrder INTEGER NOT NULL CHECK (StepOrder >= 0),
            Stage TEXT NOT NULL,
            Status TEXT NOT NULL CHECK (Status IN ('pending','running','completed','failed','cancelled')),
            StartedAtUtc TEXT,
            CompletedAtUtc TEXT,
            UNIQUE (BuildJobId, StepOrder)
        ) STRICT;

        CREATE TABLE Artifacts (
            ArtifactId TEXT PRIMARY KEY NOT NULL,
            BuildJobId TEXT NOT NULL REFERENCES BuildJobs(BuildJobId) ON DELETE CASCADE,
            BuildStepId TEXT REFERENCES BuildSteps(BuildStepId) ON DELETE SET NULL,
            Role TEXT NOT NULL,
            Target TEXT CHECK (Target IS NULL OR Target IN ('portable','unity','unreal')),
            LogicalReference TEXT NOT NULL,
            Sha256 TEXT CHECK (Sha256 IS NULL OR (length(Sha256) = 64 AND Sha256 NOT GLOB '*[^0-9a-f]*')),
            ByteCount INTEGER CHECK (ByteCount IS NULL OR ByteCount >= 0),
            LifecycleState TEXT NOT NULL CHECK (LifecycleState IN ('staged','validated','promoted')),
            CreatedAtUtc TEXT NOT NULL
        ) STRICT;

        CREATE TABLE ValidationFindings (
            FindingId TEXT PRIMARY KEY NOT NULL,
            BuildJobId TEXT NOT NULL REFERENCES BuildJobs(BuildJobId) ON DELETE CASCADE,
            ArtifactId TEXT REFERENCES Artifacts(ArtifactId) ON DELETE SET NULL,
            Code TEXT NOT NULL,
            Severity TEXT NOT NULL CHECK (Severity IN ('info','warning','error','fatal')),
            Explanation TEXT NOT NULL,
            Source TEXT NOT NULL,
            SuggestedAction TEXT,
            BlocksRelease INTEGER NOT NULL CHECK (BlocksRelease IN (0, 1)),
            CreatedAtUtc TEXT NOT NULL
        ) STRICT;

        CREATE TABLE ToolInstallations (
            ToolInstallationId TEXT PRIMARY KEY NOT NULL,
            ToolName TEXT NOT NULL,
            Version TEXT NOT NULL,
            ExecutableReference TEXT NOT NULL,
            Sha256 TEXT CHECK (Sha256 IS NULL OR (length(Sha256) = 64 AND Sha256 NOT GLOB '*[^0-9a-f]*')),
            IsApproved INTEGER NOT NULL CHECK (IsApproved IN (0, 1)),
            DiscoveredAtUtc TEXT NOT NULL,
            UNIQUE (ToolName, Version, ExecutableReference)
        ) STRICT;

        CREATE TABLE EngineVersions (
            EngineVersionId TEXT PRIMARY KEY NOT NULL,
            EngineKind TEXT NOT NULL CHECK (EngineKind IN ('blender','unity','unreal')),
            Version TEXT NOT NULL,
            Status TEXT NOT NULL,
            ToolInstallationId TEXT REFERENCES ToolInstallations(ToolInstallationId) ON DELETE SET NULL,
            LastValidatedAtUtc TEXT,
            UNIQUE (EngineKind, Version)
        ) STRICT;

        CREATE TABLE RequirementsProfiles (
            RequirementsProfileId TEXT PRIMARY KEY NOT NULL,
            Marketplace TEXT NOT NULL,
            ProfileVersion TEXT NOT NULL,
            EffectiveDate TEXT NOT NULL,
            SourceReference TEXT NOT NULL,
            ProfileJson TEXT NOT NULL,
            Sha256 TEXT NOT NULL CHECK (length(Sha256) = 64 AND Sha256 NOT GLOB '*[^0-9a-f]*'),
            IsApproved INTEGER NOT NULL CHECK (IsApproved IN (0, 1)),
            UNIQUE (Marketplace, ProfileVersion)
        ) STRICT;

        CREATE TABLE Settings (
            SettingKey TEXT PRIMARY KEY NOT NULL,
            ValueJson TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        ) STRICT;

        CREATE INDEX IX_ProductVersions_ProductId ON ProductVersions(ProductId);
        CREATE INDEX IX_BuildJobs_ProductVersionId ON BuildJobs(ProductVersionId);
        CREATE INDEX IX_BuildJobs_State ON BuildJobs(State);
        CREATE INDEX IX_BuildSteps_BuildJobId ON BuildSteps(BuildJobId);
        CREATE INDEX IX_Artifacts_BuildJobId ON Artifacts(BuildJobId);
        CREATE INDEX IX_ValidationFindings_BuildJobId ON ValidationFindings(BuildJobId);
        CREATE INDEX IX_ValidationFindings_ArtifactId ON ValidationFindings(ArtifactId);
        CREATE INDEX IX_EngineVersions_ToolInstallationId ON EngineVersions(ToolInstallationId);
        PRAGMA user_version = 1;
        """;

    internal const string CreateVersionTwo = """
        ALTER TABLE EngineVersions RENAME TO EngineVersionsV1;

        CREATE TABLE EngineVersions (
            EngineVersionId TEXT PRIMARY KEY NOT NULL,
            EngineKind TEXT NOT NULL CHECK (EngineKind IN ('blender','unity','unreal')),
            Version TEXT NOT NULL,
            Status TEXT NOT NULL CHECK (Status IN ('discovered','installed','candidate','approved-latest','rejected','last-known-good')),
            ToolInstallationId TEXT REFERENCES ToolInstallations(ToolInstallationId) ON DELETE SET NULL,
            DiscoveredAtUtc TEXT NOT NULL,
            LastValidatedAtUtc TEXT,
            UpdatedAtUtc TEXT NOT NULL,
            Revision INTEGER NOT NULL CHECK (Revision >= 0),
            UNIQUE (EngineKind, Version)
        ) STRICT;

        INSERT INTO EngineVersions
            (EngineVersionId, EngineKind, Version, Status, ToolInstallationId,
             DiscoveredAtUtc, LastValidatedAtUtc, UpdatedAtUtc, Revision)
        SELECT EngineVersionId, EngineKind, Version, Status, ToolInstallationId,
               COALESCE(LastValidatedAtUtc, '1970-01-01T00:00:00.0000000+00:00'),
               LastValidatedAtUtc,
               COALESCE(LastValidatedAtUtc, '1970-01-01T00:00:00.0000000+00:00'),
               0
        FROM EngineVersionsV1;

        DROP TABLE EngineVersionsV1;

        CREATE TABLE EngineVersionModules (
            EngineVersionId TEXT NOT NULL REFERENCES EngineVersions(EngineVersionId) ON DELETE CASCADE,
            ModuleName TEXT NOT NULL,
            PRIMARY KEY (EngineVersionId, ModuleName)
        ) STRICT;

        CREATE TABLE EngineVersionTransitions (
            TransitionId TEXT PRIMARY KEY NOT NULL,
            EngineVersionId TEXT NOT NULL REFERENCES EngineVersions(EngineVersionId) ON DELETE CASCADE,
            FromStatus TEXT CHECK (FromStatus IS NULL OR FromStatus IN ('discovered','installed','candidate','approved-latest','rejected','last-known-good')),
            ToStatus TEXT NOT NULL CHECK (ToStatus IN ('discovered','installed','candidate','approved-latest','rejected','last-known-good')),
            OccurredAtUtc TEXT NOT NULL,
            CompatibilityRunId TEXT,
            CompatibilityOutcome TEXT CHECK (CompatibilityOutcome IS NULL OR CompatibilityOutcome IN ('passed','failed')),
            TotalTests INTEGER CHECK (TotalTests IS NULL OR TotalTests > 0),
            PassedTests INTEGER CHECK (PassedTests IS NULL OR PassedTests >= 0),
            FailedTests INTEGER CHECK (FailedTests IS NULL OR FailedTests >= 0),
            EvidenceReference TEXT,
            EvidenceSha256 TEXT CHECK (EvidenceSha256 IS NULL OR (length(EvidenceSha256) = 64 AND EvidenceSha256 NOT GLOB '*[^0-9a-f]*')),
            CompletedAtUtc TEXT,
            CHECK (
                (FromStatus IS NULL AND ToStatus = 'discovered'
                    AND CompatibilityRunId IS NULL AND CompatibilityOutcome IS NULL
                    AND TotalTests IS NULL AND PassedTests IS NULL AND FailedTests IS NULL
                    AND EvidenceReference IS NULL AND EvidenceSha256 IS NULL AND CompletedAtUtc IS NULL)
                OR
                (FromStatus = 'candidate' AND ToStatus = 'approved-latest'
                    AND CompatibilityOutcome = 'passed' AND FailedTests = 0
                    AND CompatibilityRunId IS NOT NULL AND TotalTests IS NOT NULL
                    AND PassedTests = TotalTests AND EvidenceReference IS NOT NULL
                    AND EvidenceSha256 IS NOT NULL AND CompletedAtUtc IS NOT NULL)
                OR
                (FromStatus = 'candidate' AND ToStatus = 'rejected'
                    AND CompatibilityOutcome = 'failed' AND FailedTests > 0
                    AND CompatibilityRunId IS NOT NULL AND TotalTests IS NOT NULL
                    AND PassedTests + FailedTests = TotalTests AND EvidenceReference IS NOT NULL
                    AND EvidenceSha256 IS NOT NULL AND CompletedAtUtc IS NOT NULL)
                OR
                (FromStatus IS NOT NULL
                    AND NOT (FromStatus = 'candidate' AND ToStatus IN ('approved-latest','rejected'))
                    AND CompatibilityRunId IS NULL AND CompatibilityOutcome IS NULL
                    AND TotalTests IS NULL AND PassedTests IS NULL AND FailedTests IS NULL
                    AND EvidenceReference IS NULL AND EvidenceSha256 IS NULL AND CompletedAtUtc IS NULL)
            )
        ) STRICT;

        INSERT INTO EngineVersionTransitions
            (TransitionId, EngineVersionId, FromStatus, ToStatus, OccurredAtUtc)
        SELECT 'migration-v1.' || EngineVersionId, EngineVersionId, NULL, Status, DiscoveredAtUtc
        FROM EngineVersions;

        CREATE INDEX IX_EngineVersions_ToolInstallationId ON EngineVersions(ToolInstallationId);
        CREATE UNIQUE INDEX UX_EngineVersions_ApprovedLatest
            ON EngineVersions(EngineKind) WHERE Status = 'approved-latest';
        CREATE INDEX IX_EngineVersionModules_EngineVersionId
            ON EngineVersionModules(EngineVersionId);
        CREATE INDEX IX_EngineVersionTransitions_EngineVersionIdOccurred
            ON EngineVersionTransitions(EngineVersionId, OccurredAtUtc, TransitionId);
        PRAGMA user_version = 2;
        """;
}
