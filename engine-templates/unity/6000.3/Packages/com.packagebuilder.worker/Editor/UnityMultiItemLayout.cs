using System;
using System.Linq;
using PackageBuilder.MultiItem;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Adapts current renderer bounds to the shared layout policy; modifies scene instances only.</summary>
    internal static class UnityMultiItemLayout
    {
        /// <summary>Measures reset-parent instances and applies or verifies their deterministic translations.</summary>
        internal static bool Arrange(GameObject[] items, double gap, bool verifyOnly = false)
        {
            var measurements = new ItemBounds[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                Renderer[] renderers = items[index].GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { return false; }
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers.Skip(1)) { bounds.Encapsulate(renderer.bounds); }
                Vector3 offset = verifyOnly ? items[index].transform.localPosition : Vector3.zero;
                measurements[index] = new ItemBounds { ItemId = items[index].name,
                    MinX = bounds.min.x - offset.x, MaxX = bounds.max.x - offset.x,
                    MinY = bounds.min.y - offset.y, MaxY = bounds.max.y - offset.y,
                    MinZ = bounds.min.z - offset.z, MaxZ = bounds.max.z - offset.z };
            }
            ItemPlacement[] layout;
            if (!OverviewLayoutPolicy.TryPlan(measurements, gap, out layout)) { return false; }
            for (int index = 0; index < items.Length; index++)
            {
                var position = new Vector3((float)layout[index].X, (float)layout[index].Y, (float)layout[index].Z);
                if (verifyOnly) { if ((items[index].transform.localPosition - position).sqrMagnitude > 0.000001f) { return false; } }
                else { items[index].transform.localPosition = position; }
            }
            return true;
        }
    }
}
