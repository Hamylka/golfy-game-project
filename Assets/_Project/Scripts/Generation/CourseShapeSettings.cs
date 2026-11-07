using System;
using System.Collections.Generic;
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

    /// <summary>Another island of the same course, beside the main one along X. It shares the main island's width noise scale.</summary>
    [Serializable]
    public struct ExtraIsland
    {
        [SerializeField] private float centerX;
        [SerializeField, Min(1f)] private float halfLength;
        [SerializeField, Min(0.5f)] private float halfWidth;
        [SerializeField, Min(0f)] private float widthWobble;

        [Tooltip("Leave 0 for an island on the play axis. A dogleg's leg sits off to the side.")]
        [SerializeField] private float centerZ;

        [Tooltip("The island's long axis, in degrees from +X, positive toward +Z. 0 runs along the play axis.")]
        [SerializeField, Range(-45f, 45f)] private float heading;

        public ExtraIsland(float centerX, float halfLength, float halfWidth, float widthWobble, float centerZ = 0f, float heading = 0f)
        {
            this.centerX = centerX;
            this.halfLength = halfLength;
            this.halfWidth = halfWidth;
            this.widthWobble = widthWobble;
            this.centerZ = centerZ;
            this.heading = heading;
        }

        public float CenterX => centerX;
        public float CenterZ => centerZ;
        public float Heading => heading;
        public float HalfLength => halfLength;
        public float HalfWidth => halfWidth;
        public float WidthWobble => widthWobble;
    }

    /// <summary>
    /// A hand-written course shape, with the X axis running from tee to hole. The defaults are the
    /// par 3 from the look test. It's one source of a <see cref="CourseLayout"/>; M3's generator is the other.
    /// </summary>
    [CreateAssetMenu(fileName = "CourseShape", menuName = "Iso Golf/Course Shape")]
    public sealed class CourseShapeSettings : ScriptableObject
    {
        [Header("Island")]
        [SerializeField, Min(1f)] private float halfLength = 17f;
        [SerializeField, Min(0.5f)] private float halfWidth = 5.2f;
        [SerializeField, Min(0f)] private float widthWobble = 3.2f;
        [SerializeField, Min(0f)] private float widthNoiseScale = 0.18f;

        [Header("Gaps")]
        [Tooltip("Where the main island is centred along X. Leave 0 for a single island on the origin.")]
        [SerializeField] private float centerX;

        [Tooltip("More islands of the same course, with open sky between them. Empty for a course with no gap.")]
        [SerializeField] private ExtraIsland[] extraIslands = Array.Empty<ExtraIsland>();

        [Header("Fairway")]
        [Tooltip("When set, the fairway, its stripes and the calm band of the relief follow the centreline, and the fairway's start and end are distances along it from the tee. Off, they run along X (the par 3, pinned).")]
        [SerializeField] private bool alongCentreline;
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
        [Tooltip("Where the centreline turns between the tee and the hole, for a dogleg. Empty for a straight course.")]
        [SerializeField] private Vector2[] centrelineBends = Array.Empty<Vector2>();
        [Tooltip("The ground is flat this far around the tee and the hole, in metres.")]
        [SerializeField, Min(0f)] private float flatRadius = 1.2f;
        [Tooltip("How far the ground then takes to return to its full slope, in metres.")]
        [SerializeField, Min(0.01f)] private float flatBlend = 0.8f;

        [Header("Edge")]
        [Tooltip("Shortest and longest depth of the earth band under the island edge, in metres.")]
        [SerializeField] private Vector2 skirtDepth = new Vector2(0.9f, 1.8f);
        [SerializeField, Min(0f)] private float skirtNoiseScale = 0.7f;

        public float CenterX => centerX;
        public IReadOnlyList<ExtraIsland> ExtraIslands => extraIslands;
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
        public IReadOnlyList<Vector2> CentrelineBends => centrelineBends;
        public float FlatRadius => flatRadius;
        public float FlatBlend => flatBlend;
        public Vector2 SkirtDepth => skirtDepth;
        public float SkirtNoiseScale => skirtNoiseScale;

        /// <summary>
        /// This hand-made shape as the plain layout the terrain, the ball and the trees read: one island,
        /// one green, one bunker, one pond, and a straight centreline from the tee to the hole.
        /// </summary>
        public CourseLayout ToLayout()
        {
            var islands = new List<IslandOutline> { new IslandOutline(halfLength, halfWidth, widthWobble, widthNoiseScale, centerX) };
            foreach (ExtraIsland extra in extraIslands ?? Array.Empty<ExtraIsland>())
            {
                islands.Add(new IslandOutline(extra.HalfLength, extra.HalfWidth, extra.WidthWobble, widthNoiseScale, extra.CenterX, extra.CenterZ, extra.Heading));
            }

            var centreline = new List<Vector2> { tee };
            centreline.AddRange(centrelineBends ?? Array.Empty<Vector2>());
            centreline.Add(hole);

            return new CourseLayout(
                islands,
                new CoursePath(centreline),
                new FairwayLayout(fairwayStart, fairwayEnd, fairwayHalfWidth, fairwayWobble, stripeWidth),
                new[] { new GreenLayout(green, greenTilt) },
                new[] { bunker },
                new[] { pond },
                swells ?? Array.Empty<GroundSwell>(),
                waterLevel,
                new GroundRelief(cellSize, axisRelief, edgeRelief, reliefDistance, reliefNoiseScale),
                tee,
                hole,
                flatRadius,
                flatBlend,
                new EdgeLayout(skirtDepth, skirtNoiseScale),
                alongCentreline);
        }
    }
}
