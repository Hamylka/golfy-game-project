using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The ball on exact planes, where each rule can be checked alone. The real course has its own tests.</summary>
    public class BallSimulationTests
    {
        private const int StepLimit = 60 * 120;

        private ShotSettings _settings;

        [SetUp]
        public void SetUp()
        {
            // The real asset, so these rules are checked against the values that ship.
            _settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
        }

        private void RunUntilSettled(BallSimulation ball)
        {
            for (int i = 0; i < StepLimit && ball.State != BallState.Resting && ball.State != BallState.Holed; i++)
            {
                ball.Step();
            }

            Assert.That(ball.State == BallState.Resting || ball.State == BallState.Holed, $"Still {ball.State} after {StepLimit} steps");
        }

        private float PuttDistance(Vector2 tilt, Vector2 direction, float power)
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Green, tilt), _settings, Vector2.zero);
            ball.Shoot(direction, power);
            RunUntilSettled(ball);
            return new Vector2(ball.Position.x, ball.Position.z).magnitude;
        }

        private float ShotDistance(CourseSurface surface)
        {
            // Played from fairway, so it's an arc wherever it lands. The surface starts well short of the landing.
            var ground = new PlaneBallGround(Vector2.zero, (x, z) => x < 1f ? CourseSurface.Fairway : surface);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);
            ball.Shoot(Vector2.right, 0.5f);
            RunUntilSettled(ball);
            return ball.Position.x;
        }

        [Test]
        public void Ball_StartsAtRestOnTheGround()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.1f, 0f));

            var ball = new BallSimulation(ground, _settings, new Vector2(5f, 2f));

            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
            Assert.That(ball.Position, Is.EqualTo(new Vector3(5f, 0.5f, 2f)));
        }

        [Test]
        public void Flight_FollowsItsArcExactly_AndLandsWhereThePreviewSays()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.05f, -0.02f));
            var ball = new BallSimulation(ground, _settings, Vector2.zero);
            var direction = new Vector2(1f, 0.4f);

            // What the aim preview will read, before the shot exists.
            BallisticArc preview = ball.ArcFor(direction, 0.7f);
            ArcLanding landing = preview.FindLanding(ground, _settings.LandingSearchStep);

            Assert.That(ball.Shoot(direction, 0.7f), Is.True);
            int steps = 0;
            while (ball.State == BallState.Flying && ball.Bounces == 0)
            {
                ball.Step();
                steps++;
                float time = steps * _settings.FixedTimeStep;
                if (time < landing.Time)
                {
                    Assert.That(ball.Position, Is.EqualTo(preview.PositionAt(time)), $"at step {steps}");
                }
                else
                {
                    Assert.That(ball.Position, Is.EqualTo(landing.Point), "at landing");
                    break;
                }
            }

            Assert.That(steps * _settings.FixedTimeStep, Is.EqualTo(landing.Time).Within(_settings.FixedTimeStep));
        }

        [Test]
        public void Flight_NeverGoesBelowTheGround()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.1f, 0.05f));
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 1f);
            for (int i = 0; i < StepLimit && ball.State != BallState.Resting; i++)
            {
                ball.Step();
                Assert.That(ball.Position.y, Is.GreaterThanOrEqualTo(ground.Height(ball.Position.x, ball.Position.z) - 0.0001f));
            }
        }

        [Test]
        public void Shot_FliesThenBouncesThenRollsThenRests()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Fairway), _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 1f);
            Assert.That(ball.State, Is.EqualTo(BallState.Flying));

            int largestBounces = 0;
            bool rolled = false;
            for (int i = 0; i < StepLimit && ball.State != BallState.Resting; i++)
            {
                ball.Step();
                largestBounces = Mathf.Max(largestBounces, ball.Bounces);
                rolled |= ball.State == BallState.Rolling;
                if (rolled)
                {
                    Assert.That(ball.State, Is.Not.EqualTo(BallState.Flying), "Once rolling, it stays on the ground.");
                }
            }

            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
            Assert.That(largestBounces, Is.GreaterThan(0));
            Assert.That(largestBounces, Is.LessThanOrEqualTo(_settings.MaxBounces));
            Assert.That(rolled, Is.True);
            Assert.That(ball.Position.x, Is.GreaterThan(_settings.CarryFor(1f)), "Bounces and roll add to the carry.");
            Assert.That(ball.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Bounces_GetLower()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Fairway), _settings, Vector2.zero);
            ball.Shoot(Vector2.right, 1f);

            float[] highest = new float[_settings.MaxBounces + 1];
            for (int i = 0; i < StepLimit && ball.State != BallState.Resting; i++)
            {
                ball.Step();
                highest[ball.Bounces] = Mathf.Max(highest[ball.Bounces], ball.Position.y);
            }

            Assert.That(highest[1], Is.GreaterThan(0f));
            Assert.That(highest[1], Is.LessThan(highest[0]));
        }

        [Test]
        public void Bounce_OnASlope_KicksTheBallDownIt()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0f, 0.1f));
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 1f);
            while (ball.Bounces == 0 && ball.State == BallState.Flying)
            {
                ball.Step();
            }

            Assert.That(ball.Velocity.z, Is.LessThan(0f), "The ground rises toward +Z, so the kick is toward -Z.");
        }

        [Test]
        public void Putt_OnLevelGreen_RollsTheDistanceThePowerSays()
        {
            // Within about one step's travel of the formula: integration rounds each way a little.
            Assert.That(PuttDistance(Vector2.zero, Vector2.right, 0.5f), Is.EqualTo(_settings.PuttDistanceFor(0.5f)).Within(0.1f));
            Assert.That(PuttDistance(Vector2.zero, Vector2.right, 1f), Is.EqualTo(_settings.PuttDistanceFor(1f)).Within(0.1f));
        }

        [Test]
        public void Putt_StaysOnTheGroundEveryStep()
        {
            var ground = PlaneBallGround.All(CourseSurface.Green, new Vector2(0.04f, 0.02f));
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            Assert.That(ball.IsPutt, Is.True);
            ball.Shoot(Vector2.right, 0.6f);
            Assert.That(ball.State, Is.EqualTo(BallState.Rolling));
            while (ball.State == BallState.Rolling)
            {
                ball.Step();
                Assert.That(ball.Position.y, Is.EqualTo(ground.Height(ball.Position.x, ball.Position.z)).Within(0.0001f));
            }
        }

        [Test]
        public void GreensBorder_IsStrict_ShotsJustOutsideAreArcs()
        {
            var ground = new PlaneBallGround(Vector2.zero, (x, z) => x < 0f ? CourseSurface.Fairway : CourseSurface.Green);

            var outside = new BallSimulation(ground, _settings, new Vector2(-0.01f, 0f));
            var inside = new BallSimulation(ground, _settings, new Vector2(0.01f, 0f));
            outside.Shoot(Vector2.right, 0f);
            inside.Shoot(Vector2.right, 0f);

            Assert.That(outside.IsPutt, Is.False);
            Assert.That(outside.State, Is.EqualTo(BallState.Flying));
            Assert.That(inside.IsPutt, Is.True);
            Assert.That(inside.State, Is.EqualTo(BallState.Rolling));
        }

        [Test]
        public void Putt_TravelsFurtherDownhill_AndLessUphill()
        {
            float level = PuttDistance(Vector2.zero, Vector2.right, 0.5f);
            float uphill = PuttDistance(new Vector2(0.05f, 0f), Vector2.right, 0.5f);
            float downhill = PuttDistance(new Vector2(-0.05f, 0f), Vector2.right, 0.5f);

            Assert.That(uphill, Is.LessThan(level * 0.9f));
            Assert.That(downhill, Is.GreaterThan(level * 1.1f));
        }

        [Test]
        public void Putt_CurvesDownASideSlope()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Green, new Vector2(0f, 0.05f)), _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.5f);
            var headings = new System.Collections.Generic.List<float>();
            while (ball.State == BallState.Rolling)
            {
                ball.Step();
                if (ball.State == BallState.Rolling)
                {
                    headings.Add(Mathf.Atan2(-ball.Velocity.z, ball.Velocity.x) * Mathf.Rad2Deg);
                }
            }

            Assert.That(ball.Position.z, Is.LessThan(-0.1f), "The ground rises toward +Z, so the ball leans toward -Z.");

            // A straight drift would hold one heading. A curve keeps turning downhill.
            Assert.That(headings[headings.Count / 4], Is.GreaterThan(0f));
            Assert.That(headings[headings.Count / 2], Is.GreaterThan(headings[headings.Count / 4]));
            Assert.That(headings[headings.Count * 3 / 4], Is.GreaterThan(headings[headings.Count / 2]));
        }

        [Test]
        public void Ball_StopsOnASlopeFrictionCanHold_AndStaysThere()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Green, new Vector2(0.05f, 0f)), _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.3f);
            RunUntilSettled(ball);
            Vector3 resting = ball.Position;
            for (int i = 0; i < 120; i++)
            {
                ball.Step();
            }

            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
            Assert.That(ball.Position, Is.EqualTo(resting));
        }

        [Test]
        public void Ball_OnASlopeTooSteepToHold_RollsBackDown()
        {
            float deceleration = _settings.ResponseOf(CourseSurface.Rough).RollingDeceleration;
            float steep = (deceleration / _settings.SlopePull) * 1.5f;
            var ground = new PlaneBallGround(new Vector2(steep, 0f), (x, z) => CourseSurface.Rough);
            var ball = new BallSimulation(ground, _settings, new Vector2(0f, 0f));

            // Played uphill from the rough, so it lands, slows and turns around.
            ball.Shoot(Vector2.right, 0.1f);
            float furthest = 0f;
            for (int i = 0; i < 600; i++)
            {
                ball.Step();
                furthest = Mathf.Max(furthest, ball.Position.x);
            }

            Assert.That(ball.State, Is.EqualTo(BallState.Rolling));
            Assert.That(ball.Position.x, Is.LessThan(furthest - 0.1f));
        }

        [Test]
        public void Surfaces_BunkerStopsSoonest_ThenRough_ThenFairway()
        {
            float fairway = ShotDistance(CourseSurface.Fairway);
            float rough = ShotDistance(CourseSurface.Rough);
            float bunker = ShotDistance(CourseSurface.Bunker);

            Assert.That(rough, Is.LessThan(fairway));
            Assert.That(bunker, Is.LessThan(rough));
        }

        [Test]
        public void Bunker_TakesNoBounce()
        {
            var ground = new PlaneBallGround(Vector2.zero, (x, z) => x < 1f ? CourseSurface.Fairway : CourseSurface.Bunker);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.5f);
            RunUntilSettled(ball);

            Assert.That(ball.Bounces, Is.EqualTo(0));
        }

        [Test]
        public void Hole_CapturesASlowPutt()
        {
            var ground = PlaneBallGround.All(CourseSurface.Green);
            ground.Hole = new Vector2(2f, 0f);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.3f);
            RunUntilSettled(ball);

            Assert.That(ball.State, Is.EqualTo(BallState.Holed));
        }

        [Test]
        public void Hole_ABallThatIsTooFast_RunsOver()
        {
            var ground = PlaneBallGround.All(CourseSurface.Green);
            ground.Hole = new Vector2(0.5f, 0f);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 1f);
            RunUntilSettled(ball);

            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
            Assert.That(ball.Position.x, Is.GreaterThan(0.5f));
        }

        [Test]
        public void Hole_ABallThatStopsJustShort_IsNotCaptured()
        {
            float distance = _settings.PuttDistanceFor(0.1f);
            var ground = PlaneBallGround.All(CourseSurface.Green);
            ground.Hole = new Vector2(distance + _settings.HoleCaptureRadius + 0.5f, 0f);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.1f);
            RunUntilSettled(ball);

            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
        }

        [Test]
        public void Hole_TheBallSettlesBelowTheGround_AtItsCenter()
        {
            var ground = PlaneBallGround.All(CourseSurface.Green);
            ground.Hole = new Vector2(2f, 0f);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.3f);
            RunUntilSettled(ball);
            for (int i = 0; i < 120; i++)
            {
                ball.Step();
            }

            Assert.That(ball.Position, Is.EqualTo(new Vector3(2f, -_settings.HoleDepth, 0f)));
            Assert.That(ball.Shoot(Vector2.right, 1f), Is.False, "A holed ball can't be played.");
        }

        [Test]
        public void Water_ARollingBallIsLostAtTheOutline_ThoughTheGroundIsFlat()
        {
            var ground = new PlaneBallGround(Vector2.zero, (x, z) => x < 1f ? CourseSurface.Green : CourseSurface.Water);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 1f);
            while (ball.State == BallState.Rolling)
            {
                ball.Step();
            }

            Assert.That(ball.State, Is.EqualTo(BallState.InWater));
            Assert.That(ball.Position.x, Is.InRange(1f, 1.2f));
        }

        [Test]
        public void Water_ALandingBallIsLost_AndSinks()
        {
            var ground = new PlaneBallGround(Vector2.zero, (x, z) => x < 1f ? CourseSurface.Fairway : CourseSurface.Water);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 0.5f);
            while (ball.State == BallState.Flying)
            {
                ball.Step();
            }

            Assert.That(ball.State, Is.EqualTo(BallState.InWater));
            Assert.That(ball.Position.y, Is.EqualTo(PlaneBallGround.WaterLevel));
            ball.Step();
            Assert.That(ball.Position.y, Is.LessThan(PlaneBallGround.WaterLevel));
        }

        [Test]
        public void OffIsland_ARollingBallFallsWithTheSpeedItHad()
        {
            var ground = new PlaneBallGround(Vector2.zero, (x, z) => x < 1f ? CourseSurface.Green : CourseSurface.OffIsland);
            var ball = new BallSimulation(ground, _settings, Vector2.zero);

            ball.Shoot(Vector2.right, 1f);
            while (ball.State == BallState.Rolling)
            {
                ball.Step();
            }

            Assert.That(ball.State, Is.EqualTo(BallState.OffIsland));
            float speed = ball.Velocity.x;
            Assert.That(speed, Is.GreaterThan(0f));
            for (int i = 0; i < 10; i++)
            {
                ball.Step();
            }

            Assert.That(ball.Velocity.x, Is.EqualTo(speed));
            Assert.That(ball.Position.y, Is.LessThan(0f));
        }

        [TestCase(CourseSurface.Water)]
        [TestCase(CourseSurface.OffIsland)]
        public void LostBall_ReturnsToWhereTheShotWasPlayed_AfterTheDelay(CourseSurface hazard)
        {
            var ground = new PlaneBallGround(new Vector2(0.02f, 0f), (x, z) => x < 6f ? CourseSurface.Fairway : hazard);
            var start = new Vector2(3f, 0f);
            var ball = new BallSimulation(ground, _settings, start);
            Vector3 origin = ball.Position;

            ball.Shoot(Vector2.right, 0.5f);
            while (ball.State == BallState.Flying)
            {
                ball.Step();
            }

            Assert.That(ball.State, Is.Not.EqualTo(BallState.Resting));
            int steps = 0;
            while (ball.State != BallState.Resting)
            {
                ball.Step();
                steps++;
                Assert.That(steps, Is.LessThan(StepLimit));
            }

            Assert.That(steps * _settings.FixedTimeStep, Is.EqualTo(_settings.LostSeconds).Within(_settings.FixedTimeStep * 1.5f));
            Assert.That(ball.Position, Is.EqualTo(origin));
            Assert.That(ball.InterpolatedPosition, Is.EqualTo(origin), "Drawing must not sweep it across the course.");
            Assert.That(ball.Shoot(Vector2.right, 0.5f), Is.True, "It can be played again.");
        }

        [Test]
        public void Shoot_IsRefusedWhileTheBallMoves()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Fairway), _settings, Vector2.zero);

            Assert.That(ball.Shoot(Vector2.right, 0.5f), Is.True);
            BallisticArc arc = ball.ArcFor(Vector2.up, 0.5f);

            Assert.That(ball.Shoot(Vector2.up, 1f), Is.False);
            Assert.That(ball.State, Is.EqualTo(BallState.Flying));
            Assert.That(arc.Start, Is.EqualTo(ball.Position));
        }

        [Test]
        public void Shoot_WithNoDirection_IsRefused()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Fairway), _settings, Vector2.zero);

            Assert.That(ball.Shoot(Vector2.zero, 1f), Is.False);
            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
        }

        [Test]
        public void Advance_GivesTheSameResultAtAnyFrameRate()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.03f, 0.04f));
            var byStep = new BallSimulation(ground, _settings, Vector2.zero);
            var byUnevenFrames = new BallSimulation(ground, _settings, Vector2.zero);
            byStep.Shoot(Vector2.right, 0.8f);
            byUnevenFrames.Shoot(Vector2.right, 0.8f);

            RunUntilSettled(byStep);

            float[] frames = { 0.016f, 0.033f, 0.007f, 0.05f, 0.021f };
            for (int i = 0; i < StepLimit && byUnevenFrames.State != BallState.Resting; i++)
            {
                byUnevenFrames.Advance(frames[i % frames.Length]);
            }

            Assert.That(byUnevenFrames.Position, Is.EqualTo(byStep.Position));
        }

        [Test]
        public void InterpolatedPosition_LiesBetweenTheLastTwoSteps()
        {
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Green), _settings, Vector2.zero);
            ball.Shoot(Vector2.right, 1f);

            ball.Step();
            Vector3 first = ball.Position;
            ball.Step();
            Vector3 second = ball.Position;
            ball.Advance(_settings.FixedTimeStep * 0.5f);

            Assert.That(ball.InterpolatedPosition.x, Is.EqualTo((first.x + second.x) * 0.5f).Within(0.0001f));
        }

        [Test]
        public void SameShot_PlaysOutTheSameEveryTime()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.03f, -0.02f));
            var first = new BallSimulation(ground, _settings, Vector2.zero);
            var second = new BallSimulation(ground, _settings, Vector2.zero);
            first.Shoot(new Vector2(1f, 0.3f), 0.65f);
            second.Shoot(new Vector2(1f, 0.3f), 0.65f);

            for (int i = 0; i < 1200; i++)
            {
                first.Step();
                second.Step();
                Assert.That(second.Position, Is.EqualTo(first.Position), $"at step {i}");
            }
        }
    }
}
