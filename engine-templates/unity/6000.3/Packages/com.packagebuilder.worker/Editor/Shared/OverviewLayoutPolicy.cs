using System;
using System.Collections.Generic;

namespace PackageBuilder.MultiItem
{
    /// <summary>Engine-neutral measured item bounds at its unchanged prefab transform.</summary>
    public sealed class ItemBounds
    {
        public string ItemId { get; set; } = string.Empty;
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }

    /// <summary>One translation for a scene instance; source prefabs are never repositioned.</summary>
    public sealed class ItemPlacement
    {
        public string ItemId { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    /// <summary>Canonical ordered, grounded row layout shared by Application and Unity without engine dependencies.</summary>
    public static class OverviewLayoutPolicy
    {
        /// <summary>Rejects invalid/duplicate/unbounded measurements; preserves input order and the requested nonnegative gap.</summary>
        public static bool TryPlan(ItemBounds[] bounds, double gap, out ItemPlacement[] placements)
        {
            placements = Array.Empty<ItemPlacement>();
            if (bounds == null || bounds.Length == 0 || bounds.Length > 10000 || !Finite(gap) || gap < 0 || gap > 1000000)
            { return false; }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<ItemPlacement>();
            double cursor = 0;
            foreach (ItemBounds item in bounds)
            {
                if (item == null || string.IsNullOrEmpty(item.ItemId) || !ids.Add(item.ItemId) ||
                    !Axis(item.MinX, item.MaxX) || !Axis(item.MinY, item.MaxY) || !Axis(item.MinZ, item.MaxZ))
                { return false; }
                result.Add(new ItemPlacement { ItemId = item.ItemId, X = cursor - item.MinX, Y = -item.MinY, Z = -(item.MinZ + item.MaxZ) / 2 });
                cursor += item.MaxX - item.MinX + gap;
                if (!Finite(cursor) || cursor > 10000000)
                { return false; }
            }
            double center = (cursor - gap) / 2;
            foreach (ItemPlacement placement in result)
            { placement.X -= center; }
            placements = result.ToArray();
            return true;
        }

        private static bool Axis(double min, double max) => Finite(min) && Finite(max) && min <= max && Math.Abs(min) <= 1000000 && Math.Abs(max) <= 1000000;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
