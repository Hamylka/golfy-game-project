using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// One island's outline: a long oval whose width wobbles with noise, centred on (<see cref="CenterX"/>,
    /// <see cref="CenterZ"/>) and running along X, or turned by <see cref="Heading"/> for a dogleg's leg. Several
    /// islands with open sky between them make a course with gaps; overlapping ones make one island that bends.
    /// </summary>
    public readonly struct IslandOutline
    {
        public IslandOutline(float halfLength, float halfWidth, float widthWobble, float widthNoiseScale, float centerX = 0f, float centerZ = 0f, float heading = 0f, float noiseOffset = 0f)
        {
            NoiseOffset = noiseOffset;
            HalfLength = halfLength;
            HalfWidth = halfWidth;
            WidthWobble = widthWobble;
            WidthNoiseScale = widthNoiseScale;
            CenterX = centerX;
            CenterZ = centerZ;
            Heading = heading;
        }

        public float CenterX { get; }
        public float CenterZ { get; }

        /// <summary>The direction of the island's long axis, in degrees from +X, positive toward +Z.</summary>
        public float Heading { get; }

        public float HalfLength { get; }
        public float HalfWidth { get; }
        public float WidthWobble { get; }
        public float WidthNoiseScale { get; }

        /// <summary>Shifts the width noise, so generated islands of the same size get different wobbles. 0 on hand-made courses.</summary>
        public float NoiseOffset { get; }

        /// <summary>True for an island along X on z = 0, which every course had before doglegs.</summary>
        public bool IsOnAxis => Heading == 0f && CenterZ == 0f;

        /// <summary>How far the island can reach from the course origin along X and across Z, wobble included.</summary>
        public Vector2 HalfExtent
        {
            get
            {
                float width = HalfWidth + (WidthWobble * 0.5f);
                if (IsOnAxis)
                {
                    return new Vector2(Mathf.Abs(CenterX) + HalfLength, width);
                }

                // The bounding box of the turned oval.
                float radians = Heading * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                float alongX = Mathf.Sqrt((HalfLength * cos * HalfLength * cos) + (width * sin * width * sin));
                float alongZ = Mathf.Sqrt((HalfLength * sin * HalfLength * sin) + (width * cos * width * cos));
                return new Vector2(Mathf.Abs(CenterX) + alongX, Mathf.Abs(CenterZ) + alongZ);
            }
        }

        /// <summary>
        /// A ground point in the island's own frame, where it runs along X on z = 0 around <see cref="CenterX"/>. The
        /// point itself, untouched, for an island on the axis, so courses without doglegs keep every vertex.
        /// </summary>
        public Vector3 ToOwnFrame(Vector3 p)
        {
            if (IsOnAxis)
            {
                return p;
            }

            float radians = Heading * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            float dx = p.x - CenterX;
            float dz = p.z - CenterZ;
            return new Vector3(CenterX + (dx * cos) + (dz * sin), p.y, (dz * cos) - (dx * sin));
        }
    }

    /// <summary>What lies just inside a piece of the island edge. Tufts skip water; a waterfall could fall from it later.</summary>
    public enum EdgeSurface
    {
        Land,
        Water,
    }

    /// <summary>
    /// The course's centreline, tee to hole, as a polyline: one leg on a straight course, one more per bend. The camera's
    /// headings follow its legs, and on a generated course so do the fairway and its stripes.
    /// </summary>
    public sealed class CoursePath
    {
        private readonly Vector2[] _points;

        public CoursePath(IEnumerable<Vector2> points)
        {
            _points = new List<Vector2>(points).ToArray();
            if (_points.Length < 2)
            {
                throw new ArgumentException("A path needs at least two points.", nameof(points));
            }

            for (int i = 1; i < _points.Length; i++)
            {
                Length += Vector2.Distance(_points[i - 1], _points[i]);
            }
        }

        public IReadOnlyList<Vector2> Points => _points;

        /// <summary>The path's length in metres, along every leg.</summary>
        public float Length { get; }

        public Vector2 Start => _points[0];

        public Vector2 End => _points[_points.Length - 1];

        /// <summary>
        /// How far along the path the point nearest to <paramref name="p"/> lies, in metres from the start, and the signed
        /// distance across it (positive on the left of the direction of travel, which is +Z for a path along +X). The two
        /// end legs run on past the tee and the hole, so a point behind the tee has a negative distance along and is
        /// measured square to the first leg. Near a bend the nearest leg wins, which keeps the distance across continuous.
        /// </summary>
        public float Project(Vector2 p, out float across)
        {
            float best = float.MaxValue;
            float along = 0f;
            across = 0f;
            float legStart = 0f;
            int last = _points.Length - 2;
            for (int i = 0; i <= last; i++)
            {
                Vector2 a = _points[i];
                Vector2 leg = _points[i + 1] - a;
                float length = leg.magnitude;
                Vector2 direction = leg / length;
                float t = Vector2.Dot(p - a, direction);
                float clamped = Mathf.Clamp(t, i == 0 ? float.MinValue : 0f, i == last ? float.MaxValue : length);
                Vector2 offset = p - (a + (direction * clamped));
                float distance = offset.sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    along = legStart + clamped;
                    float side = (direction.x * offset.y) - (direction.y * offset.x);
                    across = side < 0f ? -Mathf.Sqrt(distance) : Mathf.Sqrt(distance);
                }

                legStart += length;
            }

            return along;
        }

        /// <summary>The point this far along the path; the end legs run on past both ends.</summary>
        public Vector2 PointAt(float along)
        {
            int last = _points.Length - 2;
            float legStart = 0f;
            for (int i = 0; i <= last; i++)
            {
                Vector2 leg = _points[i + 1] - _points[i];
                float length = leg.magnitude;
                if (along <= legStart + length || i == last)
                {
                    return _points[i] + (leg / length * (along - legStart));
                }

                legStart += length;
            }

            return End;
        }
    }

    /// <summary>
    /// The mown strip from the tee toward the green. <see cref="Start"/> and <see cref="End"/> are along X on a hand-made
    /// course, and distances along the centreline from the tee when <see cref="CourseLayout.AlongCentreline"/> is set.
    /// </summary>
    public readonly struct FairwayLayout
    {
        public FairwayLayout(float start, float end, float halfWidth, float wobble, float stripeWidth, float noiseOffset = 0f)
        {
            NoiseOffset = noiseOffset;
            Start = start;
            End = end;
            HalfWidth = halfWidth;
            Wobble = wobble;
            StripeWidth = stripeWidth;
        }

        public float Start { get; }
        public float End { get; }
        public float HalfWidth { get; }
        public float Wobble { get; }
        public float StripeWidth { get; }

        /// <summary>Shifts the edge wobble's noise. 0 on hand-made courses.</summary>
        public float NoiseOffset { get; }
    }

    /// <summary>A green: its outline and its tilt, the rise per metre along X and along Z, measured from the hole.</summary>
    public readonly struct GreenLayout
    {
        public GreenLayout(EllipseShape outline, Vector2 tilt)
        {
            Outline = outline;
            Tilt = tilt;
        }

        public EllipseShape Outline { get; }
        public Vector2 Tilt { get; }
    }

    /// <summary>The small facets on the ground: grid size and how broken the ground is, calm on the axis and rougher toward the edges.</summary>
    public readonly struct GroundRelief
    {
        public GroundRelief(float cellSize, float axisRelief, float edgeRelief, float reliefDistance, float noiseScale, float noiseOffset = 0f)
        {
            NoiseOffset = noiseOffset;
            CellSize = cellSize;
            AxisRelief = axisRelief;
            EdgeRelief = edgeRelief;
            ReliefDistance = reliefDistance;
            NoiseScale = noiseScale;
        }

        public float CellSize { get; }
        public float AxisRelief { get; }
        public float EdgeRelief { get; }
        public float ReliefDistance { get; }
        public float NoiseScale { get; }

        /// <summary>Shifts the facets' noise. 0 on hand-made courses.</summary>
        public float NoiseOffset { get; }
    }

    /// <summary>The earth band under the island edge.</summary>
    public readonly struct EdgeLayout
    {
        public EdgeLayout(Vector2 skirtDepth, float skirtNoiseScale)
        {
            SkirtDepth = skirtDepth;
            SkirtNoiseScale = skirtNoiseScale;
        }

        /// <summary>Shortest and longest depth below the edge, in metres.</summary>
        public Vector2 SkirtDepth { get; }
        public float SkirtNoiseScale { get; }
    }

    /// <summary>
    /// Everything that describes one course, in the course's local space with X running from tee to hole.
    /// It's plain data, read-only once built: the hand-made <see cref="CourseShapeSettings"/> produce one,
    /// and the M3 generator will too, so the terrain, the ball's ground and the trees never need to know
    /// where a course came from. Surfaces are lists, so a course can have several greens, bunkers or ponds,
    /// or none.
    /// </summary>
    public sealed class CourseLayout
    {
        private readonly IslandOutline[] _islands;
        private readonly GreenLayout[] _greens;
        private readonly EllipseShape[] _bunkers;
        private readonly EllipseShape[] _ponds;
        private readonly GroundSwell[] _swells;

        public CourseLayout(
            IEnumerable<IslandOutline> islands,
            CoursePath centreline,
            FairwayLayout fairway,
            IEnumerable<GreenLayout> greens,
            IEnumerable<EllipseShape> bunkers,
            IEnumerable<EllipseShape> ponds,
            IEnumerable<GroundSwell> swells,
            float waterLevel,
            GroundRelief relief,
            Vector2 tee,
            Vector2 hole,
            float flatRadius,
            float flatBlend,
            EdgeLayout edge,
            bool alongCentreline = false)
        {
            AlongCentreline = alongCentreline;
            _islands = new List<IslandOutline>(islands).ToArray();
            if (_islands.Length == 0)
            {
                throw new ArgumentException("A course needs at least one island.", nameof(islands));
            }

            Centreline = centreline ?? throw new ArgumentNullException(nameof(centreline));
            Fairway = fairway;
            _greens = new List<GreenLayout>(greens).ToArray();
            _bunkers = new List<EllipseShape>(bunkers).ToArray();
            _ponds = new List<EllipseShape>(ponds).ToArray();
            _swells = new List<GroundSwell>(swells).ToArray();
            WaterLevel = waterLevel;
            Relief = relief;
            Tee = tee;
            Hole = hole;
            FlatRadius = flatRadius;
            FlatBlend = flatBlend;
            Edge = edge;

            foreach (IslandOutline island in _islands)
            {
                HalfExtent = Vector2.Max(HalfExtent, island.HalfExtent);
            }
        }

        public IReadOnlyList<IslandOutline> Islands => _islands;
        public CoursePath Centreline { get; }
        public FairwayLayout Fairway { get; }
        public IReadOnlyList<GreenLayout> Greens => _greens;
        public IReadOnlyList<EllipseShape> Bunkers => _bunkers;
        public IReadOnlyList<EllipseShape> Ponds => _ponds;
        public IReadOnlyList<GroundSwell> Swells => _swells;
        public float WaterLevel { get; }
        public GroundRelief Relief { get; }
        public Vector2 Tee { get; }
        public Vector2 Hole { get; }

        /// <summary>The ground is flat this far around the tee and the hole, in metres.</summary>
        public float FlatRadius { get; }

        /// <summary>How far the ground then takes to return to its full slope, in metres.</summary>
        public float FlatBlend { get; }

        public EdgeLayout Edge { get; }

        /// <summary>
        /// True when the fairway, its mowing stripes and the calm band of the ground relief follow the centreline, as on
        /// every generated course. False keeps them along the X axis, exactly as the hand-made courses were pinned.
        /// </summary>
        public bool AlongCentreline { get; }

        /// <summary>How far land can reach from the origin along X and across Z, over every island. The terrain grid covers this.</summary>
        public Vector2 HalfExtent { get; }
    }
}
