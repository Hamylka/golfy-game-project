using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CameraNavigatorTests
    {
        private const float Aspect = 1280f / 2800f;
        private const float ScreenFill = 0.94f;
        private const float ClosestZoom = 0.3f;
        private const float Tolerance = 0.01f;

        private static readonly Bounds Island = new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f));
        private static readonly Rect Limits = new Rect(-17f, -5f, 34f, 10f);

        private CameraView _view;
        private Vector3 _home;
        private CameraNavigator _navigator;

        [SetUp]
        public void SetUp()
        {
            _view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, Aspect);
            _home = CameraFraming.Fit(CameraFraming.Corners(Island), _view, ScreenFill);
            _navigator = new CameraNavigator(_home, _view, Limits, 0f, ClosestZoom);
        }

        private void ZoomFullyIn()
        {
            _navigator.ZoomAround(Vector2.zero, 0.01f);
        }

        private Vector3 CenterOnGround()
        {
            return _view.OnPlane(_navigator.Position, Vector2.zero, 0f);
        }

        [Test]
        public void Starts_FullyZoomedOut_AtTheFramedPosition()
        {
            Assert.That((_navigator.Position - _home).magnitude, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_navigator.Zoom, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ZoomOut_NeverGoesPastTheFramedPosition()
        {
            _navigator.ZoomAround(Vector2.zero, 3f);

            Assert.That((_navigator.Position - _home).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ZoomIn_StopsAtTheClosestLimit()
        {
            ZoomFullyIn();

            Assert.That(_navigator.Zoom, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(_navigator.Position.y, Is.EqualTo(_home.y * ClosestZoom).Within(Tolerance));
        }

        [Test]
        public void ZoomAround_KeepsTheFocusedGroundPointInPlaceOnScreen()
        {
            ZoomFullyIn();
            _navigator.ZoomAround(Vector2.zero, 1.5f);
            var focus = new Vector2(0.1f, 0.05f);
            Vector3 ground = _view.OnPlane(_navigator.Position, focus, 0f);

            _navigator.ZoomAround(focus, 0.9f);

            Vector2 after = _view.ToViewport(_navigator.Position, ground);
            Assert.That(after.x, Is.EqualTo(focus.x).Within(Tolerance));
            Assert.That(after.y, Is.EqualTo(focus.y).Within(Tolerance));
        }

        [Test]
        public void Pan_KeepsTheGrabbedGroundUnderTheFinger()
        {
            ZoomFullyIn();
            var from = new Vector2(0f, 0f);
            var to = new Vector2(0.1f, 0.05f);
            Vector3 grabbed = _view.OnPlane(_navigator.Position, from, 0f);

            _navigator.Pan(from, to);

            Vector2 after = _view.ToViewport(_navigator.Position, grabbed);
            Assert.That(after.x, Is.EqualTo(to.x).Within(Tolerance));
            Assert.That(after.y, Is.EqualTo(to.y).Within(Tolerance));
        }

        [Test]
        public void Pan_KeepsTheCameraHeight()
        {
            ZoomFullyIn();
            float height = _navigator.Position.y;

            _navigator.Pan(Vector2.zero, new Vector2(0.1f, 0.05f));

            Assert.That(_navigator.Position.y, Is.EqualTo(height).Within(Tolerance));
        }

        [Test]
        public void Pan_HasNoEffect_WhenFullyZoomedOut()
        {
            _navigator.Pan(Vector2.zero, new Vector2(0.5f, 0.5f));

            Assert.That((_navigator.Position - _home).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Pan_CannotLeaveTheIsland()
        {
            ZoomFullyIn();

            for (int i = 0; i < 200; i++)
            {
                _navigator.Pan(Vector2.zero, new Vector2(0.9f, 0.9f));
            }

            Vector3 center = CenterOnGround();
            Assert.That(center.x, Is.GreaterThanOrEqualTo(Limits.xMin - Tolerance).And.LessThanOrEqualTo(Limits.xMax + Tolerance));
            Assert.That(center.z, Is.GreaterThanOrEqualTo(Limits.yMin - Tolerance).And.LessThanOrEqualTo(Limits.yMax + Tolerance));
        }

        [Test]
        public void ZoomingBackOut_ReturnsToTheFramedPosition_AfterPanning()
        {
            ZoomFullyIn();
            _navigator.Pan(Vector2.zero, new Vector2(0.6f, -0.4f));

            _navigator.ZoomAround(new Vector2(0.3f, 0.2f), 100f);

            Assert.That((_navigator.Position - _home).magnitude, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Gesture_FingersApart_ZoomsIn()
        {
            _navigator.ApplyGesture(new Vector2(-0.1f, 0f), new Vector2(0.1f, 0f), new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));

            Assert.That(_navigator.Zoom, Is.GreaterThan(0f));
            Assert.That(_navigator.Position.y, Is.LessThan(_home.y));
        }

        [Test]
        public void Gesture_FingersTogether_ZoomsOut()
        {
            ZoomFullyIn();
            float closest = _navigator.Position.y;

            _navigator.ApplyGesture(new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f), new Vector2(-0.1f, 0f), new Vector2(0.1f, 0f));

            Assert.That(_navigator.Position.y, Is.GreaterThan(closest));
        }

        [Test]
        public void Gesture_SameSpread_MovedTogether_PansWithoutZooming()
        {
            ZoomFullyIn();
            _navigator.ZoomAround(Vector2.zero, 1.5f);
            float height = _navigator.Position.y;
            Vector3 before = _navigator.Position;

            _navigator.ApplyGesture(new Vector2(-0.1f, 0f), new Vector2(0.1f, 0f), new Vector2(0f, 0.05f), new Vector2(0.2f, 0.05f));

            Assert.That(_navigator.Position.y, Is.EqualTo(height).Within(0.5f));
            Assert.That((_navigator.Position - before).magnitude, Is.GreaterThan(0.1f));
        }
    }
}
