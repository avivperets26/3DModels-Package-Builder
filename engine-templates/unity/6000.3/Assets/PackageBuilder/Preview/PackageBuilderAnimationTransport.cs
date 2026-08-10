using System;
using System.Collections.Generic;
using UnityEngine;

namespace PackageBuilder.Preview
{
    /// <summary>Identifies the shared animation-transport state exposed by the Unity adapter.</summary>
    public enum PackageBuilderPlaybackState
    {
        Unavailable = 0,
        Stopped,
        Playing,
        Paused,
        Completed,
    }

    /// <summary>
    /// Adapts an Animator to preview contract v1. Playback and loop overrides are held only in
    /// scene runtime state; packaged AnimationClip and controller assets are never modified.
    /// </summary>
    public sealed class PackageBuilderAnimationTransport : MonoBehaviour
    {
        public const float KeyboardTimelineStepSeconds = 0.1f;

        [SerializeField] private Animator animator;

        [SerializeField] private AnimationClip[] clips = Array.Empty<AnimationClip>();
        [SerializeField] private int selectedIndex = -1;
        [SerializeField] private PackageBuilderPlaybackState playback =
            PackageBuilderPlaybackState.Unavailable;
        [SerializeField] private float currentTimeSeconds;
        [SerializeField] private bool loopEnabled;

        /// <summary>Gets the Animator sampled by this preview-only transport.</summary>
        public Animator Animator => animator;

        /// <summary>Gets whether at least one selectable animation is available.</summary>
        public bool Available => selectedIndex >= 0 && selectedIndex < clips.Length;

        /// <summary>Gets clip names in their deterministic controller order.</summary>
        public string[] ClipNames
        {
            get
            {
                var names = new string[clips.Length];
                for (int index = 0; index < clips.Length; index++)
                {
                    names[index] = clips[index].name;
                }

                return names;
            }
        }

        /// <summary>Gets the selected clip index, or -1 when transport is unavailable.</summary>
        public int SelectedIndex => selectedIndex;

        /// <summary>Gets the current shared playback state.</summary>
        public PackageBuilderPlaybackState Playback => playback;

        /// <summary>Gets the clamped current timeline position.</summary>
        public float CurrentTimeSeconds => currentTimeSeconds;

        /// <summary>Gets the selected clip duration, or zero when unavailable.</summary>
        public float DurationSeconds => Available ? clips[selectedIndex].length : 0f;

        /// <summary>Gets the preview-only loop override.</summary>
        public bool LoopEnabled => loopEnabled;

        /// <summary>
        /// Discovers unique controller clips, selects the first clip, and stops at time zero.
        /// The Animator is sampled manually so preview loop overrides do not change source assets.
        /// </summary>
        public void Configure(Animator value)
        {
            animator = value;
            clips = DiscoverClips(value);
            selectedIndex = -1;
            playback = PackageBuilderPlaybackState.Unavailable;
            currentTimeSeconds = 0f;
            loopEnabled = false;

            if (animator == null || clips.Length == 0)
            {
                return;
            }

            animator.speed = 0f;
            Select(0);
        }

        /// <summary>Selects by deterministic order and restores source-derived loop state.</summary>
        public bool Select(int index)
        {
            if (index < 0 || index >= clips.Length || animator == null)
            {
                return false;
            }

            selectedIndex = index;
            playback = PackageBuilderPlaybackState.Stopped;
            currentTimeSeconds = 0f;
            loopEnabled = clips[index].isLooping;
            SampleCurrent();
            return true;
        }

        /// <summary>Starts, resumes, or restarts a completed selected clip.</summary>
        public void Play()
        {
            if (!Available)
            {
                return;
            }

            if (playback == PackageBuilderPlaybackState.Completed)
            {
                currentTimeSeconds = 0f;
            }

            playback = PackageBuilderPlaybackState.Playing;
            SampleCurrent();
        }

        /// <summary>Pauses active playback and safely ignores all other states.</summary>
        public void Pause()
        {
            if (playback == PackageBuilderPlaybackState.Playing)
            {
                playback = PackageBuilderPlaybackState.Paused;
            }
        }

        /// <summary>Restarts the selected clip at zero in the playing state.</summary>
        public void Replay()
        {
            if (!Available)
            {
                return;
            }

            currentTimeSeconds = 0f;
            playback = PackageBuilderPlaybackState.Playing;
            SampleCurrent();
        }

        /// <summary>Clamps timeline input and pauses, or completes a non-looping clip at its end.</summary>
        public bool Scrub(float timeSeconds)
        {
            if (!Available || !float.IsFinite(timeSeconds) || timeSeconds < 0f)
            {
                return false;
            }

            currentTimeSeconds = Mathf.Min(timeSeconds, DurationSeconds);
            playback = currentTimeSeconds >= DurationSeconds && !loopEnabled
                ? PackageBuilderPlaybackState.Completed
                : PackageBuilderPlaybackState.Paused;
            SampleCurrent();
            return true;
        }

        /// <summary>Changes preview looping without mutating the selected AnimationClip.</summary>
        public void SetLoop(bool enabled)
        {
            if (!Available)
            {
                return;
            }

            loopEnabled = enabled;
            if (playback == PackageBuilderPlaybackState.Completed && enabled)
            {
                playback = PackageBuilderPlaybackState.Paused;
            }
        }

        /// <summary>Advances deterministic preview time; exposed for engine integration tests.</summary>
        public void Tick(float deltaSeconds)
        {
            if (playback != PackageBuilderPlaybackState.Playing || !float.IsFinite(deltaSeconds) ||
                deltaSeconds <= 0f || !Available)
            {
                return;
            }

            float duration = DurationSeconds;
            currentTimeSeconds += deltaSeconds;
            if (currentTimeSeconds >= duration)
            {
                if (loopEnabled && duration > 0f)
                {
                    currentTimeSeconds %= duration;
                }
                else
                {
                    currentTimeSeconds = duration;
                    playback = PackageBuilderPlaybackState.Completed;
                }
            }

            SampleCurrent();
        }

        private void OnEnable()
        {
            if (animator != null && clips.Length == 0)
            {
                Configure(animator);
            }
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void SampleCurrent()
        {
            if (!Available || animator == null)
            {
                return;
            }

            float duration = DurationSeconds;
            float normalizedTime = duration <= 0f ? 0f : Mathf.Clamp01(currentTimeSeconds / duration);
            animator.Play(clips[selectedIndex].name, 0, normalizedTime);
            animator.Update(0f);
        }

        private static AnimationClip[] DiscoverClips(Animator value)
        {
            if (value == null || value.runtimeAnimatorController == null)
            {
                return Array.Empty<AnimationClip>();
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var discovered = new List<AnimationClip>();
            foreach (AnimationClip clip in value.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.length > 0f && unique.Add(clip.name))
                {
                    discovered.Add(clip);
                }
            }

            return discovered.ToArray();
        }
    }
}
