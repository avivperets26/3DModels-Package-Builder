using System.Collections.Immutable;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Trusted orchestration identity. Item keys come from the selected product manifest.</summary>
public sealed record FabValidationContext(BuildJobId JobId, string ProductKey,
    FabListingConfiguration Listing, ImmutableArray<string> ItemKeys);

/// <summary>Completed upstream validation of exact bytes, produced by trusted target orchestration,
/// never deserialized from a supplier's pass flag. Missing, failed or stale evidence cannot pass.</summary>
public sealed record FabTargetEvidence(BuildJobId JobId, BuildArtifactId ArtifactId,
    ArtifactContentIdentity Content, string ProductKey, string Stage, bool Completed,
    ImmutableArray<ValidationFinding> Findings);

/// <summary>Findings carry ordinary report-compatible codes/actions; the result also pins the rules used.</summary>
public sealed record FabArtifactValidation(string ProfileSha256, ImmutableArray<ValidationFinding> Findings)
{
    /// <summary>Only release-blocking findings fail this adapter result; other release gates remain separate.</summary>
    public bool Passed => !Findings.Any(finding => finding.BlocksRelease);
}

/// <summary>Shared Fab rule evaluation and deterministic finding construction, independent of engine APIs.</summary>
internal sealed class FabValidation(FabRequirementsProfile profile)
{
    private readonly List<ValidationFinding> _findings = [];

    /// <summary>Preserves all findings from validators invoked within this composition operation.</summary>
    internal void Include(FabArtifactValidation result) => _findings.AddRange(result.Findings);

    /// <summary>Accumulates a blocking finding using the existing report contract.</summary>
    internal void Add(string code, string message, string action, BuildArtifactId? artifact = null) =>
        _findings.Add(ValidationFinding.Create(FindingCode.Create(code).Value, FindingSeverity.Error,
            FindingExplanation.Create(message).Value, FindingSourceComponent.Create("fab-validator").Value,
            artifact, CorrectiveAction.Create(action).Value, true).Value!);

    /// <summary>Rejects incomplete manifest identities and listings the shared resolver cannot support.</summary>
    internal bool Context(FabValidationContext? context)
    {
        if (context?.JobId is null || string.IsNullOrWhiteSpace(context.ProductKey)
            || context.ProductKey.Length > 128 || context.ItemKeys.IsDefaultOrEmpty
            || context.ItemKeys.Length > 10_000 || context.ItemKeys.Any(string.IsNullOrWhiteSpace)
            || context.ItemKeys.Distinct(StringComparer.Ordinal).Count() != context.ItemKeys.Length
            || !FabRequiredTargetResolver.Resolve(profile, context.Listing).IsSuccess)
        {
            Add("FAB_CONTEXT_INVALID", "The listing or product identity is incomplete.", "Supply the selected manifest and listing configuration.");
            return false;
        }
        return true;
    }

    /// <summary>Binds trusted upstream findings to this job, stage and exact artifact before forwarding them.</summary>
    internal bool Evidence(FabTargetEvidence? evidence, FabValidationContext context,
        BuildArtifactId artifact, ArtifactContentIdentity content, string stage)
    {
        if (evidence is null || !evidence.Completed || evidence.Findings.IsDefault
            || evidence.Findings.Any(finding => finding is null)
            || !Equals(evidence.JobId, context.JobId) || !Equals(evidence.ArtifactId, artifact)
            || !Equals(evidence.Content, content) || evidence.ProductKey != context.ProductKey || evidence.Stage != stage)
        {
            Add("FAB_EVIDENCE_INVALID", "Required target evidence is missing, incomplete or belongs to different content.",
                "Run the required target validation on the exact artifact in this job.", artifact);
            return false;
        }
        _findings.AddRange(evidence.Findings);
        return !evidence.Findings.Any(finding => finding.BlocksRelease);
    }

    /// <summary>Applies the pinned profile's numeric comparison; missing or unresolved bounds fail closed.</summary>
    internal bool Bound(string id, string section, string unit, long measured, BuildArtifactId? artifact = null)
    {
        FabRequirementRule? rule = profile.Document.Rules.SingleOrDefault(value => value.Id == id);
        if (rule?.Status != "verified" || rule.Section != section || rule.Unit != unit || rule.Limit is null)
        {
            Add("FAB_RULE_REVIEW_REQUIRED", $"Required rule {id} has no verified numeric bound.",
                "Review and approve a profile with a verified bound before release.", artifact);
            return false;
        }
        bool passed = measured >= 0 && rule.Comparison switch
        {
            "lt" => measured < rule.Limit.Value,
            "lte" => measured <= rule.Limit.Value,
            "gte" => measured >= rule.Limit.Value,
            _ => false,
        };
        if (!passed)
        {
            Add("FAB_LIMIT_EXCEEDED", $"The artifact does not satisfy profile rule {id}.",
                "Adjust the artifact to the selected profile limit.", artifact);
        }
        return passed;
    }

    /// <summary>Preserves unresolved applicable requirements as explicit release blockers.</summary>
    internal void Review(IReadOnlyCollection<string> sections, string? excludedRule = null)
    {
        foreach (FabRequirementRule rule in profile.Document.Rules.Where(rule =>
            rule.Status == "unresolved" && rule.Id != excludedRule && sections.Contains(rule.Section, StringComparer.Ordinal)))
        {
            Add("FAB_RULE_REVIEW_REQUIRED", $"Rule {rule.Id} requires explicit review.",
                "Clarify the sourced requirement and approve a new profile revision.");
        }
    }

    /// <summary>Requires an explicitly verified capability in the selected profile.</summary>
    internal void RequireRule(string id, string section)
    {
        if (!profile.Document.Rules.Any(rule => rule.Id == id && rule.Section == section && rule.Status == "verified"))
        { Add("FAB_RULE_REVIEW_REQUIRED", $"Required rule {id} is unavailable.", "Approve the required rule in the selected profile."); }
    }

    /// <summary>Returns deduplicated findings in stable order with the exact profile hash.</summary>
    internal FabArtifactValidation Result() => new(profile.Sha256,
        [.. _findings.Distinct().OrderBy(finding => finding.Code.Value, StringComparer.Ordinal)
            .ThenBy(finding => finding.RelatedArtifactId?.Value, StringComparer.Ordinal)
            .ThenBy(finding => finding.Explanation.Value, StringComparer.Ordinal)]);
}
