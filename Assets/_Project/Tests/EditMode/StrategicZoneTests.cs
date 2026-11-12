using System.Collections.Generic;
using System.Diagnostics;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Strategic obstacles S1 (`feature/strategic-zones`, plan §2 and §7): the zones a strategic prop may use (direct and cut corridors
    /// open, safe legs only under their planned arc), the planned arc against the real flight, and the <c>SafeLegsFly</c> check.
    /// No strategic prop is placed yet, so every checked course stays bit-identical to the reference captured before S1.
    /// </summary>
    public class StrategicZoneTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        // --- The planned arc -------------------------------------------------------------------------------------------------

        [Test]
        public void ArcHeightModelMatchesSimulation()
        {
            // Flat ground: the planned power of a leg as long as a power's carry is that power, and the arc's height matches the
            // real arc's all along, within 2 cm.
            var flat = PlaneBallGround.All(CourseSurface.Fairway);
            for (int i = 0; i <= 10; i++)
            {
                float power = i / 10f;
                var ball = new BallSimulation(flat, Shot, Vector2.zero);
                BallisticArc real = ball.ArcFor(Vector2.right, power);
                float carry = Shot.CarryFor(power);
                var planned = new PlannedArc(Vector2.zero, 0f, new Vector2(carry, 0f), 0f, Settings);
                Assert.That(planned.Power, Is.EqualTo(power).Within(1e-3f), $"power {power}");
                AssertSameHeights(real, planned, carry, $"power {power}");
            }

            // Uphill and downhill legs: the arc at the planned power comes down at the leg's end, within 2 cm, and matches the
            // planned heights along the way.
            foreach (float tilt in new[] { -0.08f, -0.03f, 0.03f, 0.08f })
            {
                var plane = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(tilt, 0f));
                foreach (float length in new[] { 8f, 15f, 22f })
                {
                    var planned = new PlannedArc(Vector2.zero, 0f, new Vector2(length, 0f), length * tilt, Settings);
                    Assume.That(planned.Power, Is.GreaterThan(0f).And.LessThan(1f));
                    var ball = new BallSimulation(plane, Shot, Vector2.zero);
                    BallisticArc real = ball.ArcFor(Vector2.right, planned.Power);
                    float time = length / real.Velocity.x;
                    Assert.That(real.PositionAt(time).y, Is.EqualTo(length * tilt).Within(0.02f), $"tilt {tilt} length {length}");
                    AssertSameHeights(real, planned, length, $"tilt {tilt} length {length}");
                }
            }
        }

        private static void AssertSameHeights(BallisticArc real, PlannedArc planned, float length, string what)
        {
            for (int s = 0; s <= 20; s++)
            {
                float along = length * s / 20f;
                float time = along / real.Velocity.x;
                Assert.That(planned.HeightAt(along), Is.EqualTo(real.PositionAt(time).y).Within(0.02f), $"{what}, {along:F2} m along");
            }
        }

        [Test]
        public void SafeLegStartsAreOnFairway()
        {
            // The planned arc uses the fairway's carry (scale 1): it holds only if every leg is played from fairway.
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < CheckedCourseReferenceTests.Seeds; seed++)
                {
                    CourseLayout layout = CheckedCourse.Generate(seed, par, Settings, Shot).Layout;
                    var ground = new BallGround(layout);
                    IReadOnlyList<Vector2> route = layout.Plan.SafeRoute;
                    for (int k = 0; k + 1 < route.Count; k++)
                    {
                        Assert.That(ground.Surface(route[k].x, route[k].y), Is.EqualTo(CourseSurface.Fairway), $"seed {seed} par {par} leg {k}");
                    }
                }
            }
        }

        // --- Zones -----------------------------------------------------------------------------------------------------------

        [Test]
        public void StrategicKeepOuts()
        {
            var rules = Settings.Props;
            int openedOnDirectOrCut = 0;
            int openedUnderSafeLeg = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 10; seed++)
                {
                    CourseLayout layout = CheckedCourse.Generate(seed, par, Settings, Shot).Layout;
                    var zones = new PropZones(layout, Settings);
                    var fields = new CourseFields(layout);

                    // A safe leg: a low prop under its middle passes under the arc, a tall one doesn't, and neither does a low one at
                    // the take-off or the landing.
                    for (int i = 0; i < zones.Lines.Count; i++)
                    {
                        if (zones.Kinds[i] != ShotLineKind.Safe)
                        {
                            continue;
                        }

                        PlannedArc arc = zones.SafeArc(i);
                        Vector2 at(float along) => arc.From + (arc.Direction * along);
                        Assert.That(zones.UnderSafeArc(i, at(0.4f), 0.3f, 0.5f), Is.False, $"seed {seed} par {par} take-off");
                        Assert.That(zones.UnderSafeArc(i, at(arc.Length - 0.4f), 0.3f, 0.5f), Is.False, $"seed {seed} par {par} landing");
                        Assert.That(zones.UnderSafeArc(i, at(arc.Length * 0.5f), 0.3f, 12f), Is.False, $"seed {seed} par {par} tall");
                        if (arc.Length > 10f && arc.Power > 0.3f)
                        {
                            Assert.That(zones.UnderSafeArc(i, at(arc.Length * 0.5f), 0.3f, 0.5f), Is.True, $"seed {seed} par {par} low, mid-leg");
                        }
                    }

                    // On a 0.5 m grid: the strategic rule is never stricter than the strict one, it keeps every capsule, the tee and
                    // the green, and inside a safe leg's corridor it only lets in what passes under the arc.
                    Vector2 half = layout.HalfExtent;
                    for (float x = -half.x; x <= half.x; x += 0.5f)
                    {
                        for (float z = -half.y; z <= half.y; z += 0.5f)
                        {
                            var p = new Vector2(x, z);
                            const float radius = 0.5f;
                            const float height = 1f;
                            bool strict = zones.ClearOfPlay(p, radius, height);
                            bool strategic = zones.StrategicClearOfPlay(p, radius, height);
                            if (strict)
                            {
                                Assert.That(strategic, Is.True, $"seed {seed} par {par} at {p}: stricter than the strict rule");
                                continue;
                            }

                            if (!strategic)
                            {
                                continue;
                            }

                            Assert.That(Vector2.Distance(p, layout.Tee), Is.GreaterThanOrEqualTo(rules.TeeKeepOut + radius - 0.011f));
                            foreach (Vector2[] capsule in zones.Capsules)
                            {
                                for (int k = 0; k + 1 < capsule.Length; k++)
                                {
                                    Assert.That(Geo.DistanceToSegment(p, capsule[k], capsule[k + 1]), Is.GreaterThanOrEqualTo(zones.CapsuleHalfWidth + radius - 0.011f), $"seed {seed} par {par} capsule at {p}");
                                }
                            }

                            Assert.That(fields.Green(Geo.At(p)), Is.GreaterThan(0f), $"seed {seed} par {par} on the green at {p}");

                            bool inSafe = false;
                            bool inOther = false;
                            for (int i = 0; i < zones.Lines.Count; i++)
                            {
                                // The rule compares whole centimetres: a point within a centimetre of the corridor's edge may go either way.
                                bool inCorridor = Geo.DistanceToSegment(p, zones.Lines[i][0], zones.Lines[i][1]) < zones.Corridor + radius - 0.011f;
                                if (!inCorridor)
                                {
                                    continue;
                                }

                                if (zones.Kinds[i] == ShotLineKind.Safe)
                                {
                                    inSafe = true;
                                    Assert.That(zones.UnderSafeArc(i, p, radius, height), Is.True, $"seed {seed} par {par} safe leg {i} at {p}");
                                }
                                else
                                {
                                    inOther = true;
                                }
                            }

                            openedUnderSafeLeg += inSafe ? 1 : 0;
                            openedOnDirectOrCut += inOther && !inSafe ? 1 : 0;
                        }
                    }
                }
            }

            // Both openings exist somewhere on these 30 courses.
            Assert.That(openedOnDirectOrCut, Is.GreaterThan(0), "no spot opened on a direct or cut corridor only");
            Assert.That(openedUnderSafeLeg, Is.GreaterThan(0), "no spot opened under a safe leg");
        }

        // --- SafeLegsFly ------------------------------------------------------------------------------------------------------

        [Test]
        public void SafeLegsFly_PassesOnCheckedCourses() => SafeLegsFlyPasses(CheckedCourseReferenceTests.Seeds);

        [Test]
        [Explicit("Full sweep, run by hand only when the director asks")]
        [Category("FullSweep")]
        public void SafeLegsFly_PassesOnCheckedCourses_FullSweep() => SafeLegsFlyPasses(200);

        private static void SafeLegsFlyPasses(int seeds)
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CheckedCourse.Generate(seed, par, Settings, Shot).Layout;
                    var ground = new BallGround(layout);
                    BallObstacles obstacles = BallObstacles.ForCourse(layout.PropShapes, ground, Shot);
                    var struck = new List<int>();
                    Assert.That(CourseChecks.SafeLegsFly(ground, layout.Plan, Shot, Settings, obstacles, struck), Is.True, $"seed {seed} par {par} struck {struck.Count}");
                }
            }
        }

        [Test]
        public void SafeLegsFly_FailsOnAPropBeforeALanding_AndDropsIt()
        {
            // A tall stone block 1.5 m short of a leg's landing, across the leg: the falling ball strikes it. The check fails, names
            // the block, and KeepProps drops it, not the course.
            CheckedCourseResult result = CheckedCourse.Generate(0, 4, Settings, Shot);
            CourseLayout layout = result.Layout;
            Vector2 from = layout.Plan.SafeRoute[0];
            Vector2 to = layout.Plan.SafeRoute[1];
            Vector2 along = (to - from).normalized;
            Vector2 at = to - (along * 1.5f);
            var fields = new CourseFields(layout);
            var block = new CourseProp(PropKind.Wall, new Vector3(at.x, 0f, at.y), new Vector3(at.x, 0f, at.y), 1f, 0, 99,
                new[] { PropShape.Box(new Vector3(at.x, fields.Height(at.x, at.y) + 1.5f, at.y), new Vector3(0.2f, 1.5f, 2f), Mathf.Atan2(along.y, along.x), PropMaterial.Stone) });
            CourseLayout blocked = layout.WithProps(new List<CourseProp>(layout.Props) { block });

            var ground = new BallGround(blocked);
            var struck = new List<int>();
            Assert.That(CourseChecks.SafeLegsFly(ground, blocked.Plan, Shot, Settings, BallObstacles.ForCourse(blocked.PropShapes, ground, Shot), struck), Is.False);
            Assert.That(struck, Has.Some.Matches<int>(shape => blocked.Props[blocked.PropOfShape(shape)].Cluster == 99));
            Assert.That(CourseChecks.Run(blocked, Shot, Settings, true) & CourseCheck.SafeLegHit, Is.EqualTo(CourseCheck.SafeLegHit));
            Assert.That(CourseChecks.Run(blocked, Shot, Settings), Is.EqualTo(CourseCheck.None), "the bare check doesn't play props");

            CourseLayout kept = CheckedCourse.KeepProps(blocked, CourseCheck.None, Shot, Settings, 0, 4, out int dropped);
            Assert.That(dropped, Is.GreaterThanOrEqualTo(1));
            Assert.That(kept.Props, Has.None.Matches<CourseProp>(p => p.Cluster == 99));
            Assert.That(CourseChecks.Run(kept, Shot, Settings, true), Is.EqualTo(CourseCheck.None));
        }

        // S1's PlainCoursesBitIdentical held every checked course to `CheckedCourseReferenceBefore` while nothing strategic was placed.
        // S2 places the strategic group, so that guard moved to `StrategicPropTests.OtherPropsBitIdentical`, which leaves the
        // group (and on fork courses the spine, Q7 B) aside.

        // --- Measurement ------------------------------------------------------------------------------------------------------

        [Test]
        [Explicit("Measurement")]
        [Category("Measurement")]
        public void SafeLegsFlyCost()
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                double check = 0;
                double generate = 0;
                int legs = 0;
                var clock = new Stopwatch();
                for (int seed = 0; seed < CheckedCourseReferenceTests.Seeds; seed++)
                {
                    clock.Restart();
                    CourseLayout layout = CheckedCourse.Generate(seed, par, Settings, Shot).Layout;
                    generate += clock.Elapsed.TotalMilliseconds;

                    var ground = new BallGround(layout);
                    BallObstacles obstacles = BallObstacles.ForCourse(layout.PropShapes, ground, Shot);
                    clock.Restart();
                    CourseChecks.SafeLegsFly(ground, layout.Plan, Shot, Settings, obstacles);
                    check += clock.Elapsed.TotalMilliseconds;
                    legs += layout.Plan.SafeRoute.Count - 1;
                }

                int n = CheckedCourseReferenceTests.Seeds;
                Debug.Log($"SAFE_LEGS_FLY_COST par {par}: {legs} legs on {n} courses, check {check / n:F3} ms per course, checked generation {generate / n:F1} ms per course (Editor)");
            }
        }
    }
}
