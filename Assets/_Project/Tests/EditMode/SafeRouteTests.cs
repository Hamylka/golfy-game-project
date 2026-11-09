using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The safe-route check on small hand-built courses: what blocks a route and what leaves one.</summary>
    public class SafeRouteTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        // A 60 m island (or two with a 10 m gap), 12 m wide, tee at -25, hole at 25.
        private static CourseLayout Layout(bool gap)
        {
            var tee = new Vector2(-25f, 0f);
            var hole = new Vector2(25f, 0f);
            IslandOutline[] islands = gap
                ? new[] { new IslandOutline(12.5f, 6f, 0f, 0.2f, -17.5f), new IslandOutline(12.5f, 6f, 0f, 0.2f, 17.5f) }
                : new[] { new IslandOutline(30f, 6f, 0f, 0.2f) };
            return new CourseLayout(
                islands,
                new CoursePath(new[] { tee, hole }),
                new FairwayLayout(-26f, 22f, 2.5f, 0f, 1f),
                new[] { new GreenLayout(new EllipseShape(new Vector2(24f, 0f), new Vector2(3f, 2.7f), 0f), Vector2.zero) },
                new EllipseShape[0],
                new PondLayout[0],
                new GroundSwell[0],
                -0.2f,
                new GroundRelief(1f, 0f, 0f, 4f, 0.5f),
                tee,
                hole,
                1f,
                1f,
                new EdgeLayout(new Vector2(0.9f, 1.8f), 0.7f),
                alongCentreline: true);
        }

        private static List<Vector2> Find(CourseLayout layout, params HazardOutline[] hazards)
        {
            var world = new RouteWorld(new CourseFields(layout), layout.Centreline, Settings, new Vector2(23f, 0f), Vector2.right, 4);
            return SafeRoute.Find(world, hazards, out _);
        }

        private static HazardOutline Pond(float x, float z, float radiusX, float radiusZ) =>
            HazardOutline.Of(new EllipseShape(new Vector2(x, z), new Vector2(radiusX, radiusZ), 0f), true, HazardRole.Flank);

        [Test]
        public void BareIsland_HasARoute_InAFewShots()
        {
            List<Vector2> route = Find(Layout(false));
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Count - 1, Is.InRange(2, 3), "50 m takes two or three steps of 24 m");
        }

        [Test]
        public void APondAcrossTheWholeWidth_AlongTheCourse_LeavesNoRoute()
        {
            Assert.That(Find(Layout(false), Pond(0f, 0f, 40f, 9f)), Is.Null);
        }

        [Test]
        public void TwoPondsLeavingAFourMetreLane_StillLeaveARoute()
        {
            Assert.That(Find(Layout(false), Pond(0f, 5.5f, 40f, 3.5f), Pond(0f, -5.5f, 40f, 3.5f)), Is.Not.Null);
        }

        [Test]
        public void TwoPondsLeavingAThreeMetreLane_LeaveNone()
        {
            Assert.That(Find(Layout(false), Pond(0f, 5f, 40f, 3.5f), Pond(0f, -5f, 40f, 3.5f)), Is.Null, "the capsule is 3 m wide and keeps 0.5 m from a hazard");
        }

        [Test]
        public void ATenMetreGap_NeedsALayUpSpotWithinSeventyPercentPower()
        {
            CourseLayout layout = Layout(true);
            List<Vector2> route = Find(layout);
            Assert.That(route, Is.Not.Null, "the bare course has a lay-up spot");

            // The longest step is the one over the gap, and it stays within 70% power's carry.
            float carry = Settings.CarryAt(Settings.LayupPower);
            bool crossed = false;
            for (int i = 1; i < route.Count; i++)
            {
                if (route[i - 1].x < -5f && route[i].x > 5f)
                {
                    crossed = true;
                    Assert.That(Vector2.Distance(route[i - 1], route[i]), Is.LessThanOrEqualTo(carry + 0.01f));
                }
            }

            Assert.That(crossed, Is.True);

            // Block the last stretch before the gap: no lay-up spot is within 18.5 m of the far side any more.
            Assert.That(Find(layout, Pond(-14f, 0f, 12f, 8f)), Is.Null);
        }
    }
}
