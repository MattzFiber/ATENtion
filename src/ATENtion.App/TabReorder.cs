using System.Collections.Generic;

namespace ATENtion.App
{
    /// <summary>Where a tab being dragged should sit in the strip.</summary>
    internal static class TabReorder
    {
        /// <summary>
        /// The index for the dragged tab: the number of other tabs whose midpoint lies left of the
        /// pointer.
        /// </summary>
        /// <remarks>
        /// Measuring against the other tabs rather than the dragged one keeps it stable when widths
        /// differ. A move shifts the tabs it passes by the dragged tab's width, away from the
        /// pointer, so the same position gives the same answer instead of swapping back.
        /// </remarks>
        /// <param name="otherMidpoints">Horizontal midpoints of every tab except the dragged one.</param>
        /// <param name="pointerX">The pointer's horizontal position, in the same coordinates.</param>
        internal static int TargetIndex(IEnumerable<double> otherMidpoints, double pointerX)
        {
            int index = 0;
            foreach (double midpoint in otherMidpoints)
                if (pointerX > midpoint) index++;
            return index;
        }
    }
}
