using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class BallisticArcTests
    {
        private const float Gravity = 9.81f;
        private const float Angle = 40f;
        private const float SearchStep = 0.02f;

        private static BallisticArc ArcAlongX(float carry)
        {
            return BallisticArc.ForCarry(Vector3.zero, Vector2.right, carry, Angle, Gravity);
        }

        private static float LevelFlightTime(BallisticArc arc) => 2f * arc.Velocity.y / Gravity;

        [TestCase(1.5f)]
        [TestCase(10f)]
        [TestCase(22f)]
        public void ForCarry_TravelsThatFarOverLevelGround(float carry)
        {
            BallisticArc arc = ArcAlongX(carry);

            Vector3 end = arc.PositionAt(LevelFlightTime(arc));

            Assert.That(end.x, Is.EqualTo(carry).Within(0.001f));
            Assert.That(end.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ForCarry_LeavesAtTheLaunchAngle_InTheAimedDirection()
        {
            var direction = new Vector2(3f, 4f);
            BallisticArc arc = BallisticArc.ForCarry(Vector3.zero, direction, 10f, Angle, Gravity);

            var along = new Vector2(arc.Velocity.x, arc.Velocity.z);

            Assert.That(Mathf.Atan2(arc.Velocity.y, along.magnitude) * Mathf.Rad2Deg, Is.EqualTo(Angle).Within(0.01f));
            Assert.That(Vector2.Dot(along.normalized, direction.normalized), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Arc_IsABell_RisingAndFallingTheSame()
        {
            BallisticArc arc = ArcAlongX(20f);
            float total = LevelFlightTime(arc);

            for (float t = 0f; t < total * 0.5f; t += 0.1f)
            {
                Assert.That(arc.PositionAt(t).y, Is.EqualTo(arc.PositionAt(total - t).y).Within(0.001f));
            }
        }

        [Test]
        public void Arc_ReachesItsApexHalfwayAlong()
        {
            BallisticArc arc = ArcAlongX(20f);
            float total = LevelFlightTime(arc);

            Vector3 middle = arc.PositionAt(total * 0.5f);

            Assert.That(middle.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(arc.VelocityAt(total * 0.5f).y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FindLanding_OnLevelGround_MatchesTheFormula()
        {
            BallisticArc arc = ArcAlongX(20f);

            ArcLanding landing = arc.FindLanding(PlaneBallGround.All(CourseSurface.Fairway), SearchStep);

            Assert.That(landing.Time, Is.EqualTo(LevelFlightTime(arc)).Within(0.001f));
            Assert.That(landing.Point.x, Is.EqualTo(20f).Within(0.02f));
            Assert.That(landing.Point.y, Is.EqualTo(0f));
            Assert.That(landing.Surface, Is.EqualTo(CourseSurface.Fairway));
        }

        [Test]
        public void FindLanding_LandsShortUphill_AndLongDownhill()
        {
            BallisticArc arc = ArcAlongX(20f);

            float level = arc.FindLanding(PlaneBallGround.All(CourseSurface.Fairway), SearchStep).Point.x;
            float uphill = arc.FindLanding(PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.1f, 0f)), SearchStep).Point.x;
            float downhill = arc.FindLanding(PlaneBallGround.All(CourseSurface.Fairway, new Vector2(-0.1f, 0f)), SearchStep).Point.x;

            Assert.That(uphill, Is.LessThan(level));
            Assert.That(downhill, Is.GreaterThan(level));
        }

        [Test]
        public void FindLanding_OnASlope_EndsOnTheGround()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.1f, 0.05f));

            ArcLanding landing = ArcAlongX(20f).FindLanding(ground, SearchStep);

            Assert.That(landing.Point.y, Is.EqualTo(ground.Height(landing.Point.x, landing.Point.z)).Within(0.0001f));
        }

        [Test]
        public void FindLanding_BarelyDependsOnTheSearchStep()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.1f, 0f));
            BallisticArc arc = ArcAlongX(20f);

            float coarse = arc.FindLanding(ground, 0.05f).Point.x;
            float fine = arc.FindLanding(ground, 0.005f).Point.x;

            Assert.That(coarse, Is.EqualTo(fine).Within(0.01f));
        }

        [Test]
        public void FindLanding_OverWater_EndsAtTheWaterNotTheGroundBelowIt()
        {
            var ground = PlaneBallGround.All(CourseSurface.Water);

            ArcLanding landing = ArcAlongX(20f).FindLanding(ground, SearchStep);

            Assert.That(landing.Surface, Is.EqualTo(CourseSurface.Water));
            Assert.That(landing.Point.y, Is.EqualTo(PlaneBallGround.WaterLevel));
            Assert.That(landing.Point.x, Is.GreaterThan(20f), "The water is lower than the ground, so the arc runs on past level ground's carry.");
        }
    }
}
