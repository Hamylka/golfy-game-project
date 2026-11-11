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

        /// <summary>
        /// A value that depends on the distance along the path and across it (the ground's elevation, say), read leg by
        /// leg and blended. <see cref="Project"/> picks the nearest leg, so on the inside of a bend its distance along
        /// jumps from one leg to the next; a value built on it would step. Here every leg gives its own unclamped
        /// distance along and across (measured on the line of the leg, the end legs running on), and the values are
        /// averaged with weights that fade from 1 for the nearest leg to 0 for a leg <paramref name="band"/> metres
        /// farther away than it. Away from bends only one leg has weight, so the result is that leg's value exactly.
        /// </summary>
        public float Blend(Vector2 p, float band, Func<float, float, float> valueAt)
        {
            int legs = _points.Length - 1;
            if (legs == 1)
            {
                Vector2 only = (_points[1] - _points[0]).normalized;
                Vector2 from = p - _points[0];
                return valueAt(Vector2.Dot(from, only), (only.x * from.y) - (only.y * from.x));
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < legs; i++)
            {
                nearest = Mathf.Min(nearest, LegDistance(p, i));
            }

            float total = 0f;
            float sum = 0f;
            float legStart = 0f;
            for (int i = 0; i < legs; i++)
            {
                Vector2 a = _points[i];
                Vector2 leg = _points[i + 1] - a;
                float length = leg.magnitude;
                float weight = 1f - Mathf.SmoothStep(0f, 1f, (LegDistance(p, i) - nearest) / band);
                if (weight > 0f)
                {
                    Vector2 direction = leg / length;
                    Vector2 offset = p - a;
                    total += weight;
                    sum += weight * valueAt(legStart + Vector2.Dot(offset, direction), (direction.x * offset.y) - (direction.y * offset.x));
                }

                legStart += length;
            }

            return sum / total;
        }

        /// <summary>How far a point is from one leg, the end legs running on past both ends like in <see cref="Project"/>.</summary>
        private float LegDistance(Vector2 p, int leg)
        {
            Vector2 a = _points[leg];
            Vector2 vector = _points[leg + 1] - a;
            float length = vector.magnitude;
            float t = Vector2.Dot(p - a, vector / length);
            float clamped = Mathf.Clamp(t, leg == 0 ? float.MinValue : 0f, leg == _points.Length - 2 ? float.MaxValue : length);
            return (p - (a + (vector / length * clamped))).magnitude;
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
        /// <param name="pieces">
        /// The strips the fairway is made of, on a generated course (split fairway plan §2). Null or empty gives the plain fairway:
        /// one piece from <paramref name="start"/> to <paramref name="end"/>, offset 0, <paramref name="halfWidth"/>, the two caps.
        /// </param>
        /// <param name="roughOvals">Ovals of rough cut out of the pieces. Null for none.</param>
        public FairwayLayout(
            float start,
            float end,
            float halfWidth,
            float wobble,
            float stripeWidth,
            float noiseOffset = 0f,
            float startCap = 0f,
            float endCap = 0f,
            IEnumerable<FairwayPiece> pieces = null,
            IEnumerable<EllipseShape> roughOvals = null)
        {
            StartCap = startCap;
            EndCap = endCap;
            NoiseOffset = noiseOffset;
            Start = start;
            End = end;
            HalfWidth = halfWidth;
            Wobble = wobble;
            StripeWidth = stripeWidth;

            FairwayPiece[] given = pieces == null ? new FairwayPiece[0] : new List<FairwayPiece>(pieces).ToArray();
            _pieces = given.Length > 0 ? given : new[] { FairwayPiece.Plain(start, end, halfWidth, startCap, endCap) };
            _roughOvals = roughOvals == null ? new EllipseShape[0] : new List<EllipseShape>(roughOvals).ToArray();
        }

        private readonly FairwayPiece[] _pieces;
        private readonly EllipseShape[] _roughOvals;

        /// <summary>
        /// The strips whose union is the fairway, read only on a generated course (<see cref="CourseLayout.AlongCentreline"/>). One
        /// piece on a plain course. Every piece shares the edge wobble, so where two overlap their borders carry the same noise.
        /// </summary>
        public IReadOnlyList<FairwayPiece> Pieces => _pieces ?? new FairwayPiece[0];

        /// <summary>Ovals of rough cut out of the pieces (rough islands, F3.2). Empty on a plain course.</summary>
        public IReadOnlyList<EllipseShape> RoughOvals => _roughOvals ?? new EllipseShape[0];

        /// <summary>
        /// True for the one-strip fairway every course had before F3.1: a single piece with no offset and no ease, and no rough
        /// oval. Its field is the old strip's, bit for bit.
        /// </summary>
        public bool IsPlain => Pieces.Count == 1 && Pieces[0].IsPlain && RoughOvals.Count == 0;

        public float Start { get; }
        public float End { get; }
        public float HalfWidth { get; }
        public float Wobble { get; }
        public float StripeWidth { get; }

        /// <summary>Shifts the edge wobble's noise. 0 on hand-made courses.</summary>
        public float NoiseOffset { get; }

        /// <summary>
        /// How far the rounded tip at the start reaches in along the strip, in metres: the full width narrows to a point at
        /// <see cref="Start"/> over this length (a quarter ellipse). 0 is the flat cut of hand-made courses. Generated courses only.
        /// </summary>
        public float StartCap { get; }

        /// <summary>The same at the end: the strip narrows to a point at <see cref="End"/> over this length. 0 is a flat cut.</summary>
        public float EndCap { get; }
    }

    /// <summary>
    /// One strip of a generated fairway, described in the main centreline's frame (split fairway plan §2): it runs from
    /// <see cref="From"/> to <see cref="To"/> metres along the centreline, its middle sits <see cref="Offset"/> metres across it
    /// (positive on the left, like <see cref="CoursePath.Project"/>), and it is <see cref="HalfWidth"/> half wide, plus the fairway's
    /// edge wobble. The offset and the half width can ease in from their edge values (0, <see cref="EdgeHalfWidth"/>) at both ends
    /// with a smoothstep, so a branch opens and closes like a Y and a widening swells smoothly. Each end is a rounded tip reaching
    /// its cap in along the strip, or a flat cut (cap 0) where the piece joins another one that overlaps it.
    /// </summary>
    public readonly struct FairwayPiece
    {
        public FairwayPiece(
            float from,
            float to,
            float halfWidth,
            float fromCap = 0f,
            float toCap = 0f,
            float offset = 0f,
            float offsetEase = 0f,
            float edgeHalfWidth = 0f,
            float widthEase = 0f)
        {
            From = from;
            To = to;
            HalfWidth = halfWidth;
            FromCap = fromCap;
            ToCap = toCap;
            Offset = offset;
            OffsetEase = offsetEase;
            EdgeHalfWidth = edgeHalfWidth;
            WidthEase = widthEase;
        }

        /// <summary>The plain strip: offset 0, one half width all along, no ease.</summary>
        public static FairwayPiece Plain(float from, float to, float halfWidth, float fromCap, float toCap) =>
            new FairwayPiece(from, to, halfWidth, fromCap, toCap);

        /// <summary>Where the piece starts and ends, in metres along the centreline from the tee (its tips included).</summary>
        public float From { get; }
        public float To { get; }

        /// <summary>The half width between the eases, before the wobble is added.</summary>
        public float HalfWidth { get; }

        /// <summary>How far the rounded tip at each end reaches in along the strip (a quarter ellipse). 0 is a flat cut.</summary>
        public float FromCap { get; }
        public float ToCap { get; }

        /// <summary>The middle's distance across the centreline between the eases, positive on its left. 0 at both ends when eased.</summary>
        public float Offset { get; }

        /// <summary>The length over which the offset eases in from 0 at each end. 0 holds the offset all along.</summary>
        public float OffsetEase { get; }

        /// <summary>The half width at both ends when <see cref="WidthEase"/> is set.</summary>
        public float EdgeHalfWidth { get; }

        /// <summary>The length over which the half width eases from <see cref="EdgeHalfWidth"/> at each end. 0 holds it all along.</summary>
        public float WidthEase { get; }

        /// <summary>No offset and no ease: the strip every course had before F3.1.</summary>
        public bool IsPlain => Offset == 0f && OffsetEase <= 0f && WidthEase <= 0f;

        /// <summary>The offset at this distance along the centreline.</summary>
        public float OffsetAt(float along)
        {
            if (OffsetEase <= 0f || Offset == 0f)
            {
                return Offset;
            }

            float ease = Ease(along, OffsetEase);
            return ease >= 1f ? Offset : Offset * ease;
        }

        /// <summary>The half width at this distance along the centreline, before the wobble.</summary>
        public float HalfWidthAt(float along)
        {
            if (WidthEase <= 0f)
            {
                return HalfWidth;
            }

            // The stored value itself between the eases: Lerp(a, b, 1) is not always exactly b in floats.
            float ease = Ease(along, WidthEase);
            return ease >= 1f ? HalfWidth : (ease <= 0f ? EdgeHalfWidth : EdgeHalfWidth + ((HalfWidth - EdgeHalfWidth) * ease));
        }

        /// <summary>0 at both ends of the piece, 1 between the two eases, a smoothstep over each ease length.</summary>
        private float Ease(float along, float length)
        {
            float into = Mathf.Min(along - From, To - along);
            if (into >= length)
            {
                return 1f;
            }

            if (into <= 0f)
            {
                return 0f;
            }

            float t = into / length;
            return t * t * (3f - (2f * t));
        }
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

    /// <summary>
    /// A pond: its outline and the height of its water. Each pond has its own level, because on ground with relief a
    /// shared one would leave a tall bank on a rise and put the water above the ground in a dip.
    /// </summary>
    public readonly struct PondLayout
    {
        public PondLayout(EllipseShape outline, float waterLevel)
        {
            Outline = outline;
            WaterLevel = waterLevel;
        }

        public EllipseShape Outline { get; }

        /// <summary>The height of the water surface, in the course's local space.</summary>
        public float WaterLevel { get; }
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
        private readonly PondLayout[] _ponds;
        private readonly GroundSwell[] _swells;
        private CourseProp[] _props = new CourseProp[0];
        private PropShape[] _propShapes = new PropShape[0];
        private int[] _propOfShape = new int[0];

        public CourseLayout(
            IEnumerable<IslandOutline> islands,
            CoursePath centreline,
            FairwayLayout fairway,
            IEnumerable<GreenLayout> greens,
            IEnumerable<EllipseShape> bunkers,
            IEnumerable<PondLayout> ponds,
            IEnumerable<GroundSwell> swells,
            float waterLevel,
            GroundRelief relief,
            Vector2 tee,
            Vector2 hole,
            float flatRadius,
            float flatBlend,
            EdgeLayout edge,
            bool alongCentreline = false,
            ElevationProfile elevation = null,
            CoursePlan plan = null)
        {
            AlongCentreline = alongCentreline;
            Plan = plan;
            Elevation = elevation ?? ElevationProfile.Empty;
            _islands = new List<IslandOutline>(islands).ToArray();
            if (_islands.Length == 0)
            {
                throw new ArgumentException("A course needs at least one island.", nameof(islands));
            }

            Centreline = centreline ?? throw new ArgumentNullException(nameof(centreline));
            Fairway = fairway;
            _greens = new List<GreenLayout>(greens).ToArray();
            _bunkers = new List<EllipseShape>(bunkers).ToArray();
            _ponds = new List<PondLayout>(ponds).ToArray();
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
        public IReadOnlyList<PondLayout> Ponds => _ponds;
        public IReadOnlyList<GroundSwell> Swells => _swells;

        /// <summary>The props standing on the course (M4 PR 4.3), from <see cref="PropPlacement"/>. Empty on hand-made courses.</summary>
        public IReadOnlyList<CourseProp> Props => _props;

        /// <summary>Every prop's collision shapes in one list, prop by prop, in the order of <see cref="Props"/>.</summary>
        public IReadOnlyList<PropShape> PropShapes => _propShapes;

        /// <summary>Which prop of <see cref="Props"/> the shape at this index of <see cref="PropShapes"/> belongs to.</summary>
        public int PropOfShape(int shape) => _propOfShape[shape];

        /// <summary>
        /// The same course with these props instead of its own. Everything else is shared, untouched: props never move an
        /// island, a slope, the green, the pin or a hazard.
        /// </summary>
        public CourseLayout WithProps(IEnumerable<CourseProp> props)
        {
            var copy = (CourseLayout)MemberwiseClone();
            copy._props = new List<CourseProp>(props).ToArray();
            var shapes = new List<PropShape>();
            var owners = new List<int>();
            for (int i = 0; i < copy._props.Length; i++)
            {
                foreach (PropShape shape in copy._props[i].Shapes)
                {
                    shapes.Add(shape);
                    owners.Add(i);
                }
            }

            copy._propShapes = shapes.ToArray();
            copy._propOfShape = owners.ToArray();
            return copy;
        }

        /// <summary>What the generator meant by this course (hazard roles, shot lines, the safe route). <c>null</c> on hand-made courses.</summary>
        public CoursePlan Plan { get; }

        /// <summary>The default water level: the hand-made courses' ponds, and the value away from any pond. Each pond has its own, see <see cref="PondLayout"/>.</summary>
        public float WaterLevel { get; }
        public GroundRelief Relief { get; }
        public Vector2 Tee { get; }
        public Vector2 Hole { get; }

        /// <summary>The ground is flat this far around the tee and the hole, in metres.</summary>
        public float FlatRadius { get; }

        /// <summary>How far the ground then takes to return to its full slope, in metres.</summary>
        public float FlatBlend { get; }

        public EdgeLayout Edge { get; }

        /// <summary>The broad rises, dips and side slopes along the centreline. Empty (adding exactly 0) on hand-made courses.</summary>
        public ElevationProfile Elevation { get; }

        /// <summary>
        /// True when the fairway, its mowing stripes and the calm band of the ground relief follow the centreline, as on
        /// every generated course. False keeps them along the X axis, exactly as the hand-made courses were pinned.
        /// </summary>
        public bool AlongCentreline { get; }

        /// <summary>How far land can reach from the origin along X and across Z, over every island. The terrain grid covers this.</summary>
        public Vector2 HalfExtent { get; }
    }
}
