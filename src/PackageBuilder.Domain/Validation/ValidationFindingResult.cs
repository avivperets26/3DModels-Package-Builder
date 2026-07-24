namespace PackageBuilder.Domain.Validation;

/// <summary>Identifies expected validation-finding input failures.</summary>
public enum ValidationFindingError
{
    None = 0,
    NullCode,
    EmptyCode,
    WhitespaceOnlyCode,
    MalformedCode,
    NullSeverity,
    EmptySeverityToken,
    WhitespaceOnlySeverityToken,
    UnknownSeverityToken,
    NullSource,
    EmptySource,
    WhitespaceOnlySource,
    MalformedSource,
    NullExplanation,
    EmptyExplanation,
    WhitespaceOnlyExplanation,
    ExplanationEdgeWhitespace,
    ExplanationContainsControlCharacter,
    NullCorrectiveAction,
    EmptyCorrectiveAction,
    WhitespaceOnlyCorrectiveAction,
    CorrectiveActionEdgeWhitespace,
    CorrectiveActionContainsControlCharacter,
}

/// <summary>Represents a structured, non-throwing PB-0109 creation or parse outcome.</summary>
public sealed class ValidationFindingResult<T>
    where T : class
{
    private ValidationFindingResult(bool isValid, T? value, ValidationFindingError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public T? Value { get; }

    public ValidationFindingError Error { get; }

    internal static ValidationFindingResult<T> Success(T value) =>
        new(true, value, ValidationFindingError.None);

    internal static ValidationFindingResult<T> Failure(ValidationFindingError error) =>
        new(false, null, error);
}
