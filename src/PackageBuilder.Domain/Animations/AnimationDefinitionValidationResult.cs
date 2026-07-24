namespace PackageBuilder.Domain.Animations;

/// <summary>Identifies why animation clip metadata could not be created.</summary>
public enum AnimationDefinitionValidationError
{
    None = 0,
    NullName,
    EmptyName,
    WhitespaceOnlyName,
    NameEdgeWhitespace,
    NameContainsControlCharacter,
    ReversedFrameRange,
    InvalidFramesPerSecond,
    DurationNotFinite,
    NullLoopBehavior,
    NullRootMotionStatus,
    NullRig,
    UnexpectedRootMotionBone,
    RootMotionBoneMismatch,
}

/// <summary>Represents expected success or failure when creating animation metadata.</summary>
public sealed class AnimationDefinitionValidationResult
{
    private AnimationDefinitionValidationResult(
        bool isValid,
        AnimationDefinition? value,
        AnimationDefinitionValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public AnimationDefinition? Value { get; }

    public AnimationDefinitionValidationError Error { get; }

    internal static AnimationDefinitionValidationResult Success(AnimationDefinition value) =>
        new(true, value, AnimationDefinitionValidationError.None);

    internal static AnimationDefinitionValidationResult Failure(
        AnimationDefinitionValidationError error) =>
        new(false, null, error);
}
