namespace PackageBuilder.Domain.Tools;

/// <summary>Identifies an expected tool-model validation failure.</summary>
public enum ToolModelValidationError
{
    /// <summary>Validation succeeded.</summary>
    None = 0,

    /// <summary>A required text value was null.</summary>
    Null,

    /// <summary>A required text value was empty.</summary>
    Empty,

    /// <summary>A required text value contained only whitespace.</summary>
    WhitespaceOnly,

    /// <summary>A value began or ended with whitespace.</summary>
    LeadingOrTrailingWhitespace,

    /// <summary>The tool enum value is not supported.</summary>
    UndefinedTool,

    /// <summary>The version does not use the canonical grammar for its tool.</summary>
    InvalidVersion,

    /// <summary>An installation paired a version with a different tool.</summary>
    VersionToolMismatch,

    /// <summary>A required version object was absent.</summary>
    MissingVersion,

    /// <summary>A path was not a fully qualified Windows drive path.</summary>
    PathNotRooted,

    /// <summary>A path contained traversal, alternate separators, or a noncanonical representation.</summary>
    PathNotCanonical,

    /// <summary>The configured approved root was not a canonical tools directory.</summary>
    InvalidToolsRoot,

    /// <summary>A selected executable was outside the approved tools root.</summary>
    SelectionOutsideToolsRoot,
}

/// <summary>Returns a validated immutable tool model without throwing for expected input errors.</summary>
/// <typeparam name="T">The model type produced on success.</typeparam>
public sealed class ToolModelValidationResult<T>
    where T : class
{
    private ToolModelValidationResult(bool isValid, T? value, ToolModelValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable model, or <see langword="null"/> on failure.</summary>
    public T? Value { get; }

    /// <summary>Gets the stable validation error.</summary>
    public ToolModelValidationError Error { get; }

    internal static ToolModelValidationResult<T> Success(T value) => new(true, value, ToolModelValidationError.None);

    internal static ToolModelValidationResult<T> Failure(ToolModelValidationError error) => new(false, null, error);
}
