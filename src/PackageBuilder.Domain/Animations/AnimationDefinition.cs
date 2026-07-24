using PackageBuilder.Domain.Rigging;

namespace PackageBuilder.Domain.Animations;

/// <summary>Represents immutable renderer-independent animation clip metadata.</summary>
public sealed class AnimationDefinition : IEquatable<AnimationDefinition>
{
    private AnimationDefinition(
        string name,
        long startFrame,
        long endFrame,
        double framesPerSecond,
        LoopBehavior loopBehavior,
        RootMotionStatus rootMotionStatus,
        string? rootMotionBoneIdentity,
        RigDefinition rig)
    {
        Name = name;
        StartFrame = startFrame;
        EndFrame = endFrame;
        FramesPerSecond = framesPerSecond;
        LoopBehavior = loopBehavior;
        RootMotionStatus = rootMotionStatus;
        RootMotionBoneIdentity = rootMotionBoneIdentity;
        Rig = rig;
    }

    public string Name { get; }

    /// <summary>Gets the inclusive first source frame. Negative source frames are valid.</summary>
    public long StartFrame { get; }

    /// <summary>Gets the inclusive final source frame.</summary>
    public long EndFrame { get; }

    public double FramesPerSecond { get; }

    public LoopBehavior LoopBehavior { get; }

    public RootMotionStatus RootMotionStatus { get; }

    /// <summary>Gets the validated root identity when root motion is declared; otherwise null.</summary>
    public string? RootMotionBoneIdentity { get; }

    public RigDefinition Rig { get; }

    /// <summary>Gets the inclusive number of sampled frames without a fixed clip-length limit.</summary>
    public decimal InclusiveFrameCount => (decimal)EndFrame - StartFrame + 1m;

    /// <summary>
    /// Gets playback duration in seconds. Because both range endpoints are inclusive samples,
    /// duration is the number of intervals (<c>EndFrame - StartFrame</c>) divided by FPS.
    /// A one-frame clip therefore has zero duration.
    /// </summary>
    public double DurationSeconds => (double)((decimal)EndFrame - StartFrame) / FramesPerSecond;

    /// <summary>Validates and creates renderer-independent animation metadata.</summary>
    public static AnimationDefinitionValidationResult Create(
        string? name,
        long startFrame,
        long endFrame,
        double framesPerSecond,
        LoopBehavior? loopBehavior,
        RootMotionStatus? rootMotionStatus,
        string? rootMotionBoneIdentity,
        RigDefinition? rig)
    {
        AnimationDefinitionValidationError nameError = ValidateName(name);
        if (nameError != AnimationDefinitionValidationError.None)
        {
            return AnimationDefinitionValidationResult.Failure(nameError);
        }

        if (endFrame < startFrame)
        {
            return AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.ReversedFrameRange);
        }

        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0d)
        {
            return AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.InvalidFramesPerSecond);
        }

        double duration = (double)((decimal)endFrame - startFrame) / framesPerSecond;
        if (!double.IsFinite(duration))
        {
            return AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.DurationNotFinite);
        }

        if (loopBehavior is null)
        {
            return AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.NullLoopBehavior);
        }

        if (rootMotionStatus is null)
        {
            return AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.NullRootMotionStatus);
        }

        if (rig is null)
        {
            return AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.NullRig);
        }

        bool usesRootMotion = rootMotionStatus.Equals(RootMotionStatus.RootBone);
        return !usesRootMotion && rootMotionBoneIdentity is not null
            ? AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.UnexpectedRootMotionBone)
            : usesRootMotion &&
            !string.Equals(
                rootMotionBoneIdentity,
                rig.Skeleton.Root.Identity,
                StringComparison.Ordinal)
            ? AnimationDefinitionValidationResult.Failure(
                AnimationDefinitionValidationError.RootMotionBoneMismatch)
            : AnimationDefinitionValidationResult.Success(
            new AnimationDefinition(
                name!,
                startFrame,
                endFrame,
                framesPerSecond,
                loopBehavior,
                rootMotionStatus,
                rootMotionBoneIdentity,
                rig));
    }

    public bool Equals(AnimationDefinition? other) =>
        other is not null &&
        string.Equals(Name, other.Name, StringComparison.Ordinal) &&
        StartFrame == other.StartFrame &&
        EndFrame == other.EndFrame &&
        FramesPerSecond.Equals(other.FramesPerSecond) &&
        LoopBehavior.Equals(other.LoopBehavior) &&
        RootMotionStatus.Equals(other.RootMotionStatus) &&
        string.Equals(
            RootMotionBoneIdentity,
            other.RootMotionBoneIdentity,
            StringComparison.Ordinal) &&
        Rig.Equals(other.Rig);

    public override bool Equals(object? obj) => obj is AnimationDefinition other && Equals(other);

    public override int GetHashCode() =>
        StableRigHash.Create()
            .Add(Name)
            .Add(StartFrame)
            .Add(EndFrame)
            .Add(FramesPerSecond)
            .Add(LoopBehavior.CanonicalIdentifier)
            .Add(RootMotionStatus.CanonicalIdentifier)
            .Add(RootMotionBoneIdentity ?? string.Empty)
            .Add(Rig.GetHashCode())
            .ToHashCode();

    private static AnimationDefinitionValidationError ValidateName(string? name)
    {
        return name is null
            ? AnimationDefinitionValidationError.NullName
            : name.Length == 0
            ? AnimationDefinitionValidationError.EmptyName
            : string.IsNullOrWhiteSpace(name)
            ? AnimationDefinitionValidationError.WhitespaceOnlyName
            : char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1])
            ? AnimationDefinitionValidationError.NameEdgeWhitespace
            : name.Any(char.IsControl)
            ? AnimationDefinitionValidationError.NameContainsControlCharacter
            : AnimationDefinitionValidationError.None;
    }
}
