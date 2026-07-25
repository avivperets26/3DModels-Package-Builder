using System.Text.Json;
using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Contracts.Manifests;
using PackageBuilder.Contracts.Profiles;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Migrations;

public sealed class ManifestProfileMigrationEngine(
    MigrationRegistryResult registryResult,
    Func<MigrationDocumentFamily, string, MigrationFinalizationResult> finalize)
{
    private readonly MigrationRegistryResult _registryResult = registryResult;
    private readonly Func<
        MigrationDocumentFamily,
        string,
        MigrationFinalizationResult> _finalize = finalize;

    public MigrationResult Inspect(string? json) => Process(json, execute: false);

    public MigrationResult Migrate(string? json) => Process(json, execute: true);

    private MigrationResult Process(string? json, bool execute)
    {
        DetectionResult detection = Detect(json);
        if (!detection.IsSuccessful)
        {
            return Result(
                MigrationStatus.InvalidDocument,
                detection.Family,
                detection.Version,
                null,
                json,
                null,
                null,
                detection.DiagnosticCode);
        }

        MigrationDocumentFamily family = detection.Family!.Value;
        SchemaVersion sourceVersion = detection.Version!;
        if (!_registryResult.CurrentVersions.TryGetValue(
                family,
                out SchemaVersion? targetVersion))
        {
            return Result(
                MigrationStatus.MigrationFailure,
                family,
                sourceVersion,
                null,
                json,
                null,
                null,
                "MIGRATION_REGISTRY_INVALID");
        }

        int comparison = sourceVersion.CompareTo(targetVersion);
        if (comparison > 0)
        {
            return Result(
                MigrationStatus.UnsupportedNewerVersion,
                family,
                sourceVersion,
                targetVersion,
                json,
                null,
                null,
                "MIGRATION_VERSION_NEWER_UNSUPPORTED");
        }

        if (comparison == 0)
        {
            MigrationFinalizationResult finalized = _finalize(family, json!);
            return finalized.IsSuccessful && finalized.Document is not null
                ? Result(
                    MigrationStatus.CurrentDocument,
                    family,
                    sourceVersion,
                    targetVersion,
                    json,
                    finalized.Document,
                    null,
                    "MIGRATION_NOT_REQUIRED")
                : Result(
                    MigrationStatus.InvalidDocument,
                    family,
                    sourceVersion,
                    targetVersion,
                    json,
                    null,
                    null,
                    DiagnosticOrFallback(
                        finalized.DiagnosticCode,
                        "MIGRATION_CURRENT_DOCUMENT_INVALID"));
        }

        if (!_registryResult.IsValid)
        {
            return RegistryFailure(
                family,
                sourceVersion,
                targetVersion,
                json!);
        }

        MigrationRegistry registry = _registryResult.Registry!;
        return !registry.HasRegistrations(family)
            ? Result(
                MigrationStatus.UnsupportedOlderVersion,
                family,
                sourceVersion,
                targetVersion,
                json,
                null,
                null,
                "MIGRATION_VERSION_OLDER_UNSUPPORTED")
            : !registry.TryGetStep(family, sourceVersion, out _)
            ? Result(
                MigrationStatus.MissingMigrationStep,
                family,
                sourceVersion,
                targetVersion,
                json,
                null,
                null,
                "MIGRATION_STEP_MISSING")
            : !execute
            ? Result(
                MigrationStatus.MigrationAvailable,
                family,
                sourceVersion,
                targetVersion,
                json,
                null,
                null,
                "MIGRATION_AVAILABLE")
            : Execute(json!, family, sourceVersion, targetVersion, registry);
    }

    private MigrationResult Execute(
        string originalJson,
        MigrationDocumentFamily family,
        SchemaVersion sourceVersion,
        SchemaVersion targetVersion,
        MigrationRegistry registry)
    {
        string currentJson = originalJson;
        SchemaVersion currentVersion = sourceVersion;
        var ledger = new List<MigrationChange>();
        while (currentVersion.CompareTo(targetVersion) < 0)
        {
            IJsonMigrationStep step = registry.GetRequiredStep(family, currentVersion);
            MigrationStepResult stepResult;
            try
            {
                using var sourceDocument = JsonDocument.Parse(currentJson);
                stepResult = step.Migrate(sourceDocument.RootElement);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException and
                not StackOverflowException)
            {
                return Failure("MIGRATION_STEP_FAILED");
            }

            if (!stepResult.IsSuccessful ||
                stepResult.OutputJson is null ||
                stepResult.Changes.Any(change => change is null || !change.IsStructurallyValid))
            {
                return Failure(
                    DiagnosticOrFallback(
                        stepResult.DiagnosticCode,
                        "MIGRATION_STEP_FAILED"));
            }

            JsonInputError outputError = JsonInputSafeguards.TryParseObject(
                stepResult.OutputJson,
                JsonInputSafeguards.MaximumInputCharacters,
                out JsonDocument? outputDocument);
            if (outputError != JsonInputError.None)
            {
                return Failure("MIGRATION_STEP_OUTPUT_INVALID");
            }

            using (outputDocument!)
            {
                DetectionResult outputDetection = DetectDocument(outputDocument!.RootElement);
                if (!outputDetection.IsSuccessful ||
                    outputDetection.Family != family ||
                    !Equals(outputDetection.Version, step.ToVersion) ||
                    !LedgerCoversAllChanges(currentJson, stepResult.OutputJson, stepResult.Changes))
                {
                    return Failure("MIGRATION_CHANGE_LEDGER_INCOMPLETE");
                }
            }

            ledger.AddRange(stepResult.Changes);
            currentJson = stepResult.OutputJson;
            currentVersion = step.ToVersion;
        }

        MigrationFinalizationResult finalization = _finalize(family, currentJson);
        return finalization.IsSuccessful && finalization.Document is not null
            ? Result(
                MigrationStatus.SuccessfullyMigrated,
                family,
                sourceVersion,
                targetVersion,
                originalJson,
                finalization.Document,
                ledger,
                "MIGRATION_SUCCEEDED")
            : Failure(
                DiagnosticOrFallback(
                    finalization.DiagnosticCode,
                    "MIGRATION_FINAL_DOCUMENT_INVALID"));

        MigrationResult Failure(string code) =>
            Result(
                MigrationStatus.MigrationFailure,
                family,
                sourceVersion,
                targetVersion,
                originalJson,
                null,
                ledger,
                code);
    }

    private MigrationResult RegistryFailure(
        MigrationDocumentFamily family,
        SchemaVersion sourceVersion,
        SchemaVersion targetVersion,
        string originalJson)
    {
        MigrationStatus status = _registryResult.Error switch
        {
            MigrationRegistryError.AmbiguousPath => MigrationStatus.AmbiguousMigrationPath,
            MigrationRegistryError.Gap => MigrationStatus.MissingMigrationStep,
            _ => MigrationStatus.MigrationFailure,
        };
        return Result(
            status,
            family,
            sourceVersion,
            targetVersion,
            originalJson,
            null,
            null,
            $"MIGRATION_REGISTRY_{_registryResult.Error.ToString().ToUpperInvariant()}");
    }

    private static DetectionResult Detect(string? json)
    {
        JsonInputError error = JsonInputSafeguards.TryParseObject(
            json,
            JsonInputSafeguards.MaximumInputCharacters,
            out JsonDocument? document);
        if (error != JsonInputError.None)
        {
            return DetectionResult.Failure($"MIGRATION_JSON_{error.ToString().ToUpperInvariant()}");
        }

        using (document!)
        {
            return DetectDocument(document!.RootElement);
        }
    }

    private static DetectionResult DetectDocument(JsonElement root)
    {
        MigrationDocumentFamily? family = DetectFamily(root);
        if (family is null)
        {
            return DetectionResult.Failure("MIGRATION_DOCUMENT_FAMILY_UNKNOWN");
        }

        if (!root.TryGetProperty("schemaVersion", out JsonElement versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt64(out long versionValue))
        {
            return DetectionResult.Failure("MIGRATION_SCHEMA_VERSION_INVALID", family);
        }

        SchemaVersionResult version = SchemaVersion.Create(versionValue);
        return version.IsValid
            ? DetectionResult.Success(family.Value, version.Value!)
            : DetectionResult.Failure("MIGRATION_SCHEMA_VERSION_INVALID", family);
    }

    private static MigrationDocumentFamily? DetectFamily(JsonElement root)
    {
        bool product = HasAny(
            root,
            "publisherProfileReference",
            "product",
            "targets",
            "sourceAssets",
            "materials",
            "animations");
        bool publisher = HasAny(
            root,
            "root",
            "displayName",
            "supportContact",
            "copyright",
            "aiDisclosure",
            "branding");
        bool marketplace = HasAny(root, "marketplace", "profile");
        int count = (product ? 1 : 0) + (publisher ? 1 : 0) + (marketplace ? 1 : 0);
        return count != 1
            ? null
            : product
                ? MigrationDocumentFamily.ProductManifest
                : publisher
                    ? MigrationDocumentFamily.PublisherProfile
                    : MigrationDocumentFamily.MarketplaceProfile;
    }

    private static bool HasAny(JsonElement root, params string[] propertyNames) =>
        propertyNames.Any(name => root.TryGetProperty(name, out _));

    private static bool LedgerCoversAllChanges(
        string beforeJson,
        string afterJson,
        IReadOnlyCollection<MigrationChange> ledger)
    {
        JsonNode before = JsonNode.Parse(beforeJson)!;
        JsonNode after = JsonNode.Parse(afterJson)!;
        Dictionary<string, string> beforeLeaves = Flatten(before);
        Dictionary<string, string> afterLeaves = Flatten(after);

        foreach (KeyValuePair<string, string> leaf in beforeLeaves)
        {
            if (!afterLeaves.TryGetValue(leaf.Key, out string? afterValue))
            {
                if (!ledger.Any(
                        change =>
                            string.Equals(
                                change.SourcePointer,
                                leaf.Key,
                                StringComparison.Ordinal) &&
                            change.Kind is MigrationChangeKind.Removal
                                or MigrationChangeKind.Rename
                                or MigrationChangeKind.Conversion))
                {
                    return false;
                }
            }
            else if (!string.Equals(leaf.Value, afterValue, StringComparison.Ordinal) &&
                     !ledger.Any(
                         change =>
                             change.Kind == MigrationChangeKind.Conversion &&
                             string.Equals(
                                 change.SourcePointer,
                                 leaf.Key,
                                 StringComparison.Ordinal) &&
                             string.Equals(
                                 change.TargetPointer,
                                 leaf.Key,
                                 StringComparison.Ordinal)))
            {
                return false;
            }
        }

        foreach (string pointer in afterLeaves.Keys.Except(beforeLeaves.Keys, StringComparer.Ordinal))
        {
            if (!ledger.Any(
                    change =>
                        string.Equals(change.TargetPointer, pointer, StringComparison.Ordinal) &&
                        change.Kind is MigrationChangeKind.Addition
                            or MigrationChangeKind.Default
                            or MigrationChangeKind.Rename
                            or MigrationChangeKind.Conversion))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> Flatten(JsonNode node)
    {
        var leaves = new Dictionary<string, string>(StringComparer.Ordinal);
        Visit(node, string.Empty, leaves);
        return leaves;
    }

    private static void Visit(
        JsonNode? node,
        string pointer,
        Dictionary<string, string> leaves)
    {
        if (node is JsonObject objectNode)
        {
            leaves[pointer] = "$object";
            foreach (KeyValuePair<string, JsonNode?> property in objectNode)
            {
                Visit(
                    property.Value,
                    $"{pointer}/{EscapePointer(property.Key)}",
                    leaves);
            }
        }
        else if (node is JsonArray arrayNode)
        {
            leaves[pointer] = "$array";
            for (int index = 0; index < arrayNode.Count; index++)
            {
                Visit(arrayNode[index], $"{pointer}/{index}", leaves);
            }
        }
        else
        {
            leaves[pointer] = $"$value:{node?.ToJsonString() ?? "null"}";
        }
    }

    private static string EscapePointer(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string DiagnosticOrFallback(string? candidate, string fallback) =>
        candidate is not null && FindingCode.Create(candidate).IsValid
            ? candidate
            : fallback;

    private static MigrationResult Result(
        MigrationStatus status,
        MigrationDocumentFamily? family,
        SchemaVersion? source,
        SchemaVersion? target,
        string? original,
        MigrationFinalDocument? finalDocument,
        IEnumerable<MigrationChange>? changes,
        string code) =>
        new(status, family, source, target, original, finalDocument, changes, code);

    private sealed class DetectionResult
    {
        private DetectionResult(
            MigrationDocumentFamily? family,
            SchemaVersion? version,
            string diagnosticCode)
        {
            Family = family;
            Version = version;
            DiagnosticCode = diagnosticCode;
        }

        public bool IsSuccessful => Family.HasValue && Version is not null;

        public MigrationDocumentFamily? Family { get; }

        public SchemaVersion? Version { get; }

        public string DiagnosticCode { get; }

        public static DetectionResult Success(
            MigrationDocumentFamily family,
            SchemaVersion version) =>
            new(family, version, string.Empty);

        public static DetectionResult Failure(
            string diagnosticCode,
            MigrationDocumentFamily? family = null) =>
            new(family, null, diagnosticCode);
    }
}

/// <summary>
/// Production manifest/profile migration boundary. Public production history begins at version 1,
/// so the production registry is intentionally empty until a later public schema is approved.
/// </summary>
public static class ManifestProfileMigration
{
    private static readonly SchemaVersion _versionOne = SchemaVersion.Create(1).Value!;
    private static readonly MigrationRegistryResult _registry = MigrationRegistry.Create(
        [],
        new Dictionary<MigrationDocumentFamily, SchemaVersion>
        {
            [MigrationDocumentFamily.ProductManifest] = _versionOne,
            [MigrationDocumentFamily.PublisherProfile] = _versionOne,
            [MigrationDocumentFamily.MarketplaceProfile] = _versionOne,
        });
    private static readonly ManifestProfileMigrationEngine _engine =
        new(_registry, FinalizeCurrent);
    private static readonly Func<string, MigrationFinalizationResult>[] _finalizers =
    [
        FinalizeProduct,
        FinalizePublisher,
        FinalizeMarketplace,
    ];

    public static MigrationResult Inspect(string? json) => _engine.Inspect(json);

    public static MigrationResult Migrate(string? json) => _engine.Migrate(json);

    private static MigrationFinalizationResult FinalizeCurrent(
        MigrationDocumentFamily family,
        string json) =>
        _finalizers[(int)family](json);

    private static MigrationFinalizationResult FinalizeProduct(string json)
    {
        ProductManifestDeserializationResult parsed = ProductManifestJson.Deserialize(json);
        if (!parsed.IsSuccessful)
        {
            return MigrationFinalizationResult.Failure("MIGRATION_FINAL_SCHEMA_OR_DOMAIN_INVALID");
        }

        ProductManifestSerializationResult serialized =
            ProductManifestJson.Serialize(parsed.Value);
        return MigrationFinalizationResult.Success(
            MigrationFinalDocument.Product(serialized.Json!, parsed.Value!));
    }

    private static MigrationFinalizationResult FinalizePublisher(string json)
    {
        ProfileJsonDeserializationResult<Domain.Profiles.PublisherProfile> parsed =
            PublisherProfileJson.Deserialize(json);
        if (!parsed.IsSuccessful)
        {
            return MigrationFinalizationResult.Failure("MIGRATION_FINAL_SCHEMA_OR_DOMAIN_INVALID");
        }

        ProfileJsonSerializationResult serialized =
            PublisherProfileJson.Serialize(parsed.Value);
        return MigrationFinalizationResult.Success(
            MigrationFinalDocument.Publisher(serialized.Json!, parsed.Value!));
    }

    private static MigrationFinalizationResult FinalizeMarketplace(string json)
    {
        ProfileJsonDeserializationResult<Domain.Profiles.MarketplaceProfile> parsed =
            MarketplaceProfileJson.Deserialize(json);
        if (!parsed.IsSuccessful)
        {
            return MigrationFinalizationResult.Failure("MIGRATION_FINAL_SCHEMA_OR_DOMAIN_INVALID");
        }

        ProfileJsonSerializationResult serialized =
            MarketplaceProfileJson.Serialize(parsed.Value);
        return MigrationFinalizationResult.Success(
            MigrationFinalDocument.Marketplace(serialized.Json!, parsed.Value!));
    }
}
