using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class GroundPathTests
    {
        private const float Tolerance = 1e-4f;

        // Along +X for 10 m, then 35 degrees toward -Z for 10 m: a dogleg to the right on screen.
        private static GroundPath Dogleg()
        {
            Vector2 bend = new Vector2(10f, 0f);
            float radians = -35f * Mathf.Deg2Rad;
            return new GroundPath(new[] { Vector2.zero, bend, bend + (new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 10f) });
        }

        [Test]
        public void Straight_ProjectsToXAndZ()
        {
            var path = new GroundPath(new[] { new Vector2(-3f, 1f), new Vector2(7f, 1f) });

            float along = path.Project(new Vector2(2f, 4f), out float across);

            Assert.That(along, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(across, Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void Ends_RunOnAsStraightLines()
        {
            GroundPath path = Dogleg();

            Assert.That(path.Project(new Vector2(-4f, -1f), out float before), Is.EqualTo(-4f).Within(Tolerance));
            Assert.That(before, Is.EqualTo(-1f).Within(Tolerance));
            Vector2 past = path.PointAt(path.Length + 5f);
            Assert.That((past - path.PointAt(path.Length)).magnitude, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(Vector2.Angle(past - path.PointAt(path.Length), path.LegDirection(1)), Is.LessThan(0.01f));
        }

        [Test]
        public void PointAt_AndProject_AreInverse_OnEveryLeg()
        {
            GroundPath path = Dogleg();
            for (float distance = -3f; distance <= path.Length + 3f; distance += 0.7f)
            {
                Vector2 point = path.PointAt(distance) + (GroundPath.Normal(path.DirectionAt(distance)) * 0.5f);

                Assert.That(path.Project(point, out float across), Is.EqualTo(distance).Within(1e-3f), $"at {distance}");
                Assert.That(across, Is.EqualTo(0.5f).Within(1e-3f), $"at {distance}");
            }
        }

        [Test]
        public void OffsetSlice_KeepsEveryLegTheSameDistanceAway()
        {
            GroundPath path = Dogleg();

            Vector2[] slice = path.OffsetSlice(2f, path.Length - 2f, 1.5f);

            Assert.That(slice.Length, Is.EqualTo(3), "start, the mitred bend, end");

            // Each end lies square beside its leg, and the mitred bend 1.5 m from both legs' lines.
            Assert.That(Vector2.Dot(slice[0] - path.PointAt(0f), GroundPath.Normal(path.LegDirection(0))), Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(Vector2.Dot(slice[1] - path.PointAt(0f), GroundPath.Normal(path.LegDirection(0))), Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(Vector2.Dot(slice[1] - path.PointAt(path.Length), GroundPath.Normal(path.LegDirection(1))), Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(Vector2.Dot(slice[2] - path.PointAt(path.Length), GroundPath.Normal(path.LegDirection(1))), Is.EqualTo(1.5f).Within(1e-3f));

            // The legs stay parallel to the path's.
            Assert.That(Vector2.Angle(slice[1] - slice[0], path.LegDirection(0)), Is.LessThan(0.01f));
            Assert.That(Vector2.Angle(slice[2] - slice[1], path.LegDirection(1)), Is.LessThan(0.01f));
        }

        [Test]
        public void OffsetSlice_WithOneOffsetPerLeg_MeetsWhereTheLegsCross()
        {
            GroundPath path = Dogleg();

            Vector2[] slice = path.OffsetSlice(0f, path.Length, new[] { 1f, -0.5f });

            Vector2 firstLeg = slice[1] - slice[0];
            Vector2 secondLeg = slice[2] - slice[1];
            Assert.That(Vector2.Angle(firstLeg, path.LegDirection(0)), Is.LessThan(0.01f));
            Assert.That(Vector2.Angle(secondLeg, path.LegDirection(1)), Is.LessThan(0.01f));
            Assert.That(Vector2.Dot(slice[2] - path.PointAt(path.Length), GroundPath.Normal(path.LegDirection(1))), Is.EqualTo(-0.5f).Within(1e-3f));
        }

        [Test]
        public void RepeatedPoints_AreDropped()
        {
            var path = new GroundPath(new[] { Vector2.zero, Vector2.zero, Vector2.right });

            Assert.That(path.LegCount, Is.EqualTo(1));
            Assert.That(new GroundPath(new[] { Vector2.one }).LegCount, Is.EqualTo(0));
        }
    }
}
