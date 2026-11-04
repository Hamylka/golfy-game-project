using UnityEngine;

namespace IsoGolf.Rendering
{
    [CreateAssetMenu(fileName = "PixelCameraSettings", menuName = "Iso Golf/Pixel Camera Settings")]
    public sealed class PixelCameraSettings : ScriptableObject
    {
        [Tooltip("Game width to aim for, in game pixels. The integer upscale picks the closest match per screen.")]
        [SerializeField, Min(1)] private int targetWidth = 270;

        [Tooltip("Game pixels per world unit. Sets how much of the island is visible.")]
        [SerializeField, Min(1)] private int pixelsPerUnit = 24;

        [Tooltip("Extra game pixels rendered on each side, so sub-pixel panning never exposes an empty edge.")]
        [SerializeField, Min(0)] private int marginTexels = 1;

        public int TargetWidth => targetWidth;
        public int PixelsPerUnit => pixelsPerUnit;
        public int MarginTexels => marginTexels;
    }
}
