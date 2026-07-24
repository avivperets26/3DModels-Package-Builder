using System.Buffers;
using System.Text;
using System.Text.Json;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Validation;

/// <summary>
/// Stable PB-0109 JSON representation. Property names, severity tokens, order, and omission of
/// absent optional values are compatibility commitments and may change only through an explicitly
/// versioned migration. This type is a finding contract, not the PB-0910 report schema.
/// </summary>
public static class ValidationFindingJson
{
    private static readonly HashSet<string> _knownProperties =
        new(StringComparer.Ordinal)
        {
            "code",
            "severity",
            "explanation",
            "source",
            "relatedArtifactId",
            "suggestedAction",
            "blocksRelease",
        };

    public static ValidationFindingSerializationResult Serialize(
        PackageBuilder.Domain.Validation.ValidationFinding? finding)
    {
        if (finding is null)
        {
            return ValidationFindingSerializationResult.Failure(
                ValidationFindingJsonError.NullFinding);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("code", finding.Code.Value);
            writer.WriteString("severity", finding.Severity.SerializedToken);
            writer.WriteString("explanation", finding.Explanation.Value);
            writer.WriteString("source", finding.Source.Value);
            if (finding.RelatedArtifactId is not null)
            {
                writer.WriteString("relatedArtifactId", finding.RelatedArtifactId.Value);
            }

            if (finding.SuggestedAction is not null)
            {
                writer.WriteString("suggestedAction", finding.SuggestedAction.Value);
            }

            writer.WriteBoolean("blocksRelease", finding.BlocksRelease);
            writer.WriteEndObject();
        }

        return ValidationFindingSerializationResult.Success(
            Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    public static ValidationFindingDeserializationResult Deserialize(string? json)
    {
        if (json is null)
        {
            return Failure(ValidationFindingJsonError.NullJson);
        }

        if (json.Length == 0)
        {
            return Failure(ValidationFindingJsonError.EmptyJson);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return Failure(ValidationFindingJsonError.MalformedJson);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure(ValidationFindingJsonError.RootMustBeObject);
            }

            var encountered = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!_knownProperties.Contains(property.Name))
                {
                    return Failure(ValidationFindingJsonError.UnknownProperty);
                }

                if (!encountered.Add(property.Name))
                {
                    return Failure(ValidationFindingJsonError.DuplicateProperty);
                }
            }

            ValidationFindingDeserializationResult? requiredFailure = ReadRequiredString(
                root,
                "code",
                ValidationFindingJsonError.MissingCode,
                ValidationFindingJsonError.InvalidCode,
                out string? codeText);
            if (requiredFailure is not null)
            {
                return requiredFailure;
            }

            requiredFailure = ReadRequiredString(
                root,
                "severity",
                ValidationFindingJsonError.MissingSeverity,
                ValidationFindingJsonError.InvalidSeverity,
                out string? severityText);
            if (requiredFailure is not null)
            {
                return requiredFailure;
            }

            requiredFailure = ReadRequiredString(
                root,
                "explanation",
                ValidationFindingJsonError.MissingExplanation,
                ValidationFindingJsonError.InvalidExplanation,
                out string? explanationText);
            if (requiredFailure is not null)
            {
                return requiredFailure;
            }

            requiredFailure = ReadRequiredString(
                root,
                "source",
                ValidationFindingJsonError.MissingSource,
                ValidationFindingJsonError.InvalidSource,
                out string? sourceText);
            if (requiredFailure is not null)
            {
                return requiredFailure;
            }

            if (!root.TryGetProperty("blocksRelease", out JsonElement blockingElement))
            {
                return Failure(ValidationFindingJsonError.MissingBlocksRelease);
            }

            if (blockingElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                return Failure(ValidationFindingJsonError.InvalidBlocksRelease);
            }

            ValidationFindingResult<FindingCode> code = FindingCode.Create(codeText);
            if (!code.IsValid)
            {
                return Failure(
                    ValidationFindingJsonError.InvalidCode,
                    domainError: code.Error);
            }

            ValidationFindingResult<FindingSeverity> severity =
                FindingSeverity.ParseToken(severityText);
            if (!severity.IsValid)
            {
                return Failure(
                    ValidationFindingJsonError.InvalidSeverity,
                    domainError: severity.Error);
            }

            ValidationFindingResult<FindingExplanation> explanation =
                FindingExplanation.Create(explanationText);
            if (!explanation.IsValid)
            {
                return Failure(
                    ValidationFindingJsonError.InvalidExplanation,
                    domainError: explanation.Error);
            }

            ValidationFindingResult<FindingSourceComponent> source =
                FindingSourceComponent.Create(sourceText);
            if (!source.IsValid)
            {
                return Failure(
                    ValidationFindingJsonError.InvalidSource,
                    domainError: source.Error);
            }

            ValidationFindingDeserializationResult? optionalFailure = ReadOptionalString(
                root,
                "relatedArtifactId",
                ValidationFindingJsonError.InvalidRelatedArtifactId,
                out string? relatedArtifactText);
            if (optionalFailure is not null)
            {
                return optionalFailure;
            }

            optionalFailure = ReadOptionalString(
                root,
                "suggestedAction",
                ValidationFindingJsonError.InvalidSuggestedAction,
                out string? suggestedActionText);
            if (optionalFailure is not null)
            {
                return optionalFailure;
            }

            BuildArtifactId? relatedArtifactId = null;
            if (relatedArtifactText is not null)
            {
                BuildModelValidationResult<BuildArtifactId> artifact =
                    BuildArtifactId.Create(relatedArtifactText);
                if (!artifact.IsValid)
                {
                    return Failure(
                        ValidationFindingJsonError.InvalidRelatedArtifactId,
                        artifactIdError: artifact.Error);
                }

                relatedArtifactId = artifact.Value;
            }

            CorrectiveAction? suggestedAction = null;
            if (suggestedActionText is not null)
            {
                ValidationFindingResult<CorrectiveAction> action =
                    CorrectiveAction.Create(suggestedActionText);
                if (!action.IsValid)
                {
                    return Failure(
                        ValidationFindingJsonError.InvalidSuggestedAction,
                        domainError: action.Error);
                }

                suggestedAction = action.Value;
            }

            ValidationFindingResult<PackageBuilder.Domain.Validation.ValidationFinding> finding =
                PackageBuilder.Domain.Validation.ValidationFinding.Create(
                    code.Value,
                    severity.Value,
                    explanation.Value,
                    source.Value,
                    relatedArtifactId,
                    suggestedAction,
                    blockingElement.GetBoolean());
            return ValidationFindingDeserializationResult.Success(finding.Value!);
        }
    }

    private static ValidationFindingDeserializationResult? ReadRequiredString(
        JsonElement root,
        string propertyName,
        ValidationFindingJsonError missingError,
        ValidationFindingJsonError invalidError,
        out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return Failure(missingError);
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return Failure(invalidError);
        }

        value = element.GetString();
        return null;
    }

    private static ValidationFindingDeserializationResult? ReadOptionalString(
        JsonElement root,
        string propertyName,
        ValidationFindingJsonError invalidError,
        out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return Failure(invalidError);
        }

        value = element.GetString();
        return null;
    }

    private static ValidationFindingDeserializationResult Failure(
        ValidationFindingJsonError error,
        ValidationFindingError domainError = ValidationFindingError.None,
        BuildModelValidationError artifactIdError = BuildModelValidationError.None) =>
        ValidationFindingDeserializationResult.Failure(error, domainError, artifactIdError);
}
