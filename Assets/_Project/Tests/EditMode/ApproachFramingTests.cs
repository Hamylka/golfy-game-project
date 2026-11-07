using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Approach framing: near the hole, the camera at rest comes just close enough to put the hole at a set height on
    /// screen, with the ball at its rest spot. Like the heading, only at rest and on the reset view.
    /// </summary>
    public class ApproachFramingTests
    {
        private const float Frame = 1f / 60f;
        private const float Aspect = 9f / 19.5f;

        private static ApproachFraming Approach(CameraCourse course)
        {
            CameraRigSettings rig = course.Rig;
            return new ApproachFraming(course.Layout.Hole, course.Reach, rig.ApproachHoleViewportY, rig.DefaultZoom, rig.ApproachClosestZoom);
        }

        // A ball resting this far before the hole, on the line from the tee.
        private static Vector3 BallBefore(CameraCourse course, float metres)
        {
            Vector2 hole = course.Layout.Hole;
            Vector2 back = (course.Layout.Tee - hole).normalized;
            Vector2 at = hole + (back * metres);
            return new Vector3(at.x, CameraCourse.BallHeight, at.y);
        }

        private static float YawAt(CameraCourse course, Vector3 ball)
        {
            return course.Rule.YawFor(new Vector2(ball.x, ball.z));
        }

        private static CameraFollow FollowWithApproach(CameraCourse course, CameraNavigator navigator)
        {
            CameraFollow follow = course.Follow(navigator);
            follow.Approach = Approach(course);
            return follow;
        }

        // A shot that ends here: one frame moving, then at rest until the camera has settled.
        private static void ShotEndsAt(CameraFollow follow, Vector3 ball)
        {
            follow.Update(ball, FollowedBallState.Moving, false, Frame);
            for (int i = 0; i < 1200; i++)
            {
                follow.Update(ball, FollowedBallState.AtRest, false, Frame);
                if (!follow.IsEasing)
                {
                    return;
                }
            }

            Assert.Fail("the rest ease never ended");
        }

        // ----- The zoom -----

        [Test]
        public void Tee_OnMain_KeepsTheDefaultZoom()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();

            float zoom = Approach(course).ZoomFor(navigator, course.Tee, course.Rig.RestBallViewport, YawAt(course, course.Tee));

            Assert.That(zoom, Is.EqualTo(course.Rig.DefaultZoom));
        }

        [Test]
        public void BeyondAFullShot_KeepsTheDefaultZoom()
        {
            var course = new CameraCourse(CameraCourse.LongPath, Aspect);
            Vector3 ball = BallBefore(course, course.Reach + 5f);

            float zoom = Approach(course).ZoomFor(course.Navigator(), ball, course.Rig.RestBallViewport, YawAt(course, ball));

            Assert.That(zoom, Is.EqualTo(course.Rig.DefaultZoom));
        }

        [Test]
        public void AShortPutt_TakesTheClosestZoom()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            Vector3 ball = BallBefore(course, 1f);

            float zoom = Approach(course).ZoomFor(course.Navigator(), ball, course.Rig.RestBallViewport, YawAt(course, ball));

            Assert.That(zoom, Is.EqualTo(course.Rig.ApproachClosestZoom));
        }

        [Test]
        public void ABallAtTheHole_TakesTheClosestZoom()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            Vector3 ball = BallBefore(course, 0.1f);

            float zoom = Approach(course).ZoomFor(course.Navigator(), ball, course.Rig.RestBallViewport, YawAt(course, ball));

            Assert.That(zoom, Is.EqualTo(course.Rig.ApproachClosestZoom));
        }

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.LongPath)]
        [TestCase(CameraCourse.GapPath)]
        [TestCase(CameraCourse.DoglegPath)]
        public void CloserToTheHole_NeverZoomsFurtherOut_AndStaysWithinRange(string path)
        {
            var course = new CameraCourse(path, Aspect);
            CameraNavigator navigator = course.Navigator();
            ApproachFraming approach = Approach(course);
            float previous = course.Rig.DefaultZoom;
            for (float metres = course.Reach; metres >= 0.5f; metres -= 0.5f)
            {
                Vector3 ball = BallBefore(course, metres);
                float zoom = approach.ZoomFor(navigator, ball, course.Rig.RestBallViewport, YawAt(course, ball));

                Assert.That(zoom, Is.GreaterThanOrEqualTo(previous - 1e-4f), $"at {metres} m");
                Assert.That(zoom, Is.InRange(course.Rig.DefaultZoom, course.Rig.ApproachClosestZoom), $"at {metres} m");
                previous = zoom;
            }
        }

        [TestCase(5f)]
        [TestCase(6f)]
        [TestCase(7.5f)]
        public void AMidApproach_PutsTheHoleAtItsHeight_AboveTheBall(float metres)
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            Vector3 ball = BallBefore(course, metres);
            float yaw = YawAt(course, ball);

            float zoom = Approach(course).ZoomFor(navigator, ball, course.Rig.RestBallViewport, yaw);
            Vector3 camera = navigator.FreePositionWith(zoom, ball, course.Rig.RestBallViewport, yaw, out CameraView view);
            Vector2 hole = view.ToViewport(camera, new Vector3(course.Layout.Hole.x, ball.y, course.Layout.Hole.y));

            Assert.That(zoom, Is.GreaterThan(course.Rig.DefaultZoom));
            Assert.That(zoom, Is.LessThan(course.Rig.ApproachClosestZoom));
            Assert.That(hole.y, Is.EqualTo(course.Rig.ApproachHoleViewportY).Within(1e-3f));
            // The heading faces the hole, which then sits above the ball with the convention's slight lean to the right.
            Assert.That(hole.x, Is.InRange(0.05f, 0.5f), "above the ball, leaning right");
        }

        [Test]
        public void TheContinuousRange_RunsFromAboutTenMetresToAboutFour_OnMain()
        {
            // The figures the decision was taken on (Main, ball 30% up, hole 62% up), on a 9:19.5 screen.
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            ApproachFraming approach = Approach(course);

            float At(float metres)
            {
                Vector3 ball = BallBefore(course, metres);
                return approach.ZoomFor(navigator, ball, course.Rig.RestBallViewport, YawAt(course, ball));
            }

            Assert.That(At(12f), Is.EqualTo(course.Rig.DefaultZoom), "12 m");
            Assert.That(At(8f), Is.GreaterThan(course.Rig.DefaultZoom), "8 m");
            Assert.That(At(5f), Is.LessThan(course.Rig.ApproachClosestZoom), "5 m");
            Assert.That(At(3f), Is.EqualTo(course.Rig.ApproachClosestZoom), "3 m");
        }

        [Test]
        public void ZoomAbout_KeepsThePointWhereItIsOnScreen()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            navigator.ZoomTo(course.Rig.DefaultZoom);
            Vector3 point = BallBefore(course, 6f);
            navigator.Move(point - navigator.View.OnPlane(navigator.Position, new Vector2(0f, -0.4f), point.y));
            Vector2 before = navigator.View.ToViewport(navigator.Position, point);

            navigator.ZoomAbout(point, 0.9f);

            Assert.That(navigator.Zoom, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That((navigator.View.ToViewport(navigator.Position, point) - before).magnitude, Is.LessThan(1e-3f));
        }

        // ----- In the follow -----

        [Test]
        public void AtRestNearTheHole_TheCameraZoomsIn_WithTheBallAtItsSpotAndTheHoleAbove()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);
            Vector3 ball = BallBefore(course, 6f);

            ShotEndsAt(follow, ball);

            float wanted = Approach(course).ZoomFor(navigator, ball, course.Rig.RestBallViewport, follow.Heading);
            Assert.That(navigator.Zoom, Is.EqualTo(wanted).Within(0.005f));
            Assert.That((navigator.View.ToViewport(navigator.Position, ball) - course.Rig.RestBallViewport).magnitude, Is.LessThan(0.01f));
            Vector2 hole = navigator.View.ToViewport(navigator.Position, new Vector3(course.Layout.Hole.x, ball.y, course.Layout.Hole.y));
            Assert.That(hole.y, Is.EqualTo(course.Rig.ApproachHoleViewportY).Within(0.02f));
        }

        [Test]
        public void AFullPull_StillFitsBelowTheBall_AtTheClosestApproach()
        {
            // The pull is 30% of the screen's width, straight down from the ball at worst.
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            Vector3 ball = BallBefore(course, 1f);

            ShotEndsAt(follow, ball);

            float ballY = navigator.View.ToViewport(navigator.Position, ball).y;
            float pullInViewportY = 0.3f * 2f * Aspect;
            Assert.That(ballY - pullInViewportY, Is.GreaterThan(-1f));
        }

        [Test]
        public void AManualZoom_LastsUntilTheNextShot_ThenTheApproachZoomWins()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            Vector3 ball = BallBefore(course, 6f);
            ShotEndsAt(follow, ball);
            float approachZoom = navigator.Zoom;

            navigator.ZoomTo(0.1f);
            for (int i = 0; i < 120; i++)
            {
                follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(navigator.Zoom, Is.EqualTo(0.1f).Within(1e-3f), "at rest, the player's zoom stays");

            ShotEndsAt(follow, ball);

            Assert.That(navigator.Zoom, Is.EqualTo(approachZoom).Within(0.005f), "the next rest takes the approach zoom");
        }

        [Test]
        public void AFarRest_ComesBackToTheDefaultZoom()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            navigator.ZoomTo(0.9f);

            ShotEndsAt(follow, BallBefore(course, 20f));

            Assert.That(navigator.Zoom, Is.EqualTo(course.Rig.DefaultZoom).Within(0.005f));
        }

        [Test]
        public void InFlight_TheZoomNeverChanges()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);
            Vector3 from = course.Tee, to = BallBefore(course, 3f);

            for (int i = 0; i <= 120; i++)
            {
                follow.Update(Vector3.Lerp(from, to, i / 120f), FollowedBallState.Moving, false, Frame);
                Assert.That(navigator.Zoom, Is.EqualTo(course.Rig.DefaultZoom).Within(1e-4f), $"frame {i}");
            }
        }

        [Test]
        public void ATwistDuringTheRestEase_StopsTheZoomWhereItIs()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);
            Vector3 ball = BallBefore(course, 2f);
            follow.Update(ball, FollowedBallState.Moving, false, Frame);
            for (int i = 0; i < 10; i++)
            {
                follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            follow.SetTwist(20f, ball);
            float stopped = navigator.Zoom;
            for (int i = 0; i < 120; i++)
            {
                follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(stopped, Is.GreaterThan(course.Rig.DefaultZoom), "the zoom had started");
            Assert.That(navigator.Zoom, Is.EqualTo(stopped).Within(1e-4f));
        }

        [Test]
        public void TheResetView_NearTheHole_TakesTheApproachZoom()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            Vector3 ball = BallBefore(course, 6f);
            ShotEndsAt(follow, ball);
            float approachZoom = navigator.Zoom;
            navigator.ZoomTo(0.2f);
            follow.SetTwist(30f, ball);

            follow.BeginReset(ball);
            for (int i = 0; i < 120; i++)
            {
                follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(navigator.Zoom, Is.EqualTo(approachZoom).Within(0.005f));
            Assert.That(follow.Twist, Is.EqualTo(0f));
        }

        [Test]
        public void TheResetView_FarFromTheHole_TakesTheDefaultZoom()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = FollowWithApproach(course, navigator);
            follow.PlaceOn(course.Tee);
            navigator.ZoomTo(0.1f);

            follow.BeginReset(course.Tee);
            for (int i = 0; i < 120; i++)
            {
                follow.Update(course.Tee, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(navigator.Zoom, Is.EqualTo(course.Rig.DefaultZoom).Within(0.005f));
        }

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.DoglegPath)]
        public void TheLevelStart_IsUnchanged_ByApproachFraming(string path)
        {
            var course = new CameraCourse(path, Aspect);
            CameraNavigator plain = course.Navigator();
            plain.ZoomTo(course.Rig.DefaultZoom);
            course.Follow(plain).PlaceOn(course.Tee);
            CameraNavigator framed = course.Navigator();
            framed.ZoomTo(course.Rig.DefaultZoom);
            FollowWithApproach(course, framed).PlaceOn(course.Tee);

            Assert.That(framed.Position, Is.EqualTo(plain.Position));
            Assert.That(framed.Yaw, Is.EqualTo(plain.Yaw));
        }

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.LongPath)]
        [TestCase(CameraCourse.GapPath)]
        [TestCase(CameraCourse.DoglegPath)]
        public void TheApproachCamera_IsNeverHigherThanTheDefaultOne_SoTheShadowFadeIsNoWorse(string path)
        {
            // Closer only: the camera's height never exceeds the default zoom's, so nothing new lies further away.
            var course = new CameraCourse(path, Aspect);
            CameraNavigator navigator = course.Navigator();
            ApproachFraming approach = Approach(course);
            Vector3 defaultCamera = navigator.FreePositionWith(course.Rig.DefaultZoom, course.Tee, course.Rig.RestBallViewport, course.StartYaw, out _);
            for (float metres = course.Reach; metres >= 0.5f; metres -= 1f)
            {
                Vector3 ball = BallBefore(course, metres);
                float yaw = YawAt(course, ball);
                Vector3 camera = navigator.FreePositionWith(approach.ZoomFor(navigator, ball, course.Rig.RestBallViewport, yaw), ball, course.Rig.RestBallViewport, yaw, out _);
                Assert.That(camera.y - ball.y, Is.LessThanOrEqualTo(defaultCamera.y - course.Tee.y + 1e-3f), $"at {metres} m");
            }
        }
    }
}
