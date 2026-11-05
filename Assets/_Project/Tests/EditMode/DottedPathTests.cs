using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using IsoGolf.UI;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class DottedPathTests
    {
        private readonly List<Vector3> _dots = new List<Vector3>();

        private static List<Vector3> StraightPath(float length, int points)
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= points; i++)
            {
                path.Add(new Vector3(length * i / points, 0.5f, 0f));
            }

            return path;
        }

        private static (BallisticArc arc, ArcLanding landing) Flight(Vector2 direction, float carry)
        {
            BallisticArc arc = BallisticArc.ForCarry(Vector3.zero, direction, carry, 40f, 9.81f);
            return (arc, arc.FindLanding(PlaneBallGround.All(CourseSurface.Fairway), 0.02f));
        }

        [Test]
        public void Arc_DotsLieOnTheFlightLiftedToTheBallsCenter()
        {
            (BallisticArc arc, ArcLanding landing) = Flight(new Vector2(1f, 0.5f), 10f);

            DottedPath.AlongArc(arc, landing, 0.6f, 0.12f, _dots);

            Assert.That(_dots, Is.Not.Empty);
            float interval = 0.6f / new Vector2(arc.Velocity.x, arc.Velocity.z).magnitude;
            for (int i = 0; i < _dots.Count; i++)
            {
                Vector3 expected = arc.PositionAt((i + 1) * interval) + (Vector3.up * 0.12f);
                Assert.That(Vector3.Distance(_dots[i], expected), Is.LessThan(0.0001f), $"dot {i}");
            }
        }

        [Test]
        public void Arc_DotsAreEvenlySpacedOnTheGround()
        {
            (BallisticArc arc, ArcLanding landing) = Flight(Vector2.right, 10f);

            DottedPath.AlongArc(arc, landing, 0.6f, 0f, _dots);

            for (int i = 1; i < _dots.Count; i++)
            {
                Assert.That(_dots[i].x - _dots[i - 1].x, Is.EqualTo(0.6f).Within(0.001f), $"dot {i}");
            }
        }

        [Test]
        public void Arc_StopsHalfASpacingShortOfTheLanding()
        {
            (BallisticArc arc, ArcLanding landing) = Flight(Vector2.right, 10f);

            DottedPath.AlongArc(arc, landing, 0.6f, 0f, _dots);

            float gap = landing.Point.x - _dots[_dots.Count - 1].x;
            Assert.That(gap, Is.GreaterThanOrEqualTo(0.3f - 0.001f));
            Assert.That(gap, Is.LessThan(0.9f + 0.001f));
        }

        [Test]
        public void Path_ShowsOnlyTheFirstShare()
        {
            DottedPath.AlongPath(StraightPath(10f, 200), 0.4f, 0.25f, _dots);

            Assert.That(_dots.Count, Is.EqualTo(16));
            Assert.That(_dots[0].x, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(_dots[15].x, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void Path_KeepsEachDotOnThePathsHeight()
        {
            DottedPath.AlongPath(StraightPath(10f, 200), 0.4f, 0.25f, _dots);

            foreach (Vector3 dot in _dots)
            {
                Assert.That(dot.y, Is.EqualTo(0.5f).Within(0.0001f));
            }
        }

        [Test]
        public void Path_ShortOfOneSpacingShare_StillShowsTheDirection()
        {
            DottedPath.AlongPath(StraightPath(0.3f, 20), 0.4f, 0.25f, _dots);

            Assert.That(_dots.Count, Is.EqualTo(1));
            Assert.That(_dots[0].x, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void Path_NeverGoesPastItsEnd()
        {
            DottedPath.AlongPath(StraightPath(0.2f, 20), 0.4f, 0.25f, _dots);

            Assert.That(_dots, Is.Empty);
        }

        [Test]
        public void Path_OfJustAStart_HasNoDots()
        {
            DottedPath.AlongPath(new List<Vector3> { Vector3.zero }, 0.4f, 0.25f, _dots);

            Assert.That(_dots, Is.Empty);
        }
    }
}
