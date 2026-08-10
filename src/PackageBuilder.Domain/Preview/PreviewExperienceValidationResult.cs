namespace PackageBuilder.Domain.Preview;

/// <summary>Identifies why a shared interactive-preview value or transition was rejected.</summary>
public enum PreviewExperienceValidationError
{
    None = 0,
    UnsupportedContractVersion,
    NullPresentation,
    NullNavigation,
    NullLightControls,
    NullOverlay,
    NullItemSelection,
    NullAnimationTransport,
    NullBindings,
    EmptyBindings,
    NullBinding,
    DuplicateBinding,
    MissingRequiredBinding,
    NullAccessibility,
    EmptyAccessibilityControls,
    NullAccessibilityControl,
    DuplicateControlId,
    DuplicateFocusOrder,
    MissingAccessibleName,
    InvalidNumericRange,
    InvalidStep,
    InvalidState,
    NullItems,
    NullItem,
    DuplicateItemId,
    UnknownItem,
    InvalidItemIndex,
    NullAnimations,
    NullAnimation,
    DuplicateAnimationName,
    UnknownAnimation,
    InvalidAnimationDuration,
    InvalidTimelinePosition,
}

/// <summary>Returns an immutable value or an explicit expected-input rejection.</summary>
public sealed class PreviewExperienceValidationResult<T>
    where T : class
{
    private PreviewExperienceValidationResult(T? value, PreviewExperienceValidationError error)
    {
        Value = value;
        Error = error;
    }

    public bool IsValid => Error == PreviewExperienceValidationError.None;

    public T? Value { get; }

    public PreviewExperienceValidationError Error { get; }

    internal static PreviewExperienceValidationResult<T> Success(T value) =>
        new(value, PreviewExperienceValidationError.None);

    internal static PreviewExperienceValidationResult<T> Failure(
        PreviewExperienceValidationError error) => new(null, error);
}
