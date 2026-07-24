using PackageBuilder.Domain.BuildJobs;

namespace PackageBuilder.Domain.Validation;

/// <summary>
/// Immutable validation result. Every severity may be blocking or non-blocking because release
/// policy is explicit and independent from diagnostic severity. A related artifact is optional
/// and identifies an existing logical build artifact; it is not a filename or filesystem path.
/// </summary>
public sealed class ValidationFinding : IEquatable<ValidationFinding>
{
    private ValidationFinding(
        FindingCode code,
        FindingSeverity severity,
        FindingExplanation explanation,
        FindingSourceComponent source,
        BuildArtifactId? relatedArtifactId,
        CorrectiveAction? suggestedAction,
        bool blocksRelease)
    {
        Code = code;
        Severity = severity;
        Explanation = explanation;
        Source = source;
        RelatedArtifactId = relatedArtifactId;
        SuggestedAction = suggestedAction;
        BlocksRelease = blocksRelease;
    }

    public FindingCode Code { get; }

    public FindingSeverity Severity { get; }

    public FindingExplanation Explanation { get; }

    public FindingSourceComponent Source { get; }

    public BuildArtifactId? RelatedArtifactId { get; }

    public CorrectiveAction? SuggestedAction { get; }

    public bool BlocksRelease { get; }

    public static ValidationFindingResult<ValidationFinding> Create(
        FindingCode? code,
        FindingSeverity? severity,
        FindingExplanation? explanation,
        FindingSourceComponent? source,
        BuildArtifactId? relatedArtifactId,
        CorrectiveAction? suggestedAction,
        bool blocksRelease)
    {
        ValidationFindingError error = code is null
            ? ValidationFindingError.NullCode
            : severity is null
            ? ValidationFindingError.NullSeverity
            : explanation is null
            ? ValidationFindingError.NullExplanation
            : source is null
            ? ValidationFindingError.NullSource
            : ValidationFindingError.None;
        return error == ValidationFindingError.None
            ? ValidationFindingResult<ValidationFinding>.Success(
                new ValidationFinding(
                    code!,
                    severity!,
                    explanation!,
                    source!,
                    relatedArtifactId,
                    suggestedAction,
                    blocksRelease))
            : ValidationFindingResult<ValidationFinding>.Failure(error);
    }

    public bool Equals(ValidationFinding? other) =>
        other is not null &&
        Code.Equals(other.Code) &&
        Severity.Equals(other.Severity) &&
        Explanation.Equals(other.Explanation) &&
        Source.Equals(other.Source) &&
        Equals(RelatedArtifactId, other.RelatedArtifactId) &&
        Equals(SuggestedAction, other.SuggestedAction) &&
        BlocksRelease == other.BlocksRelease;

    public override bool Equals(object? obj) => obj is ValidationFinding other && Equals(other);

    public override int GetHashCode() =>
        StableFindingHash.Create()
            .Add(Code.Value)
            .Add(Severity.SerializedToken)
            .Add(Explanation.Value)
            .Add(Source.Value)
            .Add(RelatedArtifactId?.Value ?? string.Empty)
            .Add(SuggestedAction?.Value ?? string.Empty)
            .Add(BlocksRelease)
            .ToHashCode();
}
