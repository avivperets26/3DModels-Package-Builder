using UnityEngine;

namespace PackageBuilder.Preview
{
    /// <summary>
    /// Frames and navigates a generated product preview by moving only the preview camera.
    /// Product transforms are never translated, rotated, or scaled by this component.
    /// </summary>
    public sealed class PackageBuilderPreviewController : MonoBehaviour
    {
        private const float MinimumBoundsSize = 0.01f;
        private const float MinimumDistanceMultiplier = 0.15f;
        private const float MaximumDistanceMultiplier = 50f;

        [SerializeField]
        private Transform previewTarget;

        [SerializeField]
        private Camera previewCamera;

        [SerializeField]
        [Min(1.01f)]
        private float framingPadding = 1.25f;

        /// <summary>Gets the product container whose renderers define the framing bounds.</summary>
        public Transform PreviewTarget => previewTarget;

        /// <summary>Gets the camera moved by framing, orbit, and zoom operations.</summary>
        public Camera PreviewCamera => previewCamera;

        /// <summary>Assigns the scene references used by all navigation operations.</summary>
        public void Configure(Transform target, Camera camera)
        {
            previewTarget = target;
            previewCamera = camera;
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
            float verticalDistance = bounds.extents.y / Mathf.Tan(verticalHalfField);
            float horizontalDistance = bounds.extents.x / Mathf.Tan(horizontalHalfField);
            float distance = (Mathf.Max(verticalDistance, horizontalDistance) + bounds.extents.z) *
                Mathf.Max(1.01f, framingPadding);
            distance = Mathf.Max(distance, bounds.size.magnitude);

            Vector3 direction = previewCamera.transform.position - bounds.center;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = new Vector3(1f, 0.65f, -1f);
            }

            previewCamera.transform.position = bounds.center + direction.normalized * distance;
            previewCamera.transform.LookAt(bounds.center, Vector3.up);
            previewCamera.nearClipPlane = Mathf.Max(0.01f, distance - bounds.extents.magnitude * 2f);
            previewCamera.farClipPlane = Mathf.Max(previewCamera.nearClipPlane + 1f,
                distance + bounds.extents.magnitude * 4f);
            return true;
        }

        /// <summary>Orbits the camera around the current product bounds without touching the product.</summary>
        public bool Orbit(float yawDegrees, float pitchDegrees)
        {
            if (!TryGetProductBounds(out Bounds bounds) || previewCamera == null)
            {
                return false;
            }

            Vector3 offset = previewCamera.transform.position - bounds.center;
            if (offset.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Quaternion yaw = Quaternion.AngleAxis(yawDegrees, Vector3.up);
            Vector3 yawedOffset = yaw * offset;
            Vector3 pitchAxis = Vector3.Cross(Vector3.up, yawedOffset.normalized);
            if (pitchAxis.sqrMagnitude > 0.0001f)
            {
                yawedOffset = Quaternion.AngleAxis(pitchDegrees, pitchAxis.normalized) * yawedOffset;
            }

            previewCamera.transform.position = bounds.center + yawedOffset;
            previewCamera.transform.LookAt(bounds.center, Vector3.up);
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
                if (rendererValue == null || !rendererValue.enabled)
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

        [ContextMenu("Auto Frame Preview")]
        private void AutoFrameFromInspector()
        {
            AutoFrame();
        }

        [ContextMenu("Orbit Preview 15 Degrees")]
        private void OrbitFromInspector()
        {
            Orbit(15f, 0f);
        }

        [ContextMenu("Zoom Preview In")]
        private void ZoomFromInspector()
        {
            Zoom(0.2f);
        }
    }
}
