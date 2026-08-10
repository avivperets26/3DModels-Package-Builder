namespace PackageBuilder.Domain.Preview;

/// <summary>Identifies why an engine-neutral preview presentation value was rejected.</summary>
public enum PreviewPresentationValidationError
{
    None = 0,
    NullProductCase,
    GroupDefinitionRequired,
    NullViewId,
    NullViewKind,
    NullVisibility,
    NullSelectedItemId,
    NullViews,
    NullView,
    EmptyViews,
    DuplicateViewId,
    MissingHeroView,
    MissingAnimationPoseView,
    MissingSetOverviewView,
    MissingCollectionOverviewView,
    ViewNotAllowedForProductCase,
    VisibilityNotAllowedForProductCase,
    UnknownSelectedItem,
    NullBackground,
    NullLighting,
    NullKeyLight,
    NullFillLight,
    NullOuterColour,
    NullCentreColour,
    NullLightColour,
    ColourRedOutsideUnitInterval,
    ColourGreenOutsideUnitInterval,
    ColourBlueOutsideUnitInterval,
    BackgroundCentreXOutsideUnitInterval,
    BackgroundCentreYOutsideUnitInterval,
    BackgroundRadiusNotPositiveFinite,
    BackgroundHorizontalScaleNotPositiveFinite,
    LightYawOutsideRange,
    LightPitchOutsideRange,
    LightIntensityNegativeOrNotFinite,
}

/// <summary>Represents expected success or failure when creating a preview presentation value.</summary>
public sealed class PreviewPresentationValidationResult<T>
    where T : class
{
    private PreviewPresentationValidationResult(
        bool isValid,
        T? value,
        PreviewPresentationValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable value, or null when validation failed.</summary>
    public T? Value { get; }

    /// <summary>Gets the expected-input rejection reason.</summary>
    public PreviewPresentationValidationError Error { get; }

    internal static PreviewPresentationValidationResult<T> Success(T value) =>
        new(true, value, PreviewPresentationValidationError.None);

    internal static PreviewPresentationValidationResult<T> Failure(
        PreviewPresentationValidationError error) =>
        new(false, null, error);
}
