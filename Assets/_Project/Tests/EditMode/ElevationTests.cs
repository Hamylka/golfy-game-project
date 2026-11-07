using System.Collections.Generic;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The seeded elevation: the profile curve, the leg blend at bends, the generated heights and tilts, and the ball on them.</summary>
    public class ElevationTests
    {
        private const int Seeds = 60;
        private const float Probe = 0.1f;

        // The green tilt fades out over about 3.6 m past the green, and the felt ground slopes more there than the tilt itself;
        // beyond this distance from the hole only the profile and camber are left.
        private const float RingDistance = 8.5f;

        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        private static readonly int[] Pars = { 3, 4, 5 };

        // ---- The profile curve -------------------------------------------------------------------------------------

        private static ElevationProfile Profile(params float[] heights)
        {
            var knots = new List<ElevationKnot>();
            for (int i = 0; i < heights.Length; i++)
            {
                knots.Add(new ElevationKnot(10f * i, heights[i], 0f));
            }

            return new ElevationProfile(knots, 4f);
        }

        [Test]
        public void Profile_Empty_AddsNothing()
        {
            Assert.That(ElevationProfile.Empty.IsEmpty, Is.True);
            Assert.That(ElevationProfile.Empty.Height(12f), Is.EqualTo(0f));
            Assert.That(ElevationProfile.Empty.At(12f, 3f), Is.EqualTo(0f));
        }

        [Test]
        public void Profile_IsFlatBeyondItsEndKnots_AndPassesThroughEveryKnot()
        {
            ElevationProfile profile = Profile(0f, 0.4f, -0.2f, 0.9f);
            Assert.That(profile.Height(-50f), Is.EqualTo(0f));
            Assert.That(profile.Height(80f), Is.EqualTo(0.9f));
            Assert.That(profile.Height(10f), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(profile.Height(20f), Is.EqualTo(-0.2f).Within(1e-5f));
            Assert.That(profile.Height(0.2f), Is.EqualTo(0f).Within(0.01f), "Zero tangent at the plateau.");
        }

        [Test]
        public void Profile_NeverOvershootsItsKnots()
        {
            ElevationProfile profile = Profile(0f, 0.4f, 0.4f, -0.3f, 0.5f);
            for (float s = 0f; s <= 40f; s += 0.05f)
            {
                Assert.That(profile.Height(s), Is.InRange(-0.3f - 1e-5f, 0.5f + 1e-5f), $"s = {s}");
            }

            // Between two equal knots it stays level instead of ringing.
            for (float s = 10f; s <= 20f; s += 0.05f)
            {
                Assert.That(profile.Height(s), Is.LessThanOrEqualTo(0.4f + 1e-4f), $"s = {s}");
            }
        }

        [Test]
        public void Profile_SlopeNeverExceedsTwiceAStepsRiseOverItsRun_AndHasNoCreases()
        {
            ElevationProfile profile = Profile(0f, 0.45f, 0.9f, 0.5f, 0.8f, 0.8f);
            const float Step = 0.01f;
            float largestSecant = 0.45f / 10f;
            float previousSlope = 0f;
            for (float s = 0f; s <= 50f; s += Step)
            {
                float slope = (profile.Height(s + Step) - profile.Height(s)) / Step;
                Assert.That(Mathf.Abs(slope), Is.LessThanOrEqualTo(2f * largestSecant + 1e-3f), $"s = {s}");
                Assert.That(Mathf.Abs(slope - previousSlope), Is.LessThan(0.01f), $"A crease at s = {s}");
                previousSlope = slope;
            }
        }

        [Test]
        public void Profile_ASteadyClimbHasNoTerraces()
        {
            ElevationProfile profile = Profile(0f, 0.3f, 0.6f, 0.9f, 1.2f);
            for (float s = 10f; s <= 30f; s += 0.1f)
            {
                float slope = (profile.Height(s + 0.1f) - profile.Height(s)) / 0.1f;
                Assert.That(slope, Is.GreaterThan(0.02f), $"A flat spot at s = {s}");
            }
        }

        // ---- The leg blend -----------------------------------------------------------------------------------------

        private static CoursePath Bend(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new CoursePath(new[] { Vector2.zero, new Vector2(20f, 0f), new Vector2(20f, 0f) + (new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 20f) });
        }

        [Test]
        public void Blend_OnAStraightPath_IsTheDistanceAlongAndAcross()
        {
            var path = new CoursePath(new[] { new Vector2(-3f, 1f), new Vector2(17f, 1f) });
            float seen = path.Blend(new Vector2(5f, 3f), 4f, (along, across) => (along * 100f) + across);
            Assert.That(seen, Is.EqualTo(800f + 2f).Within(1e-4f), "8 m along, 2 m to the left.");
        }

        [Test]
        public void Blend_AwayFromABend_IsTheNearestLegsOwnValue()
        {
            CoursePath path = Bend(35f);
            var point = new Vector2(8f, 1.5f);
            float blended = path.Blend(point, 4f, (along, across) => (along * 100f) + across);
            float along1 = path.Project(point, out float across1);
            Assert.That(blended, Is.EqualTo((along1 * 100f) + across1).Within(1e-3f));
        }

        [Test]
        public void Blend_StaysContinuousAroundABend_WhereProjectJumps()
        {
            CoursePath path = Bend(35f);
            float worstBlend = 0f;
            float worstProject = 0f;
            for (float z = 0f; z <= 14f; z += 0.02f)
            {
                var a = new Vector2(17.6f, z);
                var b = new Vector2(a.x, z + 0.02f);
                float blendA = path.Blend(a, 4f, (along, across) => along);
                float blendB = path.Blend(b, 4f, (along, across) => along);
                worstBlend = Mathf.Max(worstBlend, Mathf.Abs(blendB - blendA));
                float projectA = path.Project(a, out _);
                float projectB = path.Project(b, out _);
                worstProject = Mathf.Max(worstProject, Mathf.Abs(projectB - projectA));
            }

            Debug.Log($"Along-distance step per 2 cm across a 35 degree bend: blend {worstBlend:F4} m, nearest leg {worstProject:F4} m.");
            Assert.That(worstBlend, Is.LessThan(0.1f), "Blended: no step.");
            Assert.That(worstProject, Is.GreaterThan(1f), "The nearest leg alone jumps; this is what the blend fixes.");
        }

        // ---- Hand-made courses ---------------------------------------------------------------------------------------

        [Test]
        public void HandMadeCourse_HasAnEmptyProfile_AndNoElevation()
        {
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
            CourseLayout layout = shape.ToLayout();
            var fields = new CourseFields(layout);
            Assert.That(layout.Elevation.IsEmpty, Is.True);
            Assert.That(fields.Elevation(3f, 1f), Is.EqualTo(0f));
        }

        // ---- Generated courses ---------------------------------------------------------------------------------------

        private static CourseLayout Course(int seed, int par) => CourseGenerator.Generate(seed, par, Settings);

        [Test]
        public void SameSeed_GivesTheSameHeights()
        {
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < 8; seed++)
                {
                    Assert.That(Heights(Course(seed, par)), Is.EqualTo(Heights(Course(seed, par))), $"par {par} seed {seed}");
                }
            }

            Assert.That(Heights(Course(1, 4)), Is.Not.EqualTo(Heights(Course(2, 4))));
        }

        private static string Heights(CourseLayout layout)
        {
            var fields = new CourseFields(layout);
            var text = new StringBuilder();
            for (float x = -30f; x <= 30f; x += 3.3f)
            {
                for (float z = -6f; z <= 6f; z += 3.1f)
                {
                    text.Append(fields.Height(x, z).ToString("R")).Append(' ');
                }
            }

            return text.ToString();
        }

        [Test]
        public void Elevation_StaysWithinTheDrawnRanges()
        {
            foreach (int par in Pars)
            {
                ParRules rules = Settings.Rules(par);
                float relief = Mathf.Max(rules.FairwayRelief, Mathf.Max(Mathf.Abs(rules.GreenHeight.x), Mathf.Abs(rules.GreenHeight.y)));
                for (int seed = 0; seed < Seeds; seed++)
                {
                    ElevationProfile profile = Course(seed, par).Elevation;
                    Assert.That(profile.IsEmpty, Is.False);
                    Assert.That(profile.Knot(0).Height, Is.EqualTo(0f), "Tee knot.");
                    ElevationKnot green = profile.Knot(profile.KnotCount - 1);
                    Assert.That(Mathf.Abs(green.Height), Is.LessThanOrEqualTo(1.5f + 1e-4f), $"par {par} seed {seed}: green height");
                    Assert.That(green.Camber, Is.EqualTo(0f));
                    for (int k = 0; k < profile.KnotCount; k++)
                    {
                        Assert.That(Mathf.Abs(profile.Knot(k).Height), Is.LessThanOrEqualTo(relief + 1e-4f), $"par {par} seed {seed} knot {k}");
                        Assert.That(Mathf.Abs(profile.Knot(k).Camber), Is.LessThanOrEqualTo(Settings.MaxCamber * 0.01f + 1e-6f));
                    }
                }
            }
        }

        [Test]
        public void Elevation_MakesRealRisesAndDips()
        {
            foreach (int par in new[] { 4, 5 })
            {
                float widest = 0f;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    ElevationProfile profile = Course(seed, par).Elevation;
                    float low = 0f, high = 0f;
                    for (int k = 0; k < profile.KnotCount; k++)
                    {
                        low = Mathf.Min(low, profile.Knot(k).Height);
                        high = Mathf.Max(high, profile.Knot(k).Height);
                    }

                    widest = Mathf.Max(widest, high - low);
                }

                Assert.That(widest, Is.GreaterThan(1f), $"par {par}: at least one course should span over a metre of height");
            }
        }

        [Test]
        public void TeeIsFlat_AndSoIsTheGroundAroundTheHole()
        {
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    var fields = new CourseFields(layout);
                    Assert.That(MaxFeltGradient(fields, layout.Tee, layout.FlatRadius), Is.LessThan(0.005f), $"par {par} seed {seed}: tee");
                    Assert.That(MaxFeltGradient(fields, layout.Hole, 1.5f), Is.LessThan(0.01f), $"par {par} seed {seed}: around the hole, 1% across 1.5 m");
                }
            }
        }

        [Test]
        public void GreenTilt_IsTwoToSixPercent_OnTheFeltGround()
        {
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    var fields = new CourseFields(layout);
                    Vector2 tilt = layout.Greens[0].Tilt;
                    Assert.That(tilt.magnitude, Is.InRange(0.02f - 1e-4f, 0.06f + 1e-4f), $"par {par} seed {seed}");

                    // The tilt fades in around the hole, so its slope reaches full strength only a little way out: the steepest felt
                    // gradient over the green is the tilt itself, and never more.
                    float steepest = 0f;
                    for (float x = -layout.HalfExtent.x; x <= layout.HalfExtent.x; x += 0.25f)
                    {
                        for (float z = -layout.HalfExtent.y; z <= layout.HalfExtent.y; z += 0.25f)
                        {
                            if (fields.Green(new Vector3(x, 0f, z)) < -0.3f)
                            {
                                steepest = Mathf.Max(steepest, FeltGradient(fields, new Vector2(x, z)).magnitude);
                            }
                        }
                    }

                    Assert.That(steepest, Is.InRange(tilt.magnitude * 0.75f, tilt.magnitude + 0.005f), $"par {par} seed {seed}");
                }
            }
        }

        [Test]
        public void Gradient_OnTheFairwayAndGreen_StaysNearTheSlopeLimit()
        {
            foreach (int par in Pars)
            {
                float worstFairway = 0f;
                float worstGreen = 0f;
                float worstRing = 0f;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    var fields = new CourseFields(layout);
                    float extent = Mathf.Max(layout.HalfExtent.x, layout.HalfExtent.y);
                    for (float x = -extent; x <= extent; x += 0.5f)
                    {
                        for (float z = -extent; z <= extent; z += 0.5f)
                        {
                            var p = new Vector3(x, 0f, z);
                            if (fields.Island(p) >= -0.3f)
                            {
                                continue;
                            }

                            bool green = fields.Green(p) < 0f;
                            bool fairway = fields.Fairway(p) < 0f;
                            if (!green && !fairway)
                            {
                                continue;
                            }

                            float gradient = FeltGradient(fields, new Vector2(x, z)).magnitude;
                            if (green)
                            {
                                worstGreen = Mathf.Max(worstGreen, gradient);
                            }
                            else if (Vector2.Distance(new Vector2(x, z), layout.Hole) < RingDistance)
                            {
                                worstRing = Mathf.Max(worstRing, gradient);
                            }
                            else
                            {
                                worstFairway = Mathf.Max(worstFairway, gradient);
                            }
                        }
                    }
                }

                Debug.Log($"par {par}: steepest felt gradient over {Seeds} seeds, fairway {worstFairway * 100f:F1}%, green {worstGreen * 100f:F1}%, the green tilt's fade ring {worstRing * 100f:F1}%.");
                Assert.That(worstRing, Is.LessThan(0.16f), $"par {par} fade ring");
                Assert.That(worstFairway, Is.LessThan(0.115f), $"par {par} fairway");
                Assert.That(worstGreen, Is.LessThan(0.075f), $"par {par} green");
            }
        }

        [Test]
        public void Elevation_HasNoStepAnywhereOnTheIsland_EvenAtDoglegBends()
        {
            float worst = 0f;
            int bent = 0;
            foreach (int par in new[] { 4, 5 })
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    if (layout.Centreline.Points.Count < 3)
                    {
                        continue;
                    }

                    bent++;
                    var fields = new CourseFields(layout);
                    float extent = Mathf.Max(layout.HalfExtent.x, layout.HalfExtent.y);
                    for (float x = -extent; x <= extent; x += 0.25f)
                    {
                        for (float z = -extent; z <= extent; z += 0.25f)
                        {
                            if (fields.Island(new Vector3(x, 0f, z)) >= 0f)
                            {
                                continue;
                            }

                            float here = fields.Elevation(x, z);
                            worst = Mathf.Max(worst, Mathf.Abs(fields.Elevation(x + Probe, z) - here) / Probe, Mathf.Abs(fields.Elevation(x, z + Probe) - here) / Probe);
                        }
                    }
                }
            }

            Debug.Log($"Steepest broad slope anywhere on the island, {bent} bent courses: {worst * 100f:F1}%.");
            Assert.That(bent, Is.GreaterThan(10));
            Assert.That(worst, Is.LessThan(0.25f), "A step at a bend would show as a slope far beyond this.");
        }

        [Test]
        public void Skirt_KeepsItsDepthBelowTheEdge()
        {
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    var fields = new CourseFields(layout);
                    for (float x = -layout.HalfExtent.x; x <= layout.HalfExtent.x; x += 2.3f)
                    {
                        float depth = fields.Elevation(x, 1.1f) - fields.SkirtBottom(new Vector3(x, 0f, 1.1f));
                        Assert.That(depth, Is.InRange(layout.Edge.SkirtDepth.x - 1e-4f, layout.Edge.SkirtDepth.y + 1e-4f), $"par {par} seed {seed} x {x}");
                    }
                }
            }
        }

        private static Vector2 FeltGradient(CourseFields fields, Vector2 p)
        {
            return new Vector2(
                (fields.FeltHeight(p.x + Probe, p.y) - fields.FeltHeight(p.x - Probe, p.y)) / (2f * Probe),
                (fields.FeltHeight(p.x, p.y + Probe) - fields.FeltHeight(p.x, p.y - Probe)) / (2f * Probe));
        }

        private static float MaxFeltGradient(CourseFields fields, Vector2 centre, float radius)
        {
            float worst = 0f;
            for (float x = -radius; x <= radius; x += 0.25f)
            {
                for (float z = -radius; z <= radius; z += 0.25f)
                {
                    if (new Vector2(x, z).magnitude <= radius)
                    {
                        worst = Mathf.Max(worst, FeltGradient(fields, centre + new Vector2(x, z)).magnitude);
                    }
                }
            }

            return worst;
        }

        // ---- The ball on the elevated ground ---------------------------------------------------------------------------

        /// <summary>A long straight island along X whose whole top is green, so every shot is a putt, with this profile and green tilt.</summary>
        private static CourseLayout RampLayout(ElevationProfile profile, Vector2 tilt, Vector2 hole)
        {
            var tee = new Vector2(-40f, 0f);
            return new CourseLayout(
                new[] { new IslandOutline(60f, 20f, 0f, 0.2f) },
                new CoursePath(new[] { tee, new Vector2(40f, 0f) }),
                new FairwayLayout(-40f, 40f, 4f, 0f, 1f),
                new[] { new GreenLayout(new EllipseShape(hole, new Vector2(55f, 18f), 0f), tilt) },
                new EllipseShape[0],
                new PondLayout[0],
                new GroundSwell[0],
                -0.2f,
                new GroundRelief(1f, 0f, 0f, 1f, 1f),
                tee,
                hole,
                1.2f,
                0.8f,
                new EdgeLayout(new Vector2(0.9f, 1.8f), 0.7f),
                alongCentreline: true,
                elevation: profile);
        }

        private static ElevationProfile Climb()
        {
            // 9% on average from x = -40 to 40 along the centreline (the tee is at -40, so along = x + 40).
            return new ElevationProfile(new[] { new ElevationKnot(0f, 0f, 0f), new ElevationKnot(40f, 3.6f, 0f), new ElevationKnot(80f, 7.2f, 0f) }, 4f);
        }

        private static BallSimulation Ball(BallGround ground, Vector2 start)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            return new BallSimulation(ground, settings, start);
        }

        private static void Settle(BallSimulation ball, List<Vector3> path = null)
        {
            for (int i = 0; i < 60 * 120 && ball.State != BallState.Resting && ball.State != BallState.Holed; i++)
            {
                ball.Step();
                path?.Add(ball.Position);
            }

            Assert.That(ball.State == BallState.Resting || ball.State == BallState.Holed, Is.True, $"Still {ball.State}");
        }

        [Test]
        public void Ball_RollsFurtherDownA9PercentSlopeThanUpIt()
        {
            var ground = new BallGround(RampLayout(Climb(), Vector2.zero, new Vector2(1000f, 0f)));
            float Roll(Vector2 direction)
            {
                BallSimulation ball = Ball(ground, new Vector2(0f, 0f));
                ball.Shoot(direction, 0.5f);
                Settle(ball);
                return new Vector2(ball.Position.x, ball.Position.z).magnitude;
            }

            float up = Roll(Vector2.right);
            float down = Roll(Vector2.left);
            Debug.Log($"Putt at half power on a 9% slope: up {up:F2} m, down {down:F2} m.");
            Assert.That(down, Is.GreaterThan(up * 1.3f));
        }

        [Test]
        public void Ball_CurvesDownACamber()
        {
            // Constant 4% camber: the ground rises toward +Z, so a putt along +X leans toward -Z.
            var profile = new ElevationProfile(new[] { new ElevationKnot(0f, 0f, 0.04f), new ElevationKnot(80f, 0f, 0.04f) }, 4f);
            var ground = new BallGround(RampLayout(profile, Vector2.zero, new Vector2(1000f, 0f)));
            BallSimulation ball = Ball(ground, new Vector2(0f, 0f));
            ball.Shoot(Vector2.right, 0.5f);
            Settle(ball);
            Assert.That(ball.Position.z, Is.LessThan(-0.05f));
            Assert.That(ball.Position.x, Is.GreaterThan(2f));
        }

        [Test]
        public void Ball_BreaksOnATiltedGreen()
        {
            // Rising toward +Z at 6% from the hole: a putt away from the hole drifts toward -Z once past its flat area.
            var hole = new Vector2(0f, 0f);
            var ground = new BallGround(RampLayout(ElevationProfile.Empty, new Vector2(0f, 0.06f), hole));
            BallSimulation ball = Ball(ground, new Vector2(2.2f, 0f));
            ball.Shoot(Vector2.right, 0.5f);
            Settle(ball);
            Assert.That(ball.Position.z, Is.LessThan(-0.1f));
        }

        [Test]
        public void Ground_UnderTheBall_HasNoBumps()
        {
            // Second differences of the ball's height curve along a line over a 9% climb with a side slope.
            var profile = new ElevationProfile(new[] { new ElevationKnot(0f, 0f, 0f), new ElevationKnot(27f, 2.4f, 0.04f), new ElevationKnot(54f, 0.6f, -0.03f), new ElevationKnot(80f, 1.2f, 0f) }, 4f);
            var ground = new BallGround(RampLayout(profile, Vector2.zero, new Vector2(1000f, 0f)));
            const float D = 0.1f;
            float worst = 0f;
            for (float x = -35f; x <= 35f; x += 0.05f)
            {
                float second = ground.Height(x + D, 1.5f) - (2f * ground.Height(x, 1.5f)) + ground.Height(x - D, 1.5f);
                worst = Mathf.Max(worst, Mathf.Abs(second));
            }

            Debug.Log($"Largest second difference of the ground height at {D} m: {worst:F5} m.");
            Assert.That(worst, Is.LessThan(0.002f));
        }
    }
}
