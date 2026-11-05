using IsoGolf.Gameplay;
using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CameraFollowTests
    {
        private const float Aspect = 1280f / 2800f;
        private const float Frame = 1f / 60f;
        private const float Tolerance = 0.01f;
        private const float ViewportTolerance = 0.01f;

        // Zoomed out, the view only travels along X, so a ball off the line it travels on settles close to the spot.
        private const float RestTolerance = 0.05f;
        private const float BallHeight = 0.12f;
        private const float FramedLength = 36f;
        private const float FollowTime = 0.15f;
        private const float ReturnTime = 0.6f;

        private static readonly Rect Zone = Rect.MinMaxRect(-0.8f, -0.85f, 0.8f, 0.7f);
        private static readonly Vector2 Rest = new Vector2(0f, -0.4f);
        private static readonly Vector3 Tee = new Vector3(-37f, BallHeight, 0f);

        private CameraView _view;
        private CameraNavigator _navigator;
        private CameraFollow _follow;

        [SetUp]
        public void SetUp()
        {
            _view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, Aspect);
            UseCourse(new Bounds(Vector3.zero, new Vector3(80f, 4f, 10f)));
        }

        private void UseCourse(Bounds course)
        {
            CourseFrame frame = CameraFraming.FitCourse(CameraFraming.Corners(course), _view, 0.94f, FramedLength);
            Rect limits = Rect.MinMaxRect(course.min.x, course.min.z, course.max.x, course.max.z);
            _navigator = new CameraNavigator(frame.Home, _view, limits, 0f, 0.3f, frame.CorridorLength);
            _follow = new CameraFollow(_navigator, _view, Zone, Rest, FollowTime, ReturnTime);
        }

        private Vector2 OnScreen(Vector3 ball)
        {
            return _view.ToViewport(_navigator.Position, ball);
        }

        private void Run(Vector3 ball, FollowedBallState state, float seconds, bool fingerDown = false, float frame = Frame)
        {
            for (float time = 0f; time < seconds; time += frame)
            {
                _follow.Update(ball, state, fingerDown, frame);
            }
        }

        /// <summary>A ball this far along the view's up direction, at rest there, with the camera settled on it.</summary>
        private Vector3 BallAboveTheZone()
        {
            Vector3 ball = _view.OnPlane(_navigator.Position, new Vector2(0f, 0.95f), BallHeight);
            Assert.That(OnScreen(ball).y, Is.GreaterThan(Zone.yMax));
            return ball;
        }

        [Test]
        public void AtRest_WithNoShot_NeverMovesTheCamera()
        {
            _follow.PlaceOn(Tee);
            _navigator.Move(new Vector3(8f, 0f, 0f));
            Vector3 panned = _navigator.Position;

            Run(Tee, FollowedBallState.AtRest, 2f);

            Assert.That((_navigator.Position - panned).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void PlaceOn_PutsTheBallAtItsRestSpot_AtOnce()
        {
            var ball = new Vector3(0f, BallHeight, 0f);

            _follow.PlaceOn(ball);

            Vector2 screen = OnScreen(ball);
            Assert.That(screen.y, Is.EqualTo(Rest.y).Within(RestTolerance));
        }

        [Test]
        public void DefaultZoom_StartsCloserThanFullyOut()
        {
            var camera = AssetDatabase.LoadAssetAtPath<CameraRigSettings>("Assets/_Project/Settings/CameraRigSettings.asset");

            Assert.That(camera.DefaultZoom, Is.GreaterThan(0f));
        }

        [Test]
        public void Moving_InsideTheZone_LeavesTheCameraStill()
        {
            _follow.PlaceOn(Tee);
            Vector3 before = _navigator.Position;

            Run(Tee + new Vector3(4f, 0f, 0f), FollowedBallState.Moving, 1f);

            Assert.That((_navigator.Position - before).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Moving_PastTheZone_GlidesUntilTheBallIsBackAtItsEdge()
        {
            _follow.PlaceOn(Tee);
            Vector3 ball = BallAboveTheZone();

            Run(ball, FollowedBallState.Moving, 3f);

            Assert.That(OnScreen(ball).y, Is.EqualTo(Zone.yMax).Within(ViewportTolerance));
        }

        [Test]
        public void Moving_GlideStartsGently()
        {
            _follow.PlaceOn(Tee);
            Vector3 ball = BallAboveTheZone();
            Vector3 before = _navigator.Position;

            _follow.Update(ball, FollowedBallState.Moving, false, Frame);
            float first = (_navigator.Position - before).magnitude;

            before = _navigator.Position;
            _follow.Update(ball, FollowedBallState.Moving, false, Frame);
            float second = (_navigator.Position - before).magnitude;

            Assert.That(first, Is.LessThan(second));
        }

        [TestCase(1f)]
        [TestCase(0.65f)]
        [TestCase(0.01f)]
        public void FullPowerShot_NeverLeavesTheView_NorPullsTheCameraBack(float zoomScale)
        {
            // The longest arc at 40 degrees: 22 m of carry, about 4.6 m high. 0.65 is the default zoom and 0.01 zooms fully in. That leaves
            // this course's camera about 8 m up. It doesn't cover Main's par 3, whose closest camera is 6.2 m up: there
            // a near full power apex passes the top of the view for under a second, known and left to the tuning PR.
            const float Speed = 14.8f;
            const float Gravity = 9.81f;
            float horizontal = Speed * Mathf.Cos(40f * Mathf.Deg2Rad);
            float vertical = Speed * Mathf.Sin(40f * Mathf.Deg2Rad);
            float flight = 2f * vertical / Gravity;

            _navigator.ZoomAround(Vector2.zero, zoomScale);
            _follow.PlaceOn(Tee);
            float previousX = _navigator.Position.x;

            for (float time = 0f; time < flight; time += Frame)
            {
                var ball = new Vector3(Tee.x + (horizontal * time), BallHeight + (vertical * time) - (0.5f * Gravity * time * time), 0f);
                _follow.Update(ball, FollowedBallState.Moving, false, Frame);

                Vector2 screen = OnScreen(ball);
                Assert.That(screen.y, Is.LessThan(1f), $"at {time} s");
                Assert.That(Mathf.Abs(screen.x), Is.LessThan(1f), $"at {time} s");
                Assert.That(_navigator.Position.x, Is.GreaterThanOrEqualTo(previousX - Tolerance), $"at {time} s");
                previousX = _navigator.Position.x;
            }
        }

        [Test]
        public void Finger_DuringAShot_PausesTheFollow_EvenAfterItLifts()
        {
            _follow.PlaceOn(Tee);
            Vector3 ball = BallAboveTheZone();
            Vector3 before = _navigator.Position;

            Run(ball, FollowedBallState.Moving, 0.5f, fingerDown: true);
            Run(ball, FollowedBallState.Moving, 1f);

            Assert.That((_navigator.Position - before).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void BallStopped_WaitsForTheFingerToLift_ThenEasesToIt()
        {
            _follow.PlaceOn(Tee);
            // Far enough to need the camera, and not so far that the end of the course stops it short.
            var ball = new Vector3(-5f, BallHeight, 0f);
            Assert.That(OnScreen(ball).y, Is.GreaterThan(Rest.y));
            Run(ball, FollowedBallState.Moving, 0.1f, fingerDown: true);
            Vector3 before = _navigator.Position;

            Run(ball, FollowedBallState.AtRest, 1f, fingerDown: true);
            Assert.That((_navigator.Position - before).magnitude, Is.EqualTo(0f).Within(Tolerance));

            Run(ball, FollowedBallState.AtRest, 5f);
            Assert.That(OnScreen(ball).y, Is.EqualTo(Rest.y).Within(RestTolerance));
            Assert.That(OnScreen(ball).x, Is.EqualTo(Rest.x).Within(0.2f));
        }

        [Test]
        public void Finger_DuringTheEase_CancelsIt_AndItDoesNotResume()
        {
            _follow.PlaceOn(Tee);
            Vector3 ball = BallAboveTheZone();
            _follow.Update(ball, FollowedBallState.Moving, false, Frame);
            Run(ball, FollowedBallState.AtRest, 0.2f);
            Assert.That(_follow.IsReturning, Is.True);

            _follow.Update(ball, FollowedBallState.AtRest, true, Frame);
            Vector3 stopped = _navigator.Position;
            Assert.That(_follow.IsReturning, Is.False);

            Run(ball, FollowedBallState.AtRest, 2f);

            Assert.That((_navigator.Position - stopped).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Ease_Ends_AndHandsTheCameraBackToThePlayer()
        {
            _follow.PlaceOn(Tee);
            Vector3 ball = BallAboveTheZone();
            _follow.Update(ball, FollowedBallState.Moving, false, Frame);
            Run(ball, FollowedBallState.AtRest, 10f);

            Assert.That(_follow.IsReturning, Is.False);

            _navigator.Move(new Vector3(5f, 0f, 0f));
            Vector3 panned = _navigator.Position;
            Run(ball, FollowedBallState.AtRest, 1f);

            Assert.That((_navigator.Position - panned).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void HeldBall_IsNotChased_ThenTheCameraReturnsOnceItIsBack()
        {
            _follow.PlaceOn(Tee);
            Vector3 lost = BallAboveTheZone();
            Vector3 before = _navigator.Position;

            Run(lost, FollowedBallState.Holding, 1f);
            Assert.That((_navigator.Position - before).magnitude, Is.EqualTo(0f).Within(Tolerance));

            // The ball returns to where the shot was played.
            Vector3 origin = _view.OnPlane(_navigator.Position, new Vector2(0f, 0.2f), BallHeight);
            Run(origin, FollowedBallState.AtRest, 5f);
            Assert.That(OnScreen(origin).y, Is.EqualTo(Rest.y).Within(RestTolerance));
        }

        [Test]
        public void FramedPar3_FullyZoomedOut_NeverMoves()
        {
            UseCourse(new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f)));
            Vector3 home = _navigator.Position;
            var teeOnPar3 = new Vector3(-14f, BallHeight, 0f);

            _follow.PlaceOn(teeOnPar3);
            Run(new Vector3(14f, 4f, 3f), FollowedBallState.Moving, 2f);
            Run(new Vector3(14f, BallHeight, 3f), FollowedBallState.AtRest, 5f);

            Assert.That((_navigator.Position - home).magnitude, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_follow.IsReturning, Is.False);
        }

        [Test]
        public void ZoomedInPar3_FollowsTheBall_AtThePlayersZoom()
        {
            UseCourse(new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f)));
            _navigator.ZoomAround(Vector2.zero, 0.5f);
            float height = _navigator.Position.y;
            Vector3 ball = BallAboveTheZone();

            Run(ball, FollowedBallState.Moving, 3f);

            Assert.That(OnScreen(ball).y, Is.EqualTo(Zone.yMax).Within(ViewportTolerance));
            Assert.That(_navigator.Position.y, Is.EqualTo(height).Within(Tolerance));
        }

        [Test]
        public void Follow_EndsInTheSamePlace_AtAnyFrameRate()
        {
            _follow.PlaceOn(Tee);
            Vector3 ball = BallAboveTheZone();
            Vector3 start = _navigator.Position;

            Run(ball, FollowedBallState.Moving, 3f, frame: 1f / 30f);
            Vector3 at30 = _navigator.Position;

            UseCourse(new Bounds(Vector3.zero, new Vector3(80f, 4f, 10f)));
            _follow.PlaceOn(Tee);
            Assert.That((_navigator.Position - start).magnitude, Is.EqualTo(0f).Within(Tolerance));
            Run(ball, FollowedBallState.Moving, 3f, frame: 1f / 120f);

            Assert.That((_navigator.Position - at30).magnitude, Is.EqualTo(0f).Within(0.05f));
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 20f)]
        [TestCase(1280f / 2800f)]
        public void RestSpot_LeavesRoomBelowTheBallForAFullPull(float aspect)
        {
            var camera = AssetDatabase.LoadAssetAtPath<CameraRigSettings>("Assets/_Project/Settings/CameraRigSettings.asset");
            var shot = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");

            // As shares of the screen's height: the pull is measured in screen widths.
            float roomBelow = (camera.RestBallViewport.y + 1f) * 0.5f;
            float fullPull = shot.FullPowerPull * aspect;

            Assert.That(roomBelow, Is.GreaterThanOrEqualTo(fullPull));
        }
    }
}
