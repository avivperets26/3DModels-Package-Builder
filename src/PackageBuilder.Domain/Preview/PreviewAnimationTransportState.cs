using System.Collections.ObjectModel;
using PackageBuilder.Domain.Animations;

namespace PackageBuilder.Domain.Preview;

/// <summary>Identifies the shared playback state presented by every engine adapter.</summary>
public enum PreviewPlaybackState
{
    Unavailable = 0,
    Stopped,
    Playing,
    Paused,
    Completed,
}

/// <summary>Projects existing validated animation metadata into non-destructive preview state.</summary>
public sealed record PreviewAnimationClip(string Name, double DurationSeconds, bool SourceLoops)
{
    /// <summary>Projects duration and loop defaults from the canonical animation definition.</summary>
    public static PreviewAnimationClip From(AnimationDefinition animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        return new(
            animation.Name,
            animation.DurationSeconds,
            animation.LoopBehavior.Equals(LoopBehavior.Loop));
    }
}

/// <summary>
/// Owns engine-neutral select/play/pause/replay/scrub/loop semantics; loop changes are preview-only
/// and never mutate the source animation definition.
/// </summary>
public sealed class PreviewAnimationTransportState
{
    private PreviewAnimationTransportState(
        IReadOnlyList<PreviewAnimationClip> animations,
        int? selectedIndex,
        PreviewPlaybackState playback,
        double currentTimeSeconds,
        bool loopEnabled)
    {
        Animations = animations;
        SelectedIndex = selectedIndex;
        Playback = playback;
        CurrentTimeSeconds = currentTimeSeconds;
        LoopEnabled = loopEnabled;
    }

    public IReadOnlyList<PreviewAnimationClip> Animations { get; }
    public int? SelectedIndex { get; }
    public PreviewAnimationClip? SelectedAnimation =>
        SelectedIndex is int index ? Animations[index] : null;
    public PreviewPlaybackState Playback { get; }
    public double CurrentTimeSeconds { get; }
    public double DurationSeconds => SelectedAnimation?.DurationSeconds ?? 0d;
    public bool LoopEnabled { get; }

    /// <summary>Creates a validated list in source order and applies the contract's initial state.</summary>
    public static PreviewExperienceValidationResult<PreviewAnimationTransportState> Create(
        IEnumerable<AnimationDefinition?>? animations,
        PreviewAnimationTransportPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (animations is null)
        {
            return PreviewExperienceValidationResult<PreviewAnimationTransportState>.Failure(
                PreviewExperienceValidationError.NullAnimations);
        }

        AnimationDefinition?[] values = [.. animations];
        if (values.Any(value => value is null))
        {
            return PreviewExperienceValidationResult<PreviewAnimationTransportState>.Failure(
                PreviewExperienceValidationError.NullAnimation);
        }

        PreviewAnimationClip[] clips = [.. values.Select(value => PreviewAnimationClip.From(value!))];
        return clips.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != clips.Length
            ? PreviewExperienceValidationResult<PreviewAnimationTransportState>.Failure(
                PreviewExperienceValidationError.DuplicateAnimationName)
            : clips.Length == 0 || !policy.SelectFirstAnimation
            ? PreviewExperienceValidationResult<PreviewAnimationTransportState>.Success(
                new PreviewAnimationTransportState(
                    new ReadOnlyCollection<PreviewAnimationClip>(clips),
                    null,
                    PreviewPlaybackState.Unavailable,
                    0d,
                    false))
            : PreviewExperienceValidationResult<PreviewAnimationTransportState>.Success(
            NewSelected(
                new ReadOnlyCollection<PreviewAnimationClip>(clips),
                0,
                policy.InitiallyPlaying));
    }

    /// <summary>Selects by deterministic source order and resets timeline and source-derived loop state.</summary>
    public PreviewExperienceValidationResult<PreviewAnimationTransportState> Select(int index) =>
        index < 0 || index >= Animations.Count
            ? PreviewExperienceValidationResult<PreviewAnimationTransportState>.Failure(
                PreviewExperienceValidationError.InvalidItemIndex)
            : PreviewExperienceValidationResult<PreviewAnimationTransportState>.Success(
                NewSelected(Animations, index, false));

    /// <summary>Starts or resumes the selected animation, including from a completed one-shot.</summary>
    public PreviewAnimationTransportState Play()
    {
        if (SelectedAnimation is null)
        {
            return this;
        }

        double time = Playback == PreviewPlaybackState.Completed ? 0d : CurrentTimeSeconds;
        return Copy(PreviewPlaybackState.Playing, time, LoopEnabled);
    }

    /// <summary>Pauses only active playback; unavailable and non-playing states are safe no-ops.</summary>
    public PreviewAnimationTransportState Pause() =>
        Playback == PreviewPlaybackState.Playing
            ? Copy(PreviewPlaybackState.Paused, CurrentTimeSeconds, LoopEnabled)
            : this;

    /// <summary>Restarts the selected animation at zero and enters playing state.</summary>
    public PreviewAnimationTransportState Replay() => SelectedAnimation is null
        ? this
        : Copy(PreviewPlaybackState.Playing, 0d, LoopEnabled);

    /// <summary>Clamps timeline input; a non-looping end position is completed, not playing.</summary>
    public PreviewExperienceValidationResult<PreviewAnimationTransportState> Scrub(
        double timeSeconds)
    {
        if (!double.IsFinite(timeSeconds) || timeSeconds < 0d || SelectedAnimation is null)
        {
            return PreviewExperienceValidationResult<PreviewAnimationTransportState>.Failure(
                PreviewExperienceValidationError.InvalidTimelinePosition);
        }

        double time = Math.Min(timeSeconds, DurationSeconds);
        PreviewPlaybackState playback = time >= DurationSeconds && !LoopEnabled
            ? PreviewPlaybackState.Completed
            : PreviewPlaybackState.Paused;
        return PreviewExperienceValidationResult<PreviewAnimationTransportState>.Success(
            Copy(playback, time, LoopEnabled));
    }

    /// <summary>Changes preview looping without changing the packaged animation definition.</summary>
    public PreviewAnimationTransportState SetLoop(bool enabled) => SelectedAnimation is null
        ? this
        : Copy(
            Playback == PreviewPlaybackState.Completed && enabled
                ? PreviewPlaybackState.Paused
                : Playback,
            CurrentTimeSeconds,
            enabled);

    private static PreviewAnimationTransportState NewSelected(
        IReadOnlyList<PreviewAnimationClip> animations,
        int index,
        bool play) => new(
            animations,
            index,
            play ? PreviewPlaybackState.Playing : PreviewPlaybackState.Stopped,
            0d,
            animations[index].SourceLoops);

    private PreviewAnimationTransportState Copy(
        PreviewPlaybackState playback,
        double time,
        bool loop) => new(Animations, SelectedIndex, playback, time, loop);
}
