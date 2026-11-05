using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CameraGestureRouterTests
    {
        private const float Tolerance = 0.001f;
        private const float Frame = 1f / 60f;

        private static readonly Bounds Island = new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f));
        private static readonly Rect Limits = new Rect(-17f, -5f, 34f, 10f);

        private static readonly Vector2 Start = new Vector2(0f, 0f);
        private static readonly Vector2 Moved = new Vector2(0.1f, 0.05f);

        private CameraNavigator _navigator;
        private CameraGestureRouter _router;
        private Vector3 _before;

        [SetUp]
        public void SetUp()
        {
            var view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, 1280f / 2800f);
            Vector3 home = CameraFraming.Fit(CameraFraming.Corners(Island), view, 0.94f);
            _navigator = new CameraNavigator(home, view, Limits, 0f, 0.3f);

            // Zoomed in, so the view has room to pan.
            _navigator.ZoomAround(Vector2.zero, 0.01f);
            _router = new CameraGestureRouter(_navigator, new PanMomentum(0.5f, 0.1f, 0.2f, 0.03f));
            _before = _navigator.Position;
        }

        private float MovedBy => (_navigator.Position - _before).magnitude;

        private void OneFinger(Vector2 position, bool enabled = true, bool claimed = false)
        {
            _router.Update(1, position, default, enabled, _ => claimed, Frame);
        }

        private void TwoFingers(Vector2 a, Vector2 b)
        {
            _router.Update(2, a, b, true, _ => false, Frame);
        }

        private void NoFingers()
        {
            _router.Update(0, default, default, true, null, Frame);
        }

        [Test]
        public void OneFinger_Dragging_PansTheCamera()
        {
            OneFinger(Start);
            OneFinger(Moved);

            Assert.That(MovedBy, Is.GreaterThan(0.1f));
        }

        [Test]
        public void OneFinger_LandingAlone_DoesNotMoveTheCamera()
        {
            OneFinger(Moved);

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ClaimedTouch_LeavesTheCameraAlone()
        {
            OneFinger(Start, claimed: true);
            OneFinger(Moved, claimed: true);

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ClaimedTouch_IsOnlyAskedOnce_WhenTheFingerLands()
        {
            int asked = 0;
            _router.Update(1, Start, default, true, _ => { asked++; return false; }, Frame);
            _router.Update(1, Moved, default, true, _ => { asked++; return true; }, Frame);

            Assert.That(asked, Is.EqualTo(1));
            Assert.That(MovedBy, Is.GreaterThan(0.1f));
        }

        [Test]
        public void ClaimedTouch_IgnoresASecondFinger()
        {
            OneFinger(Start, claimed: true);
            TwoFingers(Start, Moved);
            TwoFingers(Start, new Vector2(0.4f, 0.4f));

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void AfterAClaimedTouchEnds_TheNextDragPans()
        {
            OneFinger(Start, claimed: true);
            NoFingers();
            OneFinger(Start);
            OneFinger(Moved);

            Assert.That(MovedBy, Is.GreaterThan(0.1f));
        }

        [Test]
        public void Disabled_LeavesTheCameraAlone_UntilEveryFingerLifts()
        {
            OneFinger(Start, enabled: false);
            OneFinger(Moved, enabled: true);

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));

            NoFingers();
            OneFinger(Start);
            OneFinger(Moved);

            Assert.That(MovedBy, Is.GreaterThan(0.1f));
        }

        [Test]
        public void SecondFingerLanding_DoesNotJump()
        {
            OneFinger(Start);
            TwoFingers(Start, new Vector2(0.5f, 0.5f));

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void SecondFingerLifting_DoesNotJump_AndPanningContinues()
        {
            TwoFingers(new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));
            OneFinger(new Vector2(0.2f, 0f));

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));

            OneFinger(new Vector2(0.3f, 0.05f));

            Assert.That(MovedBy, Is.GreaterThan(0.1f));
        }

        [Test]
        public void TwoFingers_ZoomWhenSpreading()
        {
            _navigator.ZoomAround(Vector2.zero, 2f);
            float height = _navigator.Position.y;

            TwoFingers(new Vector2(-0.1f, 0f), new Vector2(0.1f, 0f));
            TwoFingers(new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));

            Assert.That(_navigator.Position.y, Is.LessThan(height));
        }

        [Test]
        public void ThirdFinger_StopsTheGesture()
        {
            TwoFingers(new Vector2(-0.1f, 0f), new Vector2(0.1f, 0f));
            _router.Update(3, new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), true, null, Frame);

            Assert.That(MovedBy, Is.EqualTo(0f).Within(Tolerance));
        }

        private void Flick()
        {
            OneFinger(Start);
            OneFinger(Moved);
            OneFinger(Moved * 2f);
            OneFinger(Moved * 3f);
        }

        private float GlideDistance(int frames, bool enabled = true)
        {
            Vector3 from = _navigator.Position;
            for (int i = 0; i < frames; i++)
            {
                _router.Update(0, default, default, enabled, null, Frame);
            }

            return (_navigator.Position - from).magnitude;
        }

        [Test]
        public void Glide_KeepsMovingAfterAQuickDragIsReleased()
        {
            Flick();

            Assert.That(GlideDistance(10), Is.GreaterThan(0.05f));
        }

        [Test]
        public void Glide_NoneAfterAFingerPausesBeforeLifting()
        {
            Flick();
            for (int i = 0; i < 12; i++)
            {
                OneFinger(Moved * 3f);
            }

            Assert.That(GlideDistance(10), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Glide_StopsWhenAFingerLands()
        {
            Flick();
            GlideDistance(2);
            OneFinger(Start);
            NoFingers();

            Assert.That(GlideDistance(10), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Glide_StopsWhenAClaimedTouchLands()
        {
            Flick();
            GlideDistance(2);
            OneFinger(Start, claimed: true);
            NoFingers();

            Assert.That(GlideDistance(10), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Glide_StopsWhenGesturesAreTurnedOff()
        {
            Flick();
            GlideDistance(2);

            Assert.That(GlideDistance(10, enabled: false), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(GlideDistance(10), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Glide_NoneAfterAPinch()
        {
            TwoFingers(new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));
            TwoFingers(new Vector2(-0.1f, 0.2f), new Vector2(0.3f, 0.2f));
            NoFingers();

            Assert.That(GlideDistance(10), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Glide_NoneWhenPanningIsLockedFullyZoomedOut()
        {
            _navigator.ZoomAround(Vector2.zero, 100f);
            Vector3 home = _navigator.Position;
            Flick();
            GlideDistance(30);

            Assert.That((_navigator.Position - home).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
