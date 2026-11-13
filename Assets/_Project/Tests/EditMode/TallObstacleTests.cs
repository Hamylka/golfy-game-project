using System.Collections.Generic;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Tall obstacles (feature/tall-obstacles, director Q1 B, Q2 A, Q3 A, Q4 A, Q5 B, Q6 A): an umbrella pine, a cypress clump or a
    /// dovecote that closes a line where a full block is allowed. Default suite: seeds 0-49, variant 0.
    /// </summary>
    public class TallObstacleTests
    {
        private const int Seeds = 50;
        private static readonly int[] Pars = { 3, 4, 5 };
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        /// <summary>
        /// Each tall kind's shapes come from the kit's settings alone (the same for every seed), stand 5.5-6 m tall, and hold its
        /// whole mesh above the ground line, so what is drawn is what the ball strikes and the placement knows the footprint before
        /// any draw. Logs each kind's reach, height and triangles.
        /// </summary>
        [Test]
        public void TallShapesHoldTheirMeshAndIgnoreTheSeed()
        {
            var report = new StringBuilder("TALL_KIT");
            foreach (PropKind kind in PropPlacement.TallKinds)
            {
                PropShape[] first = null;
                int triangles = 0;
                for (int seed = 0; seed < 40; seed++)
                {
                    var builder = new PropBuilder(Settings.PropKit, null);
                    PropShape[] shapes = builder.Add(kind, Vector3.zero, Vector3.zero, 1f, seed * 7919, 0f);
                    first ??= shapes;
                    Assert.AreEqual(first.Length, shapes.Length, $"{kind} seed {seed}: shape count");
                    for (int i = 0; i < shapes.Length; i++)
                    {
                        Assert.AreEqual(first[i].Center, shapes[i].Center, $"{kind} seed {seed}: shape {i} centre");
                        Assert.AreEqual(first[i].Extents, shapes[i].Extents, $"{kind} seed {seed}: shape {i} extents");
                    }

                    triangles = builder.Mesh.TriangleCount;
                    foreach (Vector3 v in builder.Mesh.Positions)
                    {
                        // Trunks and walls start below the ground line on purpose (the ground mesh follows the field at its corners only).
                        if (v.y < 0.001f)
                        {
                            continue;
                        }

                        float level = float.MaxValue;
                        foreach (PropShape shape in shapes)
                        {
                            level = Mathf.Min(level, shape.Level(v));
                        }

                        Assert.LessOrEqual(level, 1.02f, $"{kind} seed {seed}: vertex {v} outside its shapes");
                    }
                }

                PropPlacement.Reach(Vector3.zero, Vector3.zero, first, out float reach, out float height);
                Assert.That(height, Is.InRange(5.5f, 6.01f), $"{kind}: height");
                report.Append($" {kind}: reach {reach:0.00} m, height {height:0.00} m, base {PropBuilder.TallBase(kind, Settings.PropKit):0.00}, {triangles} triangles;");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Every tall obstacle stands on a par 4 or 5, out of every keep-out (its base on rough with the fairway margin, its crown
        /// out of the tee, capsules, green, hazards and safe flights, its shadow off the safe legs and the capsules:
        /// <see cref="PropPlacementTests.BrokenKeepOut"/>), and closes a line where Q5 allows a full block (re-read from the plan:
        /// a cut line or a fork's direct line, the safe route reaching the green in at most par − 1 shots, never a straight course's
        /// tee shot); every other direct or cut line it reaches stays playable or is one where a block is allowed too. Some par 5
        /// gets one (else the test proves nothing).
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void TallObstaclesCloseOnlyAllowedLinesAndKeepOut()
        {
            var failures = new StringBuilder();
            var count = new Dictionary<int, int> { { 3, 0 }, { 4, 0 }, { 5, 0 } };
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    PropZones zones = null;
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Zone != StrategicZone.BlockLine)
                        {
                            continue;
                        }

                        count[par]++;
                        string where = $"par {par} seed {seed} {prop.Kind}";
                        if (par == 3 || System.Array.IndexOf(PropPlacement.TallKinds, prop.Kind) < 0 || !prop.Strategic)
                        {
                            failures.AppendLine($"{where}: not a tall strategic obstacle on a par 4 or 5");
                        }

                        string broken = PropPlacementTests.BrokenKeepOut(layout, prop);
                        if (broken != null)
                        {
                            failures.AppendLine($"{where}: breaks {broken}");
                        }

                        zones ??= new PropZones(layout, Settings);
                        List<StrategicFoot> feet = StrategicPropTests.Feet(prop);
                        CoursePlan plan = layout.Plan;
                        bool closes = false;
                        for (int i = 0; i < zones.Lines.Count; i++)
                        {
                            ShotLineKind kind = zones.Kinds[i];
                            if (kind == ShotLineKind.Safe || !zones.Intrudes(i, feet[0].Centre, feet[0].Radius) || zones.Playable(i, feet))
                            {
                                continue;
                            }

                            bool fromTee = Vector2.Distance(zones.Lines[i][0], layout.Tee) < 0.01f;
                            bool allowed = (kind == ShotLineKind.Cut || (kind == ShotLineKind.Direct && StrategicPropTests.IsFork(layout)))
                                && plan.SafeRoute.Count - 1 <= par - 1 && (plan.Dogleg || !fromTee);
                            if (!allowed)
                            {
                                failures.AppendLine($"{where}: closes {kind} line {i} where no full block is allowed");
                            }

                            closes |= allowed;
                        }

                        if (!closes)
                        {
                            failures.AppendLine($"{where}: closes no line");
                        }
                    }
                }
            }

            Debug.Log($"TALL_BLOCKS seeds 0-49, variant 0: par 3 {count[3]}, par 4 {count[4]}, par 5 {count[5]}");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
            Assert.Greater(count[5], 0, "no par 5 got a tall obstacle");
        }

        /// <summary>
        /// Q5 B, bit for bit: with the tall weights set to zero no tall obstacle can be drawn, which is S2's code path (the spots are
        /// searched but nothing is drawn). Every course without a tall obstacle is identical to that run, prop for prop; a course
        /// with one differs from it only in its strategic props. Logs which courses changed.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void CoursesWithoutATallObstacleAreUnchanged()
        {
            LevelGenSettings s2 = WithoutTall();
            var failures = new StringBuilder();
            var changed = new StringBuilder();
            try
            {
                foreach (int par in Pars)
                {
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        CourseLayout now = CourseGenerator.Generate(seed, par, Settings);
                        CourseLayout before = CourseGenerator.Generate(seed, par, s2);
                        bool tall = false;
                        foreach (CourseProp prop in now.Props)
                        {
                            tall |= prop.Zone == StrategicZone.BlockLine;
                        }

                        List<ulong> a = Hashes(now, !tall);
                        List<ulong> b = Hashes(before, !tall);
                        if (!SpinePropTests.Same(a, b))
                        {
                            failures.AppendLine($"par {par} seed {seed}: {(tall ? "other props" : "props")} changed");
                        }

                        if (tall)
                        {
                            changed.Append($" p{par}s{seed}");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(s2);
            }

            Debug.Log($"TALL_CHANGED courses with a tall obstacle (strategic group replaced):{changed}");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        /// <summary>
        /// Shots along each closed line, from its start and from a few metres short of the obstacle, at every power: whenever the
        /// aim preview (<see cref="BallSimulation.PredictHit"/>) finds a hit, the played ball's first strike is that hit (time, point,
        /// shape), and a shot predicted clear strikes nothing in flight. Some shots strike the tall obstacle first.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void PreviewStopsAtATallObstacle()
        {
            int tallHits = 0;
            foreach (int par in new[] { 4, 5 })
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    for (int t = 0; t < layout.Props.Count; t++)
                    {
                        CourseProp tall = layout.Props[t];
                        if (tall.Zone != StrategicZone.BlockLine)
                        {
                            continue;
                        }

                        var ground = new BallGround(layout);
                        BallObstacles obstacles = BallObstacles.ForCourse(layout.PropShapes, ground, Shot);
                        var at = new Vector2(tall.Root.x, tall.Root.z);
                        foreach (ShotLine line in layout.Plan.Lines)
                        {
                            if (line.Kind == ShotLineKind.Safe || Geo.DistanceToSegment(at, line.From, line.To) > layout.Plan.ShotCorridorHalfWidth + 2.5f)
                            {
                                continue;
                            }

                            Vector2 toward = (at - line.From).normalized;
                            for (int start = 0; start <= 3; start++)
                            {
                                Vector2 from = start == 0 ? line.From : at - (toward * (2f + (2f * start)));
                                for (int p = 2; p <= 40; p += 2)
                                {
                                    float power = p / 40f;
                                    var ball = new BallSimulation(ground, Shot, from, obstacles);
                                    BallisticArc arc = ball.ArcFor(toward, power);
                                    bool predicted = ball.PredictHit(arc, ball.PredictLanding(arc), out ArcHit hit);
                                    ball.Shoot(toward, power);
                                    for (int i = 0; i < 60 * 60 && ball.State == BallState.Flying && ball.Hits == 0 && ball.Bounces == 0; i++)
                                    {
                                        ball.Step();
                                    }

                                    string where = $"par {par} seed {seed} start {start} power {power}";
                                    if (!predicted)
                                    {
                                        Assert.That(ball.Hits, Is.EqualTo(0), where);
                                        continue;
                                    }

                                    Assert.That(ball.Hits, Is.EqualTo(1), where);
                                    Assert.That(ball.LastHit.Time, Is.EqualTo(hit.Time), where);
                                    Assert.That(ball.LastHit.Point, Is.EqualTo(hit.Point), where);
                                    Assert.That(ball.LastHit.Index, Is.EqualTo(hit.Index), where);
                                    tallHits += !hit.IsPole && layout.PropOfShape(hit.Index) == t ? 1 : 0;
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"TALL_PREVIEW {tallHits} shots struck a tall obstacle first");
            Assert.Greater(tallHits, 0, "no shot struck a tall obstacle first");
        }

        /// <summary>A copy of the settings with the tall weights at zero: no tall obstacle can be drawn (S2's behaviour).</summary>
        internal static LevelGenSettings WithoutTall()
        {
            LevelGenSettings copy = Object.Instantiate(Settings);
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("props.tallWeights").vector3IntValue = Vector3Int.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        private static List<ulong> Hashes(CourseLayout layout, bool all)
        {
            var hashes = new List<ulong>();
            foreach (CourseProp prop in layout.Props)
            {
                if (all || !prop.Strategic)
                {
                    hashes.Add(CourseFingerprint.Prop(prop));
                }
            }

            return hashes;
        }
    }
}
