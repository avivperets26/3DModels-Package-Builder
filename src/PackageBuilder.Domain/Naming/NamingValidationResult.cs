namespace PackageBuilder.Domain.Naming;

/// <summary>Identifies why a product or texture naming value was rejected.</summary>
public enum NamingValidationError
{
    /// <summary>The value is valid.</summary>
    None = 0,

    /// <summary>The input was null.</summary>
    Null,

    /// <summary>The input was empty.</summary>
    Empty,

    /// <summary>The input contained only whitespace.</summary>
    WhitespaceOnly,

    /// <summary>The input began or ended with whitespace.</summary>
    LeadingOrTrailingWhitespace,

    /// <summary>The input contained a control character.</summary>
    ControlCharacter,

    /// <summary>The input used a rooted or drive-qualified path form.</summary>
    RootedPath,

    /// <summary>The input contained a current-directory or parent-directory segment.</summary>
    Traversal,

    /// <summary>The input contained a directory separator.</summary>
    DirectorySeparator,

    /// <summary>A filesystem name ended with a dot or space.</summary>
    TrailingDotOrSpace,

    /// <summary>A filesystem name matched a Windows-reserved device name.</summary>
    ReservedFileSystemName,

    /// <summary>The input contained a character that its naming grammar does not allow.</summary>
    InvalidCharacter,

    /// <summary>The texture naming token is not part of the currently supported canonical set.</summary>
    UnsupportedCanonicalToken,
}

/// <summary>Represents the expected success or failure of validating user-supplied naming input.</summary>
/// <typeparam name="T">The immutable naming value produced on success.</typeparam>
public sealed class NamingValidationResult<T>
    where T : class
{
    private NamingValidationResult(bool isValid, T? value, NamingValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the validated value, or <see langword="null"/> when validation failed.</summary>
    public T? Value { get; }

    /// <summary>Gets <see cref="NamingValidationError.None"/> on success or the rejection reason.</summary>
    public NamingValidationError Error { get; }

    internal static NamingValidationResult<T> Success(T value) =>
        new(true, value, NamingValidationError.None);

    internal static NamingValidationResult<T> Failure(NamingValidationError error) =>
        new(false, null, error);
}
