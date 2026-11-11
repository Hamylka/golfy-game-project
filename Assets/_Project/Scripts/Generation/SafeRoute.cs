using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The course as the safe-route check sees it: the centreline, which stretches of it are open sky, and the clear ground
    /// each possible rest spot needs, measured once on the bare island. Hazards are added per call to
    /// <see cref="SafeRoute.Find"/>; a hazard's blocked spots are cached, because the placement asks about every candidate.
    /// </summary>
    internal sealed class RouteWorld
    {
        // The grid of possible rest spots, in metres along the centreline.
        public const float Grid = 0.5f;

        // How far a hazard must stay from the ground a ball lands on.
        public const float HazardMargin = 0.5f;

        private readonly bool[] _bareOk;

        // The bare course's clear spots before any keep-out. Hazard masks are measured against these, so one mask is right in
        // every world that shares the cache (a world with keep-outs only clears fewer spots).
        private readonly bool[] _islandOk;
        private readonly Dictionary<HazardOutline, bool[]> _masks;
        private readonly IReadOnlyList<GroundKeepOut> _keepOuts;

        /// <summary>A world with the same island and gaps, whose rest spots and final approach also keep off <paramref name="keepOuts"/>.</summary>
        private RouteWorld(RouteWorld bare, IReadOnlyList<GroundKeepOut> keepOuts)
        {
            Fields = bare.Fields;
            Path = bare.Path;
            Settings = bare.Settings;
            SafeTarget = bare.SafeTarget;
            Approach = bare.Approach;
            MaxShots = bare.MaxShots;
            Gaps = bare.Gaps;
            Offset = bare.Offset;
            _islandOk = bare._islandOk;
            _masks = bare._masks;
            _crossings = bare._crossings;
            _keepOuts = new List<GroundKeepOut>(keepOuts);
            _bareOk = (bool[])bare._islandOk.Clone();

            float reach = Settings.SafeLandingBack + Settings.SafeLandingAhead + Settings.SafeLandingHalfWidth + 1f;
            foreach (GroundKeepOut keepOut in _keepOuts)
            {
                for (int k = 0; k < _bareOk.Length; k++)
                {
                    float along = k * Grid;
                    if (!_bareOk[k] || along < keepOut.From - reach || along > keepOut.To + reach)
                    {
                        continue;
                    }

                    foreach (Vector2 p in CapsuleSamples(along))
                    {
                        if (keepOut.Blocks(p, Path))
                        {
                            _bareOk[k] = false;
                            break;
                        }
                    }
                }
            }
        }

        /// <param name="offset">On a fork course (F3.3), where the rest spots move across onto the safe branch. None elsewhere.</param>
        public RouteWorld(CourseFields fields, CoursePath path, LevelGenSettings settings, Vector2 safeTarget, Vector2 approach, int maxShots, RouteOffset? offset = null)
        {
            _masks = new Dictionary<HazardOutline, bool[]>();
            _crossings = new Dictionary<long, bool>();
            _keepOuts = new GroundKeepOut[0];
            Offset = offset;
            Fields = fields;
            Path = path;
            Settings = settings;
            SafeTarget = safeTarget;
            Approach = approach;
            MaxShots = maxShots;

            Gaps = FindGaps(fields, path);
            int count = Mathf.FloorToInt(path.Length / Grid) + 1;
            _bareOk = new bool[count];
            for (int k = 0; k < count; k++)
            {
                _bareOk[k] = BareClear(k * Grid);
            }

            _islandOk = _bareOk;
        }

        /// <summary>
        /// This world with ground the route may not land on (fairway shapes, F3.2). It shares the island, the gaps and the hazard
        /// masks; with no keep-out it is this world itself, so a plain course routes exactly as before.
        /// </summary>
        public RouteWorld WithKeepOuts(IReadOnlyList<GroundKeepOut> keepOuts)
        {
            return keepOuts == null || keepOuts.Count == 0 ? this : new RouteWorld(this, keepOuts);
        }

        /// <summary>The ground the route keeps off, empty on a plain course.</summary>
        public IReadOnlyList<GroundKeepOut> KeepOuts => _keepOuts;

        /// <summary>Where the rest spots move across (a fork's safe branch), or none.</summary>
        public RouteOffset? Offset { get; }

        // Whether a step between two spots crosses a spine, by (from, to) node: it does not depend on the hazards, so every call to
        // SafeRoute.Find on this world (and on its keep-out copies, which share the spine) reuses it.
        private readonly Dictionary<long, bool> _crossings;

        /// <summary>
        /// The rest spot at this distance along the centreline: on it, or moved across onto a fork's safe branch. Exactly
        /// <see cref="CoursePath.PointAt"/> where there is no offset, so plain and other shaped courses route as before.
        /// </summary>
        public Vector2 SpotPoint(float along)
        {
            float offset = Offset?.At(along) ?? 0f;
            if (offset == 0f)
            {
                return Path.PointAt(along);
            }

            return Path.PointAt(along) + (Geo.Left(Geo.PathDirection(Path, along)) * offset);
        }

        /// <summary>Whether the step between two nodes of <see cref="SafeRoute.Find"/> crosses a fork's spine. Cached.</summary>
        public bool StepCrossesSpine(int fromNode, int toNode, Vector2 from, Vector2 to)
        {
            bool any = false;
            foreach (GroundKeepOut keepOut in _keepOuts)
            {
                any |= keepOut.IsSpine;
            }

            if (!any)
            {
                return false;
            }

            long key = ((long)fromNode << 32) ^ (uint)toNode;
            if (_crossings.TryGetValue(key, out bool crosses))
            {
                return crosses;
            }

            crosses = false;
            foreach (GroundKeepOut keepOut in _keepOuts)
            {
                if (keepOut.IsSpine && keepOut.Crosses(from, to, Path))
                {
                    crosses = true;
                    break;
                }
            }

            _crossings[key] = crosses;
            return crosses;
        }

        public CourseFields Fields { get; }
        public CoursePath Path { get; }
        public LevelGenSettings Settings { get; }
        public Vector2 SafeTarget { get; }

        /// <summary>The unit direction the last leg heads, which the final approach to the safe target follows.</summary>
        public Vector2 Approach { get; }
        /// <summary>The most shots the route may take from the tee to the safe target.</summary>
        public int MaxShots { get; }

        /// <summary>The stretches of the centreline (x: start, y: end, metres from the tee) that lie off every island.</summary>
        public IReadOnlyList<Vector2> Gaps { get; }

        public int Spots => _bareOk.Length;

        /// <summary>Whether a spot of the grid has a landing capsule on the island, ignoring hazards.</summary>
        public bool BareClear(int k) => _bareOk[k];

        /// <summary>
        /// The ground a ball landing at this distance along the centreline needs: from <see cref="LevelGenSettings.SafeLandingBack"/>
        /// before it to <see cref="LevelGenSettings.SafeLandingAhead"/> after it (the roll of a full shot, and a metre), as wide
        /// as <see cref="LevelGenSettings.SafeLandingHalfWidth"/> either side, sampled every half metre.
        /// </summary>
        public List<Vector2> CapsuleSamples(float along)
        {
            var samples = new List<Vector2>();
            float step = Grid;
            for (float s = along - Settings.SafeLandingBack; s <= along + Settings.SafeLandingAhead + 0.001f; s += step)
            {
                Vector2 centre = Path.PointAt(s);
                Vector2 side = Geo.Left(Geo.PathDirection(Path, s));

                // On a fork's safe branch the capsule follows the spot across, sample row by sample row.
                float offset = Offset?.At(s) ?? 0f;
                if (offset != 0f)
                {
                    centre += side * offset;
                }

                for (float across = -Settings.SafeLandingHalfWidth; across <= Settings.SafeLandingHalfWidth + 0.001f; across += step)
                {
                    samples.Add(centre + (side * across));
                }
            }

            return samples;
        }

        /// <summary>
        /// The final approach: the last 3 m before the safe target, along the last leg, 0.6 m either side, with the last metre of its middle on the
        /// green. Returns the samples and, for each, whether it has to be green.
        /// </summary>
        public List<KeyValuePair<Vector2, bool>> FinalSamples()
        {
            var samples = new List<KeyValuePair<Vector2, bool>>();
            Vector2 side = Geo.Left(Approach);
            for (float back = 3f; back >= -0.001f; back -= Grid)
            {
                for (int i = -1; i <= 1; i++)
                {
                    samples.Add(new KeyValuePair<Vector2, bool>(SafeTarget - (Approach * back) + (side * (i * 0.6f)), back <= 1f && i == 0));
                }
            }

            return samples;
        }

        /// <summary>Whether a capsule around this distance along the centreline is free of hazards and on the island.</summary>
        public bool CapsuleClear(float along, IReadOnlyList<HazardOutline> hazards)
        {
            foreach (Vector2 p in CapsuleSamples(along))
            {
                if (!OnIsland(p) || Blocked(p, hazards))
                {
                    return false;
                }
            }

            return true;
        }

        public bool FinalClear(IReadOnlyList<HazardOutline> hazards)
        {
            foreach (KeyValuePair<Vector2, bool> sample in FinalSamples())
            {
                if (Geo.Cm(Fields.Island(Geo.At(sample.Key))) > -Geo.Cm(Settings.EdgeMargin * 0.5f) || Blocked(sample.Key, hazards) || (sample.Value && Fields.Green(Geo.At(sample.Key)) >= 0f))
                {
                    return false;
                }

                foreach (GroundKeepOut keepOut in _keepOuts)
                {
                    if (keepOut.Blocks(sample.Key, Path))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>For each spot of the grid, whether this hazard blocks its capsule. Cached per hazard.</summary>
        public bool[] Mask(HazardOutline hazard)
        {
            if (_masks.TryGetValue(hazard, out bool[] mask))
            {
                return mask;
            }

            mask = new bool[_islandOk.Length];
            float reach = hazard.BoundRadius + HazardMargin + Settings.SafeLandingBack + Settings.SafeLandingHalfWidth + 1f;
            for (int k = 0; k < mask.Length; k++)
            {
                if (!_islandOk[k] || Vector2.Distance(SpotPoint(k * Grid), hazard.Centre) > reach)
                {
                    continue;
                }

                foreach (Vector2 p in CapsuleSamples(k * Grid))
                {
                    if (Geo.Cm(hazard.SignedDistance(p, HazardMargin + 1f)) < Geo.Cm(HazardMargin))
                    {
                        mask[k] = true;
                        break;
                    }
                }
            }

            _masks[hazard] = mask;
            return mask;
        }

        public bool OnIsland(Vector2 p) => Geo.Cm(Fields.Island(Geo.At(p))) <= -Geo.Cm(Settings.EdgeMargin);

        private bool Blocked(Vector2 p, IReadOnlyList<HazardOutline> hazards)
        {
            foreach (HazardOutline hazard in hazards)
            {
                if (Geo.Cm(hazard.SignedDistance(p, HazardMargin + 1f)) < Geo.Cm(HazardMargin))
                {
                    return true;
                }
            }

            return false;
        }

        private bool BareClear(float along)
        {
            foreach (Vector2 p in CapsuleSamples(along))
            {
                if (!OnIsland(p))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Vector2> FindGaps(CourseFields fields, CoursePath path)
        {
            var gaps = new List<Vector2>();
            const float Step = 0.05f;
            float start = -1f;
            for (float s = 0f; s <= path.Length; s += Step)
            {
                Vector2 p = path.PointAt(s);
                bool off = fields.Island(Geo.At(p)) >= 0f;
                if (off && start < 0f)
                {
                    start = s;
                }
                else if (!off && start >= 0f)
                {
                    gaps.Add(new Vector2(start, s));
                    start = -1f;
                }
            }

            return gaps;
        }
    }

    /// <summary>
    /// The safe route, the one thing the hazard step must never take away. It is a chain of rest spots along the centreline,
    /// from the tee to the safe target on the green, such that:
    /// <list type="bullet">
    /// <item>a step is at most <see cref="LevelGenSettings.SafeStep"/> as the crow flies;</item>
    /// <item>every rest spot has a clear landing capsule (<see cref="RouteWorld.CapsuleSamples"/>), and the safe target a clear
    /// final approach on the green;</item>
    /// <item>a step across a gap starts at a lay-up spot and is at most 70% power's carry, landing
    /// <see cref="LevelGenSettings.GapLandingMargin"/> past the gap's far edge;</item>
    /// <item>it takes at most par - 1 shots, plus <see cref="LevelGenSettings.SafeRouteExtraShots"/> (1 by default).</item>
    /// </list>
    /// It is found by counting the fewest shots from each spot back from the safe target, then walking from the tee to the furthest spot that keeps the count (a plain "furthest first" walk dead-ends before a gap), on the bare course once, and again for every candidate hazard:
    /// a candidate that breaks it is rejected. That is what makes "a safe route always exists" true by construction.
    /// </summary>
    internal static class SafeRoute
    {
        /// <summary>
        /// The route as points (tee, rest spots, safe target) with each one's distance along the centreline, or null if there is
        /// none. Hazards are the ones to keep clear of; pass none for the bare course.
        /// </summary>
        public static List<Vector2> Find(RouteWorld world, IReadOnlyList<HazardOutline> hazards, out List<float> alongs)
        {
            alongs = null;
            LevelGenSettings s = world.Settings;
            if (!world.FinalClear(hazards))
            {
                return null;
            }

            var masks = new List<bool[]>();
            foreach (HazardOutline hazard in hazards)
            {
                masks.Add(world.Mask(hazard));
            }

            // Nodes: the tee (-1), every clear spot of the grid, and the safe target (Spots).
            int count = world.Spots;
            int targetNode = count;
            float targetAlong = world.Path.Project(world.SafeTarget, out _);
            int lastSpot = Mathf.Min(count - 1, Mathf.FloorToInt((targetAlong - 1f) / RouteWorld.Grid));

            Vector2 PointOf(int node) => node < 0 ? world.Path.Start : node == targetNode ? world.SafeTarget : world.SpotPoint(node * RouteWorld.Grid);
            float AlongOf(int node) => node < 0 ? 0f : node == targetNode ? targetAlong : node * RouteWorld.Grid;
            bool Edge(int from, int to) =>
                Geo.Cm(Vector2.Distance(PointOf(from), PointOf(to))) <= Geo.Cm(s.SafeStep) && StepOk(world, AlongOf(from), PointOf(from), AlongOf(to), PointOf(to))
                && !world.StepCrossesSpine(from, to, PointOf(from), PointOf(to));

            // The fewest shots from each spot to the safe target, backwards. A greedy "furthest spot first" walk dead-ends in front
            // of a gap (it can stop where no lay-up reaches the far side), so the walk is chosen with these counts.
            const int Never = 1000;
            var shots = new int[count + 1];
            shots[targetNode] = 0;
            for (int node = count - 1; node >= -1; node--)
            {
                if (node >= 0)
                {
                    shots[node] = Never;
                    if (node > lastSpot || !world.BareClear(node) || IsMasked(masks, node))
                    {
                        continue;
                    }
                }

                int best = Never;
                int reach = Mathf.Min(count - 1, Mathf.FloorToInt((AlongOf(node) + s.SafeStep + 1f) / RouteWorld.Grid));
                for (int next = Mathf.Max(node + 1, 0); next <= Mathf.Min(reach, lastSpot) + 1; next++)
                {
                    int to = next > lastSpot ? targetNode : next;
                    if (shots[to] + 1 < best && Edge(node, to))
                    {
                        best = shots[to] + 1;
                    }

                    if (to == targetNode)
                    {
                        break;
                    }
                }

                if (node < 0)
                {
                    if (best > world.MaxShots)
                    {
                        return null;
                    }

                    shots[count] = 0;
                    return Walk(world, best, Edge, PointOf, AlongOf, shots, lastSpot, targetNode, out alongs);
                }

                shots[node] = best;
            }

            return null;
        }

        // From the tee, each time the furthest spot that still leaves the fewest shots to go.
        private static List<Vector2> Walk(RouteWorld world, int total, System.Func<int, int, bool> edge, System.Func<int, Vector2> pointOf, System.Func<int, float> alongOf, int[] shots, int lastSpot, int targetNode, out List<float> alongs)
        {
            var route = new List<Vector2> { pointOf(-1) };
            alongs = new List<float> { 0f };
            int current = -1;
            for (int togo = total; togo > 0; togo--)
            {
                int chosen = -1;
                if (togo == 1)
                {
                    chosen = targetNode;
                }
                else
                {
                    for (int k = lastSpot; k > current; k--)
                    {
                        if (shots[k] == togo - 1 && edge(current, k))
                        {
                            chosen = k;
                            break;
                        }
                    }
                }

                current = chosen;
                route.Add(pointOf(chosen));
                alongs.Add(alongOf(chosen));
            }

            return route;
        }

        /// <summary>Whether a step from one spot to the next respects the gaps between them (see the class summary).</summary>
        private static bool StepOk(RouteWorld world, float fromAlong, Vector2 from, float toAlong, Vector2 to)
        {
            LevelGenSettings s = world.Settings;
            int layup = Geo.Cm(s.CarryAt(s.LayupPower));
            foreach (Vector2 gap in world.Gaps)
            {
                // Across the gap: a lay-up carry, landing clear of its far edge. (A step that starts on the gap's far side, or
                // ends short of it, is not about it.)
                if (fromAlong < gap.x && toAlong > gap.x
                    && (Geo.Cm(toAlong) < Geo.Cm(gap.y + s.GapLandingMargin) || Geo.Cm(Vector2.Distance(from, to)) > layup))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsMasked(List<bool[]> masks, int k)
        {
            foreach (bool[] mask in masks)
            {
                if (mask[k])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
