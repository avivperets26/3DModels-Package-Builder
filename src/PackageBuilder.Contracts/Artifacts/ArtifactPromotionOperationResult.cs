using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Describes one expected atomic-promotion validation, integrity, state, or I/O failure.</summary>
public sealed record ArtifactPromotionFailure(string Code, string Location, string Diagnostic);

/// <summary>Represents either one promotion receipt or structured expected failures.</summary>
public sealed class ArtifactPromotionOperationResult
{
    internal ArtifactPromotionOperationResult(
        ArtifactPromotionReceipt? value,
        IEnumerable<ArtifactPromotionFailure> failures)
    {
        Value = value;
        Failures = new ReadOnlyCollection<ArtifactPromotionFailure>(failures.ToArray());
    }

    public bool IsSuccess => Value is not null;

    public ArtifactPromotionReceipt? Value { get; }

    public IReadOnlyList<ArtifactPromotionFailure> Failures { get; }
}

/// <summary>Creates promotion results without throwing for expected failures.</summary>
public static class ArtifactPromotionResult
{
    public static ArtifactPromotionOperationResult Success(ArtifactPromotionReceipt value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)), []);

    public static ArtifactPromotionOperationResult Failed(params ArtifactPromotionFailure[] failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Length == 0 || failures.Any(static failure => failure is null)
            ? throw new ArgumentException("At least one non-null failure is required.", nameof(failures))
            : new ArtifactPromotionOperationResult(null, failures);
    }
}
