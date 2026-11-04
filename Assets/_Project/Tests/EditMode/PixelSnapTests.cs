using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class PixelSnapTests
    {
        private const int PixelsPerUnit = 24;

        // Positions are compared in view space after float round trips, so allow a sliver of a pixel.
        private const float TexelTolerance = 0.001f;

        private static readonly Quaternion Rotation = IsoCameraConvention.Rotation;

        private static readonly Vector3[] Positions =
        {
            new Vector3(-24.49f, 20f, -24.49f),
            new Vector3(0.013f, 3.37f, -7.021f),
            new Vector3(120.777f, 20f, -24.49f),
        };

        private static Vector3 ToView(Vector3 world)
        {
            return Quaternion.Inverse(Rotation) * world;
        }

        [Test]
        public void Snap_LandsOnWholeGamePixels()
        {
            foreach (Vector3 position in Positions)
            {
                Vector3 view = ToView(PixelGrid.Snap(position, Rotation, PixelsPerUnit).Position);

                float x = view.x * PixelsPerUnit;
                float y = view.y * PixelsPerUnit;

                Assert.That(x, Is.EqualTo(Mathf.Round(x)).Within(TexelTolerance));
                Assert.That(y, Is.EqualTo(Mathf.Round(y)).Within(TexelTolerance));
            }
        }

        [Test]
        public void Snap_KeepsTheDepthAlongTheCameraAxis()
        {
            foreach (Vector3 position in Positions)
            {
                Vector3 snapped = PixelGrid.Snap(position, Rotation, PixelsPerUnit).Position;

                Assert.That(ToView(snapped).z, Is.EqualTo(ToView(position).z).Within(TexelTolerance));
            }
        }

        [Test]
        public void Residual_IsNeverMoreThanHalfAGamePixel()
        {
            for (int i = 0; i < 500; i++)
            {
                var position = new Vector3(i * 0.0173f, 20f, i * -0.0091f);
                Vector2 residual = PixelGrid.Snap(position, Rotation, PixelsPerUnit).ResidualTexels;

                Assert.That(Mathf.Abs(residual.x), Is.LessThanOrEqualTo(0.5f + TexelTolerance));
                Assert.That(Mathf.Abs(residual.y), Is.LessThanOrEqualTo(0.5f + TexelTolerance));
            }
        }

        [Test]
        public void SnappedPositionPlusResidual_GivesBackTheRequestedPosition()
        {
            foreach (Vector3 position in Positions)
            {
                PixelSnap snap = PixelGrid.Snap(position, Rotation, PixelsPerUnit);
                Vector3 view = ToView(snap.Position);
                Vector3 requested = ToView(position);

                Assert.That((view.x * PixelsPerUnit) + snap.ResidualTexels.x, Is.EqualTo(requested.x * PixelsPerUnit).Within(TexelTolerance));
                Assert.That((view.y * PixelsPerUnit) + snap.ResidualTexels.y, Is.EqualTo(requested.y * PixelsPerUnit).Within(TexelTolerance));
            }
        }

        [Test]
        public void PresentOffset_WithNoResidual_IsJustTheMargin()
        {
            PixelFrame frame = PixelGrid.Solve(new Vector2Int(1080, 2400), 270, PixelsPerUnit, 1);

            Vector2 offset = PixelGrid.PresentOffset(frame, Vector2.zero, 1);

            Assert.That(offset.x * frame.RenderSize.x, Is.EqualTo(1f).Within(TexelTolerance));
            Assert.That(offset.y * frame.RenderSize.y, Is.EqualTo(1f).Within(TexelTolerance));
        }

        [TestCase(0.26f, 0.25f)]
        [TestCase(-0.26f, -0.25f)]
        [TestCase(0.49f, 0.5f)]
        [TestCase(0.1f, 0f)]
        public void PresentOffset_RoundsTheResidualToWholeScreenPixels(float residual, float expectedTexels)
        {
            PixelFrame frame = PixelGrid.Solve(new Vector2Int(1080, 2400), 270, PixelsPerUnit, 1);

            Vector2 offset = PixelGrid.PresentOffset(frame, new Vector2(residual, residual), 1);

            Assert.That((offset.x * frame.RenderSize.x) - 1f, Is.EqualTo(expectedTexels).Within(TexelTolerance));
            Assert.That((offset.y * frame.RenderSize.y) - 1f, Is.EqualTo(expectedTexels).Within(TexelTolerance));
        }

        [Test]
        public void PresentOffset_NeverReachesPastTheMargin()
        {
            PixelFrame frame = PixelGrid.Solve(new Vector2Int(1080, 2400), 270, PixelsPerUnit, 1);

            Vector2 lowest = PixelGrid.PresentOffset(frame, new Vector2(-0.5f, -0.5f), 1);
            Vector2 highest = PixelGrid.PresentOffset(frame, new Vector2(0.5f, 0.5f), 1);

            Assert.That(lowest.x, Is.GreaterThan(0f));
            Assert.That(lowest.y, Is.GreaterThan(0f));
            Assert.That(highest.x + frame.UvScale.x, Is.LessThan(1f));
            Assert.That(highest.y + frame.UvScale.y, Is.LessThan(1f));
        }
    }
}
