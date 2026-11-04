using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CameraFramingTests
    {
        private const float ScreenFill = 0.94f;
        private const float Tolerance = 0.01f;

        private static readonly Bounds Island = new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f));

        private static CameraView View(float aspect)
        {
            return new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, aspect);
        }

        [Test]
        public void GroundFootprint_ContainsWhatEveryScreenCornerSees()
        {
            CameraView view = View(1280f / 2800f);
            var position = new Vector3(-30f, 20f, -5f);
            const float height = -20f;

            Rect footprint = CameraFraming.GroundFootprint(position, view, height);

            // The screen's middle lands inside it, and the top reaches further along the course than the bottom.
            Vector3 middle = view.OnPlane(position, Vector2.zero, height);
            Assert.That(footprint.Contains(new Vector2(middle.x, middle.z)), Is.True);
            Vector3 top = view.OnPlane(position, Vector2.up, height);
            Vector3 bottom = view.OnPlane(position, Vector2.down, height);
            Assert.That(top.x, Is.GreaterThan(bottom.x));
            Assert.That(footprint.xMax, Is.GreaterThanOrEqualTo(top.x - Tolerance));
            Assert.That(footprint.xMin, Is.LessThanOrEqualTo(bottom.x + Tolerance));
        }

        [TestCase(1280f / 2800f)]
        [TestCase(1080f / 2400f)]
        [TestCase(9f / 16f)]
        [TestCase(3f / 4f)]
        public void Fit_ShowsTheWholeIsland_FillingTheTighterAxis(float aspect)
        {
            CameraView view = View(aspect);
            Vector3[] corners = CameraFraming.Corners(Island);

            Vector3 position = CameraFraming.Fit(corners, view, ScreenFill);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector2 viewport = view.ToViewport(position, corner);
                minX = Mathf.Min(minX, viewport.x);
                maxX = Mathf.Max(maxX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            Assert.That(minX + maxX, Is.EqualTo(0f).Within(Tolerance), "centred across");
            Assert.That(minY + maxY, Is.EqualTo(0f).Within(Tolerance), "centred up and down");
            Assert.That(Mathf.Max(maxX - minX, maxY - minY) * 0.5f, Is.EqualTo(ScreenFill).Within(Tolerance), "fills the tighter axis");
        }

        [Test]
        public void Fit_KeepsEveryPointInFrontOfTheCamera()
        {
            CameraView view = View(1280f / 2800f);
            Vector3[] corners = CameraFraming.Corners(Island);

            Vector3 position = CameraFraming.Fit(corners, view, ScreenFill);

            foreach (Vector3 corner in corners)
            {
                Vector3 local = Quaternion.Inverse(view.Rotation) * (corner - position);
                Assert.That(local.z, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Fit_IsFurtherBack_OnANarrowerScreen()
        {
            Vector3[] corners = CameraFraming.Corners(Island);

            Vector3 narrow = CameraFraming.Fit(corners, View(0.4f), ScreenFill);
            Vector3 wide = CameraFraming.Fit(corners, View(0.75f), ScreenFill);

            Assert.That(narrow.y, Is.GreaterThan(wide.y));
        }

        [Test]
        public void OnPlane_AndToViewport_AreInverses()
        {
            CameraView view = View(0.5f);
            var camera = new Vector3(-30f, 25f, -5f);
            var viewport = new Vector2(0.3f, -0.4f);

            Vector3 ground = view.OnPlane(camera, viewport, 0f);
            Vector2 back = view.ToViewport(camera, ground);

            Assert.That(ground.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(back.x, Is.EqualTo(viewport.x).Within(Tolerance));
            Assert.That(back.y, Is.EqualTo(viewport.y).Within(Tolerance));
        }
    }
}
