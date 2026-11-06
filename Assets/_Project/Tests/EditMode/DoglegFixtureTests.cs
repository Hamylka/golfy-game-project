using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The hand-made dogleg used to test the camera along a bent centreline: a leg along +X, then an island turned
    /// 35 degrees toward -Z, overlapping it at the bend.
    /// </summary>
    public class DoglegFixtureTests
    {
        private CourseLayout _layout;
        private CourseFields _fields;

        [SetUp]
        public void SetUp()
        {
            _layout = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/DoglegCourseShape.asset").ToLayout();
            _fields = new CourseFields(_layout);
        }

        [Test]
        public void Centreline_TurnsOnce_By35Degrees()
        {
            var points = _layout.Centreline.Points;

            Assert.That(points.Count, Is.EqualTo(3));
            Assert.That(points[0], Is.EqualTo(_layout.Tee));
            Assert.That(points[2], Is.EqualTo(_layout.Hole));
            Assert.That(Vector2.SignedAngle(Vector2.right, points[1] - points[0]), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Vector2.SignedAngle(Vector2.right, points[2] - points[1]), Is.EqualTo(-35f).Within(0.01f));
            Assert.That(_layout.Centreline.Length, Is.InRange(40f, 55f), "a par 4's length");
        }

        [Test]
        public void Island_RunsUnbroken_UnderTheWholeCentreline()
        {
            var points = _layout.Centreline.Points;
            for (int leg = 0; leg < points.Count - 1; leg++)
            {
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    Vector2 p = Vector2.Lerp(points[leg], points[leg + 1], t);
                    Assert.That(_fields.Island(new Vector3(p.x, 0f, p.y)), Is.LessThan(-1.5f), $"leg {leg} at {t}");
                }
            }
        }

        [Test]
        public void TeeAndHole_SitOnTheirSurfaces()
        {
            Assert.That(_fields.Surface(new Vector3(_layout.Tee.x, 0f, _layout.Tee.y)), Is.EqualTo(CourseSurface.Fairway));
            Assert.That(_fields.Surface(new Vector3(_layout.Hole.x, 0f, _layout.Hole.y)), Is.EqualTo(CourseSurface.Green));
        }

        [Test]
        public void TurnedIsland_IsTheSameOvalTurned_AndTheGridCoversIt()
        {
            var turned = new IslandOutline(10f, 3f, 0f, 0.18f, 5f, -2f, -35f);
            var straight = new IslandOutline(10f, 3f, 0f, 0.18f, 5f);
            CourseLayout layout = Layout(turned);
            CourseLayout reference = Layout(straight);
            var turnedFields = new CourseFields(layout);
            var straightFields = new CourseFields(reference);

            float radians = -35f * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var across = new Vector2(-along.y, along.x);
            for (float a = -9f; a <= 9f; a += 1.5f)
            {
                for (float c = -2.5f; c <= 2.5f; c += 1.25f)
                {
                    Vector2 p = new Vector2(5f, -2f) + (along * a) + (across * c);
                    float expected = straightFields.Island(new Vector3(5f + a, 0f, c));
                    Assert.That(turnedFields.Island(new Vector3(p.x, 0f, p.y)), Is.EqualTo(expected).Within(1e-4f));
                }
            }

            // Both tips lie inside the half extent the terrain grid covers.
            foreach (float tip in new[] { -10f, 10f })
            {
                Vector2 p = new Vector2(5f, -2f) + (along * tip);
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(layout.HalfExtent.x));
                Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(layout.HalfExtent.y));
            }
        }

        [Test]
        public void IslandOnTheAxis_IsMeasuredUntouched()
        {
            var island = new IslandOutline(17f, 5.2f, 3.2f, 0.18f, 1.5f);
            var p = new Vector3(3.3f, 0.2f, -1.7f);

            Assert.That(island.IsOnAxis, Is.True);
            Assert.That(island.ToOwnFrame(p), Is.EqualTo(p));
            Assert.That(island.HalfExtent.x, Is.EqualTo(18.5f).Within(1e-5f));
            Assert.That(island.HalfExtent.y, Is.EqualTo(6.8f).Within(1e-5f));
        }

        private static CourseLayout Layout(IslandOutline island)
        {
            return new CourseLayout(
                new[] { island },
                new CoursePath(new[] { Vector2.zero, Vector2.right }),
                new FairwayLayout(0f, 0f, 1f, 0f, 1f),
                new GreenLayout[0],
                new EllipseShape[0],
                new EllipseShape[0],
                new GroundSwell[0],
                -0.2f,
                new GroundRelief(1f, 0f, 0f, 1f, 1f),
                Vector2.zero,
                Vector2.right,
                1f,
                1f,
                new EdgeLayout(Vector2.one, 1f));
        }
    }
}
