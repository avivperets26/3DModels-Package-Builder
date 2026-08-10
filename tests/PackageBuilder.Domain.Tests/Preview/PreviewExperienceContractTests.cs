using System.Reflection;
using System.Text.Json;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Domain.Rigging;
using PackageBuilder.Domain.Tests.Rigging;

namespace PackageBuilder.Domain.Tests.Preview;

[Trait("Task", "PB-0913")]
public sealed class PreviewExperienceContractTests
{
    private static readonly string[] _forbiddenAssemblyTokens =
        ["Unity", "Unreal", "WPF", "PresentationFramework", "System.Drawing"];

    [Fact]
    public void DefaultsReuseApprovedPresentationAndCoverEverySharedAction()
    {
        PreviewExperienceContract contract = PreviewExperienceDefaults.Contract;

        Assert.Equal(1, contract.ContractVersion);
        Assert.Same(PreviewPresentationDefaults.Background, contract.Background);
        Assert.Same(PreviewPresentationDefaults.Lighting, contract.Lighting);
        Assert.Equal(-80d, contract.Navigation.MinimumPitchDegrees);
        Assert.Equal(80d, contract.Navigation.MaximumPitchDegrees);
        Assert.Equal(0.75d, contract.Navigation.MinimumDistanceMultiplier);
        Assert.Equal(50d, contract.Navigation.MaximumDistanceMultiplier);
        Assert.Equal(0.22d, contract.Navigation.PointerOrbitDegreesPerUnit);
        Assert.Equal(5d, contract.Navigation.KeyboardOrbitStepDegrees);
        Assert.Equal(0.16d, contract.Navigation.PointerZoomStep);
        Assert.Equal(0.14d, contract.Navigation.KeyboardZoomStep);
        Assert.All(
            Enum.GetValues<PreviewAction>(),
            action => Assert.Contains(contract.Bindings, binding => binding.Action == action));
    }

    [Fact]
    public void AccessibilityHasUniquePredictableFocusAndHiddenRestoreSemantics()
    {
        PreviewExperienceContract contract = PreviewExperienceDefaults.Contract;
        Assert.True(PreviewAccessibilityPolicy.VisibleFocusRequired);
        Assert.Equal(
            Enumerable.Range(0, contract.Accessibility.Controls.Count),
            contract.Accessibility.Controls.Select(control => control.FocusOrder));
        Assert.All(contract.Accessibility.Controls, control =>
        {
            Assert.False(string.IsNullOrWhiteSpace(control.Label));
            Assert.False(string.IsNullOrWhiteSpace(control.AccessibleName));
            Assert.False(string.IsNullOrWhiteSpace(control.AccessibleHelp));
        });

        PreviewOverlayState hidden = PreviewOverlayState.Create(contract.Overlay).Toggle(contract.Overlay);
        Assert.False(hidden.IsVisible);
        Assert.Equal("show-controls", hidden.FocusedControlId);
        Assert.True(hidden.Toggle(contract.Overlay).IsVisible);
    }

    [Fact]
    public void CameraAndLightStatesClampWrapAndResetWithoutProductTransforms()
    {
        PreviewExperienceContract contract = PreviewExperienceDefaults.Contract;
        PreviewCameraState camera = PreviewCameraState.Reset()
            .Orbit(190d, 500d, contract.Navigation)
            .Zoom(100d, contract.Navigation.KeyboardZoomStep, contract.Navigation);
        Assert.Equal(-170d, camera.YawDegrees);
        Assert.Equal(80d, camera.PitchDegrees);
        Assert.Equal(0.75d, camera.DistanceMultiplier);
        Assert.DoesNotContain(
            typeof(PreviewCameraState).GetProperties(),
            property => property.Name.Contains("Product", StringComparison.Ordinal) ||
                property.Name.Contains("Scale", StringComparison.Ordinal));

        var reset = PreviewLightState.Reset(contract.Lighting);
        PreviewLightState moved = reset.Adjust(400d, -500d, contract.LightControls);
        Assert.Equal(8d, moved.YawDegrees);
        Assert.Equal(-80d, moved.PitchDegrees);
        Assert.Equal(-32d, reset.YawDegrees);
        Assert.Equal(42d, reset.PitchDegrees);
    }

    [Fact]
    public void ItemSelectionHandlesEmptySingleDirectAllAndWrapStates()
    {
        PreviewItemSelectionPolicy policy = PreviewExperienceDefaults.Contract.ItemSelection;
        PreviewItemSelectionState empty = Valid(
            PreviewItemSelectionState.Create([], policy));
        Assert.Same(empty, empty.Next(policy));
        Assert.Equal(PreviewItemSelectionMode.Empty, empty.Mode);

        InternalAssetId helmet = Id("Helmet");
        InternalAssetId boots = Id("Boots");
        PreviewItemSelectionState state = Valid(
            PreviewItemSelectionState.Create([helmet, boots], policy));
        Assert.Equal(PreviewItemSelectionMode.All, state.Mode);
        state = state.Next(policy);
        Assert.Equal(helmet, state.SelectedItem);
        state = state.Previous(policy);
        Assert.Equal(boots, state.SelectedItem);
        state = Valid(state.Select(helmet));
        Assert.Equal(0, state.SelectedIndex);
        Assert.Equal(PreviewItemSelectionMode.All, state.ShowAll().Mode);
        Assert.Equal(
            PreviewExperienceValidationError.UnknownItem,
            state.Select(Id("Unknown")).Error);
    }

    [Fact]
    public void AnimationTransportUsesCanonicalDurationAndKeepsLoopOverridePreviewOnly()
    {
        AnimationDefinition shot = Animation("Bow Shot", 0, 48, 24d, LoopBehavior.Once);
        PreviewAnimationTransportState state = Valid(
            PreviewAnimationTransportState.Create(
                [shot],
                PreviewExperienceDefaults.Contract.AnimationTransport));
        Assert.Equal(2d, state.DurationSeconds);
        Assert.Equal(PreviewPlaybackState.Stopped, state.Playback);
        Assert.False(state.LoopEnabled);

        state = state.Play().Pause();
        Assert.Equal(PreviewPlaybackState.Paused, state.Playback);
        state = Valid(state.Scrub(3d));
        Assert.Equal(2d, state.CurrentTimeSeconds);
        Assert.Equal(PreviewPlaybackState.Completed, state.Playback);
        state = state.Replay().SetLoop(true);
        Assert.Equal(PreviewPlaybackState.Playing, state.Playback);
        Assert.Equal(0d, state.CurrentTimeSeconds);
        Assert.True(state.LoopEnabled);
        Assert.Same(LoopBehavior.Once, shot.LoopBehavior);
    }

    [Fact]
    public void AnimationTransportHandlesEmptyDuplicateAndUnknownStates()
    {
        PreviewAnimationTransportPolicy policy = PreviewExperienceDefaults.Contract.AnimationTransport;
        PreviewAnimationTransportState empty = Valid(
            PreviewAnimationTransportState.Create([], policy));
        Assert.Equal(PreviewPlaybackState.Unavailable, empty.Playback);
        Assert.Same(empty, empty.Play());
        Assert.Equal(
            PreviewExperienceValidationError.InvalidItemIndex,
            empty.Select(0).Error);

        AnimationDefinition first = Animation("Idle", 0, 1, 30d, LoopBehavior.Loop);
        AnimationDefinition duplicate = Animation("Idle", 0, 2, 30d, LoopBehavior.Once);
        Assert.Equal(
            PreviewExperienceValidationError.DuplicateAnimationName,
            PreviewAnimationTransportState.Create([first, duplicate], policy).Error);
    }

    [Fact]
    public void ContractValidationRejectsBadRangesDuplicateBindingsAndFocus()
    {
        Assert.Equal(
            PreviewExperienceValidationError.InvalidNumericRange,
            PreviewNavigationPolicy.Create(80d, -80d, 1d, 2d, 1d, 1d, 0.1d, 0.1d).Error);
        PreviewExperienceContract defaults = PreviewExperienceDefaults.Contract;
        Assert.Equal(
            PreviewExperienceValidationError.InvalidNumericRange,
            PreviewExperienceContract.Create(
                1,
                defaults.Background,
                defaults.Lighting,
                new PreviewNavigationPolicy(80d, -80d, 1d, 2d, 1d, 1d, 0.1d, 0.1d),
                defaults.LightControls,
                defaults.Overlay,
                defaults.ItemSelection,
                defaults.AnimationTransport,
                defaults.Bindings,
                defaults.Accessibility).Error);
        Assert.Equal(
            PreviewExperienceValidationError.InvalidState,
            PreviewExperienceContract.Create(
                1,
                defaults.Background,
                defaults.Lighting,
                defaults.Navigation,
                defaults.LightControls,
                new PreviewOverlayPolicy(true, " "),
                defaults.ItemSelection,
                defaults.AnimationTransport,
                defaults.Bindings,
                defaults.Accessibility).Error);
        Assert.Equal(
            PreviewExperienceValidationError.DuplicateBinding,
            PreviewExperienceContract.Create(
                1,
                defaults.Background,
                defaults.Lighting,
                defaults.Navigation,
                defaults.LightControls,
                defaults.Overlay,
                defaults.ItemSelection,
                defaults.AnimationTransport,
                [.. defaults.Bindings, defaults.Bindings[0]],
                defaults.Accessibility).Error);
        Assert.Equal(
            PreviewExperienceValidationError.DuplicateFocusOrder,
            PreviewAccessibilityPolicy.Create(
            [
                new("a", "A", "A", "A help", 0),
                new("b", "B", "B", "B help", 0),
            ]).Error);
    }

    [Fact]
    public void SharedVectorsAreVersionedAndExerciseCrossEngineBoundaryCases()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "preview",
            "preview-experience-v1-vectors.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("contractVersion").GetInt32());
        foreach (JsonElement vector in root.GetProperty("camera").EnumerateArray())
        {
            double[] start = [.. vector.GetProperty("start").EnumerateArray().Select(value => value.GetDouble())];
            PreviewCameraState state = new(start[0], start[1], start[2]);
            if (vector.TryGetProperty("orbit", out JsonElement orbit))
            {
                double[] delta = [.. orbit.EnumerateArray().Select(value => value.GetDouble())];
                state = state.Orbit(delta[0], delta[1], PreviewExperienceDefaults.Contract.Navigation);
            }
            else
            {
                state = state.Zoom(
                    vector.GetProperty("zoomSteps").GetDouble(),
                    vector.GetProperty("step").GetDouble(),
                    PreviewExperienceDefaults.Contract.Navigation);
            }

            double[] expected = [.. vector.GetProperty("expected").EnumerateArray().Select(value => value.GetDouble())];
            Assert.Equal(expected[0], state.YawDegrees);
            Assert.Equal(expected[1], state.PitchDegrees);
            Assert.Equal(expected[2], state.DistanceMultiplier);
        }

        foreach (JsonElement vector in root.GetProperty("items").EnumerateArray())
        {
            InternalAssetId[] items = [.. vector.GetProperty("items").EnumerateArray().Select(value => Id(value.GetString()!))];
            PreviewItemSelectionState state = Valid(
                PreviewItemSelectionState.Create(
                    items,
                    PreviewExperienceDefaults.Contract.ItemSelection));
            if (vector.TryGetProperty("selectIndex", out JsonElement selected))
            {
                state = Valid(state.Select(items[selected.GetInt32()]));
            }

            state = vector.GetProperty("action").GetString() == "next"
                ? state.Next(PreviewExperienceDefaults.Contract.ItemSelection)
                : state.Previous(PreviewExperienceDefaults.Contract.ItemSelection);
            Assert.Equal(
                Enum.Parse<PreviewItemSelectionMode>(
                    vector.GetProperty("expectedMode").GetString()!,
                    false),
                state.Mode);
            Assert.Equal(
                vector.GetProperty("expectedIndex").ValueKind == JsonValueKind.Null
                    ? null
                    : vector.GetProperty("expectedIndex").GetInt32(),
                state.SelectedIndex);
        }

        foreach (JsonElement vector in root.GetProperty("animation").EnumerateArray())
        {
            bool sourceLoops = vector.GetProperty("sourceLoops").GetBoolean();
            double duration = vector.GetProperty("duration").GetDouble();
            AnimationDefinition source = Animation(
                "VectorClip",
                0,
                (long)(duration * 10d),
                10d,
                sourceLoops ? LoopBehavior.Loop : LoopBehavior.Once);
            PreviewAnimationTransportState state = Valid(
                PreviewAnimationTransportState.Create(
                    [source],
                    PreviewExperienceDefaults.Contract.AnimationTransport));
            string action = vector.GetProperty("action").GetString()!;
            state = action switch
            {
                "scrub" => Valid(state.Scrub(vector.GetProperty("value").GetDouble())),
                "loop" => state.SetLoop(vector.GetProperty("value").GetBoolean()),
                "replay" => state.Replay(),
                _ => throw new InvalidOperationException($"Unknown vector action: {action}"),
            };
            Assert.Equal(
                Enum.Parse<PreviewPlaybackState>(
                    vector.GetProperty("expectedPlayback").GetString()!,
                    false),
                state.Playback);
            Assert.Equal(vector.GetProperty("expectedTime").GetDouble(), state.CurrentTimeSeconds);
            Assert.Equal(vector.GetProperty("expectedLoop").GetBoolean(), state.LoopEnabled);
            Assert.Equal(sourceLoops, source.LoopBehavior.Equals(LoopBehavior.Loop));
        }

        JsonElement overlayVector = Assert.Single(root.GetProperty("overlay").EnumerateArray());
        var overlayPolicy = new PreviewOverlayPolicy(
            overlayVector.GetProperty("initiallyVisible").GetBoolean(),
            "show-controls");
        PreviewOverlayState overlayState = PreviewOverlayState.Create(overlayPolicy).Toggle(overlayPolicy);
        Assert.Equal(overlayVector.GetProperty("expectedVisible").GetBoolean(), overlayState.IsVisible);
        Assert.Equal(overlayVector.GetProperty("expectedFocus").GetString(), overlayState.FocusedControlId);
    }

    [Fact]
    public void PreviewContractRemainsEngineRendererFilesystemAndUiIndependent()
    {
        Assembly assembly = typeof(PreviewExperienceContract).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies().Select(value => value.Name ?? string.Empty),
        ];
        Assert.DoesNotContain(references, reference =>
            _forbiddenAssemblyTokens.Any(
                token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static InternalAssetId Id(string value) => InternalAssetId.Create(value).Value!;

    private static AnimationDefinition Animation(
        string name,
        long start,
        long end,
        double framesPerSecond,
        LoopBehavior loop) => RigTestAssertions.AssertSuccess(
            AnimationDefinition.Create(
                name,
                start,
                end,
                framesPerSecond,
                loop,
                RootMotionStatus.None,
                null,
                RigTestAssertions.CreateRig()));

    private static T Valid<T>(PreviewExperienceValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid);
        Assert.Equal(PreviewExperienceValidationError.None, result.Error);
        return Assert.IsType<T>(result.Value);
    }
}
