using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>The look of the aim preview. These are starting values, for the M2 tuning PR to judge on the phone.</summary>
    [CreateAssetMenu(fileName = "AimPreview", menuName = "Iso Golf/Aim Preview")]
    public sealed class AimPreviewSettings : ScriptableObject
    {
        [Header("Dots")]
        [Tooltip("In metres, so dots shrink with distance like everything else on the course.")]
        [SerializeField, Min(0.005f)] private float dotRadius = 0.05f;
        [SerializeField, Min(3)] private int dotSegments = 8;
        [Tooltip("Ground distance between dots on an arc, in metres.")]
        [SerializeField, Min(0.05f)] private float arcDotSpacing = 0.6f;
        [Tooltip("Ground distance between dots on a putt, in metres.")]
        [SerializeField, Min(0.05f)] private float puttDotSpacing = 0.25f;
        [Tooltip("How much of a putt's true path the dots show, by distance. The rest is left to read from the slope.")]
        [SerializeField, Range(0.05f, 1f)] private float puttShare = 0.4f;

        [Header("Landing ring")]
        [SerializeField, Min(0.01f)] private float ringRadius = 0.2f;
        [SerializeField, Min(0.005f)] private float ringInnerRadius = 0.14f;
        [Tooltip("Rings of vertices across the band, at least 2. More follow a crease between facets more closely.")]
        [SerializeField, Min(2)] private int ringVertexRings = 2;
        [SerializeField, Min(3)] private int ringSegments = 20;
        [Tooltip("How far the ring floats above the ground, in metres. It must stay above the ball's shadow.")]
        [SerializeField, Min(0f)] private float ringLift = 0.03f;

        [Header("Power gauge")]
        [Tooltip("As a share of the screen's width, so it reads the same at every zoom and every distance.")]
        [SerializeField, Range(0.02f, 0.4f)] private float gaugeWidth = 0.08f;
        [Tooltip("As a share of the screen's width.")]
        [SerializeField, Range(0.005f, 0.1f)] private float gaugeHeight = 0.014f;
        [Tooltip("As a share of the screen's width.")]
        [SerializeField, Range(0.001f, 0.02f)] private float gaugeBorder = 0.003f;
        [Tooltip("The gap from the top of the ball to the gauge, as a share of the screen's width.")]
        [SerializeField, Range(0f, 0.2f)] private float gaugeGap = 0.015f;

        public float DotRadius => dotRadius;
        public int DotSegments => dotSegments;
        public float ArcDotSpacing => arcDotSpacing;
        public float PuttDotSpacing => puttDotSpacing;
        public float PuttShare => puttShare;
        public float RingRadius => ringRadius;
        public float RingInnerRadius => ringInnerRadius;
        public int RingVertexRings => ringVertexRings;
        public int RingSegments => ringSegments;
        public float RingLift => ringLift;
        public float GaugeWidth => gaugeWidth;
        public float GaugeHeight => gaugeHeight;
        public float GaugeBorder => gaugeBorder;
        public float GaugeGap => gaugeGap;
    }
}
