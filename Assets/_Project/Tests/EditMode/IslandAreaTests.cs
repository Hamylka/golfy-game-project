using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The camera's limit: the view's centre stays over the islands with a margin, at every zoom and heading, a way
    /// across every gap, and the clouds cover every view that allows.
    /// </summary>
    public class IslandAreaTests
    {
        private const float Margin = 3f;
        private const float Tolerance = 1e-3f;

        /// <summary>How far a point is from the islands themselves, 0 on them.</summary>
        private static float FromIslands(CameraCourse course, Vector2 point)
        {
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            var islands = new IslandArea(LevelBootstrap.GroundEdges(TerrainBuilder.Build(course.Layout, palette).Edge), null, 0f);
            return (islands.Nearest(point) - point).magnitude;
        }

        [Test]
        public void Rect_InsideIsKept_OutsideComesBackToTheMargin()
        {
            IslandArea area = IslandArea.FromRect(Rect.MinMaxRect(-10f, -2f, 10f, 2f), Margin);

            Assert.That(area.Nearest(new Vector2(3f, 1f)), Is.EqualTo(new Vector2(3f, 1f)));
            Assert.That(area.Nearest(new Vector2(12f, 0f)), Is.EqualTo(new Vector2(12f, 0f)), "within the margin");
            Assert.That((area.Nearest(new Vector2(20f, 0f)) - new Vector2(13f, 0f)).magnitude, Is.LessThan(Tolerance));
            Assert.That((area.Nearest(new Vector2(20f, 10f)) - (new Vector2(10f, 2f) + (new Vector2(10f, 8f).normalized * Margin))).magnitude, Is.LessThan(Tolerance), "round at a corner");
        }

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.LongPath)]
        [TestCase(CameraCourse.GapPath)]
        [TestCase(CameraCourse.DoglegPath)]
        public void TheCourse_TeeHoleAndCentreline_AreOnTheIslandsOrInTheArea(string path)
        {
            var course = new CameraCourse(path, 9f / 16f);

            Assert.That(course.Area.OnIsland(course.Layout.Tee), Is.True);
            Assert.That(course.Area.OnIsland(course.Layout.Hole), Is.True);
            var line = new GroundPath(course.Centreline);
            for (float along = 0f; along <= line.Length; along += 0.5f)
            {
                Assert.That(course.Area.Contains(line.PointAt(along)), Is.True, $"at {along} m");
            }
        }

        [Test]
        public void Dogleg_InsideTheBend_TheViewComesBackToTheMargin()
        {
            var course = new CameraCourse(CameraCourse.DoglegPath, 9f / 16f);
            Vector2 bend = course.Centreline[1];

            // The bend turns toward -Z, so its inside is that way, between the legs.
            Vector2 inside = bend + new Vector2(4f, -16f);
            Assert.That(course.Area.OnIsland(inside), Is.False);
            Assert.That(FromIslands(course, inside), Is.GreaterThan(Margin));

            Vector2 allowed = course.Area.Nearest(inside);

            Assert.That(FromIslands(course, allowed), Is.EqualTo(Margin).Within(0.01f));
            Assert.That(course.Area.Contains(allowed), Is.True);
        }

        [Test]
        public void Gap_TheCentrelineCrossesIt_ButNotTheOpenSkyBeside()
        {
            var course = new CameraCourse(CameraCourse.GapPath, 9f / 16f);
            var line = new GroundPath(course.Centreline);

            // Somewhere along the centreline is open sky: the gap.
            Vector2 gap = Vector2.zero;
            bool found = false;
            for (float along = 0f; along <= line.Length && !found; along += 0.1f)
            {
                gap = line.PointAt(along);
                found = !course.Area.OnIsland(gap);
            }

            Assert.That(found, Is.True, "the fixture has a gap on its centreline");
            Assert.That(course.Area.Contains(gap), Is.True, "the view can cross it");

            Vector2 beside = gap + new Vector2(0f, 12f);
            Assert.That(course.Area.Contains(beside), Is.False);
            Vector2 allowed = course.Area.Nearest(beside);
            Assert.That(Mathf.Min(FromIslands(course, allowed), DistanceToCentreline(allowed, course.Centreline)), Is.EqualTo(Margin).Within(0.01f));
        }

        private static float DistanceToCentreline(Vector2 point, IReadOnlyList<Vector2> line)
        {
            float best = float.MaxValue;
            for (int i = 1; i < line.Count; i++)
            {
                Vector2 leg = line[i] - line[i - 1];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i - 1], leg) / leg.sqrMagnitude);
                best = Mathf.Min(best, (point - (line[i - 1] + (leg * t))).magnitude);
            }

            return best;
        }

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.GapPath)]
        [TestCase(CameraCourse.DoglegPath)]
        public void Navigator_AtEveryZoomAndHeading_KeepsTheViewCentreInTheArea(string path)
        {
            var course = new CameraCourse(path, 9f / 21f);
            CameraNavigator navigator = course.Navigator();
            var random = new System.Random(5);

            for (int i = 0; i < 300; i++)
            {
                navigator.ZoomTo((float)random.NextDouble());
                navigator.TurnTo((float)(random.NextDouble() * 360.0), navigator.ViewCentre);
                navigator.Move(new Vector3((float)(random.NextDouble() - 0.5) * 60f, 0f, (float)(random.NextDouble() - 0.5) * 60f));
                Vector3 centre = navigator.ViewCentre;
                var ground = new Vector2(centre.x, centre.z);

                Assert.That((course.Area.Nearest(ground) - ground).magnitude, Is.LessThan(Tolerance), $"step {i}");
            }
        }

        [Test]
        public void Navigator_APanInsideTheArea_IsNotLimited()
        {
            var course = new CameraCourse(CameraCourse.DoglegPath, 9f / 16f);
            CameraNavigator navigator = course.Navigator();
            navigator.ZoomTo(0.5f);
            navigator.TurnTo(115f, navigator.ViewCentre);
            Vector3 before = navigator.Position;

            Vector3 moved = navigator.Move(new Vector3(2f, 0f, -1f));

            Assert.That((moved - new Vector3(2f, 0f, -1f)).magnitude, Is.LessThan(Tolerance));
            Assert.That((navigator.Position - before - moved).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void ZoomedOut_TheViewCanTravelTheWholeCourse_AlongTheBend()
        {
            // Replaces PR 33's corridor: zoomed out, the view's centre reaches every spot of the centreline.
            var course = new CameraCourse(CameraCourse.DoglegPath, 9f / 16f);
            CameraNavigator navigator = course.Navigator();
            var line = new GroundPath(course.Centreline);

            for (float along = 0f; along <= line.Length; along += 2f)
            {
                Vector2 spot = line.PointAt(along);
                Vector3 centre = navigator.ViewCentre;
                navigator.Move(new Vector3(spot.x - centre.x, 0f, spot.y - centre.z));

                Assert.That((navigator.ViewCentre - new Vector3(spot.x, 0f, spot.y)).magnitude, Is.LessThan(Tolerance));
                Assert.That(navigator.Zoom, Is.EqualTo(0f).Within(Tolerance));
            }
        }

        // ----- Clouds -----

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.DoglegPath)]
        public void Clouds_CoverEveryZoomedOutView_AtEveryHeading(string path)
        {
            var course = new CameraCourse(path, 9f / 21f);
            var clouds = AssetDatabase.LoadAssetAtPath<CloudSettings>("Assets/_Project/Settings/CloudSettings.asset");
            List<float> yaws = course.Yaws(course.Rig.MaxTwist);
            CameraNavigator navigator = course.Navigator();
            var random = new System.Random(9);

            foreach (float height in new[] { clouds.Lowest, clouds.Highest, clouds.NearLowest, clouds.NearHighest })
            {
                Rect visible = IslandCamera.VisibleGround(height, course.Home, course.View, 0f, course.Area, yaws);
                for (int i = 0; i < 100; i++)
                {
                    float yaw = yaws[random.Next(yaws.Count)];
                    navigator.TurnTo(yaw, navigator.ViewCentre);
                    Rect bounds = course.Area.Bounds;
                    navigator.Move(new Vector3(bounds.xMin + ((float)random.NextDouble() * bounds.width), 0f, bounds.yMin + ((float)random.NextDouble() * bounds.height)) - navigator.ViewCentre);
                    Rect seen = CameraFraming.GroundFootprint(navigator.Position, navigator.View, height);

                    Assert.That(seen.xMin, Is.GreaterThanOrEqualTo(visible.xMin - Tolerance), $"{height} m, yaw {yaw}");
                    Assert.That(seen.xMax, Is.LessThanOrEqualTo(visible.xMax + Tolerance), $"{height} m, yaw {yaw}");
                    Assert.That(seen.yMin, Is.GreaterThanOrEqualTo(visible.yMin - Tolerance), $"{height} m, yaw {yaw}");
                    Assert.That(seen.yMax, Is.LessThanOrEqualTo(visible.yMax + Tolerance), $"{height} m, yaw {yaw}");
                }
            }
        }

        [Test]
        public void Clouds_OnTheDogleg_ReachTheSecondLegsHeading()
        {
            var course = new CameraCourse(CameraCourse.DoglegPath, 9f / 21f);
            List<float> yaws = course.Yaws();

            Assert.That(yaws, Has.Some.InRange(105f, 125f), "the second leg, or the hole from it");
            Assert.That(yaws, Has.Some.EqualTo(80f), "the first leg");
        }
    }
}
