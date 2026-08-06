using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies an expected PB-0507 report-input failure.</summary>
public enum PortableStaticValidationReportError
{
    None = 0,
    NullJobId,
    NullManifest,
    NonStaticManifest,
    PortableTargetMissing,
    NullBuildLock,
    BuildLockJobMismatch,
    InvalidBuildLock,
    NullArchiveReceipt,
    NullValidationReport,
    InvalidLogReference,
    InvalidFinding,
}

/// <summary>
/// Contains the deterministic PB-0507 JSON receipt. PB-0910 remains responsible for the complete
/// cross-target validation-report schema.
/// </summary>
public sealed class PortableStaticValidationReportDocument
{
    internal PortableStaticValidationReportDocument(string json)
    {
        Json = json;
        Utf8Bytes = new ReadOnlyCollection<byte>(Encoding.UTF8.GetBytes(json));
    }

    public string Json { get; }

    public IReadOnlyList<byte> Utf8Bytes { get; }
}

/// <summary>Returns either one PB-0507 report document or one stable expected failure.</summary>
public sealed class PortableStaticValidationReportResult
{
    private PortableStaticValidationReportResult(
        PortableStaticValidationReportDocument? value,
        PortableStaticValidationReportError error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error == PortableStaticValidationReportError.None;

    public PortableStaticValidationReportDocument? Value { get; }

    public PortableStaticValidationReportError Error { get; }

    internal static PortableStaticValidationReportResult Success(
        PortableStaticValidationReportDocument value) => new(value, PortableStaticValidationReportError.None);

    internal static PortableStaticValidationReportResult Failure(
        PortableStaticValidationReportError error) => new(null, error);
}

/// <summary>Serializes the exact static portable target decision and reproducibility inputs.</summary>
public static class PortableStaticValidationReportJson
{
    public const int SchemaVersion = 1;

    /// <summary>
    /// Produces compact UTF-8 JSON with stable property and finding order. The log reference is a
    /// project-relative logical reference and is never resolved or opened here.
    /// </summary>
    public static PortableStaticValidationReportResult Serialize(
        BuildJobId? jobId,
        ProductManifest? manifest,
        BuildLock? buildLock,
        PortableFbxArchiveReceipt? archiveReceipt,
        PortableTargetValidationReport? validationReport,
        string? logReference)
    {
        PortableStaticValidationReportError inputError = ValidateInputs(
            jobId,
            manifest,
            buildLock,
            archiveReceipt,
            validationReport,
            logReference,
            out string? buildLockJson);
        if (inputError != PortableStaticValidationReportError.None)
        {
            return PortableStaticValidationReportResult.Failure(inputError);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        using (var lockDocument = JsonDocument.Parse(buildLockJson!))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("jobId", jobId!.Value);
            writer.WriteString("productId", manifest!.AssetId.Value);
            writer.WriteString("productVersion", manifest.Version.Value);
            writer.WriteString("productCase", manifest.ProductCase.CanonicalIdentifier);
            writer.WriteString("target", "portable");
            writer.WriteString("status", validationReport!.Passed ? "passed" : "failed");
            writer.WritePropertyName("buildLock");
            lockDocument.RootElement.WriteTo(writer);
            writer.WriteStartObject("releaseArtifact");
            writer.WriteString("fileName", archiveReceipt!.ArchiveFileName);
            writer.WriteNumber("bytes", archiveReceipt.ArchiveIdentity.Bytes);
            writer.WriteString("sha256", archiveReceipt.ArchiveIdentity.Sha256.Value);
            writer.WriteString("logicalSha256", archiveReceipt.LogicalIdentity.Value);
            writer.WriteEndObject();
            writer.WriteString("logReference", logReference);
            writer.WriteStartArray("findings");
            foreach (ValidationFinding finding in validationReport.Findings
                         .OrderBy(static value => value.Code.Value, StringComparer.Ordinal)
                         .ThenBy(static value => value.Source.Value, StringComparer.Ordinal)
                         .ThenBy(static value => value.Explanation.Value, StringComparer.Ordinal))
            {
                ValidationFindingSerializationResult serialized = ValidationFindingJson.Serialize(finding);
                using var findingDocument = JsonDocument.Parse(serialized.Json!);
                findingDocument.RootElement.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return PortableStaticValidationReportResult.Success(
            new PortableStaticValidationReportDocument(Encoding.UTF8.GetString(buffer.WrittenSpan)));
    }

    private static PortableStaticValidationReportError ValidateInputs(
        BuildJobId? jobId,
        ProductManifest? manifest,
        BuildLock? buildLock,
        PortableFbxArchiveReceipt? archiveReceipt,
        PortableTargetValidationReport? validationReport,
        string? logReference,
        out string? buildLockJson)
    {
        buildLockJson = null;
        if (jobId is null)
        {
            return PortableStaticValidationReportError.NullJobId;
        }

        if (manifest is null)
        {
            return PortableStaticValidationReportError.NullManifest;
        }

        if (!manifest.ProductCase.Equals(ProductCase.Static))
        {
            return PortableStaticValidationReportError.NonStaticManifest;
        }

        if (!manifest.Targets.Any(static target =>
                target.Equals(PackageBuilder.Domain.Targets.BuildTarget.Portable)))
        {
            return PortableStaticValidationReportError.PortableTargetMissing;
        }

        if (buildLock is null)
        {
            return PortableStaticValidationReportError.NullBuildLock;
        }

        if (!buildLock.JobId.Equals(jobId))
        {
            return PortableStaticValidationReportError.BuildLockJobMismatch;
        }

        BuildLockJsonResult serializedLock = BuildLockJson.Serialize(buildLock);
        if (!serializedLock.IsSuccessful)
        {
            return PortableStaticValidationReportError.InvalidBuildLock;
        }

        if (archiveReceipt is null)
        {
            return PortableStaticValidationReportError.NullArchiveReceipt;
        }

        if (validationReport is null)
        {
            return PortableStaticValidationReportError.NullValidationReport;
        }

        if (!IsLogicalReference(logReference))
        {
            return PortableStaticValidationReportError.InvalidLogReference;
        }

        if (validationReport.Findings.Any(static finding =>
                !ValidationFindingJson.Serialize(finding).IsSuccessful))
        {
            return PortableStaticValidationReportError.InvalidFinding;
        }

        buildLockJson = serializedLock.Json;
        return PortableStaticValidationReportError.None;
    }

    private static bool IsLogicalReference(string? value)
    {
        if (value is null or { Length: 0 or > 1024 } || value[0] == '/' || value[^1] == '/' ||
            value.Contains('\\', StringComparison.Ordinal) || value.Contains(':', StringComparison.Ordinal) ||
            value.Any(char.IsControl))
        {
            return false;
        }

        string[] segments = value.Split('/');
        return segments.All(static segment =>
            segment.Length > 0 && segment != "." && segment != ".." &&
            !char.IsWhiteSpace(segment[0]) && !char.IsWhiteSpace(segment[^1]));
    }
}
