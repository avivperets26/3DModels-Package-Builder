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

        [SerializeField] private Transform previewTarget;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private Transform studioBackground;
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

        /// <summary>Shows or hides the overlay so clean screenshots can be captured.</summary>
        public void SetControlsVisible(bool visible) => controlsVisible = visible;

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
                if (rendererValue == null || !rendererValue.enabled ||
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
                Zoom(-currentEvent.delta.y * WheelZoomSpeed);
                currentEvent.Use();
                return;
            }

            Rect overlay = OverlayRect();
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                dragging = !controlsVisible || !overlay.Contains(currentEvent.mousePosition);
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
        }

        private static Rect OverlayRect() => new(12f, 12f, 256f, 228f);

        [ContextMenu("Auto Frame Preview")]
        private void AutoFrameFromInspector() => AutoFrame();

        [ContextMenu("Orbit Preview 15 Degrees")]
        private void OrbitFromInspector() => Orbit(15f, 0f);

        [ContextMenu("Zoom Preview In")]
        private void ZoomFromInspector() => Zoom(0.2f);
    }
}
