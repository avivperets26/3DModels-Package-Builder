using System.Text.Json.Nodes;
using PackageBuilder.Contracts.Logging;
using PackageBuilder.Contracts.Validation;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Application.Diagnostics;

/// <summary>Produces one canonical shareable report for HTML and support export. Redacts decoded
/// JSON strings, then revalidates semantic references/status; unsafe identifiers fail closed.</summary>
internal static class DiagnosticReportProjection
{
    internal static BuildValidationReportResult Create(BuildValidationReport? report)
    {
        BuildValidationReportResult canonical = BuildValidationReportJson.Serialize(report);
        if (!canonical.IsSuccessful)
        { return canonical; }
        // Sanitize original prose before the compatibility log redactor can shorten a quoted secret.
        canonical = BuildValidationReportJson.Serialize(report! with
        {
            Findings = report!.Findings.Select(f => ValidationFinding.Create(f.Code, f.Severity,
                FindingExplanation.Create(SensitiveDiagnosticValueRedactor.RedactForExport("explanation", f.Explanation.Value)).Value,
                f.Source, f.RelatedArtifactId, f.SuggestedAction is null ? null :
                    CorrectiveAction.Create(SensitiveDiagnosticValueRedactor.RedactForExport("action", f.SuggestedAction.Value)).Value, f.BlocksRelease).Value!).ToArray(),
        });
        if (!canonical.IsSuccessful)
        { return canonical; }
        JsonNode root = JsonNode.Parse(canonical.Json!)!;
        Sanitize(root);
        return BuildValidationReportJson.Deserialize(root.ToJsonString());
    }

    private static void Sanitize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? child) in obj.ToArray())
            {
                if (child is JsonValue value && value.TryGetValue(out string? text))
                { obj[key] = SensitiveDiagnosticValueRedactor.RedactForExport(key, text); }
                else if (child is not null)
                { Sanitize(child); }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            { if (child is not null) { Sanitize(child); } }
        }
    }
}
