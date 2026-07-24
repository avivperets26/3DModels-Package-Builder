using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.BuildJobs;

/// <summary>
/// Retains deterministic logical references for a completed step. Hash values and validation
/// findings are deliberately deferred to PB-0204 and PB-0109 respectively.
/// </summary>
public sealed class BuildStepCompletionMetadata : IEquatable<BuildStepCompletionMetadata>
{
    private BuildStepCompletionMetadata(
        ReadOnlyCollection<string> inputReferences,
        ReadOnlyCollection<string> outputReferences,
        ReadOnlyCollection<string> toolVersionReferences,
        ReadOnlyCollection<string> logReferences)
    {
        InputReferences = inputReferences;
        OutputReferences = outputReferences;
        ToolVersionReferences = toolVersionReferences;
        LogReferences = logReferences;
    }

    public IReadOnlyList<string> InputReferences { get; }

    public IReadOnlyList<string> OutputReferences { get; }

    public IReadOnlyList<string> ToolVersionReferences { get; }

    public IReadOnlyList<string> LogReferences { get; }

    public static BuildModelValidationResult<BuildStepCompletionMetadata> Create(
        IEnumerable<string?>? inputReferences,
        IEnumerable<string?>? outputReferences,
        IEnumerable<string?>? toolVersionReferences,
        IEnumerable<string?>? logReferences)
    {
        BuildModelValidationResult<ReadOnlyCollection<string>> inputs =
            BuildValueValidator.SnapshotReferences(inputReferences);
        if (!inputs.IsValid)
        {
            return Failure(inputs.Error);
        }

        BuildModelValidationResult<ReadOnlyCollection<string>> outputs =
            BuildValueValidator.SnapshotReferences(outputReferences);
        if (!outputs.IsValid)
        {
            return Failure(outputs.Error);
        }

        BuildModelValidationResult<ReadOnlyCollection<string>> tools =
            BuildValueValidator.SnapshotReferences(toolVersionReferences);
        if (!tools.IsValid)
        {
            return Failure(tools.Error);
        }

        BuildModelValidationResult<ReadOnlyCollection<string>> logs =
            BuildValueValidator.SnapshotReferences(logReferences);
        return !logs.IsValid
            ? Failure(logs.Error)
            : BuildModelValidationResult<BuildStepCompletionMetadata>.Success(
                new BuildStepCompletionMetadata(
                    inputs.Value!,
                    outputs.Value!,
                    tools.Value!,
                    logs.Value!));
    }

    public bool Equals(BuildStepCompletionMetadata? other) =>
        other is not null &&
        InputReferences.SequenceEqual(other.InputReferences) &&
        OutputReferences.SequenceEqual(other.OutputReferences) &&
        ToolVersionReferences.SequenceEqual(other.ToolVersionReferences) &&
        LogReferences.SequenceEqual(other.LogReferences);

    public override bool Equals(object? obj) =>
        obj is BuildStepCompletionMetadata other && Equals(other);

    public override int GetHashCode()
    {
        var hash = StableBuildHash.Create();
        foreach (string reference in InputReferences)
        {
            hash = hash.Add(reference);
        }

        foreach (string reference in OutputReferences)
        {
            hash = hash.Add(reference);
        }

        foreach (string reference in ToolVersionReferences)
        {
            hash = hash.Add(reference);
        }

        foreach (string reference in LogReferences)
        {
            hash = hash.Add(reference);
        }

        return hash.ToHashCode();
    }

    private static BuildModelValidationResult<BuildStepCompletionMetadata> Failure(
        BuildModelValidationError error) =>
        BuildModelValidationResult<BuildStepCompletionMetadata>.Failure(error);
}
