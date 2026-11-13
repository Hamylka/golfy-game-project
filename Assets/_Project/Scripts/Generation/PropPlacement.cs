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

        // Cluster numbers and stream keys of the built props (PR 4.4), above the clusters' own 1..7. A built cluster is
        // BuiltBase + 1 + k; the others stand alone.
        internal const int BuiltBase = 100;
        internal const int BridgeBase = 130;
        internal const int RakeBase = 140;
        internal const int LandmarkBase = 150;

        // The fork spine's cluster (F3.4): its number and stream key, after every other, so no earlier prop moves.
        internal const int SpineBase = 160;

        // The strategic group (strategic obstacles S2): its number and stream key, after the spine's, placed last.
        internal const int StrategicBase = 170;

        // A spine member with no room at its slot tries the next one this far along.
        private const float SpineSlot = 0.25f;

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
            public float Yaw;
            public Vector3 Root3;
            public Vector3 End3;
            public PropShape[] Shapes;
            public List<Vector2> Circles = new List<Vector2>();
            public float Radius;
            public float Height;
            public bool Steep;
            public StrategicZone Zone;
        }

        /// <summary>A strategic zone of this course and the feet a member may stand on in it, in a fixed order.</summary>
        internal sealed class StrategicArea
        {
            public StrategicZone Zone;
            public float Margin;
            public readonly List<Vector2> Slots = new List<Vector2>();

            /// <summary>The line a block-draw group closes here (B3): the first line where a full block is allowed that a slot reaches, or −1.</summary>
            public int BlockLine = -1;

            /// <summary>The slots that reach <see cref="BlockLine"/>'s corridor, in grid order.</summary>
            public readonly List<Vector2> BlockSlots = new List<Vector2>();
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

            // The built props come after, each from sub-streams keyed above the clusters', so the clusters above stay exactly where
            // they were with or without them.
            PlaceBuilt(par, propsSeed, zones, maker, rules, density, starts, centres, placed);

            // Last, a fork's spine (F3.4): earlier props never read later ones, so this leaves every prop above bit for bit.
            Spine(new SeededRandom(Stream(propsSeed, SpineBase)), layout, settings, zones, maker, rules, placed);

            // Then one strategic group (strategic obstacles S2), placed last of all for the same reason.
            Strategic(new SeededRandom(Stream(propsSeed, StrategicBase)), layout, par, settings, zones, maker, rules, placed);

            // Each prop was built once, without colours, when it was tried: its shapes are fitted to that mesh, and the terrain
            // view builds the same mesh from the same seed, so they are the drawn prop's own.
            foreach (Planned prop in placed)
            {
                result.Add(new CourseProp(prop.Kind, prop.Root3, prop.End3, prop.Scale, prop.Seed, prop.Cluster, prop.Shapes, prop.Yaw, prop.Zone));
            }

            return result;
        }

        /// <summary>
        /// The built props of M4 PR 4.4, in this order, each from its own sub-stream: built clusters (a shed or a bench, with a trace and
        /// a little greenery), a bridge, a rake in a bunker, a landmark. Same keep-outs as every other prop; the checks play with them.
        /// </summary>
        private static void PlaceBuilt(int par, int propsSeed, PropZones zones, Maker maker, PropRules rules, PropDensity density,
            List<Vector2> starts, List<Vector2> centres, List<Planned> placed)
        {
            int traces = 0;
            Vector2Int range = rules.BuiltClusters(par);
            int clusters = new SeededRandom(Stream(propsSeed, BuiltBase)).Int(range.x, range.y);
            for (int k = 0; k < clusters; k++)
            {
                var sub = new SeededRandom(Stream(propsSeed, BuiltBase + 1 + k));
                BuiltCluster(sub, zones, maker, rules, density, starts, centres, placed, BuiltBase + 1 + k, ref traces);
            }

            Bridge(new SeededRandom(Stream(propsSeed, BridgeBase)), zones, maker, rules, placed);
            Rake(new SeededRandom(Stream(propsSeed, RakeBase)), zones, maker, rules, placed, ref traces);
            Landmark(new SeededRandom(Stream(propsSeed, LandmarkBase)), zones, maker, rules, placed);
        }

        private static int Stream(int propsSeed, int key) => unchecked((int)CourseNoise.Hash(propsSeed, PropsStream, key));

        /// <summary>A shed or a bench facing the course, then its trace and a little greenery. A start that can't hold it tries another.</summary>
        private static void BuiltCluster(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, PropDensity density,
            List<Vector2> starts, List<Vector2> centres, List<Planned> placed, int index, ref int traces)
        {
            var free = new List<Vector2>();
            foreach (Vector2 start in starts)
            {
                if (FarFromAll(start, centres, density.ClusterSpacing))
                {
                    free.Add(start);
                }
            }

            for (int attempt = 0; attempt < rules.ClusterAttempts && free.Count > 0; attempt++)
            {
                Vector2 centre = free[sub.Int(0, free.Count - 1)];
                bool shed = sub.Chance(rules.ShedShare);
                float scale = sub.Hundredths(rules.BuiltScale);
                int yaw = zones.HeadingToCourse(centre) - 90 + sub.Int(-15, 15);

                var cluster = new List<Planned>();
                Planned main = maker.Oriented(sub, shed ? PropKind.Shed : PropKind.Bench, centre, yaw, scale, index);
                if (!TryAccept(sub, zones, rules, main, placed, cluster, false))
                {
                    continue;
                }

                if (traces < rules.TraceMax && sub.Chance(rules.BuiltTraceChance))
                {
                    if (BuiltTrace(sub, zones, maker, rules, main, shed, yaw, scale, placed, cluster, index))
                    {
                        traces++;
                    }
                }

                int greenery = sub.Int(rules.BuiltGreenery.x, rules.BuiltGreenery.y);
                for (int m = 0; m < greenery; m++)
                {
                    for (int t = 0; t < rules.MemberTries; t++)
                    {
                        PropKind kind = sub.Chance(40) ? PropKind.Olive : PropKind.Bush;
                        Vector2 at = centre + (Geo.Dir(sub.Int(0, 359)) * sub.Centimetres(new Vector2(2f, 3.4f)));
                        if (TryAccept(sub, zones, rules, maker.Single(sub, kind, at, ScaleFor(sub, rules, kind), index), placed, cluster, false))
                        {
                            break;
                        }
                    }
                }

                centres.Add(centre);
                placed.AddRange(cluster);
                return;
            }
        }

        /// <summary>
        /// The trace of a shed (a bicycle on one gable wall, or laundry on a line behind it) or of a bench (a bucket of balls at its end).
        /// A trace stands against its built prop, so it only keeps clear of the other clusters' props.
        /// </summary>
        private const float BucketReach = 0.16f * 1.4f * 1.05f;
        private const float BucketGap = 0.3f;

        private static bool BuiltTrace(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, Planned main, bool shed, int yaw,
            float scale, List<Planned> placed, List<Planned> cluster, int index)
        {
            PropKitSettings kit = maker.Kit;
            Vector2 right = Geo.Dir(yaw);
            Vector2 forward = Geo.Left(right);
            Vector2 root = main.Root;
            float side = sub.Coin() ? 1f : -1f;

            if (!shed)
            {
                // Beside the bench's end with a clear gap: half the bench, the bucket's own reach (0.16 m x its 1.4 scale) and 0.3 m of ground.
                Vector2 at = root + (right * (side * ((kit.BenchLength * scale * 0.5f) + BucketReach + BucketGap))) + (forward * 0.1f);
                return TryAccept(sub, zones, rules, maker.Oriented(sub, PropKind.Bucket, at, 0, 1.4f, index), placed, cluster, false, false);
            }

            if (sub.Chance(rules.BicycleShare))
            {
                Vector2 at = root + (right * (side * ((kit.ShedLength * scale * 0.5f) + 0.3f)));
                return TryAccept(sub, zones, rules, maker.Oriented(sub, PropKind.Bicycle, at, yaw + 90, 1f, index), placed, cluster, false, false);
            }

            for (int back = 0; back < 2; back++)
            {
                float behind = back == 0 ? -1f : 1f;
                Vector2 at = root + (forward * (behind * ((kit.ShedWidth * scale * 0.5f) + 1.4f)));
                if (TryAccept(sub, zones, rules, maker.Oriented(sub, PropKind.Laundry, at, yaw, 1f, index), placed, cluster, false, false))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// At most one bridge, on about one pond in three: only a pond that no shot line's corridor and no landing capsule passes over,
        /// across its narrowest chord, border to border plus a bank each side. It never changes the pond's risk or the safe route:
        /// every point of the span keeps clear of the corridors, the capsules and the other keep-outs.
        /// </summary>
        private static void Bridge(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, List<Planned> placed)
        {
            foreach (HazardOutline pond in zones.Ponds)
            {
                if (!sub.Chance(rules.BridgeChance))
                {
                    continue;
                }

                int best = 0;
                int bestCm = int.MaxValue;
                for (int j = 0; j < HazardOutline.RingPoints / 2; j++)
                {
                    int span = Geo.Cm(Vector2.Distance(pond.Ring[j], pond.Ring[j + (HazardOutline.RingPoints / 2)]));
                    if (span < bestCm)
                    {
                        bestCm = span;
                        best = j;
                    }
                }

                if (bestCm > Geo.Cm(rules.BridgeMaxSpan))
                {
                    continue;
                }

                Vector2 a = pond.Ring[best];
                Vector2 b = pond.Ring[best + (HazardOutline.RingPoints / 2)];
                Vector2 along = (b - a).normalized;
                Planned bridge = maker.Line(sub, PropKind.Bridge, a - (along * rules.BridgeBank), b + (along * rules.BridgeBank), 1f, BridgeBase);
                var cluster = new List<Planned>();
                if (zones.FitsBridge(bridge.Circles, bridge.Radius, bridge.Height, rules) && Clears(bridge, placed, rules))
                {
                    cluster.Add(bridge);
                    placed.AddRange(cluster);
                    return;
                }
            }
        }

        private static bool Clears(Planned prop, List<Planned> placed, PropRules rules)
        {
            foreach (Vector2 circle in prop.Circles)
            {
                if (!Clear(circle, prop.Radius, placed, rules))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A rake lying in a bunker on about half the courses that have one: at the sand's rim farthest from the shot lines. Flat, so it never collides.</summary>
        private static void Rake(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, List<Planned> placed, ref int traces)
        {
            if (traces >= rules.TraceMax || zones.Bunkers.Count == 0 || !sub.Chance(rules.RakeChance))
            {
                return;
            }

            HazardOutline bunker = zones.Bunkers[sub.Int(0, zones.Bunkers.Count - 1)];
            int turn = sub.Int(-25, 25);
            if (zones.RakeSpot(bunker, turn, out Vector2 spot, out int yaw))
            {
                placed.Add(maker.Oriented(sub, PropKind.Rake, spot, yaw, 1f, RakeBase));
                traces++;
            }
        }

        /// <summary>A chapel (door toward the course) or a lighthouse on a promontory of the island's edge, away from the play axis, on about half the courses.</summary>
        private static void Landmark(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, List<Planned> placed)
        {
            if (!sub.Chance(rules.LandmarkChance))
            {
                return;
            }

            PropKitSettings kit = maker.Kit;
            bool lighthouse = sub.Chance(rules.LighthouseShare);
            float scale = sub.Hundredths(rules.BuiltScale);
            float radius = lighthouse
                ? kit.LighthouseRadius * scale * 1.1f
                : 0.5f * Mathf.Sqrt((kit.ChapelLength * kit.ChapelLength) + (kit.ChapelWidth * kit.ChapelWidth)) * scale * 1.1f;
            float height = lighthouse ? (kit.LighthouseHeight + 1.3f) * scale : (kit.ChapelTowerHeight + 0.9f) * scale;

            List<Vector2> candidates = zones.LandmarkStarts(radius, height);
            for (int attempt = 0; attempt < rules.ClusterAttempts && candidates.Count > 0; attempt++)
            {
                Vector2 at = candidates[sub.Int(0, candidates.Count - 1)];
                int yaw = lighthouse ? sub.Int(0, 44) : zones.HeadingToCourse(at);
                var cluster = new List<Planned>();
                Planned landmark = maker.Oriented(sub, lighthouse ? PropKind.Lighthouse : PropKind.Chapel, at, yaw, scale, LandmarkBase);
                if (TryAccept(sub, zones, rules, landmark, placed, cluster, false))
                {
                    placed.AddRange(cluster);
                    return;
                }
            }
        }

        /// <summary>
        /// F3.4 (split fairway plan §4): a small cluster on a fork's spine, the rough between its two branches, from its own sub-stream
        /// keyed after every other (so every other prop stays bit for bit where it was), extra to the density's clusters. A short
        /// cypress line along the spine, or bushes and rocks led by an olive where one fits; a cypress or an olive with no room
        /// becomes a bush. Each member stands in the middle of the spine's free strip at its slot: the run of across positions on
        /// rough and clear of play for its true reach and shadow; it is then accepted only if it passes the whole of
        /// <see cref="PropZones.Fits"/>, so every keep-out (corridors and shadows, capsules, the fairway margin off both branches)
        /// applies unchanged. A spine that holds fewer than two props stays bare.
        /// </summary>
        private static void Spine(SeededRandom sub, CourseLayout layout, LevelGenSettings settings, PropZones zones, Maker maker, PropRules rules,
            List<Planned> placed)
        {
            FairwayShape? found = null;
            foreach (FairwayShape shape in layout.Plan.FairwayShapes)
            {
                if (shape.Kind == FairwayShapeKind.Fork)
                {
                    found = shape;
                }
            }

            if (found == null || !sub.Chance(rules.SpineChance))
            {
                return;
            }

            // The held stretch, where both branches run at their full offset and the spine at its full width.
            FairwayShape fork = found.Value;
            float ease = settings.FairwayShapes.ForkEase;
            float from = fork.From + ease;
            float to = fork.To - ease;
            float middle = 0.5f * (fork.SafeOffset + fork.RiskyOffset);
            float half = 0.5f * Mathf.Abs(fork.SafeOffset - fork.RiskyOffset);

            Vector2Int w = rules.SpineWeights;
            bool cypresses = sub.Int(0, Mathf.Max(0, w.x + w.y - 1)) < w.x;
            int members = sub.Int(rules.SpineMembers.x, rules.SpineMembers.y);
            float gap = cypresses ? sub.Centimetres(rules.CypressSpacing) : sub.Centimetres(rules.SpineGap);
            while (members > 2 && Geo.Cm((members - 1) * gap) > Geo.Cm(to - from))
            {
                members--;
            }

            int slack = Geo.Cm(to - from) - Geo.Cm((members - 1) * gap);
            if (slack < 0)
            {
                return;
            }

            // Each member is tried at its slot, then a step further along at a time (the spine is wider in some places than others),
            // up to MemberTries slots; the next member's slot is a gap past the last one placed. A cypress with no room (its long shadow
            // reaches a corridor on most spines) becomes a bush, and the line carries on as bushes, so it costs one member's tries.
            float s = from + (sub.Int(0, slack) / 100f);
            var cluster = new List<Planned>();
            for (int m = 0; m < members && Geo.Cm(s) <= Geo.Cm(to); m++)
            {
                PropKind wanted = cypresses ? PropKind.Cypress : (m == 0 ? PropKind.Olive : (sub.Chance(rules.BushShare) ? PropKind.Bush : PropKind.Rock));
                for (int t = 0; t < rules.MemberTries && Geo.Cm(s) <= Geo.Cm(to); t++)
                {
                    bool placedHere = SpineMember(sub, layout.Centreline, zones, maker, rules, s, middle, half, wanted, placed, cluster);
                    if (!placedHere && (wanted == PropKind.Cypress || wanted == PropKind.Olive))
                    {
                        cypresses = false;
                        wanted = PropKind.Bush;
                        placedHere = SpineMember(sub, layout.Centreline, zones, maker, rules, s, middle, half, wanted, placed, cluster);
                    }

                    if (placedHere)
                    {
                        break;
                    }

                    s += SpineSlot;
                }

                s += gap;
            }

            if (cluster.Count >= 2)
            {
                placed.AddRange(cluster);
            }
        }

        /// <summary>
        /// One spine member at this slot along the centreline: built once in the middle of the strip where its foot fits, to learn its
        /// true reach and height; then moved, same mesh seed, to the middle of the strip where that reach and its shadow fit.
        /// </summary>
        private static bool SpineMember(SeededRandom sub, CoursePath centreline, PropZones zones, Maker maker, PropRules rules, float along,
            float middle, float half, PropKind kind, List<Planned> placed, List<Planned> cluster)
        {
            Vector2 centre = centreline.PointAt(along);
            Vector2 side = Geo.Left(Geo.PathDirection(centreline, along));
            float step = rules.SpineAcrossStep;
            int reach = Mathf.FloorToInt((half / step) + 0.001f);

            if (!StripMiddle(zones, centre, side, middle, step, reach, Maker.FootRadius, 0f, ClearRule.Strict, out Vector2 foot))
            {
                return false;
            }

            // A cypress's long shadow may cross the direct and cut corridors on a spine, never a safe leg's (director Q7 B).
            ClearRule rule = kind == PropKind.Cypress ? ClearRule.SpineCypress : ClearRule.Strict;
            Planned probe = maker.Single(sub, kind, foot, ScaleFor(sub, rules, kind), SpineBase);
            if (probe == null || probe.Steep || !StripMiddle(zones, centre, side, middle, step, reach, probe.Radius, probe.Height, rule, out Vector2 at))
            {
                return false;
            }

            Planned prop = maker.Seeded(kind, at, probe.Scale, SpineBase, probe.Seed);
            return TryAccept(sub, zones, rules, prop, placed, cluster, false, true, rule);
        }

        /// <summary>
        /// The middle of the longest run of across positions (whole steps from the spine's middle, out to the branch centres) where a
        /// prop of this reach and height fits; the first such run on a tie. False if none fits.
        /// </summary>
        private static bool StripMiddle(PropZones zones, Vector2 centre, Vector2 side, float middle, float step, int reach, float radius, float height,
            ClearRule rule, out Vector2 at)
        {
            int bestFrom = 0, bestLength = 0, runFrom = 0, run = 0;
            for (int j = -reach; j <= reach + 1; j++)
            {
                bool fits = j <= reach && zones.RoughAndClearOfPlay(centre + (side * (middle + (j * step))), radius, height, rule);
                if (fits)
                {
                    if (run == 0)
                    {
                        runFrom = j;
                    }

                    run++;
                }
                else
                {
                    if (run > bestLength)
                    {
                        bestFrom = runFrom;
                        bestLength = run;
                    }

                    run = 0;
                }
            }

            // The run's middle in whole steps: an even run takes the step on the lower side of its centre.
            int j0 = bestFrom + ((bestLength - 1) / 2);
            at = centre + (side * (middle + (j0 * step)));
            return bestLength > 0;
        }

        /// <summary>
        /// Strategic obstacles S2 (plan §1-§2): one group of 1-3 props on the direct or cut line, where the course has a strategic
        /// zone (a fork's spine, a split's band, a rough island's oval, a dogleg's cut corner; a par 3 only on a rough island), on
        /// <see cref="PropRules.StrategicChance"/> of courses. Every draw comes from this sub-stream, keyed after the spine's and made
        /// after every other prop, so no other prop moves. The draws come in a fixed order: the chance, the block draw, the zone
        /// (even over the zones that can close a block line on a won block draw, B3, else over every zone), the member count, then
        /// each member's kind, scale and slot. Members are tested by the
        /// strategic rule (<see cref="PropZones.FitsStrategic(Vector2, float, float, float)"/>: the direct and cut corridors are open,
        /// a safe leg only under its planned arc), clear the props already placed, and keep each line they reach playable
        /// (<see cref="PropZones.Playable"/>) unless a full block is allowed there (<see cref="PropZones.FullBlockAllowed"/>) and the
        /// group's block draw won. The first member must reach into a direct or cut corridor, so a group is always on a line.
        /// </summary>
        private static void Strategic(SeededRandom sub, CourseLayout layout, int par, LevelGenSettings settings, PropZones zones, Maker maker, PropRules rules,
            List<Planned> placed)
        {
            // Tall obstacles first (director Q5 B: a course with a block line always tries a block). It draws nothing when no spot
            // can close a line, so such a course goes on below exactly as in S2.
            if (Tall(sub, par, zones, maker, rules, placed))
            {
                return;
            }

            if (!sub.Chance(rules.StrategicChance))
            {
                return;
            }

            List<StrategicArea> areas = StrategicAreas(layout, par, settings, zones, rules);
            if (areas.Count == 0)
            {
                return;
            }

            // Director: "B3 à 100%". The block draw comes first (always drawn, whatever its chance), so that on a win the zone is
            // drawn over the zones that can close a line where a full block is allowed; with none, over every zone as before.
            bool mayBlock = sub.Chance(rules.StrategicFullBlockChance);
            List<StrategicArea> blockAreas = mayBlock ? BlockAreas(areas) : new List<StrategicArea>();
            bool block = blockAreas.Count > 0;
            StrategicArea area = block ? blockAreas[sub.Int(0, blockAreas.Count - 1)] : areas[sub.Int(0, areas.Count - 1)];
            int members = sub.Int(rules.StrategicMembers.x, rules.StrategicMembers.y);

            List<Planned> group = block ? StrategicBlock(sub, par, zones, maker, rules, area, members, placed) : null;
            if (group == null)
            {
                // The normal rule, or, when no tall member fits on the block line, a group by the normal rule in the same zone (hub:
                // never no group), from the same sub-stream onward.
                group = new List<Planned>();
                for (int m = 0; m < members; m++)
                {
                    if (!StrategicMember(sub, par, zones, maker, rules, area, m, mayBlock && !block, -1, placed, group))
                    {
                        break;
                    }
                }
            }

            placed.AddRange(group);
        }

        /// <summary>The tall obstacles, in the order of <see cref="PropRules.TallWeights"/>.</summary>
        internal static readonly PropKind[] TallKinds = { PropKind.UmbrellaPine, PropKind.CypressClump, PropKind.Dovecote };

        /// <summary>
        /// A tall obstacle that closes a line where a full block is allowed (tall obstacles, director Q1 B, Q2 A, Q4 A, Q5 B, Q6 A):
        /// every spot that would close one is found first, with no draw (<see cref="TallSpots"/>); with none, nothing is drawn and the
        /// course gets its S2 group. Else the kind is drawn by <see cref="PropRules.TallWeights"/> over the kinds that have a spot,
        /// then an even draw over that kind's spots, then its mesh seed. It stands alone: S2's 1.2-2.0 m member gap lies inside its
        /// own reach.
        /// </summary>
        private static bool Tall(SeededRandom sub, int par, PropZones zones, Maker maker, PropRules rules, List<Planned> placed)
        {
            List<Vector2>[] spots = TallSpots(par, zones, maker, rules, placed);
            Vector3Int w = rules.TallWeights;
            int[] weights = { w.x, w.y, w.z };
            int total = 0;
            for (int k = 0; k < TallKinds.Length; k++)
            {
                weights[k] = spots[k].Count > 0 ? Mathf.Max(0, weights[k]) : 0;
                total += weights[k];
            }

            if (total == 0)
            {
                return false;
            }

            int pick = sub.Int(0, total - 1);
            int kind = 0;
            while (pick >= weights[kind])
            {
                pick -= weights[kind];
                kind++;
            }

            Vector2 at = spots[kind][sub.Int(0, spots[kind].Count - 1)];
            Planned prop = maker.Free(sub, TallKinds[kind], at, at, 1f, StrategicBase);
            prop.Zone = StrategicZone.BlockLine;
            placed.Add(prop);
            return true;
        }

        /// <summary>
        /// For each of <see cref="TallKinds"/>, every point of a <see cref="PropRules.TallSlotGrid"/> grid along each line where a
        /// full block is allowed (in line order) where that obstacle may stand and closes the line. A tall obstacle's shapes come
        /// from the kit's settings alone, so its footprint is known before any draw. It may stand where:
        /// <list type="bullet">
        /// <item>its base (the pine's trunk, the clump's trunks, the whole dovecote) passes the strategic rule with the fairway margin,
        /// on rough (director Q1 B: the crown alone may overhang the fairway);</item>
        /// <item>its whole reach (the crown) stays out of every keep-out of play, and its shadow out of the safe legs and the landing
        /// capsules (Q6 A);</item>
        /// <item>it stands clear of every prop placed;</item>
        /// <item>it makes the line unplayable by the S2 block rule (<see cref="PropZones.Playable"/>), and leaves every other direct or
        /// cut line it reaches playable, unless a full block is allowed there too;</item>
        /// <item>a dovecote stands on level ground, as every built prop.</item>
        /// </list>
        /// </summary>
        internal static List<Vector2>[] TallSpotsFor(CourseLayout layout, int par, LevelGenSettings settings)
        {
            var zones = new PropZones(layout, settings);
            return TallSpots(par, zones, new Maker(settings.PropKit, new CourseFields(layout), zones), settings.Props, new List<Planned>());
        }

        private static List<Vector2>[] TallSpots(int par, PropZones zones, Maker maker, PropRules rules, List<Planned> placed)
        {
            var spots = new List<Vector2>[TallKinds.Length];
            for (int k = 0; k < TallKinds.Length; k++)
            {
                spots[k] = new List<Vector2>();
            }

            var lines = new List<int>();
            for (int i = 0; i < zones.Lines.Count; i++)
            {
                if (zones.IsStrategicLine(i) && zones.FullBlockAllowed(i, par))
                {
                    lines.Add(i);
                }
            }

            if (lines.Count == 0)
            {
                return spots;
            }

            float g = rules.TallSlotGrid;
            for (int k = 0; k < TallKinds.Length; k++)
            {
                PropKind kind = TallKinds[k];
                maker.TallFootprint(kind, out float reach, out float height);
                float baseReach = PropBuilder.TallBase(kind, maker.Kit);
                bool whole = baseReach < 0f || Geo.Cm(baseReach) >= Geo.Cm(reach);
                foreach (int i in lines)
                {
                    Vector2 a = zones.Lines[i][0];
                    Vector2 b = zones.Lines[i][1];
                    float length = Vector2.Distance(a, b);
                    Vector2 dir = length > 1e-6f ? (b - a) / length : Vector2.right;
                    Vector2 left = Geo.Left(dir);
                    int na = Mathf.FloorToInt(length / g);
                    int nc = Mathf.FloorToInt((zones.Corridor + reach) / g);
                    for (int u = 0; u <= na; u++)
                    {
                        for (int v = -nc; v <= nc; v++)
                        {
                            Vector2 p = a + (dir * (u * g)) + (left * (v * g));
                            if (TallFits(p, i, par, kind, reach, whole ? reach : baseReach, height, zones, maker, rules, placed))
                            {
                                spots[k].Add(p);
                            }
                        }
                    }
                }
            }

            return spots;
        }

        private static bool TallFits(Vector2 p, int line, int par, PropKind kind, float reach, float baseReach, float height, PropZones zones, Maker maker,
            PropRules rules, List<Planned> placed)
        {
            // The cheap tests first: on rough, and wide enough here to close the corridor (geometry alone).
            if (!zones.OnRough(p) || !zones.Covers(line, p, reach))
            {
                return false;
            }

            if (!zones.FitsStrategic(p, baseReach, height, rules.FairwayMargin))
            {
                return false;
            }

            if (Geo.Cm(baseReach) < Geo.Cm(reach) && !zones.StrategicClearOfPlay(p, reach, height))
            {
                return false;
            }

            if (!zones.ShadowClearOfCapsules(p, reach, height) || !Clear(p, reach, placed, rules))
            {
                return false;
            }

            var feet = new[] { new StrategicFoot(p, reach, zones.GroundHeight(p) + height) };
            if (zones.Playable(line, feet))
            {
                return false;
            }

            for (int j = 0; j < zones.Lines.Count; j++)
            {
                if (j != line && zones.IsStrategicLine(j) && zones.Intrudes(j, p, reach) && !zones.Playable(j, feet) && !zones.FullBlockAllowed(j, par))
                {
                    return false;
                }
            }

            return kind != PropKind.Dovecote || !maker.SteepAt(p, zones.GroundHeight(p), reach);
        }

        /// <summary>
        /// A block-draw group (B3, then director "Pour l'instant A"): tall members only (a wall or a shed first on
        /// <see cref="PropRules.StrategicBuiltChance"/>, else an olive or a cypress), each standing in <paramref name="area"/>'s block
        /// line's corridor, up to the drawn member count. They may close the line where a full block is allowed, but they need not:
        /// on today's kit a full-power arc clears a 2-3 m prop everywhere but next to the ball and the landing, which are keep-outs, so
        /// these are tall obstacles to fly over. Null if no tall member fits: the caller then places a group by the normal rule in
        /// the same zone.
        /// </summary>
        private static List<Planned> StrategicBlock(SeededRandom sub, int par, PropZones zones, Maker maker, PropRules rules, StrategicArea area,
            int members, List<Planned> placed)
        {
            var group = new List<Planned>();
            for (int m = 0; m < members; m++)
            {
                if (!StrategicMember(sub, par, zones, maker, rules, area, m, true, area.BlockLine, placed, group))
                {
                    break;
                }
            }

            return group.Count > 0 ? group : null;
        }

        /// <summary>
        /// The zones a block-draw group may go to (B3): not a rough island (small props only, director Q2 B), and with a slot in the
        /// corridor of a line where a full block is allowed (<see cref="PropZones.FullBlockAllowed"/>, set on each area by
        /// <see cref="StrategicAreas"/>).
        /// </summary>
        internal static List<StrategicArea> BlockAreas(List<StrategicArea> areas)
        {
            var result = new List<StrategicArea>();
            foreach (StrategicArea area in areas)
            {
                if (area.Zone != StrategicZone.RoughIsland && area.BlockLine >= 0)
                {
                    result.Add(area);
                }
            }

            return result;
        }

        /// <summary>
        /// This course's strategic zones, in a fixed order (spine, split bands, rough islands, cut corners), each with its slots: the
        /// points of a grid laid along the zone, <see cref="PropRules.StrategicSlotGrid"/> apart, where a foot passes the strategic
        /// rule and lies within a direct or cut corridor plus <see cref="PropRules.StrategicSlotReach"/>. Zones with no slot are left out.
        /// </summary>
        internal static List<StrategicArea> StrategicAreas(CourseLayout layout, int par, LevelGenSettings settings, PropZones zones, PropRules rules)
        {
            var areas = new List<StrategicArea>();
            CoursePath path = layout.Centreline;
            float g = rules.StrategicSlotGrid;

            foreach (FairwayShape shape in layout.Plan.FairwayShapes)
            {
                if (par == 3 || shape.Kind != FairwayShapeKind.Fork)
                {
                    continue;
                }

                // The held stretch, between the branch centres (as the spine theme's strip).
                float ease = settings.FairwayShapes.ForkEase;
                float lo = Mathf.Min(shape.SafeOffset, shape.RiskyOffset);
                float hi = Mathf.Max(shape.SafeOffset, shape.RiskyOffset);
                var area = new StrategicArea { Zone = StrategicZone.Spine, Margin = rules.FairwayMargin };
                AlongGrid(path, shape.From + ease, shape.To - ease, lo, hi, g, p => true, zones, rules, area);
                Add(areas, area, zones, par);
            }

            foreach (FairwayShape shape in layout.Plan.FairwayShapes)
            {
                if (par == 3 || shape.Kind != FairwayShapeKind.Split)
                {
                    continue;
                }

                // The band across the strip; the fairway margin keeps members off the fairway ahead of and behind it.
                float half = layout.Fairway.HalfWidth;
                var area = new StrategicArea { Zone = StrategicZone.Split, Margin = rules.FairwayMargin };
                AlongGrid(path, shape.From, shape.To, -half, half, g, p => true, zones, rules, area);
                Add(areas, area, zones, par);
            }

            foreach (EllipseShape oval in layout.Fairway.RoughOvals)
            {
                // Inside the oval, with the oval's own, narrower fairway margin (director Q2 B).
                HazardOutline outline = HazardOutline.Of(oval, false, HazardRole.Flank);
                float along = path.Project(oval.Center, out _);
                float reach = Mathf.Max(oval.Radius.x, oval.Radius.y) * 1.5f;
                var area = new StrategicArea { Zone = StrategicZone.RoughIsland, Margin = rules.StrategicOvalMargin };
                AlongGrid(path, along - reach, along + reach, -reach, reach, g, p => outline.SignedDistance(p, 1f) < 0f, zones, rules, area);
                Add(areas, area, zones, par);
            }

            for (int i = 0; i < zones.Lines.Count && par != 3; i++)
            {
                if (zones.Kinds[i] != ShotLineKind.Cut)
                {
                    continue;
                }

                // Along the cut line, across its corridor and a slot's reach: the corner's inside, beside its carry hazard (whose
                // keep-out is unchanged), never on it.
                Vector2 a = zones.Lines[i][0];
                Vector2 b = zones.Lines[i][1];
                float length = Vector2.Distance(a, b);
                Vector2 dir = length > 1e-6f ? (b - a) / length : Vector2.right;
                Vector2 left = Geo.Left(dir);
                float across = zones.Corridor + rules.StrategicSlotReach;
                int na = Mathf.FloorToInt(length / g);
                int nc = Mathf.FloorToInt(across / g);
                var area = new StrategicArea { Zone = StrategicZone.CutCorner, Margin = rules.FairwayMargin };
                for (int u = 0; u <= na; u++)
                {
                    for (int v = -nc; v <= nc; v++)
                    {
                        TrySlot(a + (dir * (u * g)) + (left * (v * g)), zones, rules, area);
                    }
                }

                Add(areas, area, zones, par);
            }

            return areas;
        }

        private static void Add(List<StrategicArea> areas, StrategicArea area, PropZones zones, int par)
        {
            if (area.Slots.Count == 0)
            {
                return;
            }

            areas.Add(area);

            // B3: the first line, in line order, where a full block is allowed and a slot's foot reaches the corridor.
            for (int i = 0; i < zones.Lines.Count && area.BlockLine < 0; i++)
            {
                if (!zones.IsStrategicLine(i) || !zones.FullBlockAllowed(i, par))
                {
                    continue;
                }

                foreach (Vector2 slot in area.Slots)
                {
                    if (zones.Intrudes(i, slot, Maker.FootRadius))
                    {
                        area.BlockSlots.Add(slot);
                    }
                }

                if (area.BlockSlots.Count > 0)
                {
                    area.BlockLine = i;
                }
            }
        }

        /// <summary>The slots of a grid laid along the centreline: whole steps from <paramref name="from"/>, and across from <paramref name="lo"/> to <paramref name="hi"/>.</summary>
        private static void AlongGrid(CoursePath path, float from, float to, float lo, float hi, float g, System.Func<Vector2, bool> inside, PropZones zones, PropRules rules,
            StrategicArea area)
        {
            int na = Mathf.FloorToInt(((to - from) / g) + 0.001f);
            int c0 = Mathf.CeilToInt((lo / g) - 0.001f);
            int c1 = Mathf.FloorToInt((hi / g) + 0.001f);
            for (int u = 0; u <= na; u++)
            {
                float s = from + (u * g);
                Vector2 centre = path.PointAt(s);
                Vector2 left = Geo.Left(Geo.PathDirection(path, s));
                for (int v = c0; v <= c1; v++)
                {
                    Vector2 p = centre + (left * (v * g));
                    if (inside(p))
                    {
                        TrySlot(p, zones, rules, area);
                    }
                }
            }
        }

        private static void TrySlot(Vector2 p, PropZones zones, PropRules rules, StrategicArea area)
        {
            bool near = false;
            for (int i = 0; i < zones.Lines.Count && !near; i++)
            {
                near = zones.IsStrategicLine(i) && zones.Intrudes(i, p, rules.StrategicSlotReach);
            }

            if (near && zones.FitsStrategic(p, Maker.FootRadius, 0f, area.Margin))
            {
                area.Slots.Add(p);
            }
        }

        /// <summary>
        /// One member of the strategic group: its kind (a rock or a low bush on a rough island; else a short wall or a shed for the
        /// first member on <see cref="PropRules.StrategicBuiltChance"/>, or a tree, a bush or a rock by the kind weights), then up to
        /// <see cref="PropRules.MemberTries"/> slots: the first member's from an even draw onward, a later member's each an even draw
        /// over the slots <see cref="PropRules.StrategicGap"/> from the member before. A tree or a built prop with no room becomes a
        /// bush, a bush on a rough island a rock. False if no slot holds it.
        /// With a <paramref name="target"/> line (a block-draw group, B3): tall kinds only (a built prop, else an olive or a cypress
        /// by <see cref="PropRules.StrategicTreeWeights"/>; a built prop with no room becomes an olive), on the slots that reach the
        /// target's corridor, each try an even draw, and the member must reach into the target's corridor.
        /// </summary>
        private static bool StrategicMember(SeededRandom sub, int par, PropZones zones, Maker maker, PropRules rules, StrategicArea area, int m, bool mayBlock,
            int target, List<Planned> placed, List<Planned> group)
        {
            bool island = area.Zone == StrategicZone.RoughIsland;
            bool tall = target >= 0;
            PropKind kind;
            if (island)
            {
                kind = sub.Chance(rules.BushShare) ? PropKind.Bush : PropKind.Rock;
            }
            else if (m == 0 && sub.Chance(rules.StrategicBuiltChance))
            {
                bool shedHere = area.Zone == StrategicZone.Spine || area.Zone == StrategicZone.CutCorner;
                kind = shedHere && sub.Coin() ? PropKind.Shed : PropKind.Wall;
            }
            else if (tall)
            {
                Vector2Int t = rules.StrategicTreeWeights;
                kind = sub.Int(0, Mathf.Max(0, t.x + t.y - 1)) < t.x ? PropKind.Olive : PropKind.Cypress;
            }
            else
            {
                Vector2Int t = rules.StrategicTreeWeights;
                Vector2Int l = rules.StrategicLowWeights;
                int pick = sub.Int(0, Mathf.Max(0, t.x + t.y + l.x + l.y - 1));
                kind = pick < t.x ? PropKind.Olive : pick < t.x + t.y ? PropKind.Cypress : pick < t.x + t.y + l.x ? PropKind.Bush : PropKind.Rock;
            }

            List<Vector2> pool = tall ? area.BlockSlots : area.Slots;
            List<Vector2> slots = pool;
            if (m > 0)
            {
                slots = new List<Vector2>();
                Vector2 last = group[group.Count - 1].Root;
                foreach (Vector2 slot in pool)
                {
                    int d = Geo.Cm(Vector2.Distance(slot, last));
                    if (d >= Geo.Cm(rules.StrategicGap.x) && d <= Geo.Cm(rules.StrategicGap.y))
                    {
                        slots.Add(slot);
                    }
                }

                if (slots.Count == 0)
                {
                    return false;
                }
            }

            PropKind fallback = tall ? (kind == PropKind.Wall || kind == PropKind.Shed ? PropKind.Olive : kind)
                : island ? (kind == PropKind.Bush ? PropKind.Rock : kind) : (kind == PropKind.Rock ? kind : PropKind.Bush);
            int start = sub.Int(0, slots.Count - 1);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int t = 0; t < rules.MemberTries; t++)
                {
                    Vector2 at = m == 0 && !tall ? slots[(start + t) % slots.Count] : slots[sub.Int(0, slots.Count - 1)];
                    Planned prop = StrategicProp(sub, zones, maker, rules, area, kind, at);
                    if (StrategicAccept(prop, par, zones, rules, area, m, mayBlock, placed, group) && (!tall || OnLine(prop, target, zones)))
                    {
                        prop.Zone = area.Zone;
                        group.Add(prop);
                        return true;
                    }
                }

                if (fallback == kind)
                {
                    break;
                }

                kind = fallback;
            }

            return false;
        }

        /// <summary>A strategic member of this kind at this slot, built to be measured.</summary>
        private static Planned StrategicProp(SeededRandom sub, PropZones zones, Maker maker, PropRules rules, StrategicArea area, PropKind kind, Vector2 at)
        {
            switch (kind)
            {
                case PropKind.Wall:
                {
                    // Along the course on a spine or a corner; along the band, across the course, on a split.
                    Vector2 dir = zones.CourseDirection(at);
                    Vector2 along = area.Zone == StrategicZone.Split ? Geo.Left(dir) : dir;
                    float half = 0.5f * sub.Centimetres(rules.WallLength);
                    return maker.Free(sub, PropKind.Wall, at - (along * half), at + (along * half), 1f, StrategicBase);
                }

                case PropKind.Shed:
                {
                    float scale = sub.Hundredths(rules.BuiltScale);
                    int yaw = zones.HeadingToCourse(at) - 90 + sub.Int(-15, 15);
                    return maker.Oriented(sub, PropKind.Shed, at, yaw, scale, StrategicBase);
                }

                default:
                {
                    float scale = area.Zone == StrategicZone.RoughIsland && kind == PropKind.Bush
                        ? sub.Hundredths(rules.StrategicSmallBushScale)
                        : ScaleFor(sub, rules, kind);
                    return maker.Free(sub, kind, at, at, scale, StrategicBase);
                }
            }
        }

        /// <summary>
        /// Whether a strategic member may stand: small enough on a rough island, every footprint circle passing the strategic rule
        /// and clear of every prop placed and of the group, the first member reaching into a direct or cut corridor, and every such
        /// line it reaches still playable past the group (<see cref="PropZones.Playable"/>), unless a full block is allowed there and
        /// the group's block draw won.
        /// </summary>
        private static bool StrategicAccept(Planned prop, int par, PropZones zones, PropRules rules, StrategicArea area, int m, bool mayBlock,
            List<Planned> placed, List<Planned> group)
        {
            if (prop == null || prop.Steep)
            {
                return false;
            }

            if (area.Zone == StrategicZone.RoughIsland && Geo.Cm(prop.Radius) > Geo.Cm(rules.StrategicSmallReach))
            {
                return false;
            }

            foreach (Vector2 circle in prop.Circles)
            {
                if (!zones.FitsStrategic(circle, prop.Radius, prop.Height, area.Margin))
                {
                    return false;
                }

                if (!Clear(circle, prop.Radius, placed, rules) || !Clear(circle, prop.Radius, group, rules))
                {
                    return false;
                }
            }

            var feet = new List<StrategicFoot>();
            foreach (Planned member in group)
            {
                AddFeet(member, feet);
            }

            AddFeet(prop, feet);
            bool onALine = false;
            for (int i = 0; i < zones.Lines.Count; i++)
            {
                if (!zones.IsStrategicLine(i))
                {
                    continue;
                }

                bool reaches = false;
                foreach (Vector2 circle in prop.Circles)
                {
                    reaches |= zones.Intrudes(i, circle, prop.Radius);
                }

                if (!reaches)
                {
                    continue;
                }

                onALine = true;
                if (!zones.Playable(i, feet) && !(mayBlock && zones.FullBlockAllowed(i, par)))
                {
                    return false;
                }
            }

            return m > 0 || onALine;
        }


        /// <summary>Whether some footprint circle of this prop reaches into line <paramref name="line"/>'s corridor (B3).</summary>
        private static bool OnLine(Planned prop, int line, PropZones zones)
        {
            foreach (Vector2 circle in prop.Circles)
            {
                if (zones.Intrudes(line, circle, prop.Radius))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFeet(Planned prop, List<StrategicFoot> feet)
        {
            float top = Mathf.Min(prop.Root3.y, prop.End3.y) + prop.Height;
            foreach (Vector2 circle in prop.Circles)
            {
                feet.Add(new StrategicFoot(circle, prop.Radius, top));
            }
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
        private static bool TryAccept(SeededRandom sub, PropZones zones, PropRules rules, Planned prop, List<Planned> placed, List<Planned> cluster, bool rim, bool clearOfCluster = true,
            ClearRule rule = ClearRule.Strict)
        {
            if (prop == null || prop.Steep)
            {
                return false;
            }

            foreach (Vector2 circle in prop.Circles)
            {
                if (!zones.Fits(circle, prop.Radius, prop.Height, rim, rule, rules.FairwayMargin) || !Clear(circle, prop.Radius, placed, rules) || (clearOfCluster && !Clear(circle, prop.Radius, cluster, rules)))
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
            internal const float FootRadius = 0.05f;

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

            public PropKitSettings Kit => _kit;

            /// <summary>A built prop or trace facing <paramref name="yawDegrees"/>, built to be measured; the caller tests it.</summary>
            public Planned Oriented(SeededRandom sub, PropKind kind, Vector2 at, int yawDegrees, float scale, int cluster)
            {
                return Make(sub, kind, at, at, scale, cluster, yawDegrees);
            }

            /// <summary>A single prop with its mesh seed already drawn (a spine member moved to its strip's middle, F3.4); the caller tests it.</summary>
            public Planned Seeded(PropKind kind, Vector2 at, float scale, int cluster, int seed)
            {
                return Make(null, kind, at, at, scale, cluster, 0f, seed);
            }

            /// <summary>
            /// A strategic member (S2) between two ground points (the same point for a single prop), built with no foot test: its foot
            /// is a slot that passed the strategic rule already. The caller tests it.
            /// </summary>
            public Planned Free(SeededRandom sub, PropKind kind, Vector2 from, Vector2 to, float scale, int cluster)
            {
                return Make(sub, kind, from, to, scale, cluster);
            }

            /// <summary>A prop between two ground points (a bridge), built to be measured; the caller tests it.</summary>
            public Planned Line(SeededRandom sub, PropKind kind, Vector2 from, Vector2 to, float scale, int cluster)
            {
                return Make(sub, kind, from, to, scale, cluster);
            }

            private Planned Make(SeededRandom sub, PropKind kind, Vector2 from, Vector2 to, float scale, int cluster, float yaw = 0f, int seed = -1)
            {
                var prop = new Planned { Kind = kind, Root = from, End = to, Scale = scale, Cluster = cluster, Yaw = yaw, Seed = seed >= 0 ? seed : sub.Int(0, MeshSeeds - 1) };
                prop.Root3 = new Vector3(from.x, _fields.Height(from.x, from.y), from.y);
                prop.End3 = CourseProp.IsLine(kind) ? new Vector3(to.x, _fields.Height(to.x, to.y), to.y) : prop.Root3;
                prop.Shapes = new PropBuilder(_kit, null).Add(kind, prop.Root3, prop.End3, scale, prop.Seed, yaw);
                Reach(prop.Root3, prop.End3, prop.Shapes, out prop.Radius, out prop.Height);
                if (kind == PropKind.Bridge && prop.Shapes.Length > 0)
                {
                    // The bridge's block reaches below the water; only what stands above the higher bank casts a shadow.
                    prop.Height = prop.Shapes[0].Center.y + prop.Shapes[0].Extents.y - Mathf.Max(prop.Root3.y, prop.End3.y);
                }

                // Built props and traces want level ground under their footprint (a bench on a steep bank looks like it floats).
                if (kind >= PropKind.Shed && kind != PropKind.Bridge && kind != PropKind.Rake && kind != PropKind.UmbrellaPine && kind != PropKind.CypressClump)
                {
                    prop.Steep = SteepAt(from, prop.Root3.y, prop.Radius);
                }

                float length = Vector2.Distance(from, to);
                int samples = CourseProp.IsLine(kind) ? Mathf.Max(1, Mathf.CeilToInt(length / WallSample)) : 0;
                for (int i = 0; i <= samples; i++)
                {
                    prop.Circles.Add(samples == 0 ? from : Vector2.Lerp(from, to, (float)i / samples));
                }

                return prop;
            }

            /// <summary>Whether the ground around a built prop of this reach rises or falls more than <see cref="PropRules.BuiltMaxSlope"/> allows.</summary>
            public bool SteepAt(Vector2 from, float rootHeight, float radius)
            {
                float run = Mathf.Min(Mathf.Max(radius, 0.3f), 1.5f);
                float rise = 0f;
                for (int a = 0; a < 8; a++)
                {
                    Vector2 q = from + (Geo.Dir(a * 45f) * run);
                    rise = Mathf.Max(rise, Mathf.Abs(_fields.Height(q.x, q.y) - rootHeight));
                }

                return Geo.Cm(rise) > Geo.Cm(_zones.Rules.BuiltMaxSlope * run);
            }

            /// <summary>A tall obstacle's reach and height: its shapes come from the kit's settings alone, so one build at the origin gives them.</summary>
            public void TallFootprint(PropKind kind, out float radius, out float height)
            {
                PropShape[] shapes = new PropBuilder(_kit, null).Add(kind, Vector3.zero, Vector3.zero, 1f, 0, 0f);
                Reach(Vector3.zero, Vector3.zero, shapes, out radius, out height);
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

    /// <summary>How a prop keeps out of the shot lines' corridors (<see cref="PropZones"/>).</summary>
    internal enum ClearRule
    {
        /// <summary>Every prop: its body and its shadow out of every corridor.</summary>
        Strict,

        /// <summary>A spine cypress (director Q7 B): its body out of every corridor, its shadow out of the safe legs' only.</summary>
        SpineCypress,

        /// <summary>A strategic prop (S1, Q1 B): free in the direct and cut corridors, under a safe leg only where its arc passes over.</summary>
        Strategic,
    }

    /// <summary>One footprint circle of a strategic prop, for the block rule: its centre, reach, and its top in course space.</summary>
    internal readonly struct StrategicFoot
    {
        public StrategicFoot(Vector2 centre, float radius, float top)
        {
            Centre = centre;
            Radius = radius;
            Top = top;
        }

        public Vector2 Centre { get; }
        public float Radius { get; }
        public float Top { get; }
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
        private readonly List<ShotLineKind> _kinds = new List<ShotLineKind>();
        private readonly List<Vector2[]> _capsules = new List<Vector2[]>();
        private readonly LevelGenSettings _settings;

        // The ball's ground and each safe leg's planned arc, built on the first strategic test only, so the placement of every
        // other prop costs what it did (strategic obstacles S1).
        private CourseHeightField _heights;
        private PlannedArc?[] _arcs;
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
            _settings = settings;
            CoursePlan plan = layout.Plan;
            _corridor = plan != null ? plan.ShotCorridorHalfWidth : settings.ShotCorridorHalfWidth;
            _capsuleHalfWidth = settings.SafeLandingHalfWidth;
            _length = layout.Centreline.Length;

            if (plan != null)
            {
                foreach (ShotLine line in plan.Lines)
                {
                    _lines.Add(new[] { line.From, line.To });
                    _kinds.Add(line.Kind);
                }

                // Each rest spot's landing capsule (the tee and the safe target on the green have none): the band along the
                // centreline that RouteWorld.CapsuleSamples samples, as a polyline with a half width. Its samples all lie
                // within that width of it, so the band holds the capsule. On a fork the band follows the rest spots across onto the
                // safe branch, row by row, exactly as RouteWorld.CapsuleSamples does (F3.4: until then the props kept out of a
                // band on the centreline, over the spine, and not out of the real capsule).
                RouteOffset? offset = FairwayShapePlacer.RouteOffsetOf(plan.FairwayShapes, settings);
                for (int i = 1; i + 1 < plan.SafeRoute.Count; i++)
                {
                    float along = layout.Centreline.Project(plan.SafeRoute[i], out _);
                    var points = new List<Vector2>();
                    for (float s = along - settings.SafeLandingBack; s <= along + settings.SafeLandingAhead + 0.001f; s += RouteWorld.Grid)
                    {
                        Vector2 centre = layout.Centreline.PointAt(s);
                        float across = offset?.At(s) ?? 0f;
                        if (across != 0f)
                        {
                            centre += Geo.Left(Geo.PathDirection(layout.Centreline, s)) * across;
                        }

                        points.Add(centre);
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
        public bool Fits(Vector2 p, float radius, float height, bool rim) => Fits(p, radius, height, rim, ClearRule.Strict, _rules.FairwayMargin);

        /// <summary><see cref="Fits(Vector2, float, float, bool)"/> with the corridors tested by <paramref name="rule"/> and this fairway margin.</summary>
        public bool Fits(Vector2 p, float radius, float height, bool rim, ClearRule rule, float fairwayMargin)
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

            if (!ClearOfPlay(p, radius, height, rule) || OverFairway(p, radius, fairwayMargin))
            {
                return false;
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

        /// <summary>
        /// The cheap part of <see cref="Fits"/> for a spine's free strip (F3.4): on rough and clear of play (corridors and their shadows,
        /// capsules). Between a fork's branches the corridors reach further than the fairway margin, so the strip is the same; the
        /// chosen spot still passes the whole of <see cref="Fits"/> when it is accepted.
        /// </summary>
        public bool RoughAndClearOfPlay(Vector2 p, float radius, float height, ClearRule rule = ClearRule.Strict)
        {
            return _fields.Surface(Geo.At(p)) == CourseSurface.Rough && ClearOfPlay(p, radius, height, rule);
        }

        /// <summary>
        /// Whether a footprint of this reach, plus <see cref="PropRules.FairwayMargin"/>, touches fairway ground (director, PR 4.4): the
        /// fairway field is sampled at the centre and on two rings of 24 points, out to the reach plus the margin, so a crown or a
        /// shed can't overhang the fairway's edge. Off when the margin is negative.
        /// </summary>
        public bool OverFairway(Vector2 p, float radius) => OverFairway(p, radius, _rules.FairwayMargin);

        /// <summary><see cref="OverFairway(Vector2, float)"/> with this margin (a strategic prop on a rough island's oval keeps <see cref="PropRules.StrategicOvalMargin"/>).</summary>
        public bool OverFairway(Vector2 p, float radius, float margin)
        {
            if (margin < 0f)
            {
                return false;
            }

            float reach = radius + margin;
            for (int ring = 0; ring < 3; ring++)
            {
                float r = ring == 0 ? 0f : (ring == 1 ? reach * 0.5f : reach);
                int points = ring == 0 ? 1 : 24;
                for (int i = 0; i < points; i++)
                {
                    Vector2 q = p + (Geo.Dir(i * 360f / points) * r);
                    if (_fields.Surface(Geo.At(q)) == CourseSurface.Fairway)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The part of <see cref="Fits"/> that protects play: out of the tee's circle, the +X end, every shot line's corridor (the
        /// shadow of a prop <paramref name="height"/> tall too), every landing capsule, the green and the bunkers with their margins.
        /// Nothing about the ground it stands on or the ponds, so a bridge can use it across water.
        /// </summary>
        public bool ClearOfPlay(Vector2 p, float radius, float height) => ClearOfPlay(p, radius, height, ClearRule.Strict);

        /// <summary>
        /// <see cref="ClearOfPlay(Vector2, float, float)"/> for a strategic prop (strategic obstacles plan §2, director Q1 B): the
        /// direct and cut corridors are open to it and its shadow. A safe leg's corridor is too, but only where the leg's planned
        /// arc (<see cref="PlannedArc"/>) passes over it, see <see cref="UnderSafeArc"/>. The tee, the +X end, the capsules, the
        /// green and the bunkers stay as strict as for every prop. Every other prop keeps the strict rule.
        /// </summary>
        public bool StrategicClearOfPlay(Vector2 p, float radius, float height) => ClearOfPlay(p, radius, height, ClearRule.Strategic);

        /// <summary>
        /// <see cref="Fits(Vector2, float, float, bool)"/> for a strategic prop: the same ground, edge, fairway margin and ponds, with
        /// <see cref="StrategicClearOfPlay"/> in place of the strict corridors.
        /// </summary>
        public bool FitsStrategic(Vector2 p, float radius, float height) => Fits(p, radius, height, false, ClearRule.Strategic, _rules.FairwayMargin);

        /// <summary><see cref="FitsStrategic(Vector2, float, float)"/> with this fairway margin (<see cref="PropRules.StrategicOvalMargin"/> on a rough island).</summary>
        public bool FitsStrategic(Vector2 p, float radius, float height, float fairwayMargin) => Fits(p, radius, height, false, ClearRule.Strategic, fairwayMargin);

        /// <summary>
        /// Where a strategic group may block line <paramref name="line"/> fully, on its block draw (strategic obstacles plan §2, Q5:
        /// "plutôt B mais pas dans 100% des cas"): never on a par 3; only on a cut line or a fork's direct line (the risky branch); only
        /// where the safe route reaches the green in at most par − 1 shots, so going round costs no stroke to the green; and never on
        /// a straight course's tee shot (a line from the tee on a course with no dogleg, forks included: hub, literal reading).
        /// </summary>
        public bool FullBlockAllowed(int line, int par)
        {
            CoursePlan plan = _layout.Plan;
            if (par <= 3 || plan == null)
            {
                return false;
            }

            bool fork = false;
            foreach (FairwayShape shape in plan.FairwayShapes)
            {
                fork |= shape.Kind == FairwayShapeKind.Fork;
            }

            ShotLineKind kind = _kinds[line];
            if (kind != ShotLineKind.Cut && !(kind == ShotLineKind.Direct && fork))
            {
                return false;
            }

            if (plan.SafeRoute.Count - 1 > par - 1)
            {
                return false;
            }

            return plan.Dogleg || Geo.Cm(Vector2.Distance(_lines[line][0], _layout.Tee)) > 0;
        }

        /// <summary>Whether line <paramref name="line"/> is a direct or a cut line: the lines a strategic prop may stand on (S2).</summary>
        public bool IsStrategicLine(int line) => _kinds[line] != ShotLineKind.Safe;

        /// <summary>Whether a footprint circle reaches into line <paramref name="line"/>'s corridor (its body, not its shadow).</summary>
        public bool Intrudes(int line, Vector2 p, float radius) =>
            Geo.Cm(Geo.DistanceToSegment(p, _lines[line][0], _lines[line][1])) < Geo.Cm(_corridor + radius);

        /// <summary>The ball's own ground under a point (<see cref="CourseHeightField"/>, as <c>BallGround.Height</c>).</summary>
        public float BallHeight(Vector2 p)
        {
            _heights ??= new CourseHeightField(_layout);
            return _heights.Height(p.x, p.y);
        }

        /// <summary>
        /// Whether a full-power shot from line <paramref name="line"/>'s start, along it, passes over this footprint circle (top
        /// <paramref name="top"/>, in course space) with <see cref="PropRules.StrategicSafeClearance"/> between the ball's bottom and
        /// the top, along the circle's whole span (strategic obstacles plan §2, Q5, hub: the conservative reading). The full-power
        /// arc is the highest at every distance up to its carry, so no shot from there lofts what it doesn't clear.
        /// </summary>
        public bool Loftable(int line, Vector2 p, float radius, float top)
        {
            Vector2 from = _lines[line][0];
            PlannedArc arc = PlannedArc.AtFullPower(from, BallHeight(from), _lines[line][1] - from, _settings);
            float along = arc.Along(p);
            if (Geo.Cm(along - radius) <= 0 || Geo.Cm(along + radius) >= Geo.Cm(arc.Carry))
            {
                return false;
            }

            int needCm = Geo.Cm(top + _rules.StrategicSafeClearance);
            return Geo.Cm(arc.HeightAt(along - radius)) >= needCm && Geo.Cm(arc.HeightAt(along + radius)) >= needCm;
        }

        /// <summary>
        /// The block rule (strategic obstacles plan §2, Q5): whether line <paramref name="line"/> stays playable past these footprint
        /// circles. Each circle that reaches into its corridor and that a full-power shot can't loft (<see cref="Loftable"/>) covers
        /// its span across the line; the line is playable while the corridor keeps a free run of <see cref="PropRules.StrategicPassGap"/>
        /// across it. Every circle counts at once, wherever it stands along the line: conservative, a slalom counts as a wall.
        /// </summary>
        public bool Playable(int line, IReadOnlyList<StrategicFoot> feet)
        {
            Vector2 a = _lines[line][0];
            Vector2 b = _lines[line][1];
            Vector2 dir = (b - a).sqrMagnitude > 1e-12f ? (b - a).normalized : Vector2.right;
            var covered = new List<Vector2Int>();
            foreach (StrategicFoot foot in feet)
            {
                if (!Intrudes(line, foot.Centre, foot.Radius) || Loftable(line, foot.Centre, foot.Radius, foot.Top))
                {
                    continue;
                }

                float across = Cross(dir, foot.Centre - a);
                covered.Add(new Vector2Int(Geo.Cm(across - foot.Radius), Geo.Cm(across + foot.Radius)));
            }

            if (covered.Count == 0)
            {
                return true;
            }

            covered.Sort((u, v) => u.x != v.x ? u.x.CompareTo(v.x) : u.y.CompareTo(v.y));
            int edge = Geo.Cm(_corridor);
            int need = Geo.Cm(_rules.StrategicPassGap);
            int free = -edge;
            foreach (Vector2Int span in covered)
            {
                if (Mathf.Min(span.x, edge) - free >= need)
                {
                    return true;
                }

                free = Mathf.Max(free, span.y);
            }

            return edge - free >= need;
        }

        /// <summary>
        /// The geometry half of <see cref="Playable"/> for one circle (tall obstacles): whether it leaves less than
        /// <see cref="PropRules.StrategicPassGap"/> free on both sides of line <paramref name="line"/>'s corridor, if it can't be lofted.
        /// A cheap test before the loft is computed.
        /// </summary>
        public bool Covers(int line, Vector2 p, float radius)
        {
            Vector2 a = _lines[line][0];
            Vector2 b = _lines[line][1];
            Vector2 dir = (b - a).sqrMagnitude > 1e-12f ? (b - a).normalized : Vector2.right;
            float across = Cross(dir, p - a);
            int edge = Geo.Cm(_corridor);
            int need = Geo.Cm(_rules.StrategicPassGap);
            return Geo.Cm(across - radius) + edge < need && edge - Geo.Cm(across + radius) < need;
        }

        /// <summary>Whether this point of the ground is rough.</summary>
        public bool OnRough(Vector2 p) => _fields.Surface(Geo.At(p)) == CourseSurface.Rough;

        /// <summary>The visible ground's height here (<see cref="CourseFields.Height"/>), where a prop's root stands.</summary>
        public float GroundHeight(Vector2 p) => _fields.Height(p.x, p.y);

        /// <summary>
        /// Whether the shadow of a prop <paramref name="height"/> tall standing here, as wide as its reach, stays out of every
        /// landing capsule (tall obstacles, director Q6 A; every other prop tests its body only against the capsules).
        /// </summary>
        public bool ShadowClearOfCapsules(Vector2 p, float radius, float height)
        {
            Vector2 end = p + (_shadow * (height * _rules.ShadowLength));
            foreach (Vector2[] capsule in _capsules)
            {
                for (int i = 0; i + 1 < capsule.Length; i++)
                {
                    if (Geo.Cm(SegmentDistance(p, end, capsule[i], capsule[i + 1])) < Geo.Cm(_capsuleHalfWidth + radius))
                    {
                        return false;
                    }
                }

                if (capsule.Length == 1 && Geo.Cm(Geo.DistanceToSegment(capsule[0], p, end)) < Geo.Cm(_capsuleHalfWidth + radius))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether safe leg <paramref name="line"/>'s planned arc passes over a prop of this reach standing here, with
        /// <paramref name="height"/> its top above the ground at <paramref name="p"/>: its whole footprint projects strictly between
        /// the leg's take-off and landing (the ends stay strict), and over that span the ball's bottom keeps
        /// <see cref="PropRules.StrategicSafeClearance"/> above the top, so its centre passes the clearance plus its radius over it.
        /// The arc is a downward parabola, so its lowest point over the span is at one of the span's two ends: testing them is exact.
        /// </summary>
        public bool UnderSafeArc(int line, Vector2 p, float radius, float height)
        {
            if (_kinds[line] != ShotLineKind.Safe)
            {
                return false;
            }

            PlannedArc arc = SafeArc(line);
            float along = arc.Along(p);
            if (Geo.Cm(along - radius) <= 0 || Geo.Cm(along + radius) >= Geo.Cm(arc.Length))
            {
                return false;
            }

            int needCm = Geo.Cm(_fields.Height(p.x, p.y) + height + _rules.StrategicSafeClearance);
            return Geo.Cm(arc.HeightAt(along - radius)) >= needCm && Geo.Cm(arc.HeightAt(along + radius)) >= needCm;
        }

        /// <summary>The kind of each of <see cref="Lines"/>, in the same order.</summary>
        public IReadOnlyList<ShotLineKind> Kinds => _kinds;

        /// <summary>The planned arc of a safe leg among <see cref="Lines"/>, on the ball's own ground (as <c>BallGround.Height</c>).</summary>
        public PlannedArc SafeArc(int line)
        {
            _heights ??= new CourseHeightField(_layout);
            _arcs ??= new PlannedArc?[_lines.Count];
            if (_arcs[line] == null)
            {
                Vector2 from = _lines[line][0];
                Vector2 to = _lines[line][1];
                _arcs[line] = new PlannedArc(from, _heights.Height(from.x, from.y), to, _heights.Height(to.x, to.y), _settings);
            }

            return _arcs[line].Value;
        }

        private bool ClearOfPlay(Vector2 p, float radius, float height, ClearRule rule)
        {
            bool strategic = rule == ClearRule.Strategic;
            if (Geo.Cm(Vector2.Distance(p, _layout.Tee)) < Geo.Cm(_rules.TeeKeepOut + radius))
            {
                return false;
            }

            if (Geo.Cm(EndDistance(p)) < Geo.Cm(radius))
            {
                return false;
            }

            Vector2 shadowEnd = p + (_shadow * (height * _rules.ShadowLength));
            for (int i = 0; i < _lines.Count; i++)
            {
                // A strategic prop may stand in the direct and cut corridors, and under a safe leg its arc passes over.
                if (strategic && _kinds[i] != ShotLineKind.Safe)
                {
                    continue;
                }

                // The prop and its shadow stay out of the corridor: the shadow test holds the foot's own, at its start. A spine
                // cypress's shadow may cross the direct and cut corridors, never a safe leg's (director Q7 B); its body may not.
                Vector2[] line = _lines[i];
                float reach = rule == ClearRule.SpineCypress && _kinds[i] != ShotLineKind.Safe
                    ? Geo.DistanceToSegment(p, line[0], line[1])
                    : SegmentDistance(p, shadowEnd, line[0], line[1]);
                if (Geo.Cm(reach) < Geo.Cm(_corridor + radius)
                    && !(strategic && UnderSafeArc(i, p, radius, height)))
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

            return true;
        }

        public PropRules Rules => _rules;

        public IReadOnlyList<HazardOutline> Ponds => _ponds;

        public IReadOnlyList<HazardOutline> Bunkers => _bunkers;

        /// <summary>Which way the course lies from a point, as whole degrees from +X toward +Z: toward the nearest spot on the centreline.</summary>
        public int HeadingToCourse(Vector2 p)
        {
            float along = Mathf.Clamp(_layout.Centreline.Project(p, out _), 0f, _length);
            Vector2 to = _layout.Centreline.PointAt(along) - p;
            return to.sqrMagnitude < 1e-6f ? 0 : Mathf.RoundToInt(Geo.HeadingOf(to));
        }

        /// <summary>
        /// Whether no shot line's corridor and no landing capsule reaches this pond's border: a bridge may cross it only then, so the
        /// pond's risk and the safe route stay as they are. Lines and capsules are sampled every 0.25 m.
        /// </summary>
        public bool PondHasRoom(HazardOutline pond)
        {
            foreach (Vector2[] line in _lines)
            {
                if (!AwayFromPond(pond, line, _corridor))
                {
                    return false;
                }
            }

            foreach (Vector2[] capsule in _capsules)
            {
                if (!AwayFromPond(pond, capsule, _capsuleHalfWidth))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AwayFromPond(HazardOutline pond, Vector2[] polyline, float width)
        {
            for (int i = 0; i + 1 < polyline.Length; i++)
            {
                float length = Vector2.Distance(polyline[i], polyline[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.25f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 q = Vector2.Lerp(polyline[i], polyline[i + 1], (float)s / steps);
                    if (Geo.Cm(pond.SignedDistance(q, width + 1f)) < Geo.Cm(width + RingSag))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a bridge may stand along these circles (its footprint, 0.25 m apart, each of this radius and height): clear of play
        /// everywhere, on the island, its two ends on rough ground just outside the pond.
        /// </summary>
        public bool FitsBridge(List<Vector2> circles, float radius, float height, PropRules rules)
        {
            for (int i = 0; i < circles.Count; i++)
            {
                Vector2 c = circles[i];
                if (!ClearOfPlay(c, radius, height) || Geo.Cm(FromEdge(c)) < Geo.Cm(rules.EdgeMargin))
                {
                    return false;
                }

                if ((i == 0 || i == circles.Count - 1) && !Fits(c, BridgeEndRadius, 0f, true))
                {
                    return false;
                }
            }

            return circles.Count > 1;
        }

        // The ground right under a bridge's last point must be free rough, as a small prop's foot is.
        private const float BridgeEndRadius = 0.3f;

        /// <summary>
        /// Where a rake lies in a bunker: its handle runs out along the bunker's radius, turned <paramref name="turn"/> whole degrees, with
        /// its middle <see cref="RakeInset"/> in from the border at the ring point farthest from every shot line, and the whole rake on sand.
        /// False when no ring point holds one.
        /// </summary>
        public bool RakeSpot(HazardOutline bunker, int turn, out Vector2 spot, out int yaw)
        {
            spot = default;
            yaw = 0;
            int bestCm = -1;
            for (int i = 0; i < HazardOutline.RingPoints; i++)
            {
                Vector2 outward = (bunker.Ring[i] - bunker.Centre).normalized;
                int degrees = Mathf.RoundToInt(Geo.HeadingOf(outward)) + turn;
                Vector2 along = Geo.Dir(degrees);
                Vector2 middle = bunker.Ring[i] - (outward * RakeInset);
                if (_fields.Surface(Geo.At(middle)) != CourseSurface.Bunker
                    || _fields.Surface(Geo.At(middle + (along * 0.8f))) != CourseSurface.Bunker
                    || _fields.Surface(Geo.At(middle - (along * 0.7f))) != CourseSurface.Bunker)
                {
                    continue;
                }

                float far = float.MaxValue;
                foreach (Vector2[] line in _lines)
                {
                    far = Mathf.Min(far, Geo.DistanceToSegment(middle, line[0], line[1]));
                }

                int cm = Geo.Cm(far);
                if (cm > bestCm)
                {
                    bestCm = cm;
                    spot = middle;
                    yaw = degrees;
                }
            }

            return bestCm >= 0;
        }

        private const float RakeInset = 0.9f;

        /// <summary>
        /// Where a landmark of this footprint may stand: on a promontory of the island's edge (little of a ring around it is island),
        /// <see cref="PropRules.LandmarkEdgeBand"/> in from the edge and <see cref="PropRules.LandmarkFromCentreline"/> from the play
        /// axis, with every keep-out of <see cref="Fits"/> (its shadow too).
        /// </summary>
        public List<Vector2> LandmarkStarts(float radius, float height)
        {
            var starts = new List<Vector2>();
            const float Grid = 0.5f;
            Vector2 half = _layout.HalfExtent;
            int nx = Mathf.FloorToInt(2f * half.x / Grid);
            int nz = Mathf.FloorToInt(2f * half.y / Grid);
            Vector2 band = _rules.LandmarkEdgeBand;
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    var p = new Vector2(-half.x + ((i + 0.5f) * Grid), -half.y + ((j + 0.5f) * Grid));
                    int edge = Geo.Cm(FromEdge(p));
                    if (edge < Geo.Cm(band.x) || edge > Geo.Cm(band.y) || !OnPromontory(p) || !Fits(p, radius, height, false))
                    {
                        continue;
                    }

                    float along = Mathf.Clamp(_layout.Centreline.Project(p, out _), 0f, _length);
                    if (Geo.Cm(Vector2.Distance(p, _layout.Centreline.PointAt(along))) >= Geo.Cm(_rules.LandmarkFromCentreline))
                    {
                        starts.Add(p);
                    }
                }
            }

            return starts;
        }

        // A ring of points around p: a promontory has little island on it.
        private bool OnPromontory(Vector2 p)
        {
            const int Points = 16;
            int land = 0;
            for (int i = 0; i < Points; i++)
            {
                if (FromEdge(p + (Geo.Dir(i * 360f / Points) * _rules.LandmarkRing)) > 0f)
                {
                    land++;
                }
            }

            return land * 100 <= _rules.LandmarkIslandShare * Points;
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
