using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The two-finger twist: the angle between the fingers, its dead zone, its limit, and when it is cleared.</summary>
    public class CameraTwistTests
    {
        private const float Frame = 1f / 60f;
        private const float DeadZone = 4f;
        private const float MinSpread = 0.1f;
        private const float MaxTwist = 45f;

        private static readonly Rect Limits = new Rect(-100f, -100f, 200f, 200f);

        private CameraNavigator _navigator;
        private CameraFollow _follow;
        private CameraGestureRouter _router;

        // The camera as the fingers first met it: a hand moves the same way whatever the camera does meanwhile.
        private CameraView _view0;
        private Vector3 _position0;

        [SetUp]
        public void SetUp()
        {
            var view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, 1280f / 2800f);
            Vector3 home = CameraFraming.Fit(CameraFraming.Corners(new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f))), view, 0.94f);
            _navigator = new CameraNavigator(home, view, Limits, 0f, 0.3f);
            _navigator.ZoomAround(Vector2.zero, 0.6f);
            _follow = NewFollow(_navigator);
            _router = new CameraGestureRouter(_navigator, new PanMomentum(0.5f, 0.1f, 0.2f, 0.03f), new TwistFilter(DeadZone, MinSpread), _follow.TwistBy);
            _view0 = _navigator.View;
            _position0 = _navigator.Position;
        }

        private static CameraFollow NewFollow(CameraNavigator navigator)
        {
            return new CameraFollow(
                navigator,
                Rect.MinMaxRect(-0.2f, -0.2f, 0.2f, 0.2f),
                -0.85f,
                new Vector2(0f, -0.4f),
                0.08f,
                0.6f,
                ease: new HeadingEase(0.5f, 5f, MaxTwist));
        }

        private Vector2 Screen0(Vector3 ground) => _view0.ToViewport(_position0, ground);

        // Two fingers on the ground 6 m apart around the view's middle, turned by an angle, as the hand would.
        private (Vector2 A, Vector2 B) Fingers(float degrees, float halfGap = 3f)
        {
            Vector3 middle = _view0.OnPlane(_position0, Vector2.zero, 0f);
            Quaternion turn = Quaternion.Euler(0f, degrees, 0f);
            Vector3 offset = new Vector3(1f, 0f, 0.4f).normalized * halfGap;
            return (Screen0(middle + (turn * offset)), Screen0(middle - (turn * offset)));
        }

        private void Frames(float degrees, float halfGap = 3f, int steps = 1)
        {
            for (int step = 1; step <= steps; step++)
            {
                (Vector2 a, Vector2 b) = Fingers(degrees * step / steps, halfGap);
                _router.Update(2, a, b, true, _ => false, Frame);
            }
        }

        private void Land(float halfGap = 3f)
        {
            (Vector2 a, Vector2 b) = Fingers(0f, halfGap);
            _router.Update(2, a, b, true, _ => false, Frame);
        }

        private void Lift()
        {
            _router.Update(0, default, default, true, null, Frame);
        }

        private float Turned => Mathf.DeltaAngle(_follow.Heading, _navigator.Yaw);

        // ----- The angle between the fingers -----

        [TestCase(20f)]
        [TestCase(-20f)]
        public void ApplyGesture_ReturnsTheAngleTheFingersTurned(float degrees)
        {
            (Vector2 a0, Vector2 b0) = Fingers(0f);
            (Vector2 a1, Vector2 b1) = Fingers(degrees);

            float turn = _navigator.ApplyGesture(a0, b0, a1, b1);

            Assert.That(Mathf.Abs(turn), Is.EqualTo(Mathf.Abs(degrees)).Within(0.05f));
        }

        [Test]
        public void ApplyGesture_FingersThatOnlySpread_ReturnNoTurn()
        {
            (Vector2 a0, Vector2 b0) = Fingers(0f, 2f);
            (Vector2 a1, Vector2 b1) = Fingers(0f, 3f);

            Assert.That(_navigator.ApplyGesture(a0, b0, a1, b1), Is.EqualTo(0f).Within(1e-3f));
        }

        // ----- The ground stays under the fingers -----

        [TestCase(30f)]
        [TestCase(-30f)]
        public void Twist_KeepsTheGroundUnderBothFingers(float degrees)
        {
            Vector3 middle = _view0.OnPlane(_position0, Vector2.zero, 0f);
            Vector3 offset = new Vector3(1f, 0f, 0.4f).normalized * 3f;
            Vector3 groundA = middle + offset;
            Vector3 groundB = middle - offset;

            Land();
            Frames(degrees, steps: 12);

            (Vector2 a, Vector2 b) = Fingers(degrees);
            Assert.That(Mathf.Abs(Turned), Is.EqualTo(Mathf.Abs(degrees) - DeadZone).Within(0.1f), "the dead zone is the only slack");
            AssertNear(groundA, _navigator.GroundUnder(a), 0.35f, "the dead zone leaves a small slip");
            AssertNear(groundB, _navigator.GroundUnder(b), 0.35f);
        }

        [Test]
        public void Twist_WithNoDeadZone_KeepsTheGroundExactlyUnderBothFingers()
        {
            _router = new CameraGestureRouter(_navigator, new PanMomentum(0.5f, 0.1f, 0.2f, 0.03f), new TwistFilter(0f, MinSpread), _follow.TwistBy);
            Vector3 middle = _view0.OnPlane(_position0, Vector2.zero, 0f);
            Vector3 offset = new Vector3(1f, 0f, 0.4f).normalized * 3f;

            Land();
            Frames(25f, steps: 25);

            (Vector2 a, Vector2 b) = Fingers(25f);
            Assert.That(Mathf.Abs(Turned), Is.EqualTo(25f).Within(0.05f));
            AssertNear(middle + offset, _navigator.GroundUnder(a), 0.02f);
            AssertNear(middle - offset, _navigator.GroundUnder(b), 0.02f);
        }

        // ----- Dead zone -----

        [Test]
        public void ASmallWobble_DoesNotTurnTheView()
        {
            Land();
            Frames(3f, steps: 6);
            Frames(-2f, steps: 6);

            Assert.That(_navigator.Yaw, Is.EqualTo(IsoCameraConvention.StraightYaw).Within(1e-4f));
            Assert.That(_follow.Twist, Is.EqualTo(0f));
        }

        [Test]
        public void APinchAlone_LeavesTheHeadingUnchanged()
        {
            float zoomBefore = _navigator.Zoom;
            Land(2f);
            for (int step = 1; step <= 30; step++)
            {
                (Vector2 a, Vector2 b) = Fingers(0f, 2f + (step * 0.05f));
                _router.Update(2, a, b, true, _ => false, Frame);
            }

            Assert.That(_navigator.Zoom, Is.Not.EqualTo(zoomBefore).Within(0.001f), "it did zoom");
            Assert.That(_navigator.Yaw, Is.EqualTo(IsoCameraConvention.StraightYaw).Within(1e-4f));
            Assert.That(_follow.Twist, Is.EqualTo(0f));
        }

        [Test]
        public void TwistFilter_PassesOnlyWhatIsPastTheDeadZone_ThenFollowsOneToOne()
        {
            var filter = new TwistFilter(4f, 0.1f);

            Assert.That(filter.Step(3f, 1f), Is.EqualTo(0f));
            Assert.That(filter.Step(2f, 1f), Is.EqualTo(1f).Within(1e-5f), "5 degrees in, 4 of dead zone");
            Assert.That(filter.Step(0.5f, 1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(filter.Step(-3f, 1f), Is.EqualTo(-3f).Within(1e-5f));
        }

        [Test]
        public void TwistFilter_ReadsTheDeadZoneInEitherDirection()
        {
            var filter = new TwistFilter(4f, 0.1f);

            Assert.That(filter.Step(-6f, 1f), Is.EqualTo(-2f).Within(1e-5f));
        }

        [Test]
        public void TwistFilter_Reset_RestoresTheDeadZone()
        {
            var filter = new TwistFilter(4f, 0.1f);
            filter.Step(10f, 1f);

            filter.Reset();

            Assert.That(filter.Step(2f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void TwistFilter_IgnoresFingersTooCloseTogether()
        {
            var filter = new TwistFilter(0f, 0.1f);

            Assert.That(filter.Step(30f, 0.05f), Is.EqualTo(0f));
            Assert.That(filter.Step(30f, 0.2f), Is.EqualTo(30f).Within(1e-5f));
        }

        [Test]
        public void ANewGesture_StartsWithItsOwnDeadZone()
        {
            Land();
            Frames(3.5f, steps: 3);
            Lift();

            Land();
            Frames(3.5f, steps: 3);

            Assert.That(_follow.Twist, Is.EqualTo(0f), "3.5 degrees twice is not 7");
        }

        // ----- Limit -----

        [Test]
        public void Twist_StopsAtTheLimit_OnBothSides()
        {
            Land();
            Frames(90f, steps: 45);
            Assert.That(Mathf.Abs(Turned), Is.EqualTo(MaxTwist).Within(1e-3f));
            Assert.That(Mathf.Abs(_follow.Twist), Is.EqualTo(MaxTwist).Within(1e-3f));
            float side = Mathf.Sign(_follow.Twist);

            Lift();
            Land();
            Frames(-150f, steps: 75);

            Assert.That(Mathf.Sign(_follow.Twist), Is.EqualTo(-side), "and back across, to the other limit");
            Assert.That(Mathf.Abs(Turned), Is.EqualTo(MaxTwist).Within(1e-3f));
        }

        [Test]
        public void TwistingBackFromTheLimit_TurnsAtOnce()
        {
            Land();
            Frames(90f, steps: 45);
            Lift();
            Land();
            Frames(-20f, steps: 20);

            // Fingers that went on turning past the limit left no debt: the view follows them back right away.
            Assert.That(Mathf.Abs(Turned), Is.LessThan(MaxTwist - 10f));
        }

        // ----- Blocked, cleared -----

        [Test]
        public void Twist_IsIgnoredWhileGesturesAreBlocked()
        {
            for (int step = 0; step <= 20; step++)
            {
                (Vector2 a, Vector2 b) = Fingers(step);
                _router.Update(2, a, b, false, _ => false, Frame);
            }

            Assert.That(_navigator.Yaw, Is.EqualTo(IsoCameraConvention.StraightYaw).Within(1e-4f));
        }

        [Test]
        public void ARouterWithoutATwist_StillPinchesAndPans()
        {
            var plain = new CameraGestureRouter(_navigator, new PanMomentum(0.5f, 0.1f, 0.2f, 0.03f));
            (Vector2 a, Vector2 b) = Fingers(0f, 2f);
            plain.Update(2, a, b, true, _ => false, Frame);
            (a, b) = Fingers(40f, 3f);
            plain.Update(2, a, b, true, _ => false, Frame);

            Assert.That(_navigator.Yaw, Is.EqualTo(IsoCameraConvention.StraightYaw).Within(1e-4f));
        }

        [Test]
        public void TheNextShot_ClearsAGestureTwist_AndTheViewTurnsBackAtRest()
        {
            Vector3 ball = _navigator.ViewCentre;
            Land();
            Frames(30f, steps: 30);
            Lift();
            Assert.That(Mathf.Abs(_follow.Twist), Is.GreaterThan(20f));
            float twisted = _navigator.Yaw;

            _follow.Update(ball + Vector3.up, FollowedBallState.Moving, false, Frame);
            Assert.That(_follow.Twist, Is.EqualTo(0f));
            Assert.That(_navigator.Yaw, Is.EqualTo(twisted).Within(1e-4f), "during flight the camera does not turn");

            for (float time = 0f; time < 0.5f; time += Frame)
            {
                _follow.Update(ball + Vector3.up, FollowedBallState.Moving, false, Frame);
            }

            for (float time = 0f; time < 3f; time += Frame)
            {
                _follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(Turned, Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void TwistingDuringFlight_IsAllowed_AndTheViewKeepsItAtRest()
        {
            Vector3 ball = _navigator.ViewCentre;
            for (float time = 0f; time < 0.3f; time += Frame)
            {
                _follow.Update(ball + Vector3.up, FollowedBallState.Moving, false, Frame);
            }

            Land();
            Frames(30f, steps: 30);
            float twist = _follow.Twist;
            _follow.Update(ball + Vector3.up, FollowedBallState.Moving, true, Frame);
            Assert.That(Mathf.Abs(twist), Is.GreaterThan(20f));
            Assert.That(_follow.Twist, Is.EqualTo(twist), "the follow did not take the twist for a new shot");

            Lift();
            for (float time = 0f; time < 3f; time += Frame)
            {
                _follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(Turned, Is.EqualTo(twist).Within(0.1f), "it rests on the twisted heading");
        }

        [Test]
        public void TheResetView_ClearsAGestureTwist()
        {
            Vector3 ball = _navigator.ViewCentre;
            Land();
            Frames(35f, steps: 35);
            Lift();
            Assert.That(Mathf.Abs(_follow.Twist), Is.GreaterThan(20f));

            _follow.BeginReset(ball);
            for (float time = 0f; time < 1f; time += Frame)
            {
                _follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(_follow.Twist, Is.EqualTo(0f));
            Assert.That(Turned, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TwistBy_KeepsThePivotWhereItIsOnScreen()
        {
            Vector3 pivot = _navigator.ViewCentre + new Vector3(3f, 0f, -2f);
            Vector2 before = _navigator.View.ToViewport(_navigator.Position, pivot);

            _follow.TwistBy(25f, pivot);

            Assert.That(Mathf.Abs(Turned), Is.EqualTo(25f).Within(1e-3f));
            Vector2 after = _navigator.View.ToViewport(_navigator.Position, pivot);
            Assert.That((after - before).magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void ATwistDuringTheRestEase_EndsTheEase_SoNothingFightsTheFingers()
        {
            Vector3 ball = _navigator.ViewCentre + new Vector3(2f, 0f, 0f);
            _follow.Update(ball + Vector3.up, FollowedBallState.Moving, false, Frame);
            _follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            Assert.That(_follow.IsReturning);

            Land();
            Frames(15f, steps: 15);

            Assert.That(_follow.IsEasing, Is.False);
            float yaw = _navigator.Yaw;
            for (float time = 0f; time < 1f; time += Frame)
            {
                _follow.Update(ball, FollowedBallState.AtRest, true, Frame);
            }

            Assert.That(_navigator.Yaw, Is.EqualTo(yaw).Within(1e-4f));
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, float tolerance, string message = null)
        {
            Assert.That((expected - actual).magnitude, Is.LessThanOrEqualTo(tolerance), message);
        }
    }
}
