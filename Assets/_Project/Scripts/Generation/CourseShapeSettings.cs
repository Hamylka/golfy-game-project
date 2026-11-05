using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>An organic oval on the ground, used for water, bunkers and greens.</summary>
    [Serializable]
    public struct EllipseShape
    {
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 radius;
        [SerializeField] private float wobble;

        public EllipseShape(Vector2 center, Vector2 radius, float wobble)
        {
            this.center = center;
            this.radius = radius;
            this.wobble = wobble;
        }

        public Vector2 Center => center;
        public Vector2 Radius => radius;
        public float Wobble => wobble;
    }

    /// <summary>A broad rise, or a dip when its height is negative.</summary>
    [Serializable]
    public struct GroundSwell
    {
        [SerializeField] private Vector2 center;
        [SerializeField] private float radius;
        [SerializeField] private float height;

        public GroundSwell(Vector2 center, float radius, float height)
        {
            this.center = center;
            this.radius = radius;
            this.height = height;
        }

        public Vector2 Center => center;
        public float Radius => radius;
        public float Height => height;
    }

    /// <summary>
    /// A hand-written course shape, with the X axis running from tee to hole. The defaults are the
    /// par 3 from the look test. M3's generator will produce these values from a seed.
    /// </summary>
    [CreateAssetMenu(fileName = "CourseShape", menuName = "Iso Golf/Course Shape")]
    public sealed class CourseShapeSettings : ScriptableObject
    {
        [Header("Island")]
        [SerializeField, Min(1f)] private float halfLength = 17f;
        [SerializeField, Min(0.5f)] private float halfWidth = 5.2f;
        [SerializeField, Min(0f)] private float widthWobble = 3.2f;
        [SerializeField, Min(0f)] private float widthNoiseScale = 0.18f;

        [Header("Fairway")]
        [SerializeField] private float fairwayStart = -15.5f;
        [SerializeField] private float fairwayEnd = 10.5f;
        [SerializeField, Min(0.5f)] private float fairwayHalfWidth = 2.5f;
        [SerializeField, Min(0f)] private float fairwayWobble = 1.6f;
        [SerializeField, Min(0.1f)] private float stripeWidth = 1f;

        [Header("Surfaces")]
        [SerializeField] private EllipseShape green = new EllipseShape(new Vector2(12.8f, 0.2f), new Vector2(3f, 2.7f), 0.12f);
        [SerializeField] private EllipseShape bunker = new EllipseShape(new Vector2(9.4f, -2.9f), new Vector2(1.8f, 1.1f), 0.2f);
        [SerializeField] private EllipseShape pond = new EllipseShape(new Vector2(-1.5f, 3.4f), new Vector2(3.2f, 1.6f), 0.25f);
        [SerializeField] private float waterLevel = -0.22f;

        [Header("Ground")]
        [Tooltip("Metres between terrain vertices. Larger means bigger facets.")]
        [SerializeField, Min(0.25f)] private float cellSize = 1f;
        [Tooltip("Height variation at the play axis, in metres. Small, so the shot line stays calm.")]
        [SerializeField, Min(0f)] private float axisRelief = 0.05f;
        [Tooltip("Height variation at the edge of the island, in metres.")]
        [SerializeField, Min(0f)] private float edgeRelief = 0.4f;
        [SerializeField, Min(0.1f)] private float reliefDistance = 5f;
        [SerializeField, Min(0f)] private float reliefNoiseScale = 0.55f;

        [Header("Slopes")]
        [Tooltip("Broad rises and dips the ball feels. M3 generates these from a seed.")]
        [SerializeField] private GroundSwell[] swells =
        {
            new GroundSwell(new Vector2(-6f, 0.5f), 4f, 0.45f),
            new GroundSwell(new Vector2(6f, -0.5f), 3f, -0.25f),
        };

        [Tooltip("The green's rise per metre along X and along Z. 0.05 is a 5% slope.")]
        [SerializeField] private Vector2 greenTilt = new Vector2(0.03f, 0.04f);

        [Header("Play")]
        [SerializeField] private Vector2 tee = new Vector2(-14f, 0f);
        [SerializeField] private Vector2 hole = new Vector2(14.2f, 0.4f);
        [Tooltip("The ground is flat this far around the tee and the hole, in metres.")]
        [SerializeField, Min(0f)] private float flatRadius = 1.2f;
        [Tooltip("How far the ground then takes to return to its full slope, in metres.")]
        [SerializeField, Min(0.01f)] private float flatBlend = 0.8f;

        [Header("Edge")]
        [Tooltip("Shortest and longest depth of the earth band under the island edge, in metres.")]
        [SerializeField] private Vector2 skirtDepth = new Vector2(0.9f, 1.8f);
        [SerializeField, Min(0f)] private float skirtNoiseScale = 0.7f;

        public float HalfLength => halfLength;
        public float HalfWidth => halfWidth;
        public float WidthWobble => widthWobble;
        public float WidthNoiseScale => widthNoiseScale;
        public float FairwayStart => fairwayStart;
        public float FairwayEnd => fairwayEnd;
        public float FairwayHalfWidth => fairwayHalfWidth;
        public float FairwayWobble => fairwayWobble;
        public float StripeWidth => stripeWidth;
        public EllipseShape Green => green;
        public EllipseShape Bunker => bunker;
        public EllipseShape Pond => pond;
        public float WaterLevel => waterLevel;
        public float CellSize => cellSize;
        public float AxisRelief => axisRelief;
        public float EdgeRelief => edgeRelief;
        public float ReliefDistance => reliefDistance;
        public float ReliefNoiseScale => reliefNoiseScale;
        public GroundSwell[] Swells => swells;
        public Vector2 GreenTilt => greenTilt;
        public Vector2 Tee => tee;
        public Vector2 Hole => hole;
        public float FlatRadius => flatRadius;
        public float FlatBlend => flatBlend;
        public Vector2 SkirtDepth => skirtDepth;
        public float SkirtNoiseScale => skirtNoiseScale;
    }
}
