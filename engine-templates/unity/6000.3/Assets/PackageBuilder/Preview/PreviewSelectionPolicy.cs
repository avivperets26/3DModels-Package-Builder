// Unity's .NET Standard runtime has no ArgumentOutOfRangeException.ThrowIfNegative.
#pragma warning disable CA1512
using System;

namespace PackageBuilder.PreviewContract
{
    /// <summary>Engine-neutral index transitions; -1 means overview (or empty). Linked into Domain and Unity.</summary>
    public static class PreviewSelectionPolicy
    {
        public const bool InitiallyShowAll = true;
        public const bool WrapPreviousNext = true;

        /// <summary>Returns the initial visible item index without changing declaration order.</summary>
        public static int InitialIndex(int count, bool showAll) => count < 0 ? throw new ArgumentOutOfRangeException(nameof(count)) : count == 0 || showAll ? -1 : 0;

        /// <summary>Moves one step from a selected item or overview, respecting explicit wrap policy.</summary>
        public static int Move(int count, int selectedIndex, int direction, bool wrap)
        {
            if (count < 0)
            { throw new ArgumentOutOfRangeException(nameof(count)); }
            if (selectedIndex < -1 || selectedIndex >= count)
            { throw new ArgumentOutOfRangeException(nameof(selectedIndex)); }
            if (direction != -1 && direction != 1)
            { throw new ArgumentOutOfRangeException(nameof(direction)); }
            if (count == 0)
            { return -1; }
            int origin = selectedIndex < 0 ? (direction > 0 ? -1 : count) : selectedIndex;
            int target = origin + direction;
            return wrap ? (target % count + count) % count : Math.Min(Math.Max(target, 0), count - 1);
        }
    }
}
