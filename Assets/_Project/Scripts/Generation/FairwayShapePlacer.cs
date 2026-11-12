using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Ground the safe route may not land on: a rough island's oval or a split's band (split fairway plan §3). A rest spot whose
    /// landing capsule, or the final approach, has a sample on it is blocked, with no margin (strict non-overlap, in whole cm).
    /// </summary>
    internal sealed class GroundKeepOut
    {
        private GroundKeepOut(HazardOutline oval, float from, float to)
        {
            Oval = oval;
            From = from;
            To = to;
        }

        /// <summary>The rough island's outline, or null for a band.</summary>
        public HazardOutline Oval { get; }

        /// <summary>
        /// The stretch along the centreline it covers. For a band, the whole tip reach: the band itself plus a cap's depth either
        /// side, where the rounded tips leave rough beside the centreline.
        /// </summary>
        public float From { get; }
        public float To { get; }

        public static GroundKeepOut OfOval(HazardOutline oval, float from, float to) => new GroundKeepOut(oval, from, to);

        public static GroundKeepOut OfBand(float reachFrom, float reachTo) => new GroundKeepOut(null, reachFrom, reachTo);

        /// <summary>
        /// A fork's spine (F3.3): the rough between its two branches, read from the shaped fairway field itself, so it is exactly the
        /// drawn spine. <paramref name="from"/> and <paramref name="to"/> are the fork's whole stretch, eases included.
        /// </summary>
        public static GroundKeepOut OfSpine(CourseFields fields, FairwayPiece safe, FairwayPiece risky, float from, float to) =>
            new GroundKeepOut(null, from, to) { _spineFields = fields, _safe = safe, _risky = risky };

        private CourseFields _spineFields;
        private FairwayPiece _safe;
        private FairwayPiece _risky;
        private List<Vector2> _spineSamples;

        /// <summary>True for a fork's spine, which no shot line may cross either (not only no landing).</summary>
        public bool IsSpine => _spineFields != null;

        public bool Blocks(Vector2 p, CoursePath path)
        {
            if (Oval != null)
            {
                return Geo.Cm(Oval.SignedDistance(p, 1f)) <= 0;
            }

            float s = path.Project(p, out float side);
            int along = Geo.Cm(s);
            if (along < Geo.Cm(From) || along > Geo.Cm(To))
            {
                return false;
            }

            return !IsSpine || InSpine(p, s, side);
        }

        /// <summary>Strictly between the two branches' centres, on rough (the shaped fairway field above 0 in whole cm).</summary>
        private bool InSpine(Vector2 p, float along, float side)
        {
            float a = _risky.OffsetAt(along), b = _safe.OffsetAt(along);
            int lo = Geo.Cm(Mathf.Min(a, b)), hi = Geo.Cm(Mathf.Max(a, b));
            int across = Geo.Cm(side);
            return across > lo && across < hi && Geo.Cm(_spineFields.Fairway(Geo.At(p))) > 0;
        }

        /// <summary>Whether the straight segment from <paramref name="a"/> to <paramref name="b"/> crosses the spine (sampled every 10 cm).</summary>
        public bool Crosses(Vector2 a, Vector2 b, CoursePath path)
        {
            if (!IsSpine)
            {
                return false;
            }

            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.1f));
            for (int i = 0; i <= steps; i++)
            {
                if (Blocks(Vector2.Lerp(a, b, i / (float)steps), path))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Points of the spine on a 25 cm grid, for testing a hazard that might cover it without a ring point inside it.</summary>
        public IReadOnlyList<Vector2> SpineSamples(CoursePath path)
        {
            if (_spineSamples != null)
            {
                return _spineSamples;
            }

            _spineSamples = new List<Vector2>();
            for (float s = From; s <= To + 0.001f; s += 0.25f)
            {
                Vector2 centre = path.PointAt(s);
                Vector2 left = Geo.Left(Geo.PathDirection(path, s));
                float a = _risky.OffsetAt(s), b = _safe.OffsetAt(s);
                for (float across = Mathf.Min(a, b); across <= Mathf.Max(a, b) + 0.001f; across += 0.25f)
                {
                    Vector2 p = centre + (left * across);
                    if (Blocks(p, path))
                    {
                        _spineSamples.Add(p);
                    }
                }
            }

            return _spineSamples;
        }
    }

    /// <summary>
    /// Where a fork moves the safe route's rest spots across (F3.3): from the fork's start to its end, eased like a branch, so a spot on
    /// the held stretch lies with its capsule touching the spine's border (hub, Q1 A). None on every other course.
    /// </summary>
    internal readonly struct RouteOffset
    {
        public RouteOffset(float from, float to, float offset, float ease)
        {
            Piece = new FairwayPiece(from, to, 0f, 0f, 0f, offset, ease);
        }

        /// <summary>The offset is a piece's eased offset, so the spots follow the branch's own smoothstep.</summary>
        public FairwayPiece Piece { get; }

        public float At(float along) => along <= Piece.From || along >= Piece.To ? 0f : Piece.OffsetAt(along);
    }

    /// <summary>What <see cref="FairwayShapePlacer.Place"/> hands back. Empty lists on a plain course.</summary>
    internal sealed class FairwayShapeResult
    {
        public List<FairwayShape> Shapes { get; } = new List<FairwayShape>();
        public List<FairwayPiece> Pieces { get; } = new List<FairwayPiece>();
        public List<EllipseShape> Ovals { get; } = new List<EllipseShape>();
        public List<GroundKeepOut> KeepOuts { get; } = new List<GroundKeepOut>();

        /// <summary>The bare route world with the shapes' keep-outs, for the hazards; null on a plain course.</summary>
        public RouteWorld World { get; set; }

        /// <summary>The islands with a fork's bulge (F3.3), or null when no island changed: the course keeps its own outlines.</summary>
        public IReadOnlyList<IslandOutline> Islands { get; set; }

        /// <summary>On a fork the direct line bends here, on the risky branch's centre in the spine's middle (F3.3). Null otherwise.</summary>
        public Vector2? DirectWaypoint { get; set; }

        /// <summary>A placed fork's spine, for the hazards' branch guard and the tests. Null without a fork.</summary>
        public GroundKeepOut Spine { get; set; }

        /// <summary>A placed fork's branches (safe, then risky), for the branch guard and the tests.</summary>
        public FairwayPiece SafeBranch { get; set; }
        public FairwayPiece RiskyBranch { get; set; }

        /// <summary>The island bulge a placed fork needed on its safe and risky sides, in metres (0 for none), and the stretch it is held over.</summary>
        public float BulgeSafe { get; set; }
        public float BulgeRisky { get; set; }
        public Vector2 BulgeStretch { get; set; }

        /// <summary>The kind the first draw gave (null for plain), and a par 5's second (null for none), placed or not.</summary>
        public FairwayShapeKind? FirstDrawn { get; set; }
        public FairwayShapeKind? SecondDrawn { get; set; }

        /// <summary>Whether the first draw's shape, and a par 5's second, found a place.</summary>
        public bool FirstPlaced { get; set; }
        public bool SecondPlaced { get; set; }

        /// <summary>Why tries failed, by reason, for the measurements.</summary>
        public Dictionary<string, int> Rejections { get; } = new Dictionary<string, int>();

        public bool Plain => Shapes.Count == 0;
    }

    /// <summary>
    /// The fairway shapes of a generated course (split fairway plan §1, §3, §5; F3.2: rough islands and splits; forks in F3.3).
    /// Everything draws from the course's own <b>fairway shapes stream</b> (salt 8), after the green and the pin and before the
    /// hazards, so the main stream (islands, slopes) and the surfaces stream (green, pin, hazard candidates) never move.
    /// <para>
    /// Draw order: the shape kind, one integer over the par's weights; then, for each try, a fixed list (rough island: length,
    /// width, wobble, start; split: band length, start), the start being one even integer draw over every centimetre where the
    /// shape's static rules hold for those sizes (sizes first because the valid starts depend on them; the plan's §5 lists the
    /// place first). A par 5 may draw a second shape on sub-stream 2: its chance, its kind, then the same tries.
    /// </para>
    /// <para>
    /// A try is accepted if its dynamic rules hold (a rough island keeps 2 m of fairway either side and its widened strip stays
    /// inside the island) and <see cref="SafeRoute.Find"/> still finds a route with its keep-out. A shape that finds no place
    /// leaves the course plain: never a missing role, never a reroll.
    /// </para>
    /// </summary>
    internal static class FairwayShapePlacer
    {
        private const int ShapesStream = 8;

        /// <summary>Builds the bare course with other islands, fairway pieces and rough ovals (the generator's own builder), for a fork's candidate.</summary>
        public delegate CourseLayout LayoutBuilder(IReadOnlyList<IslandOutline> islands, List<FairwayPiece> pieces, List<EllipseShape> ovals);

        public static FairwayShapeResult Place(
            int seed, int par, int variant, CourseLayout bare, GreenPlan green, IReadOnlyList<float> bends, bool dogleg, bool islandGreen, LevelGenSettings s,
            LayoutBuilder build)
        {
            var result = new FairwayShapeResult();
            FairwayShapeRules rules = s.FairwayShapes;
            int course = unchecked((int)CourseNoise.Hash(seed, par, variant));

            var first = new SeededRandom(unchecked((int)CourseNoise.Hash(course, ShapesStream, 0)));
            FairwayShapeKind? kind = DrawKind(first, rules.Weights(par));

            // The second draw of a par 5 is decided now, on its own sub-stream, so the first shape's tries never move it.
            SeededRandom second = null;
            FairwayShapeKind? secondKind = null;
            if (par == 5)
            {
                second = new SeededRandom(unchecked((int)CourseNoise.Hash(course, ShapesStream, 2)));
                if (second.Chance(rules.SecondShapeChance))
                {
                    secondKind = DrawKind(second, rules.Weights(par));
                }
            }

            // What was drawn, for the measurements.
            result.FirstDrawn = kind;
            result.SecondDrawn = secondKind;
            if (kind == null && secondKind == null)
            {
                return result;
            }

            var placer = new Placer(bare, green, bends, dogleg, islandGreen, par, s, result.Rejections, build);
            if (kind != null)
            {
                result.FirstPlaced = placer.Try(first, kind.Value, 0f);
            }

            if (secondKind != null)
            {
                result.SecondPlaced = placer.Try(second, secondKind.Value, s.FullReach);
            }

            return placer.Finish(result);
        }

        /// <summary>Where a course's fork moves the safe route's rest spots across (none without a fork), from its plan's shapes.</summary>
        internal static RouteOffset? RouteOffsetOf(IReadOnlyList<FairwayShape> shapes, LevelGenSettings s)
        {
            if (shapes != null)
            {
                foreach (FairwayShape shape in shapes)
                {
                    if (shape.Kind == FairwayShapeKind.Fork)
                    {
                        return new RouteOffset(shape.From, shape.To, shape.RestOffset, s.FairwayShapes.ForkEase);
                    }
                }
            }

            return null;
        }

        /// <summary>One integer draw over the weights. Plain gives null.</summary>
        private static FairwayShapeKind? DrawKind(SeededRandom random, FairwayShapeWeights w)
        {
            int total = w.Total;
            if (total <= 0)
            {
                return null;
            }

            int pick = random.Int(0, total - 1);
            if (pick < w.PlainRange)
            {
                // The top of plain's range draws a fork (F3.3): the ranges after it, so every rough island and split, never move.
                return pick < w.PlainRange - w.ForkFromPlain ? (FairwayShapeKind?)null : FairwayShapeKind.Fork;
            }

            pick -= w.PlainRange;
            if (pick < w.RoughIsland)
            {
                return FairwayShapeKind.RoughIsland;
            }

            pick -= w.RoughIsland;
            return pick < w.Split ? FairwayShapeKind.Split : FairwayShapeKind.Fork;
        }

        /// <summary>The placement of one course's shapes, holding the bare world and what is accepted so far.</summary>
        private sealed class Placer
        {
            private readonly CourseLayout _bare;
            private readonly LayoutBuilder _build;

            // The bare course's fields and route world, until a fork is placed: then its bulged, shaped candidate's.
            private CourseFields _fields;
            private readonly CoursePath _path;
            private readonly GreenPlan _green;
            private readonly IReadOnlyList<float> _bends;
            private readonly bool _dogleg;
            private readonly bool _islandGreen;
            private readonly int _par;
            private readonly LevelGenSettings _s;
            private readonly FairwayShapeRules _r;
            private RouteWorld _world;
            private readonly bool _bareRoute;

            // A placed fork (F3.3): its branches, its spine, its bulged islands and the direct line's bend.
            private ForkPlan _fork;

            // True while a fork's starts are tested: it may run over a soft bend.
            private bool _forkReach;

            private readonly List<FairwayShape> _shapes = new List<FairwayShape>();
            private readonly List<GroundKeepOut> _keepOuts = new List<GroundKeepOut>();
            private readonly List<EllipseShape> _ovals = new List<EllipseShape>();
            private readonly List<FairwayPiece> _widenings = new List<FairwayPiece>();

            // The ground each accepted shape covers along the centreline (an oval's widening, a band's tip reach).
            private readonly List<Vector2> _reaches = new List<Vector2>();

            public Placer(CourseLayout bare, GreenPlan green, IReadOnlyList<float> bends, bool dogleg, bool islandGreen, int par, LevelGenSettings s, Dictionary<string, int> rejections, LayoutBuilder build)
            {
                _build = build;
                _bare = bare;
                _fields = new CourseFields(bare);
                _path = bare.Centreline;
                _green = green;
                _bends = bends;
                _dogleg = dogleg;
                _islandGreen = islandGreen;
                _par = par;
                _s = s;
                _r = s.FairwayShapes;
                _world = new RouteWorld(_fields, _path, s, green.SafeTarget, green.Approach, par - 1 + s.SafeRouteExtraShots);
                List<Vector2> route = SafeRoute.Find(_world, new List<HazardOutline>(), out List<float> alongs);
                _bareRoute = route != null;
                _rejections = rejections;

                // Where the shots the windows are measured from start, besides the tee: the bare safe route's rest spots.
                if (alongs != null)
                {
                    for (int i = 1; i < alongs.Count - 1; i++)
                    {
                        _restSpots.Add(alongs[i]);
                    }
                }
            }

            private readonly List<float> _restSpots = new List<float>();

            /// <summary>
            /// The stretches a shape's centre may lie in, from a window measured from the shot's origin: the tee (shifted a full reach
            /// for a par 5's second shape), or also from every rest spot of the bare safe route, or none (anywhere). Empty means anywhere.
            /// </summary>
            private List<Vector2> Windows(Vector2 window, float shift)
            {
                var windows = new List<Vector2>();
                switch (_par == 3 ? ShapeWindowOrigin.Anywhere : _r.WindowOrigin)
                {
                    case ShapeWindowOrigin.Anywhere:
                        return windows;
                    case ShapeWindowOrigin.EveryShot:
                        windows.Add(window);
                        foreach (float rest in _restSpots)
                        {
                            windows.Add(window + (Vector2.one * rest));
                        }

                        return windows;
                    default:
                        windows.Add(window + (Vector2.one * shift));
                        return windows;
                }
            }

            private readonly Dictionary<string, int> _rejections;

            /// <summary>Counts why a try failed, for the measurements; always false.</summary>
            private bool Reject(string reason)
            {
                _rejections.TryGetValue(reason, out int count);
                _rejections[reason] = count + 1;
                return false;
            }

            private FairwayLayout Fairway => _bare.Fairway;
            private float Cap => _s.FairwayCapDepth;

            /// <param name="shift">How far the shot's origin is from the tee: 0 for the first shape, a full reach for a par 5's second.</param>
            public bool Try(SeededRandom random, FairwayShapeKind kind, float shift)
            {
                for (int attempt = 0; attempt < _r.TriesPerShape; attempt++)
                {
                    bool placed = kind == FairwayShapeKind.RoughIsland ? TryRoughIsland(random, shift)
                        : kind == FairwayShapeKind.Split ? TrySplit(random, shift)
                        : TryFork(random);
                    if (placed)
                    {
                        return true;
                    }
                }

                return false;
            }

            public FairwayShapeResult Finish(FairwayShapeResult result)
            {
                if (_shapes.Count == 0)
                {
                    return result;
                }

                // In order along the centreline, so the plan and the pieces read from the tee.
                var order = new List<int>();
                for (int i = 0; i < _shapes.Count; i++)
                {
                    order.Add(i);
                }

                order.Sort((a, b) => _shapes[a].From.CompareTo(_shapes[b].From));
                foreach (int i in order)
                {
                    result.Shapes.Add(_shapes[i]);
                }

                result.Pieces.AddRange(Pieces(_shapes, _fork));
                result.Ovals.AddRange(_ovals);
                result.KeepOuts.AddRange(_keepOuts);
                result.World = _world.WithKeepOuts(_keepOuts);
                if (_fork != null)
                {
                    result.Islands = _fork.Islands;
                    result.DirectWaypoint = _fork.Waypoint;
                    result.Spine = _fork.Spine;
                    result.SafeBranch = _fork.Safe;
                    result.RiskyBranch = _fork.Risky;
                    result.BulgeSafe = _fork.BulgeSafe;
                    result.BulgeRisky = _fork.BulgeRisky;
                    result.BulgeStretch = new Vector2(_fork.Safe.From, _fork.Safe.To);
                }

                return result;
            }

            /// <summary>
            /// The fairway's pieces for these shapes: the main strip cut at every band (each cut end a rounded tip, the outer ends the
            /// fairway's own) and at a fork (flat joins: the two branches start and end exactly as the main strip), then the branches,
            /// then the rough islands' widenings. With no fork, exactly the pieces F3.2 built, in the same order.
            /// </summary>
            private List<FairwayPiece> Pieces(List<FairwayShape> shapes, ForkPlan fork)
            {
                var order = new List<FairwayShape>(shapes);
                order.Sort((a, b) => a.From.CompareTo(b.From));
                var pieces = new List<FairwayPiece>();
                float from = Fairway.Start;
                float fromCap = Fairway.StartCap;
                foreach (FairwayShape shape in order)
                {
                    if (shape.Kind == FairwayShapeKind.Split)
                    {
                        pieces.Add(new FairwayPiece(from, shape.From, Fairway.HalfWidth, fromCap, Cap));
                        from = shape.To;
                        fromCap = Cap;
                    }
                    else if (shape.Kind == FairwayShapeKind.Fork)
                    {
                        // The main strip overlaps each branch's eased end by half a metre: two flat cuts meeting exactly would leave a
                        // line of zero field across the fairway, which the terrain would read as a border.
                        pieces.Add(new FairwayPiece(from, shape.From + ForkOverlap, Fairway.HalfWidth, fromCap, 0f));
                        from = shape.To - ForkOverlap;
                        fromCap = 0f;
                    }
                }

                pieces.Add(new FairwayPiece(from, Fairway.End, Fairway.HalfWidth, fromCap, Fairway.EndCap));
                if (fork != null)
                {
                    pieces.Add(fork.Safe);
                    pieces.Add(fork.Risky);
                }

                pieces.AddRange(_widenings);
                return pieces;
            }

            // -----------------------------------------------------------------------------------------------------------
            // Fork (F3.3)

            /// <summary>A placed fork: what the rest of the course reads from it.</summary>
            private sealed class ForkPlan
            {
                public FairwayPiece Safe;
                public FairwayPiece Risky;
                public GroundKeepOut Spine;
                public IReadOnlyList<IslandOutline> Islands;
                public Vector2? Waypoint;
                public float BulgeSafe;
                public float BulgeRisky;
            }

            /// <summary>
            /// One fork candidate (split fairway plan §1, §3; director: "A, renflement"). Draws, in order: spine length, spine width, safe
            /// branch width, risky branch width (all whole cm), then the start (one even draw over the valid starts). The fork covers
            /// the spine's held stretch plus an ease at each end, where the branches open from the main strip and close back into it.
            /// <para>
            /// Sizes are minimums at every point (hub, Q2 A): a branch carries <see cref="FairwayShapeRules.BranchWobbleScale"/> of the
            /// fairway's edge wobble, so its half width and offset take the lowest and highest wobble over the fork into account. The
            /// safe branch lies on the green's safe side, the risky one on the guard's side (hub, Q3 A). The island bulges just enough
            /// on each side for the branch plus <see cref="FairwayShapeRules.ForkEdgeMargin"/> and for the safe capsule on the island,
            /// derived from the bare outline with no draw (Q5), at most <see cref="FairwayShapeRules.MaxBulge"/> (Q6).
            /// </para>
            /// </summary>
            private bool TryFork(SeededRandom random)
            {
                if (_fork != null)
                {
                    return Reject("fork: one fork per course");
                }

                float spineLength = random.Centimetres(_r.SpineLength);
                float spineWidth = random.Centimetres(_r.SpineWidth);
                float safeWidth = random.Centimetres(_r.SafeBranchWidth);
                float riskyWidth = random.Centimetres(_r.RiskyBranchWidth);

                // The safe branch is the wider one (plan §1): equal draws give the risky one a centimetre less, with no extra draw.
                if (Geo.Cm(riskyWidth) >= Geo.Cm(safeWidth))
                {
                    riskyWidth = (Geo.Cm(safeWidth) - 1) * 0.01f;
                }

                float ease = _r.ForkEase;
                float length = spineLength + (2f * ease);
                float originAlong = _path.Project(_green.DirectOrigin, out _);
                float tipClear = Mathf.Max(_r.FromFairwayTips, _r.ForkBeforeEndTip);

                // Statics: the clearances, the fork all before or all after the direct line's origin, and its end clear of the end tip.
                _forkReach = true;
                bool started = DrawStart(random, length, 0f, 0f, new List<Vector2>(), _r.FromGap, out float from, s =>
                    (Geo.Cm(s + length) <= Geo.Cm(originAlong) || Geo.Cm(s) >= Geo.Cm(originAlong))
                    && Geo.Cm(s + length) <= Geo.Cm(Fairway.End - Fairway.EndCap - tipClear));
                _forkReach = false;
                if (!started)
                {
                    return Reject("fork: no valid start");
                }

                float to = from + length;
                float mid = from + (length * 0.5f);

                // The green's safe side: where the safe target sits off the centreline, else away from the guard, else the left.
                Vector2 left = Geo.Left(Geo.PathDirection(_path, mid));
                _path.Project(_green.SafeTarget, out float targetSide);
                float safeSign = Geo.Cm(targetSide) != 0 ? Mathf.Sign(targetSide) : (Geo.Cm(Vector2.Dot(_green.Guard, left) * 100f) > 0 ? -1f : 1f);

                // The fairway's own wobble over the fork, the share a branch carries.
                float scale = _r.BranchWobbleScale;
                float wMin = float.MaxValue, wMax = float.MinValue;
                for (float t = from; t <= to + 0.001f; t += 0.1f)
                {
                    float w = EdgeWobble(t) * scale;
                    wMin = Mathf.Min(wMin, w);
                    wMax = Mathf.Max(wMax, w);
                }

                float spineHalf = spineWidth * 0.5f;
                float safeHalf = CeilCm((safeWidth * 0.5f) - Mathf.Min(0f, wMin));
                float riskyHalf = CeilCm((riskyWidth * 0.5f) - Mathf.Min(0f, wMin));
                float safeOffset = CeilCm(spineHalf + safeHalf + Mathf.Max(0f, wMax));
                float riskyOffset = CeilCm(spineHalf + riskyHalf + Mathf.Max(0f, wMax));

                var safe = new FairwayPiece(from, to, safeHalf, 0f, 0f, safeSign * safeOffset, ease, Fairway.HalfWidth, ease, scale);
                var risky = new FairwayPiece(from, to, riskyHalf, 0f, 0f, -safeSign * riskyOffset, ease, Fairway.HalfWidth, ease, scale);

                // The rest spots on the safe branch (hub, Q1 A): the capsule's inner row on the spine's widest border, never into it.
                float spineBorder = safeOffset - safeHalf - Mathf.Min(0f, wMin);
                float rest = CeilCm(spineBorder + _s.SafeLandingHalfWidth + 0.01f);

                // How far each side must reach from the centreline: the branch's outer edge plus the margin, and on the safe side the
                // capsule's outer row on the island.
                float needSafe = Mathf.Max(safeOffset + safeHalf + Mathf.Max(0f, wMax) + _r.ForkEdgeMargin, rest + _s.SafeLandingHalfWidth + _s.EdgeMargin);
                float needRisky = riskyOffset + riskyHalf + Mathf.Max(0f, wMax) + _r.ForkEdgeMargin;
                float bulgeSafe = 0f, bulgeRisky = 0f;
                for (float t = from; t <= to + 0.001f; t += 0.25f)
                {
                    bulgeSafe = Mathf.Max(bulgeSafe, needSafe - Room(t, safeSign));
                    bulgeRisky = Mathf.Max(bulgeRisky, needRisky - Room(t, -safeSign));
                }

                bulgeSafe = CeilCm(bulgeSafe);
                bulgeRisky = CeilCm(bulgeRisky);
                if (Geo.Cm(bulgeSafe) > Geo.Cm(_r.MaxBulge) || Geo.Cm(bulgeRisky) > Geo.Cm(_r.MaxBulge))
                {
                    // Bucketed by half metres, for the measurement of how far over the maximum the forks that find no room are.
                    int over = Mathf.CeilToInt((Mathf.Max(bulgeSafe, bulgeRisky) - _r.MaxBulge) * 2f);
                    Reject($"fork: bulge over the maximum by up to {over * 0.5f:F1} m");
                    return Reject("fork: bulge over the maximum");
                }

                IReadOnlyList<IslandOutline> islands = Bulged(from, to, safeSign > 0f ? bulgeSafe : bulgeRisky, safeSign > 0f ? bulgeRisky : bulgeSafe);

                // The candidate course: its bulged islands and every piece placed so far plus the fork's.
                var shapes = new List<FairwayShape>(_shapes) { new FairwayShape(FairwayShapeKind.Fork, from, to, safeSign * safeOffset, -safeSign * riskyOffset, safeSign * rest) };
                var candidate = new ForkPlan { Safe = safe, Risky = risky, Islands = islands, BulgeSafe = bulgeSafe, BulgeRisky = bulgeRisky };
                CourseLayout layout = _build(islands, Pieces(shapes, candidate), new List<EllipseShape>(_ovals));
                var fields = new CourseFields(layout);

                // The bulge did its job: each branch's true outer edge keeps the margin from the true (bulged) island edge.
                for (float t = from; t <= to + 0.001f; t += 0.25f)
                {
                    Vector2 point = _path.PointAt(t);
                    Vector2 side = Geo.Left(Geo.PathDirection(_path, t));
                    float wobble = EdgeWobble(t);
                    foreach (FairwayPiece branch in new[] { safe, risky })
                    {
                        float edge = branch.OffsetAt(t) + (Mathf.Sign(branch.Offset) * (branch.HalfWidthAt(t) + branch.WobbleAt(t, wobble)));
                        if (Geo.Cm(fields.Island(Geo.At(point + (side * edge)))) > -Geo.Cm(_r.ForkEdgeMargin))
                        {
                            return Reject("fork: branch too near the island edge");
                        }
                    }
                }

                GroundKeepOut spine = GroundKeepOut.OfSpine(fields, safe, risky, from, to);

                // No dogleg's cut line may cross it either (the fork keeps off the bends, but a long cut from the tee can still reach it).
                for (int bend = 0; _dogleg && bend < _bends.Count; bend++)
                {
                    if (HazardPlacer.CutLine(_path, _bends, bend, _s, out Vector2 cutFrom, out Vector2 cutTo, out _) && spine.Crosses(cutFrom, cutTo, _path))
                    {
                        return Reject("fork: a cut line crosses the spine");
                    }
                }

                // The direct line follows the risky branch when it runs over the fork (plan §3, Q5 A): two segments, neither crossing the spine.
                Vector2? waypoint = null;
                if (Geo.Cm(originAlong) <= Geo.Cm(from))
                {
                    waypoint = _path.PointAt(mid) + (left * risky.OffsetAt(mid));
                    if (spine.Crosses(_green.DirectOrigin, waypoint.Value, _path) || spine.Crosses(waypoint.Value, _bare.Hole, _path))
                    {
                        return Reject("fork: direct line crosses the spine");
                    }
                }

                // The safe route rests on the safe branch, its capsules off the spine, and no step crosses it.
                var world = new RouteWorld(fields, _path, _s, _green.SafeTarget, _green.Approach, _par - 1 + _s.SafeRouteExtraShots, RouteOffsetOf(shapes, _s));
                var keepOuts = new List<GroundKeepOut>(_keepOuts) { spine };
                if (_bareRoute && SafeRoute.Find(world.WithKeepOuts(keepOuts), new List<HazardOutline>(), out _) == null)
                {
                    return Reject("fork: no safe route");
                }

                candidate.Spine = spine;
                candidate.Waypoint = waypoint;
                _fork = candidate;
                _fields = fields;
                _world = world;
                _shapes.Add(shapes[shapes.Count - 1]);
                _keepOuts.Add(spine);
                _reaches.Add(new Vector2(from, to));
                return true;
            }

            private const float ForkOverlap = 0.5f;

            private static float CeilCm(float metres) => Mathf.Ceil((metres * 100f) - 0.0001f) / 100f;

            /// <summary>How far the bare island reaches from the centreline at this distance along it, on one side (bisection, to a millimetre).</summary>
            private float Room(float along, float sign)
            {
                Vector2 point = _path.PointAt(along);
                Vector2 side = Geo.Left(Geo.PathDirection(_path, along)) * sign;
                float inside = 0f, outside = 12f;
                if (_fields.Island(Geo.At(point)) >= 0f)
                {
                    return 0f;
                }

                for (int i = 0; i < 16; i++)
                {
                    float m = (inside + outside) * 0.5f;
                    if (_fields.Island(Geo.At(point + (side * m))) < 0f)
                    {
                        inside = m;
                    }
                    else
                    {
                        outside = m;
                    }
                }

                return inside;
            }

            /// <summary>
            /// The course's islands with the bulge on every one whose own stretch meets the fork (two leg islands where a soft bend runs
            /// under it), held over the fork's stretch in each island's own frame and eased beyond it. The same list when no side bulges.
            /// </summary>
            private IReadOnlyList<IslandOutline> Bulged(float from, float to, float left, float right)
            {
                IReadOnlyList<IslandOutline> islands = _bare.Islands;
                if (Geo.Cm(left) <= 0 && Geo.Cm(right) <= 0)
                {
                    return islands;
                }

                var bulged = new List<IslandOutline>();
                float ease = _r.BulgeEase;
                foreach (IslandOutline island in islands)
                {
                    float a = island.ToOwnFrame(Geo.At(_path.PointAt(from))).x;
                    float b = island.ToOwnFrame(Geo.At(_path.PointAt(to))).x;
                    float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                    bool meets = hi + ease > island.CenterX - island.HalfLength && lo - ease < island.CenterX + island.HalfLength;
                    bulged.Add(meets ? island.WithBulge(lo, hi, ease, Mathf.Max(0f, left), Mathf.Max(0f, right)) : island);
                }

                return bulged;
            }

            // -----------------------------------------------------------------------------------------------------------
            // Rough island

            private bool TryRoughIsland(SeededRandom random, float shift)
            {
                float length = random.Centimetres(_r.IslandLength);
                float width = random.Centimetres(_r.IslandWidth);
                float wobble = random.Centimetres(_r.IslandWobble);

                // The oval covers [start, start + length]; the widening reaches an ease beyond it on both sides.
                float ease = _r.IslandWidenEase;
                if (!DrawStart(random, length, 0f, ease, Windows(_r.IslandDriveWindow, shift), _r.FromGap, out float start))
                {
                    return Reject("rough island: no valid start");
                }

                float centreAlong = start + (length * 0.5f);
                Vector2 centre = _path.PointAt(centreAlong);
                var oval = new EllipseShape(centre, new Vector2(length * 0.5f, width * 0.5f), wobble, Geo.HeadingOf(Geo.PathDirection(_path, centreAlong)));
                HazardOutline outline = HazardOutline.Of(oval, false, HazardRole.Flank);

                // How far the true outline (wobble in) reaches across and along the centreline.
                float acrossReach = 0f, alongFrom = float.MaxValue, alongTo = float.MinValue;
                foreach (Vector2 p in outline.Ring)
                {
                    float along = _path.Project(p, out float side);
                    acrossReach = Mathf.Max(acrossReach, Mathf.Abs(side));
                    alongFrom = Mathf.Min(alongFrom, along);
                    alongTo = Mathf.Max(alongTo, along);
                }

                // The wobble can push the true outline a little past the drawn length: the rules hold on the true reach too.
                if (!ReachOk(alongFrom, alongTo, ease, _r.FromGap))
                {
                    return Reject("rough island: true outline breaks a clearance");
                }

                // The strip's half width over the oval: its reach plus the fairway kept on each side, minus the lowest edge wobble
                // over the oval's span, so the 2 m hold at every point (director, Q1 A). Rounded up to the centimetre.
                float lowest = float.MaxValue;
                for (float t = alongFrom - 0.5f; t <= alongTo + 0.5f; t += 0.1f)
                {
                    lowest = Mathf.Min(lowest, EdgeWobble(t));
                }

                float half = Mathf.Max(Fairway.HalfWidth, Mathf.Ceil((acrossReach + _r.IslandFairwaySide - lowest) * 100f) / 100f);
                float widenFrom = alongFrom - 0.5f - ease, widenTo = alongTo + 0.5f + ease;
                var widening = new FairwayPiece(widenFrom, widenTo, half, 0f, 0f, 0f, 0f, Fairway.HalfWidth, ease);

                // The widened strip stays inside the island, with a margin.
                for (float t = widenFrom; t <= widenTo + 0.001f; t += 0.5f)
                {
                    Vector2 point = _path.PointAt(t);
                    Vector2 left = Geo.Left(Geo.PathDirection(_path, t));
                    float edge = widening.HalfWidthAt(t) + EdgeWobble(t);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        if (Geo.Cm(_fields.Island(Geo.At(point + (left * (sign * edge))))) > -Geo.Cm(_r.IslandEdgeMargin))
                        {
                            return Reject("rough island: widened strip too near the island edge");
                        }
                    }
                }

                GroundKeepOut keepOut = GroundKeepOut.OfOval(outline, alongFrom, alongTo);
                if (!RouteSurvives(keepOut))
                {
                    return Reject("rough island: no safe route");
                }

                _shapes.Add(new FairwayShape(FairwayShapeKind.RoughIsland, alongFrom, alongTo));
                _keepOuts.Add(keepOut);
                _ovals.Add(oval);
                _widenings.Add(widening);
                _reaches.Add(new Vector2(widenFrom, widenTo));
                return true;
            }

            /// <summary>The fairway's edge wobble at a distance along the centreline, as <see cref="CourseFields.Fairway"/> adds it.</summary>
            private float EdgeWobble(float along) =>
                (CourseNoise.Value01((along * 0.2f) + 70f + Fairway.NoiseOffset, 1.3f) - 0.5f) * Fairway.Wobble;

            // -----------------------------------------------------------------------------------------------------------
            // Split

            private bool TrySplit(SeededRandom random, float shift)
            {
                float length = random.Centimetres(_r.BandLength);
                List<Vector2> window = Windows(_r.BandWindow, shift);

                // The band covers [start, start + length]; its tips leave rough up to a cap's depth beyond it.
                if (!DrawStart(random, length, Cap, 0f, window, _r.SplitFromGap, out float start))
                {
                    return Reject("split: no valid start");
                }

                float bandFrom = start, bandTo = start + length;
                GroundKeepOut keepOut = GroundKeepOut.OfBand(bandFrom - Cap, bandTo + Cap);
                if (!RouteSurvives(keepOut))
                {
                    return Reject("split: no safe route");
                }

                _shapes.Add(new FairwayShape(FairwayShapeKind.Split, bandFrom, bandTo));
                _keepOuts.Add(keepOut);
                _reaches.Add(new Vector2(bandFrom - Cap, bandTo + Cap));
                return true;
            }

            // -----------------------------------------------------------------------------------------------------------
            // Rules

            private bool RouteSurvives(GroundKeepOut candidate)
            {
                if (!_bareRoute)
                {
                    // No route on the bare course: nothing to protect (as for the hazards).
                    return true;
                }

                var keepOuts = new List<GroundKeepOut>(_keepOuts) { candidate };
                return SafeRoute.Find(_world.WithKeepOuts(keepOuts), new List<HazardOutline>(), out _) != null;
            }

            /// <summary>
            /// One even draw over every start, in whole centimetres, where a shape of this length passes the static rules: its reach
            /// (<paramref name="tips"/> before and after it for a band's tips; <paramref name="ease"/> more for a widening's ends, which
            /// must stay off the fairway's own tips) keeps clear of the tee, the gaps, the bends, the fairway's tips and any shape
            /// already placed, and its centre lies in the window. False, with no draw, if there is none.
            /// </summary>
            private bool DrawStart(SeededRandom random, float length, float tips, float ease, IReadOnlyList<Vector2> windows, float fromGap, out float start, System.Func<float, bool> also = null)
            {
                start = 0f;
                var valid = new List<int>();
                int last = Geo.Cm(_path.Length);
                for (int cm = 0; cm <= last; cm++)
                {
                    float s = cm * 0.01f;
                    if (StaticOk(s, length, tips, ease, windows, fromGap) && (also == null || also(s)))
                    {
                        valid.Add(cm);
                    }
                }

                if (valid.Count == 0)
                {
                    return false;
                }

                start = valid[random.Int(0, valid.Count - 1)] * 0.01f;
                return true;
            }

            private bool StaticOk(float start, float length, float tips, float ease, IReadOnlyList<Vector2> windows, float fromGap)
            {
                int centre = Geo.Cm(start + (length * 0.5f));
                bool inside = windows.Count == 0;
                foreach (Vector2 window in windows)
                {
                    inside |= centre >= Geo.Cm(window.x) && centre <= Geo.Cm(window.y);
                }

                if (!inside)
                {
                    return false;
                }

                return ReachOk(start - tips, start + length + tips, ease, fromGap);
            }

            /// <summary>
            /// The static rules on a shape's rough reach along the centreline, <paramref name="from"/> to <paramref name="to"/>, and on the
            /// strip it changes, <paramref name="ease"/> (plus half a metre) beyond it for a widening.
            /// </summary>
            private bool ReachOk(float from, float to, float ease, float fromGap)
            {
                int lo = Geo.Cm(from), hi = Geo.Cm(to);
                int changedLo = Geo.Cm(from - ease - 0.5f), changedHi = Geo.Cm(to + ease + 0.5f);

                if (lo < Geo.Cm(_r.FromTee) || lo < Geo.Cm(Fairway.Start + Fairway.StartCap + _r.FromFairwayTips)
                    || hi > Geo.Cm(Fairway.End - Fairway.EndCap - _r.FromFairwayTips))
                {
                    return false;
                }

                // A widening ends flat at the main strip's width: it must not reach into the fairway's rounded tips.
                if (changedLo < Geo.Cm(Fairway.Start + Fairway.StartCap) || changedHi > Geo.Cm(Fairway.End - Fairway.EndCap))
                {
                    return false;
                }

                IReadOnlyList<Vector2> gaps = _world.Gaps;
                for (int g = 0; g < gaps.Count; g++)
                {
                    // An island green's own island gets no shape: everything stays before its gap.
                    bool before = hi <= Geo.Cm(gaps[g].x - fromGap);
                    bool after = lo >= Geo.Cm(gaps[g].y + fromGap);
                    if (!before && (!after || (_islandGreen && g == gaps.Count - 1)))
                    {
                        return false;
                    }
                }

                // The gaps' bend clearance, on the rough's own reach: a widening is drawn in the centreline's frame and may run over a
                // soft bend (plan §1: soft bends are allowed); a dogleg's clearance is wider than any widening's ease.
                // A fork is built in the centreline's frame too, and runs over a soft bend whole (plan §1, Q7 A): only a dogleg's bend stops it.
                float bendClearance = _dogleg ? _s.GapFromBend : _s.GapFromSoftBend;
                foreach (float bend in _forkReach && !_dogleg ? new float[0] : _bends)
                {
                    if (hi > Geo.Cm(bend - bendClearance) && lo < Geo.Cm(bend + bendClearance))
                    {
                        return false;
                    }
                }

                foreach (Vector2 reach in _reaches)
                {
                    if (changedHi > Geo.Cm(reach.x - _r.BetweenShapes) && changedLo < Geo.Cm(reach.y + _r.BetweenShapes))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
