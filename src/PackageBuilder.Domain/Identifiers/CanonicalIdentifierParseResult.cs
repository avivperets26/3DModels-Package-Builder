namespace PackageBuilder.Domain.Identifiers;

/// <summary>Identifies why a canonical domain identifier could not be parsed.</summary>
public enum CanonicalIdentifierParseError
{
    /// <summary>The identifier is valid and supported.</summary>
    None = 0,

    /// <summary>The input was null.</summary>
    Null,

    /// <summary>The input was empty.</summary>
    Empty,

    /// <summary>The input contained only whitespace.</summary>
    WhitespaceOnly,

    /// <summary>The input did not use lowercase ASCII words separated by single hyphens.</summary>
    Malformed,

    /// <summary>The input was well formed but is not a supported identifier.</summary>
    Unknown,
}

/// <summary>Represents the expected success or failure of parsing a canonical identifier.</summary>
/// <typeparam name="T">The closed immutable domain value produced on success.</typeparam>
public sealed class CanonicalIdentifierParseResult<T>
    where T : class
{
    private CanonicalIdentifierParseResult(
        bool isValid,
        T? value,
        CanonicalIdentifierParseError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether parsing succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the parsed value, or <see langword="null"/> when parsing failed.</summary>
    public T? Value { get; }

    /// <summary>Gets <see cref="CanonicalIdentifierParseError.None"/> on success or the rejection reason.</summary>
    public CanonicalIdentifierParseError Error { get; }

    internal static CanonicalIdentifierParseResult<T> Success(T value) =>
        new(true, value, CanonicalIdentifierParseError.None);

    internal static CanonicalIdentifierParseResult<T> Failure(
        CanonicalIdentifierParseError error) =>
        new(false, null, error);
}
