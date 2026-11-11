using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// M4 PR 4.4: where the built props go. The keep-outs of every built prop are checked by
    /// <see cref="PropPlacementTests.BrokenKeepOut"/>, which the 4.3 test runs over all props of seeds 0-199; this adds what is
    /// specific to them: the 4.3 clusters don't move, the bridge never touches the safe route or the checks, the landmark's site,
    /// the rake, the frequencies.
    /// </summary>
    public class BuiltPropPlacementTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        private static LevelGenSettings Copy(System.Action<SerializedObject> change)
        {
            LevelGenSettings copy = Object.Instantiate(Settings);
            var serialized = new SerializedObject(copy);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        private static bool IsBuilt(CourseProp prop) => prop.Cluster >= PropPlacement.BuiltBase;

        [Test]
        public void BuiltProps_LeaveThe43ClustersBitIdentical()
        {
            LevelGenSettings off = Copy(s =>
            {
                s.FindProperty("props.builtClustersPar3").vector2IntValue = Vector2Int.zero;
                s.FindProperty("props.builtClustersPar4").vector2IntValue = Vector2Int.zero;
                s.FindProperty("props.builtClustersPar5").vector2IntValue = Vector2Int.zero;
                s.FindProperty("props.rakeChance").intValue = 0;
                s.FindProperty("props.bridgeChance").intValue = 0;
                s.FindProperty("props.landmarkChance").intValue = 0;
            });
            try
            {
                int compared = 0;
                int builtSeen = 0;
                foreach (int par in new[] { 3, 4, 5 })
                {
                    for (int seed = 0; seed < 25; seed++)
                    {
                        var withBuilt = new List<CourseProp>();
                        foreach (CourseProp prop in CourseGenerator.Generate(seed, par, Settings).Props)
                        {
                            if (IsBuilt(prop))
                            {
                                builtSeen++;
                            }
                            else
                            {
                                withBuilt.Add(prop);
                            }
                        }

                        IReadOnlyList<CourseProp> without = CourseGenerator.Generate(seed, par, off).Props;
                        Assert.That(without, Has.None.Matches<CourseProp>(IsBuilt), $"seed {seed} par {par}");
                        Assert.That(withBuilt.Count, Is.EqualTo(without.Count), $"seed {seed} par {par}");
                        for (int i = 0; i < without.Count; i++)
                        {
                            Assert.That(withBuilt[i].Kind, Is.EqualTo(without[i].Kind));
                            Assert.That(withBuilt[i].Root, Is.EqualTo(without[i].Root));
                            Assert.That(withBuilt[i].End, Is.EqualTo(without[i].End));
                            Assert.That(withBuilt[i].Scale, Is.EqualTo(without[i].Scale));
                            Assert.That(withBuilt[i].Seed, Is.EqualTo(without[i].Seed));
                            Assert.That(withBuilt[i].Cluster, Is.EqualTo(without[i].Cluster));
                            Assert.That(withBuilt[i].Shapes, Is.EqualTo(without[i].Shapes));
                            compared++;
                        }
                    }
                }

                Assert.That(compared, Is.GreaterThan(300));
                Assert.That(builtSeen, Is.GreaterThan(20), "the built props do appear");
            }
            finally
            {
                Object.DestroyImmediate(off);
            }
        }

        /// <summary>Whether any point of any collision shape of this prop (centre, outline rings, box corners and edge middles) is on fairway ground.</summary>
        private static bool ShapesOverFairway(CourseFields fields, CourseProp prop)
        {
            foreach (PropShape shape in prop.Shapes)
            {
                var centre = new Vector2(shape.Center.x, shape.Center.z);
                var points = new List<Vector2> { centre };
                if (shape.Kind == PropShapeKind.Box)
                {
                    Vector2 along = new Vector2(Mathf.Cos(shape.Yaw), Mathf.Sin(shape.Yaw));
                    Vector2 across = Geo.Left(along);
                    for (int i = -2; i <= 2; i++)
                    {
                        for (int j = -2; j <= 2; j++)
                        {
                            points.Add(centre + (along * (shape.Extents.x * i * 0.5f)) + (across * (shape.Extents.z * j * 0.5f)));
                        }
                    }
                }
                else
                {
                    for (int a = 0; a < 36; a++)
                    {
                        Vector2 dir = Geo.Dir(a * 10f);
                        points.Add(centre + new Vector2(dir.x * shape.Extents.x, dir.y * shape.Extents.z));
                        points.Add(centre + new Vector2(dir.x * shape.Extents.x, dir.y * shape.Extents.z) * 0.5f);
                    }
                }

                foreach (Vector2 q in points)
                {
                    if (fields.Surface(Geo.At(q)) == CourseSurface.Fairway)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Test]
        [Timeout(600000)]
        public void NoPropShapeReachesFairway()
        {
            NoPropShapeReachesFairway_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        [Timeout(600000)]
        public void NoPropShapeReachesFairway_FullSweep()
        {
            NoPropShapeReachesFairway_Run(200);
        }

        private void NoPropShapeReachesFairway_Run(int seeds)
        {
            int checkedProps = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var fields = new CourseFields(layout);
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Kind == PropKind.Bridge || prop.Kind == PropKind.Rake)
                        {
                            continue;
                        }

                        checkedProps++;
                        Assert.That(ShapesOverFairway(fields, prop), Is.False, $"seed {seed} par {par} {prop.Kind} at {prop.Root}");
                    }
                }
            }

            Assert.That(checkedProps, Is.GreaterThan(15 * seeds));
        }

        /// <summary>Props per course and the share of props whose shapes reach fairway, without the rule and with it (logged; run by hand).</summary>
        [Test, Explicit("Measurement"), Timeout(600000)]
        public void FairwayRule_BeforeAndAfter()
        {
            LevelGenSettings off = Copy(s => s.FindProperty("props.fairwayMargin").floatValue = -1f);
            try
            {
                var report = new System.Text.StringBuilder();
                foreach (int par in new[] { 3, 4, 5 })
                {
                    foreach (LevelGenSettings settings in new[] { off, Settings })
                    {
                        int props = 0, over = 0, bridges = 0;
                        for (int seed = 0; seed < 200; seed++)
                        {
                            CourseLayout layout = CourseGenerator.Generate(seed, par, settings);
                            var fields = new CourseFields(layout);
                            foreach (CourseProp prop in layout.Props)
                            {
                                props++;
                                bridges += prop.Kind == PropKind.Bridge ? 1 : 0;
                                if (prop.Kind != PropKind.Bridge && prop.Kind != PropKind.Rake && ShapesOverFairway(fields, prop))
                                {
                                    over++;
                                }
                            }
                        }

                        report.Append($"par {par} {(settings == off ? "before" : "after ")}: props/course {props / 200f:F1}, shapes over fairway {over} ({(props > 0 ? 100f * over / props : 0):F1}%), bridges {bridges}\n");
                    }
                }

                Debug.Log("[FairwayRule]\n" + report);
            }
            finally
            {
                Object.DestroyImmediate(off);
            }
        }

        [Test]
        [Timeout(600000)]
        public void BuiltProps_StandOnLevelGround()
        {
            BuiltProps_StandOnLevelGround_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-99, run by hand before a merge")]
        [Timeout(600000)]
        public void BuiltProps_StandOnLevelGround_FullSweep()
        {
            BuiltProps_StandOnLevelGround_Run(100);
        }

        private void BuiltProps_StandOnLevelGround_Run(int seeds)
        {
            PropRules rules = Settings.Props;
            int checkedProps = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var fields = new CourseFields(layout);
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Kind < PropKind.Shed || prop.Kind == PropKind.Bridge || prop.Kind == PropKind.Rake)
                        {
                            continue;
                        }

                        PropPlacement.Reach(prop.Root, prop.End, prop.Shapes, out float radius, out _);
                        float run = Mathf.Min(Mathf.Max(radius, 0.3f), 1.5f);
                        for (int a = 0; a < 8; a++)
                        {
                            Vector2 q = new Vector2(prop.Root.x, prop.Root.z) + (Geo.Dir(a * 45f) * run);
                            Assert.That(Mathf.Abs(fields.Height(q.x, q.y) - prop.Root.y), Is.LessThanOrEqualTo((rules.BuiltMaxSlope * run) + 0.011f), $"seed {seed} par {par} {prop.Kind}");
                        }

                        checkedProps++;
                    }
                }
            }

            Assert.That(checkedProps, Is.GreaterThan(50));
        }

        [Test]
        public void SameSeed_GivesTheSameBuiltProps()
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 12; seed++)
                {
                    IReadOnlyList<CourseProp> a = CourseGenerator.Generate(seed, par, Settings).Props;
                    IReadOnlyList<CourseProp> b = CourseGenerator.Generate(seed, par, Settings).Props;
                    Assert.That(b.Count, Is.EqualTo(a.Count));
                    for (int i = 0; i < a.Count; i++)
                    {
                        Assert.That(b[i].Kind, Is.EqualTo(a[i].Kind));
                        Assert.That(b[i].Root, Is.EqualTo(a[i].Root));
                        Assert.That(b[i].End, Is.EqualTo(a[i].End));
                        Assert.That(b[i].YawDegrees, Is.EqualTo(a[i].YawDegrees));
                        Assert.That(b[i].Seed, Is.EqualTo(a[i].Seed));
                        Assert.That(b[i].Shapes, Is.EqualTo(a[i].Shapes));
                    }
                }
            }
        }

        /// <summary>Frequencies and counts of every built kind over seeds 0-199 (logged, with bounds that hold whatever the tuning).</summary>
        [Test, Explicit("Measurement"), Timeout(600000)]
        public void Frequencies_OverSeeds0To199()
        {
            var report = new System.Text.StringBuilder();
            foreach (int par in new[] { 3, 4, 5 })
            {
                Vector2Int range = Settings.Props.BuiltClusters(par);
                var courses = new Dictionary<PropKind, int>();
                int totalProps = 0;
                int totalBuilt = 0;
                int builtClusterCourses = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    totalProps += layout.Props.Count;
                    var seen = new HashSet<PropKind>();
                    var builtClusters = new HashSet<int>();
                    int traces = 0;
                    int bridges = 0;
                    int landmarks = 0;
                    foreach (CourseProp prop in layout.Props)
                    {
                        seen.Add(prop.Kind);
                        if (IsBuilt(prop))
                        {
                            totalBuilt++;
                        }

                        if (prop.Cluster > PropPlacement.BuiltBase && prop.Cluster < PropPlacement.BridgeBase)
                        {
                            builtClusters.Add(prop.Cluster);
                        }

                        traces += prop.Kind == PropKind.Bucket || prop.Kind == PropKind.Bicycle || prop.Kind == PropKind.Laundry || prop.Kind == PropKind.Rake ? 1 : 0;
                        bridges += prop.Kind == PropKind.Bridge ? 1 : 0;
                        landmarks += prop.Kind == PropKind.Chapel || prop.Kind == PropKind.Lighthouse ? 1 : 0;
                    }

                    foreach (PropKind kind in seen)
                    {
                        courses[kind] = courses.TryGetValue(kind, out int n) ? n + 1 : 1;
                    }

                    builtClusterCourses += builtClusters.Count > 0 ? 1 : 0;
                    Assert.That(bridges, Is.LessThanOrEqualTo(1), $"seed {seed} par {par}");
                    Assert.That(landmarks, Is.LessThanOrEqualTo(1), $"seed {seed} par {par}");
                    Assert.That(traces, Is.LessThanOrEqualTo(Settings.Props.TraceMax), $"seed {seed} par {par}");
                    Assert.That(builtClusters.Count, Is.LessThanOrEqualTo(range.y), $"seed {seed} par {par}");
                }

                report.Append($"par {par}: props/course {totalProps / 200f:F1} ({totalBuilt / 200f:F1} built), courses with a built cluster {builtClusterCourses}/200;");
                foreach (PropKind kind in new[] { PropKind.Shed, PropKind.Bench, PropKind.Bridge, PropKind.Rake, PropKind.Bucket, PropKind.Bicycle, PropKind.Laundry, PropKind.Chapel, PropKind.Lighthouse })
                {
                    report.Append($" {kind} {(courses.TryGetValue(kind, out int n) ? n : 0)}");
                }

                report.Append('\n');
            }

            Debug.Log("[BuiltProps] seeds 0-199, courses holding each kind:\n" + report);
            Assert.That(report.Length, Is.GreaterThan(0));
        }

        [Test]
        [Timeout(600000)]
        public void Bridge_NeverChangesTheSafeRouteOrTheChecks_AndSpansItsPond()
        {
            Bridge_NeverChangesTheSafeRouteOrTheChecks_AndSpansItsPond_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        [Timeout(600000)]
        public void Bridge_NeverChangesTheSafeRouteOrTheChecks_AndSpansItsPond_FullSweep()
        {
            Bridge_NeverChangesTheSafeRouteOrTheChecks_AndSpansItsPond_Run(200);
        }

        private void Bridge_NeverChangesTheSafeRouteOrTheChecks_AndSpansItsPond_Run(int seeds)
        {
            int found = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds && found < 12; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var bridges = new List<CourseProp>();
                    var others = new List<CourseProp>();
                    foreach (CourseProp prop in layout.Props)
                    {
                        (prop.Kind == PropKind.Bridge ? bridges : others).Add(prop);
                    }

                    if (bridges.Count == 0)
                    {
                        continue;
                    }

                    found++;
                    CourseLayout without = layout.WithProps(others);

                    // The plan is the bare course's, and it is the same with or without the bridge.
                    Assert.That(without.Plan.SafeRoute, Is.EqualTo(layout.Plan.SafeRoute));

                    // No check shot touches the bridge, so the checks play exactly the same.
                    var struck = new List<int>();
                    CourseCheck with = CourseChecks.Run(layout, Shot, Settings, true, struck);
                    CourseCheck bare = CourseChecks.Run(without, Shot, Settings, true);
                    Assert.That(with, Is.EqualTo(bare), $"seed {seed} par {par}");
                    foreach (int shape in struck)
                    {
                        if (shape < layout.PropShapes.Count)
                        {
                            Assert.That(layout.Props[layout.PropOfShape(shape)].Kind, Is.Not.EqualTo(PropKind.Bridge), $"seed {seed} par {par}: a check shot struck the bridge");
                        }
                    }

                    // It spans a pond, bank to bank, with water in the middle.
                    var fields = new CourseFields(layout);
                    CourseProp bridge = bridges[0];
                    var middle = new Vector3((bridge.Root.x + bridge.End.x) * 0.5f, 0f, (bridge.Root.z + bridge.End.z) * 0.5f);
                    Assert.That(fields.Pond(middle), Is.LessThan(0f), $"seed {seed} par {par}: the bridge's middle is over water");
                    Assert.That(fields.Surface(new Vector3(bridge.Root.x, 0f, bridge.Root.z)), Is.EqualTo(CourseSurface.Rough), "a bank");
                    Assert.That(fields.Surface(new Vector3(bridge.End.x, 0f, bridge.End.z)), Is.EqualTo(CourseSurface.Rough), "the other bank");
                }
            }

            Assert.That(found, Is.GreaterThan(0), "no course in seeds 0-199 got a bridge");
        }

        /// <summary>Where the bridge candidates are lost, pond by pond over seeds 0-199 (logged; run by hand).</summary>
        [Test, Explicit("Measurement"), Timeout(600000)]
        public void BridgeFunnel_Measurement()
        {
            PropRules rules = Settings.Props;
            int ponds = 0, room = 0, narrow = 0, fits = 0, courses = 0, relaxedFits = 0;
            var relaxedCourses = new HashSet<int>();
            var spans = new List<float>();
            var lost = new Dictionary<string, int>();
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings, 0, props: false);
                    var zones = new PropZones(layout, Settings);
                    bool any = false;
                    foreach (HazardOutline pond in zones.Ponds)
                    {
                        ponds++;
                        // Counted both ways: with the pond clear of every corridor (the plan's rule), and with only the bridge itself clear.
                        bool roomy = zones.PondHasRoom(pond);
                        room += roomy ? 1 : 0;
                        int best = 0;
                        float bestSpan = float.MaxValue;
                        for (int j = 0; j < 12; j++)
                        {
                            float span = Vector2.Distance(pond.Ring[j], pond.Ring[j + 12]);
                            if (span < bestSpan)
                            {
                                bestSpan = span;
                                best = j;
                            }
                        }

                        spans.Add(bestSpan);
                        if (bestSpan > rules.BridgeMaxSpan)
                        {
                            continue;
                        }

                        narrow++;
                        Vector2 a = pond.Ring[best];
                        Vector2 b = pond.Ring[best + 12];
                        Vector2 along = (b - a).normalized;
                        Vector2 from = a - (along * rules.BridgeBank);
                        Vector2 to = b + (along * rules.BridgeBank);
                        var circles = new List<Vector2>();
                        int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.25f));
                        for (int i = 0; i <= n; i++)
                        {
                            circles.Add(Vector2.Lerp(from, to, (float)i / n));
                        }

                        if (zones.FitsBridge(circles, 0.6f, 1.2f, rules))
                        {
                            relaxedFits++;
                            relaxedCourses.Add(par * 1000 + seed);
                            fits += roomy ? 1 : 0;
                            any |= roomy;
                        }
                        else
                        {
                            string why = "?";
                            foreach (Vector2 c in circles)
                            {
                                if (!zones.ClearOfPlay(c, 0.6f, 1.2f))
                                {
                                    why = "play";
                                    break;
                                }

                                if (zones.FromEdge(c) < rules.EdgeMargin)
                                {
                                    why = "edge";
                                    break;
                                }
                            }

                            if (why == "?")
                            {
                                why = "bank end";
                            }

                            lost[why] = lost.TryGetValue(why, out int count) ? count + 1 : 1;
                        }
                    }

                    courses += any ? 1 : 0;
                }
            }

            spans.Sort();
            string median = spans.Count > 0 ? spans[spans.Count / 2].ToString("F1") : "-";
            var lostText = new System.Text.StringBuilder();
            foreach (KeyValuePair<string, int> kv in lost)
            {
                lostText.Append($" {kv.Key}={kv.Value}");
            }

            Debug.Log($"[BridgeFunnel] ponds {ponds}, with room {room}, span <= {rules.BridgeMaxSpan} m {narrow}, fits {fits} (courses {courses}); bridge alone clear of the corridors, pond or not: {relaxedFits} ponds on {relaxedCourses.Count} courses; median narrow span {median} m; lost:{lostText}");
        }

        [Test]
        [Timeout(600000)]
        public void Landmark_StandsAtTheEdgeAwayFromTheAxis_AndOutOfTheEnd()
        {
            Landmark_StandsAtTheEdgeAwayFromTheAxis_AndOutOfTheEnd_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        [Timeout(600000)]
        public void Landmark_StandsAtTheEdgeAwayFromTheAxis_AndOutOfTheEnd_FullSweep()
        {
            Landmark_StandsAtTheEdgeAwayFromTheAxis_AndOutOfTheEnd_Run(200);
        }

        private void Landmark_StandsAtTheEdgeAwayFromTheAxis_AndOutOfTheEnd_Run(int seeds)
        {
            PropRules rules = Settings.Props;
            int chapels = 0;
            int lighthouses = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var fields = new CourseFields(layout);
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Kind != PropKind.Chapel && prop.Kind != PropKind.Lighthouse)
                        {
                            continue;
                        }

                        chapels += prop.Kind == PropKind.Chapel ? 1 : 0;
                        lighthouses += prop.Kind == PropKind.Lighthouse ? 1 : 0;
                        var at = new Vector2(prop.Root.x, prop.Root.z);
                        float fromEdge = -fields.Island(new Vector3(at.x, 0f, at.y));
                        Assert.That(fromEdge, Is.InRange(rules.LandmarkEdgeBand.x - 0.011f, rules.LandmarkEdgeBand.y + 0.011f), $"seed {seed} par {par}: at the island's edge");
                        float along = Mathf.Clamp(layout.Centreline.Project(at, out _), 0f, layout.Centreline.Length);
                        Assert.That(Vector2.Distance(at, layout.Centreline.PointAt(along)), Is.GreaterThanOrEqualTo(rules.LandmarkFromCentreline - 0.011f), $"seed {seed} par {par}: away from the play axis");
                        Assert.That(PropPlacementTests.BrokenKeepOut(layout, prop), Is.Null, $"seed {seed} par {par}");
                        Assert.That(layout.Centreline.Project(at, out _) - layout.Centreline.Length, Is.LessThan(-1f), "never past the hole");
                        Assert.That(Vector2.Angle(Vector2.right, at - layout.Hole), Is.GreaterThan(rules.EndFan * 0.5f), "never in the +X fan");
                    }
                }
            }

            Debug.Log($"[BuiltProps] landmarks over seeds 0-199 x 3 pars: {chapels} chapels, {lighthouses} lighthouses");
            Assert.That(chapels, Is.GreaterThan(0));
            Assert.That(lighthouses, Is.GreaterThan(0));
        }

        [Test]
        [Timeout(600000)]
        public void Rake_LiesFlatOnSand_AndTheBallRollsOverIt()
        {
            Rake_LiesFlatOnSand_AndTheBallRollsOverIt_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        [Timeout(600000)]
        public void Rake_LiesFlatOnSand_AndTheBallRollsOverIt_FullSweep()
        {
            Rake_LiesFlatOnSand_AndTheBallRollsOverIt_Run(200);
        }

        private void Rake_LiesFlatOnSand_AndTheBallRollsOverIt_Run(int seeds)
        {
            int found = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds && found < 6; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Kind != PropKind.Rake)
                        {
                            continue;
                        }

                        found++;
                        Assert.That(prop.Shapes, Is.Empty);
                        var fields = new CourseFields(layout);
                        Vector3 at = prop.Root;
                        Assert.That(fields.Surface(new Vector3(at.x, 0f, at.z)), Is.EqualTo(CourseSurface.Bunker), "in a bunker");

                        // A layout with the rake alone: a rolling ball crossing it meets nothing.
                        CourseLayout alone = layout.WithProps(new[] { prop });
                        var ground = new BallGround(alone);
                        BallObstacles obstacles = BallObstacles.ForCourse(alone.PropShapes, ground, Shot);
                        float h = ground.Height(at.x, at.z) + Shot.BallRadius;
                        Assert.That(obstacles.FirstHit(new Vector3(at.x - 1f, h, at.z), new Vector3(at.x + 1f, h, at.z), true, BallObstacles.None, out _), Is.False);
                    }
                }
            }

            Assert.That(found, Is.GreaterThan(0), "no rake in the seeds");
        }

        [Test]
        public void ABucketStandsOnTheGroundBesideItsBench_NeverOverlappingIt()
        {
            ABucketStandsOnTheGroundBesideItsBench_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void ABucketStandsOnTheGroundBesideItsBench_FullSweep()
        {
            ABucketStandsOnTheGroundBesideItsBench_Run(200);
        }

        private void ABucketStandsOnTheGroundBesideItsBench_Run(int seeds)
        {
            int buckets = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var fields = new CourseFields(layout);
                    foreach (CourseProp bucket in layout.Props)
                    {
                        if (bucket.Kind != PropKind.Bucket)
                        {
                            continue;
                        }

                        buckets++;
                        Assert.That(bucket.Root.y, Is.EqualTo(fields.Height(bucket.Root.x, bucket.Root.z)).Within(0.02f), $"seed {seed} par {par}: the bucket's base is at ground height");
                        float radius = bucket.Shapes[0].Extents.x;
                        foreach (CourseProp bench in layout.Props)
                        {
                            if (bench.Cluster != bucket.Cluster || bench.Kind != PropKind.Bench)
                            {
                                continue;
                            }

                            PropShape box = bench.Shapes[0];
                            float yaw = box.Yaw;
                            var d = new Vector2(bucket.Root.x - box.Center.x, bucket.Root.z - box.Center.z);
                            float along = Mathf.Abs((d.x * Mathf.Cos(yaw)) + (d.y * Mathf.Sin(yaw)));
                            float across = Mathf.Abs((-d.x * Mathf.Sin(yaw)) + (d.y * Mathf.Cos(yaw)));
                            float gap = Mathf.Max(along - box.Extents.x, across - box.Extents.z) - radius;
                            Assert.That(gap, Is.GreaterThanOrEqualTo(0.25f), $"seed {seed} par {par}: a clear gap between the bucket and the bench");
                        }
                    }
                }
            }

            Assert.That(buckets, Is.GreaterThan(seeds / 10), "buckets exist");
        }

        [Test]
        public void ATraceStandsBesideItsBuiltProp()
        {
            int shedTraces = 0;
            foreach (int par in new[] { 4, 5 })
            {
                for (int seed = 0; seed < 100; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    foreach (CourseProp trace in layout.Props)
                    {
                        if (trace.Kind != PropKind.Bucket && trace.Kind != PropKind.Bicycle && trace.Kind != PropKind.Laundry)
                        {
                            continue;
                        }

                        CourseProp? main = null;
                        foreach (CourseProp prop in layout.Props)
                        {
                            if (prop.Cluster == trace.Cluster && (prop.Kind == PropKind.Shed || prop.Kind == PropKind.Bench))
                            {
                                main = prop;
                            }
                        }

                        Assert.That(main.HasValue, $"seed {seed} par {par}: {trace.Kind} has its shed or bench");
                        Assert.That(Vector3.Distance(main.Value.Root, trace.Root), Is.LessThan(3.5f));
                        shedTraces++;
                    }
                }
            }

            Assert.That(shedTraces, Is.GreaterThan(0));
        }
    }
}
