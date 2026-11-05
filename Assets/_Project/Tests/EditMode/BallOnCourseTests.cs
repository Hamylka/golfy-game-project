using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The ball on the real par 3 and real settings: the ground that ships, including its pond and edge.</summary>
    public class BallOnCourseTests
    {
        private const int StepLimit = 60 * 120;

        private ShotSettings _settings;
        private CourseShapeSettings _shape;
        private CourseFields _fields;
        private BallGround _ground;

        [SetUp]
        public void SetUp()
        {
            _settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            _shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
            _fields = new CourseFields(_shape);
            _ground = new BallGround(_shape);
        }

        private void RunUntilSettled(BallSimulation ball, string context)
        {
            for (int i = 0; i < StepLimit && ball.State != BallState.Resting && ball.State != BallState.Holed; i++)
            {
                ball.Step();
                Assert.That(float.IsNaN(ball.Position.x) || float.IsNaN(ball.Position.y) || float.IsNaN(ball.Position.z), Is.False, context);
            }

            Assert.That(ball.State == BallState.Resting || ball.State == BallState.Holed, $"Still {ball.State}: {context}");
        }

        [Test]
        public void Surface_MatchesWhatTheTerrainDraws()
        {
            var palette = ScriptableObject.CreateInstance<PaletteSettings>();
            TerrainResult terrain = TerrainBuilder.Build(_shape, palette);
            Object.DestroyImmediate(palette);

            // The mesh cuts each 1 m cell's border as a straight chord across the outline's wobble, so a land
            // piece can reach a little way past the field's border. Measured on this course, that's 0.094 of
            // the pond's field, or at most 27 cm along the ground. This keeps it from growing unnoticed.
            const float LandPastThePondsOutline = 0.1f;

            // The middle of each piece.
            for (int i = 0; i < terrain.Water.Positions.Count; i += 3)
            {
                Vector3 center = (terrain.Water.Positions[i] + terrain.Water.Positions[i + 1] + terrain.Water.Positions[i + 2]) / 3f;
                Assert.That(_ground.Surface(center.x, center.z), Is.EqualTo(CourseSurface.Water), $"water at {center}");
            }

            int checkedLand = 0;
            for (int i = 0; i < terrain.Land.Positions.Count; i += 3)
            {
                Vector3 a = terrain.Land.Positions[i];
                Vector3 b = terrain.Land.Positions[i + 1];
                Vector3 c = terrain.Land.Positions[i + 2];

                // Only ground facets: the land mesh also holds near-vertical walls under its edges.
                if (Vector3.Cross(b - a, c - a).normalized.y < 0.7f)
                {
                    continue;
                }

                Vector3 center = (a + b + c) / 3f;
                Assert.That(_fields.Pond(center), Is.GreaterThan(-LandPastThePondsOutline), $"land at {center}");
                Assert.That(_ground.Surface(center.x, center.z), Is.Not.EqualTo(CourseSurface.OffIsland), $"land at {center}");
                checkedLand++;
            }

            Assert.That(checkedLand, Is.GreaterThan(500));
        }

        [Test]
        public void Surface_KnowsTheTeeTheHoleAndTheHazards()
        {
            Assert.That(_ground.Surface(_shape.Tee.x, _shape.Tee.y), Is.EqualTo(CourseSurface.Fairway));
            Assert.That(_ground.Surface(_shape.Hole.x, _shape.Hole.y), Is.EqualTo(CourseSurface.Green));
            Assert.That(_ground.Surface(_shape.Pond.Center.x, _shape.Pond.Center.y), Is.EqualTo(CourseSurface.Water));
            Assert.That(_ground.Surface(_shape.Bunker.Center.x, _shape.Bunker.Center.y), Is.EqualTo(CourseSurface.Bunker));
            Assert.That(_ground.Surface(0f, 30f), Is.EqualTo(CourseSurface.OffIsland));
        }

        [Test]
        public void Pond_IsDecidedByItsOutline_BecauseTheHeightWouldNotShowIt()
        {
            Vector2 center = _shape.Pond.Center;

            // Why the outline rules: the height field runs on above the water as if the pond were dry.
            Assert.That(_ground.Height(center.x, center.y), Is.GreaterThan(_shape.WaterLevel));
            Assert.That(_ground.LandingHeight(center.x, center.y), Is.EqualTo(_shape.WaterLevel));
        }

        [Test]
        public void TeeShot_TowardTheHole_LandsOnTheIsland()
        {
            var ball = new BallSimulation(_ground, _settings, _ground.Tee);
            Vector2 direction = _ground.Hole - _ground.Tee;

            for (float power = 0f; power <= 1f; power += 0.1f)
            {
                ArcLanding landing = ball.ArcFor(direction, power).FindLanding(_ground, _settings.LandingSearchStep);

                Assert.That(landing.Surface, Is.Not.EqualTo(CourseSurface.OffIsland), $"power {power}");
            }
        }

        [Test]
        public void FullTeeShot_TowardTheHole_ReachesTheGreensSideOfTheCourse()
        {
            var ball = new BallSimulation(_ground, _settings, _ground.Tee);

            ball.Shoot(_ground.Hole - _ground.Tee, 1f);
            RunUntilSettled(ball, "full tee shot");

            // A par 3: one full shot should leave a putt or a short arc, not a second long shot.
            Assert.That(ball.Position.x, Is.GreaterThan(_shape.Green.Center.x - _shape.Green.Radius.x - 3f));
        }

        [Test]
        public void Shot_FromTheRealBunker_CarriesLessThanFromTheFairwayBesideIt()
        {
            Vector2 sand = _shape.Bunker.Center;
            var fairway = new Vector2(sand.x, 0f);
            Assert.That(_ground.Surface(sand.x, sand.y), Is.EqualTo(CourseSurface.Bunker));
            Assert.That(_ground.Surface(fairway.x, fairway.y), Is.EqualTo(CourseSurface.Fairway));

            float fromSand = new BallSimulation(_ground, _settings, sand).ArcFor(Vector2.left, 1f).Velocity.magnitude;
            float fromFairway = new BallSimulation(_ground, _settings, fairway).ArcFor(Vector2.left, 1f).Velocity.magnitude;

            Assert.That(fromSand, Is.LessThan(fromFairway));
        }

        [Test]
        public void Putt_FromTheGreen_CanFindTheHole()
        {
            // Close, on the side the green rises from, aimed straight: the hole's flat area holds it.
            var start = new Vector2(_shape.Hole.x - 1f, _shape.Hole.y);
            var ball = new BallSimulation(_ground, _settings, start);
            Assert.That(ball.IsPutt, Is.True);

            ball.Shoot(_ground.Hole - start, 0.1f);
            RunUntilSettled(ball, "short putt");

            Assert.That(ball.State, Is.EqualTo(BallState.Holed));
        }

        [Test]
        public void Putt_OnTheGreensTilt_BreaksDownhill()
        {
            // Aimed along the contour, so any movement across it comes from the slope alone.
            var start = new Vector2(11f, -1f);
            Vector2 acrossTilt = new Vector2(_shape.GreenTilt.y, -_shape.GreenTilt.x).normalized;
            var ball = new BallSimulation(_ground, _settings, start);
            Assert.That(ball.IsPutt, Is.True);

            ball.Shoot(acrossTilt, 0.2f);
            RunUntilSettled(ball, "putt along the contour");

            Vector2 travelled = new Vector2(ball.Position.x, ball.Position.z) - start;
            Assert.That(Vector2.Dot(travelled, _shape.GreenTilt.normalized), Is.LessThan(-0.01f));
        }

        [Test]
        public void Shot_IntoThePond_IsLost_AndReturns()
        {
            var start = new Vector2(_shape.Pond.Center.x - 5f, 0f);
            Vector2 toPond = _shape.Pond.Center - start;
            var ball = new BallSimulation(_ground, _settings, start);
            Vector3 origin = ball.Position;

            float power = -1f;
            for (float p = 0f; p <= 1f && power < 0f; p += 0.01f)
            {
                ArcLanding landing = ball.ArcFor(toPond, p).FindLanding(_ground, _settings.LandingSearchStep);
                if (landing.Surface == CourseSurface.Water && landing.Point.y == _shape.WaterLevel)
                {
                    power = p;
                }
            }

            Assert.That(power, Is.GreaterThan(0f), "No power lands in the pond from here.");
            ball.Shoot(toPond, power);
            for (int i = 0; i < StepLimit && ball.State == BallState.Flying; i++)
            {
                ball.Step();
            }

            Assert.That(ball.State, Is.EqualTo(BallState.InWater));
            RunUntilSettled(ball, "ball in the pond");
            Assert.That(ball.Position, Is.EqualTo(origin));
        }

        [Test]
        public void Shot_OverTheEdge_IsLost_AndReturns()
        {
            var start = new Vector2(0f, -2f);
            var ball = new BallSimulation(_ground, _settings, start);
            Vector3 origin = ball.Position;

            ball.Shoot(Vector2.down, 1f);
            bool wentOff = false;
            for (int i = 0; i < StepLimit && ball.State != BallState.Resting; i++)
            {
                ball.Step();
                wentOff |= ball.State == BallState.OffIsland;
            }

            Assert.That(wentOff, Is.True);
            Assert.That(ball.Position, Is.EqualTo(origin));
        }

        [Test]
        public void EveryShot_FromAroundTheCourse_EndsInASettledState()
        {
            Vector2[] starts =
            {
                _shape.Tee,
                new Vector2(-6f, 0.5f),
                new Vector2(6f, -0.5f),
                new Vector2(0f, 3.5f),
                new Vector2(11f, -1f),
                _shape.Bunker.Center,
            };

            foreach (Vector2 start in starts)
            {
                for (int degrees = 0; degrees < 360; degrees += 30)
                {
                    float radians = degrees * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    foreach (float power in new[] { 0.1f, 0.5f, 1f })
                    {
                        var ball = new BallSimulation(_ground, _settings, start);
                        ball.Shoot(direction, power);
                        RunUntilSettled(ball, $"from {start}, {degrees} degrees, power {power}");
                    }
                }
            }
        }

        [Test]
        public void RestingBall_IsOnDryLandOnTheIsland()
        {
            foreach (float power in new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f })
            {
                var ball = new BallSimulation(_ground, _settings, _ground.Tee);
                ball.Shoot(_ground.Hole - _ground.Tee, power);
                RunUntilSettled(ball, $"power {power}");

                CourseSurface surface = _ground.Surface(ball.Position.x, ball.Position.z);
                Assert.That(surface, Is.Not.EqualTo(CourseSurface.Water));
                Assert.That(surface, Is.Not.EqualTo(CourseSurface.OffIsland));
                Assert.That(ball.Position.y, Is.EqualTo(_ground.Height(ball.Position.x, ball.Position.z)).Within(0.0001f));
            }
        }
    }
}
