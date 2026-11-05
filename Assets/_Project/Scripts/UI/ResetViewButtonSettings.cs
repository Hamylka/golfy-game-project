using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>The look and size of the reset view button. Starting values, for the M2 tuning PR to judge on the phone.</summary>
    [CreateAssetMenu(fileName = "ResetViewButton", menuName = "Iso Golf/Reset View Button")]
    public sealed class ResetViewButtonSettings : ScriptableObject
    {
        [Tooltip("The drawn button, as a share of the screen's width. Small, to stay discreet.")]
        [SerializeField, Range(0.02f, 0.2f)] private float size = 0.07f;

        [Tooltip("The touch area, as a share of the screen's width. Bigger than the drawing, for a thumb.")]
        [SerializeField, Range(0.05f, 0.3f)] private float hitSize = 0.13f;

        [Tooltip("The gap from the touch area to the edges of the safe area, as a share of the screen's width.")]
        [SerializeField, Range(0f, 0.2f)] private float margin = 0.02f;

        [Tooltip("The ring's width, as a share of the button's size.")]
        [SerializeField, Range(0.02f, 0.3f)] private float ringThickness = 0.1f;

        [Tooltip("The dot in the middle, as a share of the button's size.")]
        [SerializeField, Range(0.02f, 0.4f)] private float dotRadius = 0.12f;

        [Tooltip("How much smaller the button draws while it's held down.")]
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.88f;

        [SerializeField, Min(8)] private int segments = 28;

        [Tooltip("How far in front of the camera the button is drawn, in metres. Between the near plane and the scenery.")]
        [SerializeField, Min(0.1f)] private float depth = 2f;

        public float Size => size;
        public float HitSize => hitSize;
        public float Margin => margin;
        public float RingThickness => ringThickness;
        public float DotRadius => dotRadius;
        public float PressedScale => pressedScale;
        public int Segments => segments;
        public float Depth => depth;
    }
}
