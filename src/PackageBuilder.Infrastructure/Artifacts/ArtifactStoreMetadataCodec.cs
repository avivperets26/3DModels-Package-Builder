using System.Text.Json;
using System.Text.Json.Serialization;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>Reads and writes the strict version-one artifact metadata document.</summary>
internal static class ArtifactStoreMetadataCodec
{
    private const int SchemaVersion = 1;

    private static readonly HashSet<string> _requiredProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "artifactId",
        "jobId",
        "stepId",
        "role",
        "target",
        "lifecycleState",
        "logicalReference",
        "createdAtUtc",
        "stateChangedAtUtc",
        "payloadRelativePath",
        "bytes",
        "sha256",
    };

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static byte[] Write(ArtifactStoreRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        BuildArtifact artifact = record.Artifact;
        var document = new MetadataDocument(
            SchemaVersion,
            artifact.Id.Value,
            artifact.JobId.Value,
            artifact.StepId.Value,
            artifact.Role.CanonicalIdentifier,
            artifact.Target?.CanonicalIdentifier,
            artifact.LifecycleState.CanonicalIdentifier,
            artifact.LogicalReference,
            artifact.CreatedAtUtc,
            artifact.StateChangedAtUtc,
            record.Layout.PayloadRelativePath,
            record.ContentIdentity.Bytes,
            record.ContentIdentity.Sha256.Value);
        return JsonSerializer.SerializeToUtf8Bytes(document, _options);
    }

    public static ArtifactStoreOperationResult<ArtifactStoreRecord> Read(
        byte[] json,
        ArtifactStoreLayout layout)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object || !HasExactProperties(parsed.RootElement))
            {
                return Failed("ARTIFACT_STORE_METADATA_INVALID", "metadata", "Artifact metadata must be one object with unique properties.");
            }

            MetadataDocument? document = JsonSerializer.Deserialize<MetadataDocument>(json, _options);
            if (document!.SchemaVersion != SchemaVersion)
            {
                return Failed("ARTIFACT_STORE_METADATA_VERSION", "metadata", "The artifact metadata schema version is unsupported.");
            }

            ArtifactStoreOperationResult<ArtifactStoreRecord> result = CreateRecord(document, layout);
            return result;
        }
        catch (JsonException)
        {
            return Failed("ARTIFACT_STORE_METADATA_INVALID", "metadata", "Artifact metadata is malformed or contains unsupported fields.");
        }
    }

    private static ArtifactStoreOperationResult<ArtifactStoreRecord> CreateRecord(
        MetadataDocument document,
        ArtifactStoreLayout layout)
    {
        BuildModelValidationResult<BuildArtifactId> artifactId = BuildArtifactId.Create(document.ArtifactId);
        BuildModelValidationResult<BuildJobId> jobId = BuildJobId.Create(document.JobId);
        BuildModelValidationResult<BuildStepId> stepId = BuildStepId.Create(document.StepId);
        BuildModelValidationResult<BuildArtifactRole> role = BuildArtifactRole.Create(document.Role);
        CanonicalIdentifierParseResult<BuildArtifactLifecycleState> state = BuildArtifactLifecycleState.TryParse(document.LifecycleState);
        CanonicalIdentifierParseResult<BuildTarget>? target = document.Target is null ? null : BuildTarget.TryParse(document.Target);
        ArtifactIdentityValidationResult<Sha256Digest> digest = Sha256Digest.Create(document.Sha256);
        if (!artifactId.IsValid || !jobId.IsValid || !stepId.IsValid || !role.IsValid ||
            !state.IsValid || (target is not null && !target.IsValid) || !digest.IsSuccess ||
            document.Bytes < 0 ||
            !string.Equals(document.PayloadRelativePath, layout.PayloadRelativePath, StringComparison.Ordinal))
        {
            return Failed("ARTIFACT_STORE_METADATA_INVALID", "metadata", "Artifact metadata contains invalid typed values.");
        }

        BuildModelValidationResult<BuildArtifact> artifact = BuildArtifact.Create(
            artifactId.Value,
            jobId.Value,
            stepId.Value,
            role.Value,
            target?.Value,
            state.Value,
            document.LogicalReference,
            document.CreatedAtUtc,
            document.StateChangedAtUtc);
        if (!artifact.IsValid)
        {
            return Failed("ARTIFACT_STORE_METADATA_INVALID", "metadata", "Artifact metadata contains inconsistent typed values.");
        }

        ArtifactContentIdentity identity = ArtifactContentIdentity.Create(document.Bytes, digest.Value).Value!;
        return ArtifactStoreOperationResult.Success(new ArtifactStoreRecord(artifact.Value!, identity, layout));
    }

    private static bool HasExactProperties(JsonElement root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }
        }

        return names.SetEquals(_requiredProperties);
    }

    private static ArtifactStoreOperationResult<ArtifactStoreRecord> Failed(
        string code,
        string location,
        string diagnostic) =>
        ArtifactStoreOperationResult.Failed<ArtifactStoreRecord>(
            new ArtifactStoreFailure(code, location, diagnostic));

    private sealed record MetadataDocument(
        int SchemaVersion,
        string? ArtifactId,
        string? JobId,
        string? StepId,
        string? Role,
        string? Target,
        string? LifecycleState,
        string? LogicalReference,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset StateChangedAtUtc,
        string? PayloadRelativePath,
        long Bytes,
        string? Sha256);
}
