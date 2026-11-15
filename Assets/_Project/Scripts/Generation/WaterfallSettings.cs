using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The waterfall where a pond reaches the island edge (M4 PR 4.8): one continuous curtain of broad faceted columns per stretch of
    /// edge, rolling over a foam brink, bulging out as it falls, turning pale blue and fading to transparent at its foot. The water
    /// shader makes its tone flow down.
    /// Sizes are in metres. Visual only.
    /// </summary>
    [CreateAssetMenu(fileName = "Waterfall", menuName = "Iso Golf/Waterfall")]
    public sealed class WaterfallSettings : ScriptableObject
    {
        [Header("Fall")]
        [Tooltip("How far the sheet falls below the water level, where it has faded into the sky.")]
        [SerializeField, Min(0.5f)] private float fallDepth = 6f;

        [Tooltip("Height of one row of facets near the bottom; rows are denser near the lip. At most 15 rows.")]
        [SerializeField, Min(0.4f)] private float rowHeight = 1.2f;

        [Tooltip("Radius of the rounded brink: the water leaves the pond level and rolls over the lip along it.")]
        [SerializeField, Min(0.05f)] private float brinkRadius = 0.25f;

        [Tooltip("How much farther out the water has gone once past the brink: it falls in an arc, not down a wall.")]
        [SerializeField, Min(0f)] private float reach = 0.45f;

        [Tooltip("The fall below the brink over which the arc reaches its full reach.")]
        [SerializeField, Min(0.2f)] private float arcLength = 1.5f;

        [Tooltip("How much farther out the water goes for each metre it falls after the arc.")]
        [SerializeField, Range(0f, 0.3f)] private float leanPerMetre = 0.03f;

        [Header("Sheet")]
        [Tooltip("Width of the sheet at its foot, as a share of its width at the lip.")]
        [SerializeField, Range(0.1f, 1f)] private float taper = 0.8f;

        [Tooltip("The most each end of the sheet pulls in by its foot, so a long edge stays a wide curtain.")]
        [SerializeField, Min(0f)] private float maxEndInset = 0.6f;

        [Tooltip("Width of one column of facets: broad columns, each lit on its own, give the curtain its volume.")]
        [SerializeField, Min(0.1f)] private float columnWidth = 0.6f;

        [Tooltip("Shift of the inner vertices, as a fraction of a column. Keeps the sheet faceted, not a grid.")]
        [SerializeField, Range(0f, 0.4f)] private float jitter = 0.1f;

        [Tooltip("How far the middle of the curtain bulges out past its ends, at most. The lip stays on the edge.")]
        [SerializeField, Min(0f)] private float maxBulge = 0.45f;

        [Tooltip("Bulge per metre of run, so a short run bulges less (capped by the max bulge).")]
        [SerializeField, Min(0f)] private float bulgePerMetre = 0.12f;

        [Tooltip("How far each column's light turns left or right, at most, in degrees: the columns catch the light differently.")]
        [SerializeField, Range(0f, 30f)] private float columnYaw = 12f;

        [Tooltip("Up and down shift of the foot's vertices: a calm, slightly uneven lower edge, never spikes.")]
        [SerializeField, Range(0f, 0.3f)] private float footJitter = 0.1f;

        [Header("Look")]
        [Tooltip("The foam at the brink and in the streaks, sRGB.")]
        [SerializeField] private Color foamTone = new Color(0.96f, 0.98f, 1f, 1f);

        [Tooltip("The pale blue the falling water turns to below the foam, sRGB.")]
        [SerializeField] private Color fadeTone = new Color(0.74f, 0.87f, 0.95f, 1f);

        [Tooltip("Share of the way down by which the foam has turned to the pale blue.")]
        [SerializeField, Range(0.05f, 1f)] private float foamEnd = 0.45f;

        [Tooltip("Share of the columns that carry a whiter streak down their upper half.")]
        [SerializeField, Range(0f, 1f)] private float streakShare = 0.35f;

        [Tooltip("Share of the way down where the water starts to turn transparent; it is fully clear at the foot.")]
        [SerializeField, Range(0f, 1f)] private float fadeStart = 0.35f;

        [Tooltip("How far the lighting normal turns from straight up toward the column's outward face below the brink. 0 lights the whole sheet like the pond.")]
        [SerializeField, Range(0f, 1f)] private float normalTilt = 0.6f;

        [Header("Edge pieces")]
        [Tooltip("Edge pieces whose ends are closer than this join into one run. Small, so a waterfall never bridges over land.")]
        [SerializeField, Min(0.005f)] private float gapTolerance = 0.05f;

        [Tooltip("Runs shorter than this get no waterfall.")]
        [SerializeField, Min(0f)] private float minRunLength = 0.6f;

        public float FallDepth => fallDepth;
        public float RowHeight => rowHeight;
        public float BrinkRadius => brinkRadius;
        public float Reach => reach;
        public float ArcLength => arcLength;
        public float LeanPerMetre => leanPerMetre;
        public float Taper => taper;
        public float MaxEndInset => maxEndInset;
        public float ColumnWidth => columnWidth;
        public float Jitter => jitter;
        public float MaxBulge => maxBulge;
        public float BulgePerMetre => bulgePerMetre;
        public float ColumnYaw => columnYaw;
        public float FootJitter => footJitter;
        public Color FoamTone => foamTone;
        public Color FadeTone => fadeTone;
        public float FoamEnd => foamEnd;
        public float StreakShare => streakShare;
        public float FadeStart => fadeStart;
        public float NormalTilt => normalTilt;
        public float GapTolerance => gapTolerance;
        public float MinRunLength => minRunLength;
    }
}
