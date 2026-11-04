using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// How one screen size maps onto the low-res render: all the numbers the camera and the upscale need.
    /// </summary>
    public readonly struct PixelFrame
    {
        public PixelFrame(int scale, Vector2Int viewSize, Vector2Int renderSize, float orthographicSize, Vector2 uvScale, Vector2 uvOffset)
        {
            Scale = scale;
            ViewSize = viewSize;
            RenderSize = renderSize;
            OrthographicSize = orthographicSize;
            UvScale = uvScale;
            UvOffset = uvOffset;
        }

        /// <summary>Integer upscale: screen pixels per game pixel.</summary>
        public int Scale { get; }

        /// <summary>Game pixels visible on screen.</summary>
        public Vector2Int ViewSize { get; }

        /// <summary>Render texture size: the view plus the margin on every side.</summary>
        public Vector2Int RenderSize { get; }

        public float OrthographicSize { get; }

        /// <summary>Part of the render texture that lands on screen.</summary>
        public Vector2 UvScale { get; }

        /// <summary>Where that part starts, skipping the margin.</summary>
        public Vector2 UvOffset { get; }
    }

    /// <summary>
    /// Where the camera really renders from, and how far the image must shift to make up the difference.
    /// </summary>
    public readonly struct PixelSnap
    {
        public PixelSnap(Vector3 position, Vector2 residualTexels)
        {
            Position = position;
            ResidualTexels = residualTexels;
        }

        /// <summary>Camera position on the texel grid.</summary>
        public Vector3 Position { get; }

        /// <summary>Requested position minus snapped position, in game pixels along the screen axes.</summary>
        public Vector2 ResidualTexels { get; }
    }

    public static class PixelGrid
    {
        /// <summary>
        /// Rounds a camera position to whole game pixels in the view plane.
        /// Moving only by whole pixels makes every surface land on the same pixels each frame,
        /// which is what stops edges from shimmering while panning.
        /// </summary>
        public static PixelSnap Snap(Vector3 position, Quaternion rotation, int pixelsPerUnit)
        {
            // Rounded in view space, not world space: the camera is rotated 45 degrees, so a
            // world-space grid would not line up with the screen pixels. Depth is left alone
            // because it does not move anything on screen in an orthographic view.
            Vector3 view = Quaternion.Inverse(rotation) * position;

            float texelX = Mathf.Round(view.x * pixelsPerUnit);
            float texelY = Mathf.Round(view.y * pixelsPerUnit);

            var snappedView = new Vector3(texelX / pixelsPerUnit, texelY / pixelsPerUnit, view.z);
            var residual = new Vector2((view.x * pixelsPerUnit) - texelX, (view.y * pixelsPerUnit) - texelY);

            return new PixelSnap(rotation * snappedView, residual);
        }

        /// <summary>
        /// Texture offset for the upscale that shows the requested position rather than the snapped one.
        /// The residual is rounded to whole screen pixels, so every game pixel stays exactly
        /// <see cref="PixelFrame.Scale"/> screen pixels wide while panning.
        /// </summary>
        public static Vector2 PresentOffset(in PixelFrame frame, Vector2 residualTexels, int marginTexels)
        {
            float x = Mathf.Round(residualTexels.x * frame.Scale) / frame.Scale;
            float y = Mathf.Round(residualTexels.y * frame.Scale) / frame.Scale;

            return new Vector2((marginTexels + x) / frame.RenderSize.x, (marginTexels + y) / frame.RenderSize.y);
        }

        public static PixelFrame Solve(Vector2Int screenSize, int targetWidth, int pixelsPerUnit, int marginTexels)
        {
            int width = Mathf.Max(1, screenSize.x);
            int height = Mathf.Max(1, screenSize.y);

            // Nearest scale rather than floor: floor would give a 720 px phone a 360 px wide game,
            // showing 50% more world than the 270 px target. Nearest gives 240 px instead.
            int scale = Mathf.Max(1, (int)System.Math.Round(width / (double)targetWidth, System.MidpointRounding.AwayFromZero));

            // Rounding up leaves up to (scale - 1) screen pixels cropped rather than a black bar.
            var viewSize = new Vector2Int(CeilDivide(width, scale), CeilDivide(height, scale));
            var renderSize = new Vector2Int(viewSize.x + 2 * marginTexels, viewSize.y + 2 * marginTexels);

            // Derived from the pixel density, so one world unit is the same size on every phone:
            // a taller screen shows more world, it does not stretch it.
            float orthographicSize = renderSize.y / (2f * pixelsPerUnit);

            var uvScale = new Vector2(width / (float)(scale * renderSize.x), height / (float)(scale * renderSize.y));
            var uvOffset = new Vector2(marginTexels / (float)renderSize.x, marginTexels / (float)renderSize.y);

            return new PixelFrame(scale, viewSize, renderSize, orthographicSize, uvScale, uvOffset);
        }

        private static int CeilDivide(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
