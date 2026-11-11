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

        public bool Blocks(Vector2 p, CoursePath path)
        {
            if (Oval != null)
            {
                return Geo.Cm(Oval.SignedDistance(p, 1f)) <= 0;
            }

            int along = Geo.Cm(path.Project(p, out _));
            return along >= Geo.Cm(From) && along <= Geo.Cm(To);
        }
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

        /// <summary>The kind the first draw gave (null for plain), and a par 5's second (null for none), placed or not.</summary>
        public FairwayShapeKind? FirstDrawn { get; set; }
        public FairwayShapeKind? SecondDrawn { get; set; }

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

        public static FairwayShapeResult Place(
            int seed, int par, int variant, CourseLayout bare, GreenPlan green, IReadOnlyList<float> bends, bool dogleg, bool islandGreen, LevelGenSettings s)
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

            // What was drawn, for the measurements (a fork draw included). Forks come in F3.3: until then a fork leaves it plain.
            result.FirstDrawn = kind;
            result.SecondDrawn = secondKind;
            kind = kind == FairwayShapeKind.Fork ? null : kind;
            secondKind = secondKind == FairwayShapeKind.Fork ? null : secondKind;
            if (kind == null && secondKind == null)
            {
                return result;
            }

            var placer = new Placer(bare, green, bends, dogleg, islandGreen, par, s, result.Rejections);
            if (kind != null)
            {
                placer.Try(first, kind.Value, 0f);
            }

            if (secondKind != null)
            {
                placer.Try(second, secondKind.Value, s.FullReach);
            }

            return placer.Finish(result);
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
            if (pick < w.Plain)
            {
                return null;
            }

            pick -= w.Plain;
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
            private readonly CourseFields _fields;
            private readonly CoursePath _path;
            private readonly GreenPlan _green;
            private readonly IReadOnlyList<float> _bends;
            private readonly bool _dogleg;
            private readonly bool _islandGreen;
            private readonly int _par;
            private readonly LevelGenSettings _s;
            private readonly FairwayShapeRules _r;
            private readonly RouteWorld _world;
            private readonly bool _bareRoute;

            private readonly List<FairwayShape> _shapes = new List<FairwayShape>();
            private readonly List<GroundKeepOut> _keepOuts = new List<GroundKeepOut>();
            private readonly List<EllipseShape> _ovals = new List<EllipseShape>();
            private readonly List<FairwayPiece> _widenings = new List<FairwayPiece>();

            // The ground each accepted shape covers along the centreline (an oval's widening, a band's tip reach).
            private readonly List<Vector2> _reaches = new List<Vector2>();

            public Placer(CourseLayout bare, GreenPlan green, IReadOnlyList<float> bends, bool dogleg, bool islandGreen, int par, LevelGenSettings s, Dictionary<string, int> rejections)
            {
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
            public void Try(SeededRandom random, FairwayShapeKind kind, float shift)
            {
                for (int attempt = 0; attempt < _r.TriesPerShape; attempt++)
                {
                    if (kind == FairwayShapeKind.RoughIsland ? TryRoughIsland(random, shift) : TrySplit(random, shift))
                    {
                        return;
                    }
                }
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

                // The main strip, cut at every band: each cut end gets a rounded tip, the outer ends keep the fairway's own.
                float from = Fairway.Start;
                float fromCap = Fairway.StartCap;
                foreach (int i in order)
                {
                    if (_shapes[i].Kind == FairwayShapeKind.Split)
                    {
                        result.Pieces.Add(new FairwayPiece(from, _shapes[i].From, Fairway.HalfWidth, fromCap, Cap));
                        from = _shapes[i].To;
                        fromCap = Cap;
                    }
                }

                result.Pieces.Add(new FairwayPiece(from, Fairway.End, Fairway.HalfWidth, fromCap, Fairway.EndCap));
                result.Pieces.AddRange(_widenings);
                result.Ovals.AddRange(_ovals);
                result.KeepOuts.AddRange(_keepOuts);
                result.World = _world.WithKeepOuts(_keepOuts);
                return result;
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
            private bool DrawStart(SeededRandom random, float length, float tips, float ease, IReadOnlyList<Vector2> windows, float fromGap, out float start)
            {
                start = 0f;
                var valid = new List<int>();
                int last = Geo.Cm(_path.Length);
                for (int cm = 0; cm <= last; cm++)
                {
                    float s = cm * 0.01f;
                    if (StaticOk(s, length, tips, ease, windows, fromGap))
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
                float bendClearance = _dogleg ? _s.GapFromBend : _s.GapFromSoftBend;
                foreach (float bend in _bends)
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
