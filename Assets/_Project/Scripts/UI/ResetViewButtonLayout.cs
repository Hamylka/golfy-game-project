using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>Where the reset view button sits on screen, in pixels from the bottom left.</summary>
    public readonly struct ResetViewButtonLayout
    {
        private ResetViewButtonLayout(Vector2 center, float size, Rect hit)
        {
            Center = center;
            Size = size;
            Hit = hit;
        }

        public Vector2 Center { get; }

        /// <summary>The drawn button's width and height.</summary>
        public float Size { get; }

        /// <summary>Where a touch counts as on the button. Bigger than the drawing, which is small to stay discreet.</summary>
        public Rect Hit { get; }

        /// <summary>
        /// Puts the button in the top right of the safe area. Everything is a share of the screen's width, so it
        /// has the same size on screen on any phone.
        /// </summary>
        /// <param name="safeArea">The part of the screen free of notches and rounded corners, in pixels.</param>
        /// <param name="screenWidth">The screen's width in pixels.</param>
        /// <param name="size">The drawn size, as a share of the screen width.</param>
        /// <param name="margin">The gap to the safe area's edges, as a share of the screen width.</param>
        /// <param name="hitSize">The size of the touch area, as a share of the screen width.</param>
        public static ResetViewButtonLayout TopRight(Rect safeArea, float screenWidth, float size, float margin, float hitSize)
        {
            float drawn = size * screenWidth;
            float hit = Mathf.Max(hitSize * screenWidth, drawn);

            // The touch area is what must clear the margin, so it never reaches over the edge of the safe area.
            var center = new Vector2(
                safeArea.xMax - (margin * screenWidth) - (hit * 0.5f),
                safeArea.yMax - (margin * screenWidth) - (hit * 0.5f));
            return new ResetViewButtonLayout(center, drawn, new Rect(center.x - (hit * 0.5f), center.y - (hit * 0.5f), hit, hit));
        }
    }
}
