using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Straight courses keep the camera they had before headings: the same framing, the same start of a level and the
    /// same rest after a shot along the axis, at the convention's exact rotation.
    /// </summary>
    public class CameraPathTests
    {
        private const float Tolerance = 1e-3f;

        // Captured from develop before headings (commit a53e573), with the shipped camera settings.
        [TestCase(CameraCourse.Par3Path, 9f / 16f, -27.3643f, 16.8725f, -3.8264f)]
        [TestCase(CameraCourse.Par3Path, 9f / 21f, -33.1909f, 23.0510f, -5.1404f)]
        [TestCase(CameraCourse.Par3Path, 1280f / 2800f, -31.6336f, 21.4574f, -4.8049f)]
        [TestCase(CameraCourse.LongPath, 9f / 16f, -56.5881f, 22.9004f, -3.2590f)]
        [TestCase(CameraCourse.LongPath, 9f / 21f, -65.5088f, 31.8979f, -5.5091f)]
        [TestCase(CameraCourse.LongPath, 1280f / 2800f, -63.1309f, 29.5730f, -4.9454f)]
        public void StraightCourses_KeepTheirFraming(string path, float aspect, float x, float y, float z)
        {
            var course = new CameraCourse(path, aspect);

            Assert.That(course.StartYaw, Is.EqualTo(IsoCameraConvention.StraightYaw));
            Assert.That(course.View.Rotation, Is.EqualTo(IsoCameraConvention.Rotation));
            Assert.That((course.Home - new Vector3(x, y, z)).magnitude, Is.LessThan(Tolerance));
        }

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.LongPath)]
        public void StraightCourses_FrameTheSameWithOrWithoutTheirCentreline(string path)
        {
            var course = new CameraCourse(path, 9f / 16f);

            Vector3 plain = CameraFraming.FitCourse(course.Outline, course.View, course.Rig.ScreenFill, course.Rig.FramedLength);

            Assert.That((course.Home - plain).magnitude, Is.LessThan(Tolerance));
        }

        /// <summary>
        /// The start of a level before headings: zoom to the default zoom around the middle of the view, then move the
        /// ball to its rest spot, within the island's bounding rectangle (open in full from the default zoom).
        /// </summary>
        private static Vector3 OldPlaceOn(CameraCourse course, Vector3 ball)
        {
            CameraView view = course.View;
            Vector3 home = course.Home;
            Vector3 focus = view.OnPlane(home, Vector2.zero, 0f);
            float height = Mathf.Lerp(home.y, home.y * course.Rig.ClosestZoom, course.Rig.DefaultZoom);
            Vector3 zoomed = focus + ((home - focus) * (height / home.y));
            Vector3 moved = zoomed + (ball - view.OnPlane(zoomed, course.Rig.RestBallViewport, ball.y));

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 point in course.Outline)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minZ = Mathf.Min(minZ, point.z);
                maxZ = Mathf.Max(maxZ, point.z);
            }

            Vector3 centre = view.OnPlane(moved, Vector2.zero, 0f);
            var limited = new Vector3(Mathf.Clamp(centre.x, minX, maxX), centre.y, Mathf.Clamp(centre.z, minZ, maxZ));
            return moved + (limited - centre);
        }

        [TestCase(CameraCourse.Par3Path, 9f / 16f)]
        [TestCase(CameraCourse.Par3Path, 9f / 21f)]
        [TestCase(CameraCourse.LongPath, 9f / 16f)]
        [TestCase(CameraCourse.LongPath, 9f / 21f)]
        public void StraightCourses_StartTheLevelAsBefore_AtTheConventionsRotation(string path, float aspect)
        {
            var course = new CameraCourse(path, aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = course.Follow(navigator);

            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);

            Assert.That(navigator.View.Rotation, Is.EqualTo(IsoCameraConvention.Rotation), "no turn by a hair at the tee");
            Assert.That((navigator.Position - OldPlaceOn(course, course.Tee)).magnitude, Is.LessThan(Tolerance));
        }

        [TestCase(CameraCourse.Par3Path, 5f)]
        [TestCase(CameraCourse.LongPath, -10f)]
        [TestCase(CameraCourse.LongPath, 20f)]
        public void StraightCourses_AShotAlongTheAxis_RestsAsBefore(string path, float restX)
        {
            // With and without the heading rule: the rule may only matter where the course or the hole turns the view.
            var course = new CameraCourse(path, 9f / 16f);
            CameraNavigator withNavigator = course.Navigator();
            CameraFollow with = course.Follow(withNavigator);
            CameraNavigator withoutNavigator = course.Navigator();
            CameraFollow without = course.Follow(withoutNavigator, false);
            var rest = new Vector3(restX, CameraCourse.BallHeight, 0.2f);

            foreach ((CameraNavigator navigator, CameraFollow follow) in new[] { (withNavigator, with), (withoutNavigator, without) })
            {
                navigator.ZoomTo(course.Rig.DefaultZoom);
                follow.PlaceOn(course.Tee);
                for (float t = 0f; t <= 1f; t += 1f / 60f)
                {
                    Vector3 flying = Vector3.Lerp(course.Tee, rest, t) + (Vector3.up * (4f * t * (1f - t) * 4f));
                    follow.Update(flying, FollowedBallState.Moving, false, 1f / 60f);
                }

                for (float time = 0f; time < 6f; time += 1f / 60f)
                {
                    follow.Update(rest, FollowedBallState.AtRest, false, 1f / 60f);
                }
            }

            Assert.That(withNavigator.View.Rotation, Is.EqualTo(IsoCameraConvention.Rotation));
            Assert.That((withNavigator.Position - withoutNavigator.Position).magnitude, Is.LessThan(Tolerance));
            Assert.That(course.View.ToViewport(withNavigator.Position, rest).y, Is.EqualTo(course.Rig.RestBallViewport.y).Within(0.01f));
        }
    }
}
