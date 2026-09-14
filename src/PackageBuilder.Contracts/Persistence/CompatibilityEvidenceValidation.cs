namespace PackageBuilder.Contracts.Persistence;

/// <summary>Shared engine/profile test-evidence invariants; references are logical, never machine paths.</summary>
public static class CompatibilityEvidenceValidation
{
    public static bool IsValid(PersistedCompatibilitySuiteResult? result, CompatibilitySuiteOutcome outcome) =>
        result is not null && Enum.IsDefined(outcome) && result.Outcome == outcome
        && IsText(result.RunId, 512) && result.TotalTests > 0
        && result.PassedTests >= 0 && result.FailedTests >= 0
        && (long)result.PassedTests + result.FailedTests == result.TotalTests
        && (outcome == CompatibilitySuiteOutcome.Passed
            ? result.FailedTests == 0 && result.PassedTests == result.TotalTests : result.FailedTests > 0)
        && IsReference(result.EvidenceReference) && IsSha256(result.EvidenceSha256)
        && result.CompletedAtUtc.Offset == TimeSpan.Zero;

    public static bool IsSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength
        && !char.IsWhiteSpace(value[0]) && !char.IsWhiteSpace(value[^1]) && !value.Any(char.IsControl);

    private static bool IsReference(string? value) => IsText(value, 4096)
        && value![0] != '/' && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains(':', StringComparison.Ordinal) && value.Split('/').All(segment =>
            segment.Length > 0 && segment is not "." and not ".."
            && !char.IsWhiteSpace(segment[0]) && !char.IsWhiteSpace(segment[^1]));
}
