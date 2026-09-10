using UnityEngine;

namespace PackageBuilder.Preview
{
    /// <summary>
    /// Implements the Unity interactive-preview contract by moving only the preview camera,
    /// studio backdrop, and key light. Product transforms are never modified.
    /// </summary>
    public sealed class PackageBuilderPreviewController : MonoBehaviour
    {
        public const string ContractVersion = "1";
        public const float MinimumPitchDegrees = -80f;
        public const float MaximumPitchDegrees = 80f;

        private const float MinimumBoundsSize = 0.01f;
        private const float MinimumDistanceMultiplier = 0.75f;
        private const float MaximumDistanceMultiplier = 50f;
        private const float PointerOrbitSensitivity = 0.22f;
        private const float KeyboardOrbitStep = 5f;
        private const float KeyboardZoomStep = 0.14f;
        private const float WheelZoomSpeed = 0.16f;
        private const float DefaultKeyLightYaw = -32f;
        private const float DefaultKeyLightPitch = 42f;
        private const string AnimationSelectorControl = "animation-selector";
        private const string PlayPauseControl = "animation-play-pause";
        private const string ReplayControl = "animation-replay";
        private const string TimelineControl = "animation-timeline";
        private const string LoopControl = "animation-loop";

        [SerializeField] private Transform previewTarget;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private Transform studioBackground;
        [SerializeField] private PackageBuilderAnimationTransport animationTransport;
        [SerializeField] private PackageBuilderItemSelector itemSelector;
        [SerializeField, Min(1.01f)] private float framingPadding = 1.25f;
        [SerializeField] private bool controlsVisible = true;

        private bool dragging;
        private float keyLightYaw = DefaultKeyLightYaw;
        private float keyLightPitch = DefaultKeyLightPitch;

        /// <summary>Gets the product container whose renderers define the framing bounds.</summary>
        public Transform PreviewTarget => previewTarget;

        /// <summary>Gets the camera moved by framing, orbit, and zoom operations.</summary>
        public Camera PreviewCamera => previewCamera;

        /// <summary>Gets the adjustable studio key light.</summary>
        public Light KeyLight => keyLight;

        /// <summary>Gets the horizon-free radial backdrop that follows the preview camera.</summary>
        public Transform StudioBackground => studioBackground;

        /// <summary>Gets whether the accessible capture controls are currently visible.</summary>
        public bool ControlsVisible => controlsVisible;

        /// <summary>Gets the optional shared animation-transport adapter for an animated product.</summary>
        public PackageBuilderAnimationTransport AnimationTransport => animationTransport;

        /// <summary>Gets the optional scene-instance item selector.</summary>
        public PackageBuilderItemSelector ItemSelector => itemSelector;

        /// <summary>Configures multi-item selection in the declared scene-instance order.</summary>
        public void ConfigureItems(GameObject[] items)
        {
            if (itemSelector == null) { itemSelector = gameObject.AddComponent<PackageBuilderItemSelector>(); }
            itemSelector.Configure(items);
        }

        /// <summary>Assigns the complete scene references used by preview operations.</summary>
        public void Configure(Transform target, Camera camera, Light light, Transform background)
        {
            previewTarget = target;
            previewCamera = camera;
            keyLight = light;
            studioBackground = background;
            ResetKeyLight();
            RefreshStudioBackground();
        }

        /// <summary>Connects the product Animator to preview-only transport state.</summary>
        public void ConfigureAnimation(Animator animator)
        {
            if (animationTransport == null)
            {
                animationTransport = GetComponent<PackageBuilderAnimationTransport>();
            }

            if (animationTransport != null)
            {
                animationTransport.Configure(animator);
            }
        }

        /// <summary>Retains compatibility with product scenes created before studio controls.</summary>
        public void Configure(Transform target, Camera camera)
        {
            Configure(target, camera, keyLight, studioBackground);
        }

        /// <summary>
        /// Moves the camera so every enabled renderer beneath <see cref="PreviewTarget"/> is visible.
        /// </summary>
        public bool AutoFrame()
        {
            if (!TryGetProductBounds(out Bounds bounds) || previewCamera == null)
            {
                return false;
            }

            float verticalHalfField = Mathf.Max(1f, previewCamera.fieldOfView * 0.5f) * Mathf.Deg2Rad;
            float horizontalHalfField = Mathf.Atan(Mathf.Tan(verticalHalfField) *
                Mathf.Max(0.01f, previewCamera.aspect));
            Vector3 direction = previewCamera.transform.position - bounds.center;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = new Vector3(1f, 0.65f, -1f);
            }

            Quaternion rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            float verticalTangent = Mathf.Tan(verticalHalfField);
            float horizontalTangent = Mathf.Tan(horizontalHalfField);
            float padding = Mathf.Max(1.01f, framingPadding);
            float distance = MinimumBoundsSize;

            // Fit all world-space AABB corners in camera space. This remains correct for diagonal views.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 cornerOffset = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        Vector3 cameraSpaceCorner = inverseRotation * cornerOffset;
                        distance = Mathf.Max(
                            distance,
                            Mathf.Abs(cameraSpaceCorner.x) * padding / horizontalTangent - cameraSpaceCorner.z,
                            Mathf.Abs(cameraSpaceCorner.y) * padding / verticalTangent - cameraSpaceCorner.z);
                    }
                }
            }

            distance = Mathf.Max(distance, bounds.size.magnitude);
            previewCamera.transform.SetPositionAndRotation(
                bounds.center - rotation * Vector3.forward * distance,
                rotation);
            previewCamera.nearClipPlane = Mathf.Max(0.01f, distance - bounds.extents.magnitude * 2f);
            previewCamera.farClipPlane = Mathf.Max(previewCamera.nearClipPlane + 10f,
                distance + bounds.extents.magnitude * 8f);
            RefreshStudioBackground();
            return true;
        }

        /// <summary>Orbits with bounded pitch while preserving every product transform.</summary>
        public bool Orbit(float yawDegrees, float pitchDegrees)
        {
            if (!TryGetProductBounds(out Bounds bounds) || previewCamera == null)
            {
                return false;
            }

            Vector3 offset = previewCamera.transform.position - bounds.center;
            float distance = offset.magnitude;
            if (distance < 0.0001f)
            {
                return false;
            }

            float currentPitch = Mathf.Asin(Mathf.Clamp(offset.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
            float currentYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float requestedPitch = Mathf.Clamp(
                currentPitch + pitchDegrees,
                MinimumPitchDegrees,
                MaximumPitchDegrees);
            float requestedYaw = currentYaw + yawDegrees;
            float pitchRadians = requestedPitch * Mathf.Deg2Rad;
            float yawRadians = requestedYaw * Mathf.Deg2Rad;
            float horizontalDistance = Mathf.Cos(pitchRadians) * distance;
            Vector3 requestedOffset = new(
                Mathf.Sin(yawRadians) * horizontalDistance,
                Mathf.Sin(pitchRadians) * distance,
                Mathf.Cos(yawRadians) * horizontalDistance);

            previewCamera.transform.position = bounds.center + requestedOffset;
            previewCamera.transform.LookAt(bounds.center, Vector3.up);
            RefreshStudioBackground();
            return true;
        }

        /// <summary>
        /// Changes camera distance by a normalized delta; positive values zoom closer and negative values zoom out.
        /// </summary>
        public bool Zoom(float normalizedDelta)
        {
            if (!TryGetProductBounds(out Bounds bounds) || previewCamera == null)
            {
                return false;
            }

            Vector3 offset = previewCamera.transform.position - bounds.center;
            float radius = Mathf.Max(MinimumBoundsSize, bounds.extents.magnitude);
            float currentDistance = Mathf.Max(radius * MinimumDistanceMultiplier, offset.magnitude);
            float requestedDistance = currentDistance * Mathf.Exp(-normalizedDelta);
            float distance = Mathf.Clamp(
                requestedDistance,
                radius * MinimumDistanceMultiplier,
                radius * MaximumDistanceMultiplier);
            Vector3 direction = offset.sqrMagnitude < 0.0001f ? Vector3.back : offset.normalized;
            previewCamera.transform.position = bounds.center + direction * distance;
            previewCamera.transform.LookAt(bounds.center, Vector3.up);
            RefreshStudioBackground();
            return true;
        }

        /// <summary>Shows or hides the main overlay while retaining a small restore affordance.</summary>
        public void SetControlsVisible(bool visible) => controlsVisible = visible;

        /// <summary>Restores the main overlay from its compact, hidden state.</summary>
        public void RestoreControls() => SetControlsVisible(true);

        /// <summary>Applies an absolute, bounded key-light direction without changing the product.</summary>
        public bool SetKeyLightDirection(float yawDegrees, float pitchDegrees)
        {
            if (keyLight == null || !float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees))
            {
                return false;
            }

            keyLightYaw = Mathf.Repeat(yawDegrees + 180f, 360f) - 180f;
            keyLightPitch = Mathf.Clamp(pitchDegrees, -80f, 80f);
            keyLight.transform.rotation = Quaternion.Euler(keyLightPitch, keyLightYaw, 0f);
            return true;
        }

        /// <summary>Restores the approved studio key-light direction.</summary>
        public bool ResetKeyLight() => SetKeyLightDirection(DefaultKeyLightYaw, DefaultKeyLightPitch);

        /// <summary>Places the radial backdrop behind the product and keeps it larger than the camera frustum.</summary>
        public bool RefreshStudioBackground()
        {
            if (previewCamera == null || studioBackground == null)
            {
                return false;
            }

            float distance = Mathf.Max(10f, previewCamera.farClipPlane * 0.7f);
            float height = 2f * distance * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * Mathf.Max(0.01f, previewCamera.aspect);
            studioBackground.SetPositionAndRotation(
                previewCamera.transform.position + previewCamera.transform.forward * distance,
                previewCamera.transform.rotation);
            studioBackground.localScale = new Vector3(width * 1.1f, height * 1.1f, 1f);
            return true;
        }

        /// <summary>Collects one combined world-space bound from product renderers.</summary>
        public bool TryGetProductBounds(out Bounds bounds)
        {
            bounds = default;
            if (previewTarget == null)
            {
                return false;
            }

            Renderer[] renderers = previewTarget.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            foreach (Renderer rendererValue in renderers)
            {
                if (rendererValue == null || !rendererValue.enabled || !rendererValue.gameObject.activeInHierarchy ||
                    rendererValue.transform == studioBackground)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = rendererValue.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(rendererValue.bounds);
                }
            }

            if (found && bounds.size.sqrMagnitude < MinimumBoundsSize * MinimumBoundsSize)
            {
                bounds.Expand(MinimumBoundsSize);
            }

            return found;
        }

        /// <summary>
        /// Handles pointer and keyboard navigation through IMGUI events. This path works with either
        /// Unity input backend and keeps the exported customer scene free of Input System dependencies.
        /// </summary>
        private void HandleNavigationEvent(Event currentEvent)
        {
            if (currentEvent == null)
            {
                return;
            }

            if (currentEvent.type == EventType.KeyDown)
            {
                if (HandleAnimationKeyboardEvent(currentEvent))
                {
                    return;
                }

                bool handled = true;
                switch (currentEvent.keyCode)
                {
                    case KeyCode.H:
                        controlsVisible = !controlsVisible;
                        break;
                    case KeyCode.R:
                        AutoFrame();
                        break;
                    case KeyCode.L:
                        ResetKeyLight();
                        break;
                    case KeyCode.LeftArrow:
                        Orbit(-KeyboardOrbitStep, 0f);
                        break;
                    case KeyCode.RightArrow:
                        Orbit(KeyboardOrbitStep, 0f);
                        break;
                    case KeyCode.UpArrow:
                        Orbit(0f, KeyboardOrbitStep);
                        break;
                    case KeyCode.DownArrow:
                        Orbit(0f, -KeyboardOrbitStep);
                        break;
                    case KeyCode.PageUp:
                    case KeyCode.Equals:
                    case KeyCode.KeypadPlus:
                        Zoom(KeyboardZoomStep);
                        break;
                    case KeyCode.PageDown:
                    case KeyCode.Minus:
                    case KeyCode.KeypadMinus:
                        Zoom(-KeyboardZoomStep);
                        break;
                    default:
                        handled = false;
                        break;
                }

                if (handled)
                {
                    currentEvent.Use();
                }

                return;
            }

            if (currentEvent.type == EventType.ScrollWheel)
            {
                if (controlsVisible && OverlayRect().Contains(currentEvent.mousePosition)) { return; }
                Zoom(-currentEvent.delta.y * WheelZoomSpeed);
                currentEvent.Use();
                return;
            }

            Rect interactiveControls = controlsVisible ? OverlayRect() : RestoreControlsRect();
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                dragging = !interactiveControls.Contains(currentEvent.mousePosition);
                if (dragging)
                {
                    currentEvent.Use();
                }

                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                bool wasDragging = dragging;
                dragging = false;
                if (wasDragging)
                {
                    currentEvent.Use();
                }

                return;
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && dragging)
            {
                Orbit(
                    currentEvent.delta.x * PointerOrbitSensitivity,
                    currentEvent.delta.y * PointerOrbitSensitivity);
                currentEvent.Use();
            }
        }

        private void LateUpdate() => RefreshStudioBackground();

        private void OnGUI()
        {
            HandleNavigationEvent(Event.current);
            if (!controlsVisible)
            {
                if (GUI.Button(
                    RestoreControlsRect(),
                    new GUIContent("Show Controls", "Restore the 3D preview controls (H)")))
                {
                    RestoreControls();
                }

                return;
            }

            Rect panel = OverlayRect();
            GUI.Box(panel, "3D Preview Controls");
            GUI.Label(new Rect(panel.x + 12f, panel.y + 28f, 256f, 40f),
                "Left drag: orbit   Wheel: zoom\nArrows: orbit   +/-: zoom   H: hide");
            if (GUI.Button(new Rect(panel.x + 12f, panel.y + 72f, 112f, 28f),
                new GUIContent("Reset View (R)", "Restore automatic camera framing")))
            {
                AutoFrame();
            }
            if (GUI.Button(new Rect(panel.x + 132f, panel.y + 72f, 112f, 28f),
                new GUIContent("Hide (H)", "Hide controls for capture")))
            {
                controlsVisible = false;
            }

            GUI.Label(new Rect(panel.x + 12f, panel.y + 108f, 232f, 22f), "Key light direction");
            GUI.Label(new Rect(panel.x + 12f, panel.y + 132f, 42f, 22f), "Yaw");
            float requestedYaw = GUI.HorizontalSlider(
                new Rect(panel.x + 56f, panel.y + 137f, 188f, 20f), keyLightYaw, -180f, 180f);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 158f, 42f, 22f), "Pitch");
            float requestedPitch = GUI.HorizontalSlider(
                new Rect(panel.x + 56f, panel.y + 163f, 188f, 20f), keyLightPitch, -80f, 80f);
            if (!Mathf.Approximately(requestedYaw, keyLightYaw) ||
                !Mathf.Approximately(requestedPitch, keyLightPitch))
            {
                SetKeyLightDirection(requestedYaw, requestedPitch);
            }
            if (GUI.Button(new Rect(panel.x + 12f, panel.y + 188f, 232f, 28f),
                new GUIContent("Reset Light (L)", "Restore the approved key-light direction")))
            {
                ResetKeyLight();
            }

            DrawAnimationControls(panel);
            if (itemSelector != null)
            {
                itemSelector.Draw(new Rect(panel.x + 12f, panel.y + (animationTransport != null && animationTransport.Available ? 402f : 224f), 232f, 190f));
            }
        }

        private void DrawAnimationControls(Rect panel)
        {
            if (animationTransport == null || !animationTransport.Available)
            {
                return;
            }

            GUI.Label(new Rect(panel.x + 12f, panel.y + 224f, 232f, 22f), "Animation preview");
            string[] clipNames = animationTransport.ClipNames;
            GUI.SetNextControlName(AnimationSelectorControl);
            int requestedIndex = GUI.SelectionGrid(
                new Rect(panel.x + 12f, panel.y + 248f, 232f, 28f),
                animationTransport.SelectedIndex,
                clipNames,
                Mathf.Max(1, clipNames.Length));
            if (requestedIndex != animationTransport.SelectedIndex)
            {
                animationTransport.Select(requestedIndex);
            }

            GUI.SetNextControlName(PlayPauseControl);
            string playLabel = animationTransport.Playback == PackageBuilderPlaybackState.Playing
                ? "Pause"
                : "Play";
            if (GUI.Button(
                new Rect(panel.x + 12f, panel.y + 282f, 112f, 28f),
                new GUIContent(playLabel, "Play or pause the selected animation (Enter or Space)")))
            {
                TogglePlayback();
            }

            GUI.SetNextControlName(ReplayControl);
            if (GUI.Button(
                new Rect(panel.x + 132f, panel.y + 282f, 112f, 28f),
                new GUIContent("Replay", "Restart the selected animation (Enter or Space)")))
            {
                animationTransport.Replay();
            }

            GUI.Label(new Rect(panel.x + 12f, panel.y + 316f, 232f, 20f),
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0:0.00} s / {1:0.00} s",
                    animationTransport.CurrentTimeSeconds,
                    animationTransport.DurationSeconds));
            GUI.SetNextControlName(TimelineControl);
            float requestedTime = GUI.HorizontalSlider(
                new Rect(panel.x + 12f, panel.y + 340f, 232f, 20f),
                animationTransport.CurrentTimeSeconds,
                0f,
                animationTransport.DurationSeconds);
            if (!Mathf.Approximately(requestedTime, animationTransport.CurrentTimeSeconds))
            {
                animationTransport.Scrub(requestedTime);
            }

            GUI.SetNextControlName(LoopControl);
            bool requestedLoop = GUI.Toggle(
                new Rect(panel.x + 12f, panel.y + 366f, 232f, 24f),
                animationTransport.LoopEnabled,
                new GUIContent("Loop preview", "Override looping for preview only (Enter or Space)"));
            if (requestedLoop != animationTransport.LoopEnabled)
            {
                animationTransport.SetLoop(requestedLoop);
            }

            DrawKeyboardFocus(panel);
        }

        private bool HandleAnimationKeyboardEvent(Event currentEvent)
        {
            if (!controlsVisible || (itemSelector == null && (animationTransport == null || !animationTransport.Available)))
            {
                return false;
            }

            string focused = GUI.GetNameOfFocusedControl();
            if (currentEvent.keyCode == KeyCode.Tab)
            {
                GUI.FocusControl(NextAnimationControl(focused, currentEvent.shift));
                currentEvent.Use();
                return true;
            }

            if (itemSelector != null && itemSelector.HandleKeyboard(currentEvent, focused)) { return true; }
            if (animationTransport == null || !animationTransport.Available) { return false; }

            if (focused == AnimationSelectorControl &&
                (currentEvent.keyCode == KeyCode.LeftArrow ||
                    currentEvent.keyCode == KeyCode.RightArrow))
            {
                int direction = currentEvent.keyCode == KeyCode.LeftArrow ? -1 : 1;
                int count = animationTransport.ClipNames.Length;
                animationTransport.Select((animationTransport.SelectedIndex + direction + count) % count);
            }
            else if (focused == TimelineControl &&
                (currentEvent.keyCode == KeyCode.LeftArrow ||
                    currentEvent.keyCode == KeyCode.RightArrow))
            {
                float direction = currentEvent.keyCode == KeyCode.LeftArrow ? -1f : 1f;
                animationTransport.Scrub(Mathf.Max(
                    0f,
                    animationTransport.CurrentTimeSeconds +
                        direction * PackageBuilderAnimationTransport.KeyboardTimelineStepSeconds));
            }
            else if (IsActivationKey(currentEvent) && focused == PlayPauseControl)
            {
                TogglePlayback();
            }
            else if (IsActivationKey(currentEvent) && focused == ReplayControl)
            {
                animationTransport.Replay();
            }
            else if (IsActivationKey(currentEvent) && focused == LoopControl)
            {
                animationTransport.SetLoop(!animationTransport.LoopEnabled);
            }
            else
            {
                return false;
            }

            currentEvent.Use();
            return true;
        }

        private void TogglePlayback()
        {
            if (animationTransport.Playback == PackageBuilderPlaybackState.Playing)
            {
                animationTransport.Pause();
            }
            else
            {
                animationTransport.Play();
            }
        }

        private static bool IsActivationKey(Event currentEvent) =>
            currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter ||
            currentEvent.keyCode == KeyCode.Space;

        private string NextAnimationControl(string current, bool reverse)
        {
            var available = new System.Collections.Generic.List<string>();
            if (itemSelector != null)
            {
                available.AddRange(new[] { PackageBuilderItemSelector.PreviousControl, PackageBuilderItemSelector.NextControl,
                    PackageBuilderItemSelector.SelectControl, PackageBuilderItemSelector.AllControl });
            }
            if (animationTransport != null && animationTransport.Available)
            {
                available.AddRange(new[] { AnimationSelectorControl, PlayPauseControl, ReplayControl, TimelineControl, LoopControl });
            }
            string[] controls = available.ToArray();
            int index = System.Array.IndexOf(controls, current);
            if (index < 0)
            {
                return reverse ? controls[controls.Length - 1] : controls[0];
            }

            int direction = reverse ? -1 : 1;
            return controls[(index + direction + controls.Length) % controls.Length];
        }

        private static void DrawKeyboardFocus(Rect panel)
        {
            string focused = GUI.GetNameOfFocusedControl();
            Rect focusRect;
            if (focused == AnimationSelectorControl)
            {
                focusRect = new Rect(panel.x + 9f, panel.y + 245f, 238f, 34f);
            }
            else if (focused == PlayPauseControl)
            {
                focusRect = new Rect(panel.x + 9f, panel.y + 279f, 118f, 34f);
            }
            else if (focused == ReplayControl)
            {
                focusRect = new Rect(panel.x + 129f, panel.y + 279f, 118f, 34f);
            }
            else if (focused == TimelineControl)
            {
                focusRect = new Rect(panel.x + 9f, panel.y + 334f, 238f, 28f);
            }
            else if (focused == LoopControl)
            {
                focusRect = new Rect(panel.x + 9f, panel.y + 363f, 238f, 30f);
            }
            else
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = new Color(0.35f, 0.75f, 1f, 1f);
            GUI.DrawTexture(new Rect(focusRect.x, focusRect.y, focusRect.width, 2f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(focusRect.x, focusRect.yMax - 2f, focusRect.width, 2f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(focusRect.x, focusRect.y, 2f, focusRect.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(focusRect.xMax - 2f, focusRect.y, 2f, focusRect.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private Rect OverlayRect() => new(
            12f,
            12f,
            256f,
            (animationTransport != null && animationTransport.Available ? 402f : 228f) + (itemSelector != null ? 190f : 0f));

        private static Rect RestoreControlsRect() => new(12f, 12f, 120f, 32f);

        [ContextMenu("Auto Frame Preview")]
        private void AutoFrameFromInspector() => AutoFrame();

        [ContextMenu("Orbit Preview 15 Degrees")]
        private void OrbitFromInspector() => Orbit(15f, 0f);

        [ContextMenu("Zoom Preview In")]
        private void ZoomFromInspector() => Zoom(0.2f);
    }
}
