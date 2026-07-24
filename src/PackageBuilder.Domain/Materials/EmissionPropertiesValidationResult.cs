namespace PackageBuilder.Domain.Materials;

/// <summary>Identifies why renderer-independent emission properties could not be created.</summary>
public enum EmissionPropertiesValidationError
{
    /// <summary>The emission properties are valid.</summary>
    None = 0,

    /// <summary>The red component was negative or not finite.</summary>
    InvalidRed,

    /// <summary>The green component was negative or not finite.</summary>
    InvalidGreen,

    /// <summary>The blue component was negative or not finite.</summary>
    InvalidBlue,

    /// <summary>The intensity was negative or not finite.</summary>
    InvalidIntensity,
}

/// <summary>Represents expected success or failure when creating emission properties.</summary>
public sealed class EmissionPropertiesValidationResult
{
    private EmissionPropertiesValidationResult(
        bool isValid,
        EmissionProperties? value,
        EmissionPropertiesValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable emission properties, or null on failure.</summary>
    public EmissionProperties? Value { get; }

    /// <summary>Gets the expected-input rejection reason.</summary>
    public EmissionPropertiesValidationError Error { get; }

    internal static EmissionPropertiesValidationResult Success(EmissionProperties value) =>
        new(true, value, EmissionPropertiesValidationError.None);

    internal static EmissionPropertiesValidationResult Failure(
        EmissionPropertiesValidationError error) =>
        new(false, null, error);
}
