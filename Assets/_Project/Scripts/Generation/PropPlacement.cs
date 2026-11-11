using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Places a generated course's props (M4 PR 4.3), after the hazards, from a random stream of its own, so props never move an
    /// island, a slope, the green, the pin or a hazard. Plain C#, deterministic: the same (seed, par, variant) and the same
    /// course always give the same props.
    ///
    /// How it works: <see cref="PropZones"/> measures, for any point, whether a prop of a given footprint may stand there: out of
    /// every shot line's corridor (its shadow too), every landing capsule, the tee, the green and its margin, every hazard and its
    /// margin, the void, and the +X end kept for M5. The island is then laid out as a grid of cluster starts with enough free
    /// ground around them. Each cluster draws, from a sub-stream of its own, one start (an even draw over the valid ones, the
    /// director's choice Q2 A), a theme from where the start lies (by a pond, by the edge, or inland) and its members around it,
    /// each tested against every keep-out and the props already placed. Every accept/reject compares whole centimetres; every
    /// discrete choice is an integer draw.
    /// </summary>
    public static class PropPlacement
    {
        // Salt of the props stream, next to the main stream and the surfaces stream (6).
        private const int PropsStream = 7;

        // The seed of a prop's mesh, drawn below this.
        private const int MeshSeeds = 1000000;

        // A wall is tested as circles this far apart along it.
        private const float WallSample = 0.25f;

        private enum Theme
        {
            OliveGrove,
            CypressLine,
            BushesAndRocks,
            Edge,
            PondRim,
        }

        /// <summary>A planned prop before it is built: what it is, where, and the circles it covers on the ground.</summary>
        private sealed class Planned
        {
            public PropKind Kind;
            public Vector2 Root;
            public Vector2 End;
            public float Scale;
            public int Seed;
            public int Cluster;
            public Vector3 Root3;
            public Vector3 End3;
            public PropShape[] Shapes;
            public List<Vector2> Circles = new List<Vector2>();
            public float Radius;
            public float Height;
        }

        /// <summary>The props of this course, or none if the settings have no prop kit or the course has no plan (hand-made).</summary>
        public static List<CourseProp> Place(CourseLayout layout, int seed, int par, int variant, LevelGenSettings settings)
        {
            var result = new List<CourseProp>();
            PropKitSettings kit = settings.PropKit;
            if (kit == null || layout.Plan == null)
            {
                return result;
            }

            PropRules rules = settings.Props;
            PropDensity density = rules.Density(par);
            var zones = new PropZones(layout, settings);
            var maker = new Maker(kit, new CourseFields(layout), zones);

            int propsSeed = unchecked((int)CourseNoise.Hash(unchecked((int)CourseNoise.Hash(seed, par, variant)), PropsStream, 0));
            var stream = new SeededRandom(propsSeed);
            int clusters = stream.Int(density.Clusters.x, density.Clusters.y);

            List<Vector2> starts = zones.ClusterStarts();
            var centres = new List<Vector2>();
            var placed = new List<Planned>();

            for (int k = 0; k < clusters; k++)
            {
                // One sub-stream per cluster: dropping a cluster or a prop never moves another cluster's draws.
                var sub = new SeededRandom(unchecked((int)CourseNoise.Hash(propsSeed, PropsStream, k + 1)));

                var free = new List<Vector2>();
                foreach (Vector2 start in starts)
                {
                    if (FarFromAll(start, centres, density.ClusterSpacing))
                    {
                        free.Add(start);
                    }
                }

                if (free.Count == 0)
                {
                    break;
                }

                // A cluster is two props at least; a lone prop is not a cluster. A start that can't hold two tries another, from
                // the same sub-stream, a few times before the cluster is given up and its ground stays free.
                for (int attempt = 0; attempt < rules.ClusterAttempts; attempt++)
                {
                    Vector2 centre = free[sub.Int(0, free.Count - 1)];
                    List<Planned> cluster = Cluster(sub, zones, maker, rules, density, centre, placed, k);
                    if (cluster.Count >= 2)
                    {
                        centres.Add(centre);
                        placed.AddRange(cluster);
                        break;
                    }
                }
            }

            // Each prop was built once, without colours, when it was tried: its shapes are fitted to that mesh, and the terrain
            // view builds the same mesh from the same seed, so they are the drawn prop's own.
            foreach (Planned prop in placed)
            {
                result.Add(new CourseProp(prop.Kind, prop.Root3, prop.End3, prop.Scale, prop.Seed, prop.Cluster, prop.Shapes));
            }

            return result;
        }

        /// <summary>One cluster around this start: its theme, from where the start lies, then its members.</summary>
        private static List<Planned> Cluster(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, PropDensity density, Vector2 centre, List<Planned> placed, int k)
        {
            int members = sub.Int(density.Members.x, density.Members.y);
            var cluster = new List<Planned>();
            switch (ChooseTheme(sub, zones, rules, centre))
            {
                case Theme.PondRim:
                    PondRim(sub, zones, maker, rules, centre, Mathf.Min(members, 4), placed, cluster, k);
                    break;
                case Theme.Edge:
                    EdgeCluster(sub, zones, maker, rules, centre, Mathf.Min(members, 4), placed, cluster, k);
                    break;
                case Theme.OliveGrove:
                    Around(sub, zones, maker, rules, centre, Mathf.Min(members, 5), placed, cluster, k, _ => sub.Chance(25) ? PropKind.Bush : PropKind.Olive, PropKind.Olive);
                    break;
                case Theme.CypressLine:
                    CypressLine(sub, zones, maker, rules, centre, Mathf.Min(members, 5), placed, cluster, k);
                    break;
                default:
                    Around(sub, zones, maker, rules, centre, members, placed, cluster, k, _ => sub.Chance(rules.BushShare) ? PropKind.Bush : PropKind.Rock, PropKind.Bush);
                    break;
            }

            return cluster;
        }

        private static Theme ChooseTheme(SeededRandom sub, PropZones zones, PropRules rules, Vector2 centre)
        {
            float pond = zones.NearestPond(centre, out _);
            if (Geo.Cm(pond) <= Geo.Cm(rules.PondNear) && sub.Chance(rules.RimThemeChance))
            {
                return Theme.PondRim;
            }

            if (Geo.Cm(zones.FromEdge(centre)) <= Geo.Cm(rules.EdgeBand) && Geo.Cm(pond) >= Geo.Cm(rules.EdgeFromPond) && sub.Chance(rules.EdgeThemeChance))
            {
                return Theme.Edge;
            }

            Vector3Int w = rules.InlandWeights;
            int pick = sub.Int(0, Mathf.Max(0, w.x + w.y + w.z - 1));
            if (pick < w.x)
            {
                return Theme.OliveGrove;
            }

            return pick < w.x + w.y ? Theme.CypressLine : Theme.BushesAndRocks;
        }

        /// <summary>Members scattered around the centre, the first tried right on it. <paramref name="kindOf"/> picks each member's kind after the first.</summary>
        private static void Around(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, Vector2 centre, int count,
            List<Planned> placed, List<Planned> cluster, int index, System.Func<int, PropKind> kindOf, PropKind first)
        {
            for (int m = 0; m < count; m++)
            {
                PropKind wanted = m == 0 ? first : kindOf(m);
                for (int t = 0; t < rules.MemberTries; t++)
                {
                    // A tree that finds no room in its first half of tries becomes a bush: the island is narrow between the
                    // corridors and the edge, and a cluster of greenery reads better than one of rocks.
                    PropKind kind = wanted == PropKind.Olive && t >= rules.MemberTries / 2 ? PropKind.Bush : wanted;
                    Vector2 at = m == 0 && t == 0 ? centre : centre + (Geo.Dir(sub.Int(0, 359)) * sub.Centimetres(new Vector2(0.5f, rules.ClusterRadius)));
                    Planned prop = maker.Single(sub, kind, at, ScaleFor(sub, rules, kind), index);
                    if (TryAccept(sub, zones, rules, prop, placed, cluster, false))
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>Two to five cypresses in a row, and a low wall running beside them.</summary>
        private static void CypressLine(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, Vector2 centre, int count,
            List<Planned> placed, List<Planned> cluster, int index)
        {
            // Along the course, turned a little: across it, a line would rarely fit between a corridor and the edge.
            Vector2 along = Geo.Rotate(zones.CourseDirection(centre), sub.Int(-rules.CypressLineTurn, rules.CypressLineTurn));
            float spacing = sub.Centimetres(rules.CypressSpacing);
            int trees = Mathf.Max(2, count - 1);
            for (int m = 0; m < trees; m++)
            {
                Vector2 at = centre + (along * ((m - ((trees - 1) * 0.5f)) * spacing));
                TryAccept(sub, zones, rules, maker.Single(sub, PropKind.Cypress, at, sub.Hundredths(rules.CypressScale), index), placed, cluster, false);
            }

            float length = sub.Centimetres(rules.WallLength);
            Vector2 side = Geo.Left(along) * (sub.Coin() ? rules.WallBesideLine : -rules.WallBesideLine);
            Vector2 middle = centre + side;
            TryAccept(sub, zones, rules, maker.Wall(sub, middle - (along * (length * 0.5f)), middle + (along * (length * 0.5f)), index), placed, cluster, false);
        }

        /// <summary>Rocks, or a short wall and a rock or two, along the island's edge.</summary>
        private static void EdgeCluster(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, Vector2 centre, int count,
            List<Planned> placed, List<Planned> cluster, int index)
        {
            int rocks = count;
            if (sub.Chance(rules.EdgeWallChance))
            {
                Vector2 along = zones.EdgeTangent(centre);
                float length = sub.Centimetres(rules.WallLength);
                TryAccept(sub, zones, rules, maker.Wall(sub, centre - (along * (length * 0.5f)), centre + (along * (length * 0.5f)), index), placed, cluster, false);
                rocks = Mathf.Max(1, count - 2);
            }

            Around(sub, zones, maker, rules, centre, rocks, placed, cluster, index, _ => PropKind.Rock, PropKind.Rock);
        }

        /// <summary>Round white rocks on a pond's rim, just outside its border (ART_DIRECTION §9).</summary>
        private static void PondRim(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, Vector2 centre, int count,
            List<Planned> placed, List<Planned> cluster, int index)
        {
            zones.NearestPond(centre, out HazardOutline pond);
            if (pond == null)
            {
                return;
            }

            int nearest = 0;
            for (int i = 1; i < HazardOutline.RingPoints; i++)
            {
                if (Vector2.Distance(pond.Ring[i], centre) < Vector2.Distance(pond.Ring[nearest], centre))
                {
                    nearest = i;
                }
            }

            for (int m = 0; m < count; m++)
            {
                for (int t = 0; t < rules.MemberTries; t++)
                {
                    int i = (nearest + sub.Int(-3, 3) + HazardOutline.RingPoints) % HazardOutline.RingPoints;
                    float size = sub.Centimetres(rules.RoundRockSize);
                    Vector2 outward = (pond.Ring[i] - pond.Centre).normalized;
                    Vector2 at = pond.Ring[i] + (outward * (sub.Centimetres(rules.RimGap) + (size * 0.5f)));
                    Planned prop = maker.Single(sub, PropKind.RoundRock, at, size, index, true);
                    if (TryAccept(sub, zones, rules, prop, placed, cluster, true))
                    {
                        break;
                    }
                }
            }
        }

        private static float ScaleFor(SeededRandom sub, PropRules rules, PropKind kind)
        {
            switch (kind)
            {
                case PropKind.Olive: return sub.Hundredths(rules.OliveScale);
                case PropKind.Cypress: return sub.Hundredths(rules.CypressScale);
                case PropKind.Bush: return sub.Hundredths(rules.BushScale);
                case PropKind.RoundRock: return sub.Centimetres(rules.RoundRockSize);
                default: return sub.Centimetres(rules.RockSize);
            }
        }

        /// <summary>
        /// Keeps the prop if every circle of its footprint fits the keep-outs and clears every prop already placed; then draws
        /// its mesh seed. A rim rock may stand by water, and must stand on a pond's rim.
        /// </summary>
        private static bool TryAccept(SeededRandom sub, PropZones zones, PropRules rules, Planned prop, List<Planned> placed, List<Planned> cluster, bool rim)
        {
            if (prop == null)
            {
                return false;
            }

            foreach (Vector2 circle in prop.Circles)
            {
                if (!zones.Fits(circle, prop.Radius, prop.Height, rim) || !Clear(circle, prop.Radius, placed, rules) || !Clear(circle, prop.Radius, cluster, rules))
                {
                    return false;
                }
            }

            if (rim && Geo.Cm(zones.NearestPond(prop.Root, out _)) > Geo.Cm(rules.RimGap.y + prop.Radius + 0.15f))
            {
                return false;
            }

            cluster.Add(prop);
            return true;
        }

        private static bool Clear(Vector2 p, float radius, List<Planned> props, PropRules rules)
        {
            foreach (Planned other in props)
            {
                foreach (Vector2 q in other.Circles)
                {
                    if (Geo.Cm(Vector2.Distance(p, q)) < Geo.Cm((radius + other.Radius) * rules.MemberSpacing))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool FarFromAll(Vector2 p, List<Vector2> others, float spacing)
        {
            foreach (Vector2 q in others)
            {
                if (Geo.Cm(Vector2.Distance(p, q)) < Geo.Cm(spacing))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds candidate props to measure them: each candidate draws its mesh seed and is built (without colours) on the ground,
        /// so the keep-outs test the collision shapes it will really have, not an estimate. The same seed builds the same mesh in
        /// the terrain view.
        /// </summary>
        private sealed class Maker
        {
            // No prop reaches less than this from its foot: the cheap test a candidate passes before it is built.
            private const float FootRadius = 0.05f;

            private readonly PropKitSettings _kit;
            private readonly CourseFields _fields;
            private readonly PropZones _zones;

            public Maker(PropKitSettings kit, CourseFields fields, PropZones zones)
            {
                _kit = kit;
                _fields = fields;
                _zones = zones;
            }

            public Planned Single(SeededRandom sub, PropKind kind, Vector2 at, float scale, int cluster, bool rim = false)
            {
                // Its foot alone first: building a tree costs about half a millisecond, and most tries fail here.
                return _zones.Fits(at, FootRadius, 0f, rim) ? Make(sub, kind, at, at, scale, cluster) : null;
            }

            public Planned Wall(SeededRandom sub, Vector2 from, Vector2 to, int cluster)
            {
                return _zones.Fits(from, FootRadius, 0f, false) && _zones.Fits(to, FootRadius, 0f, false) ? Make(sub, PropKind.Wall, from, to, 1f, cluster) : null;
            }

            private Planned Make(SeededRandom sub, PropKind kind, Vector2 from, Vector2 to, float scale, int cluster)
            {
                var prop = new Planned { Kind = kind, Root = from, End = to, Scale = scale, Cluster = cluster, Seed = sub.Int(0, MeshSeeds - 1) };
                prop.Root3 = new Vector3(from.x, _fields.Height(from.x, from.y), from.y);
                prop.End3 = kind == PropKind.Wall ? new Vector3(to.x, _fields.Height(to.x, to.y), to.y) : prop.Root3;
                prop.Shapes = new PropBuilder(_kit, null).Add(kind, prop.Root3, prop.End3, scale, prop.Seed);
                Reach(prop.Root3, prop.End3, prop.Shapes, out prop.Radius, out prop.Height);

                float length = Vector2.Distance(from, to);
                int samples = kind == PropKind.Wall ? Mathf.Max(1, Mathf.CeilToInt(length / WallSample)) : 0;
                for (int i = 0; i <= samples; i++)
                {
                    prop.Circles.Add(samples == 0 ? from : Vector2.Lerp(from, to, (float)i / samples));
                }

                return prop;
            }
        }

        /// <summary>
        /// How far a prop's collision shapes reach on the ground from its foot (from its line, for a wall), and how high they rise
        /// above it: the footprint the keep-outs test, and the height that sets its shadow.
        /// </summary>
        internal static void Reach(Vector3 root, Vector3 end, IReadOnlyList<PropShape> shapes, out float radius, out float height)
        {
            var from = new Vector2(root.x, root.z);
            var to = new Vector2(end.x, end.z);
            radius = 0f;
            height = 0f;
            foreach (PropShape shape in shapes)
            {
                var centre = new Vector2(shape.Center.x, shape.Center.z);
                float reach;
                if (shape.Kind == PropShapeKind.Box)
                {
                    var along = new Vector2(Mathf.Cos(shape.Yaw), Mathf.Sin(shape.Yaw));
                    Vector2 across = Geo.Left(along);
                    reach = 0f;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 corner = centre + (along * (shape.Extents.x * (i < 2 ? 1f : -1f))) + (across * (shape.Extents.z * (i % 2 == 0 ? 1f : -1f)));
                        reach = Mathf.Max(reach, Geo.DistanceToSegment(corner, from, to));
                    }
                }
                else
                {
                    reach = Geo.DistanceToSegment(centre, from, to) + Mathf.Max(shape.Extents.x, shape.Extents.z);
                }

                radius = Mathf.Max(radius, reach);
                height = Mathf.Max(height, shape.Center.y + shape.Extents.y - Mathf.Min(root.y, end.y));
            }
        }
    }

    /// <summary>
    /// Where props may stand on one course: the keep-out zones of the M4 plan (§2, Q4 A, hard), measured in metres and compared in
    /// whole centimetres. Shared by the placement and its tests.
    /// </summary>
    internal sealed class PropZones
    {
        // A hazard's or the green's distance is measured to its 24-point ring, whose chords cut inside the true, wobbling
        // outline by a few centimetres: the keep-outs around outlines add this much, so they hold against the true outline too.
        private const float RingSag = 0.1f;

        private readonly CourseLayout _layout;
        private readonly CourseFields _fields;
        private readonly PropRules _rules;
        private readonly float _corridor;
        private readonly float _capsuleHalfWidth;
        private readonly List<Vector2[]> _lines = new List<Vector2[]>();
        private readonly List<Vector2[]> _capsules = new List<Vector2[]>();
        private readonly List<HazardOutline> _bunkers = new List<HazardOutline>();
        private readonly List<HazardOutline> _ponds = new List<HazardOutline>();
        private readonly List<HazardOutline> _greens = new List<HazardOutline>();
        private readonly Vector2 _shadow;
        private readonly float _length;

        public PropZones(CourseLayout layout, LevelGenSettings settings)
        {
            _layout = layout;
            _fields = new CourseFields(layout);
            _rules = settings.Props;
            CoursePlan plan = layout.Plan;
            _corridor = plan != null ? plan.ShotCorridorHalfWidth : settings.ShotCorridorHalfWidth;
            _capsuleHalfWidth = settings.SafeLandingHalfWidth;
            _length = layout.Centreline.Length;

            if (plan != null)
            {
                foreach (ShotLine line in plan.Lines)
                {
                    _lines.Add(new[] { line.From, line.To });
                }

                // Each rest spot's landing capsule (the tee and the safe target on the green have none): the band along the
                // centreline that RouteWorld.CapsuleSamples samples, as a polyline with a half width. Its samples all lie
                // within that width of it, so the band holds the capsule.
                for (int i = 1; i + 1 < plan.SafeRoute.Count; i++)
                {
                    float along = layout.Centreline.Project(plan.SafeRoute[i], out _);
                    var points = new List<Vector2>();
                    for (float s = along - settings.SafeLandingBack; s <= along + settings.SafeLandingAhead + 0.001f; s += RouteWorld.Grid)
                    {
                        points.Add(layout.Centreline.PointAt(s));
                    }

                    _capsules.Add(points.ToArray());
                }
            }

            foreach (EllipseShape bunker in layout.Bunkers)
            {
                _bunkers.Add(HazardOutline.Of(bunker, false, HazardRole.Flank));
            }

            foreach (PondLayout pond in layout.Ponds)
            {
                _ponds.Add(HazardOutline.Of(pond.Outline, true, HazardRole.Flank));
            }

            foreach (GreenLayout green in layout.Greens)
            {
                _greens.Add(HazardOutline.Of(green.Outline, false, HazardRole.GreenGuard));
            }

            // Shadows turn with the course as the sun does (SunHeading.YawFor): a straight course reads as +X.
            Vector2 heading = layout.Centreline.Points.Count > 2 ? layout.Hole - layout.Tee : Vector2.right;
            _shadow = Geo.Dir(Geo.HeadingOf(heading) + _rules.ShadowHeading);
        }

        public IReadOnlyList<Vector2[]> Lines => _lines;
        public IReadOnlyList<Vector2[]> Capsules => _capsules;
        public float Corridor => _corridor;
        public float CapsuleHalfWidth => _capsuleHalfWidth;

        /// <summary>The direction shadows fall on the ground, unit length.</summary>
        public Vector2 Shadow => _shadow;

        /// <summary>The grid points where a cluster may start: in the rough, with <see cref="PropRules.ClusterStartRoom"/> of free ground around.</summary>
        public List<Vector2> ClusterStarts()
        {
            var starts = new List<Vector2>();
            float grid = _rules.StartGrid;
            Vector2 half = _layout.HalfExtent;
            int nx = Mathf.FloorToInt(2f * half.x / grid);
            int nz = Mathf.FloorToInt(2f * half.y / grid);
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    var p = new Vector2(-half.x + ((i + 0.5f) * grid), -half.y + ((j + 0.5f) * grid));
                    if (Fits(p, _rules.ClusterStartRoom, 0f, false))
                    {
                        starts.Add(p);
                    }
                }
            }

            return starts;
        }

        /// <summary>
        /// Whether a prop covering this circle of the ground (and, for its height, its shadow) may stand here: on rough, out of
        /// every keep-out. A rim rock (<paramref name="rim"/>) may come as close to a pond as <see cref="PropRules.RimGap"/>.
        /// </summary>
        public bool Fits(Vector2 p, float radius, float height, bool rim)
        {
            // On the island, gaps included; a crown may hang partly out over the edge.
            if (Geo.Cm(FromEdge(p)) < Geo.Cm(Mathf.Max(_rules.EdgeMargin, radius * (1f - _rules.CrownOverEdge))))
            {
                return false;
            }

            if (_fields.Surface(Geo.At(p)) != CourseSurface.Rough)
            {
                return false;
            }

            if (Geo.Cm(Vector2.Distance(p, _layout.Tee)) < Geo.Cm(_rules.TeeKeepOut + radius))
            {
                return false;
            }

            if (Geo.Cm(EndDistance(p)) < Geo.Cm(radius))
            {
                return false;
            }

            Vector2 shadowEnd = p + (_shadow * (height * _rules.ShadowLength));
            foreach (Vector2[] line in _lines)
            {
                // The prop and its shadow stay out of the corridor: the shadow test holds the foot's own, at its start.
                if (Geo.Cm(SegmentDistance(p, shadowEnd, line[0], line[1])) < Geo.Cm(_corridor + radius))
                {
                    return false;
                }
            }

            foreach (Vector2[] capsule in _capsules)
            {
                if (Geo.Cm(PolylineDistance(p, capsule)) < Geo.Cm(_capsuleHalfWidth + radius))
                {
                    return false;
                }
            }

            foreach (HazardOutline green in _greens)
            {
                if (Geo.Cm(green.SignedDistance(p, _rules.GreenKeepOut + radius + 1f)) < Geo.Cm(_rules.GreenKeepOut + radius + RingSag))
                {
                    return false;
                }
            }

            foreach (HazardOutline bunker in _bunkers)
            {
                if (Geo.Cm(bunker.SignedDistance(p, _rules.HazardKeepOut + radius + 1f)) < Geo.Cm(_rules.HazardKeepOut + radius + RingSag))
                {
                    return false;
                }
            }

            float pondMargin = rim ? _rules.RimGap.x : _rules.HazardKeepOut;
            foreach (HazardOutline pond in _ponds)
            {
                if (Geo.Cm(pond.SignedDistance(p, pondMargin + radius + 1f)) < Geo.Cm(pondMargin + radius + RingSag))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Metres from the island's edge, positive inside (the island field is a distance).</summary>
        public float FromEdge(Vector2 p) => -_fields.Island(Geo.At(p));

        /// <summary>The direction the course heads beside a point (its centreline's nearest leg).</summary>
        public Vector2 CourseDirection(Vector2 p) => Geo.PathDirection(_layout.Centreline, Mathf.Clamp(_layout.Centreline.Project(p, out _), 0f, _length));

        /// <summary>Metres to the nearest pond's border (negative inside it), and that pond; 100 m and null without one.</summary>
        public float NearestPond(Vector2 p, out HazardOutline nearest)
        {
            float best = 100f;
            nearest = null;
            foreach (HazardOutline pond in _ponds)
            {
                float d = pond.SignedDistance(p);
                if (d < best)
                {
                    best = d;
                    nearest = pond;
                }
            }

            return best;
        }

        /// <summary>The direction along the island's edge near a point, from the field's gradient.</summary>
        public Vector2 EdgeTangent(Vector2 p)
        {
            const float H = 0.25f;
            float dx = _fields.Island(Geo.At(p + new Vector2(H, 0f))) - _fields.Island(Geo.At(p - new Vector2(H, 0f)));
            float dz = _fields.Island(Geo.At(p + new Vector2(0f, H))) - _fields.Island(Geo.At(p - new Vector2(0f, H)));
            var gradient = new Vector2(dx, dz);
            return gradient.sqrMagnitude < 1e-8f ? Vector2.right : Geo.Left(gradient.normalized);
        }

        /// <summary>
        /// Metres from the +X end kept for M5, negative inside it: everything past the hole along the centreline (the island
        /// ends <see cref="LevelGenSettings.PastHole"/> past it, within <see cref="PropRules.EndPastHole"/>), and a fan of
        /// <see cref="PropRules.EndFan"/> around +X from the hole.
        /// </summary>
        public float EndDistance(Vector2 p)
        {
            float along = _layout.Centreline.Project(p, out _);
            float past = along - _length;
            float beyond = past > _rules.EndPastHole ? past - _rules.EndPastHole : -past;
            if (past >= 0f && past <= _rules.EndPastHole)
            {
                beyond = -Mathf.Min(past, _rules.EndPastHole - past);
            }

            Vector2 v = p - _layout.Hole;
            float half = _rules.EndFan * 0.5f;
            float fan;
            if (v.sqrMagnitude < 1e-8f || Vector2.Angle(Vector2.right, v) <= half)
            {
                fan = -1f;
            }
            else
            {
                fan = Mathf.Min(RayDistance(v, Geo.Dir(half)), RayDistance(v, Geo.Dir(-half)));
            }

            return Mathf.Min(beyond, fan);
        }

        private static float RayDistance(Vector2 v, Vector2 direction)
        {
            float t = Vector2.Dot(v, direction);
            return t <= 0f ? v.magnitude : Mathf.Abs((direction.x * v.y) - (direction.y * v.x));
        }

        internal static float PolylineDistance(Vector2 p, Vector2[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                best = Mathf.Min(best, Geo.DistanceToSegment(p, points[i], points[i + 1]));
            }

            return points.Length == 1 ? Vector2.Distance(p, points[0]) : best;
        }

        /// <summary>The shortest distance between two segments, 0 if they cross.</summary>
        internal static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (Cross(b - a, c - a) * Cross(b - a, d - a) < 0f && Cross(d - c, a - c) * Cross(d - c, b - c) < 0f)
            {
                return 0f;
            }

            return Mathf.Min(
                Mathf.Min(Geo.DistanceToSegment(a, c, d), Geo.DistanceToSegment(b, c, d)),
                Mathf.Min(Geo.DistanceToSegment(c, a, b), Geo.DistanceToSegment(d, a, b)));
        }

        private static float Cross(Vector2 u, Vector2 v) => (u.x * v.y) - (u.y * v.x);
    }
}
