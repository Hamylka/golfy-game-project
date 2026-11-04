using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class PixelGridTests
    {
        private const int TargetWidth = 270;
        private const int PixelsPerUnit = 24;
        private const int Margin = 1;
        private const float Tolerance = 0.00001f;

        private static PixelFrame Solve(int width, int height)
        {
            return PixelGrid.Solve(new Vector2Int(width, height), TargetWidth, PixelsPerUnit, Margin);
        }

        [TestCase(720, 3, 240)]
        [TestCase(1080, 4, 270)]
        [TestCase(1440, 5, 288)]
        public void Scale_PicksTheClosestWidthToTarget(int screenWidth, int expectedScale, int expectedViewWidth)
        {
            PixelFrame frame = Solve(screenWidth, 1920);

            Assert.That(frame.Scale, Is.EqualTo(expectedScale));
            Assert.That(frame.ViewSize.x, Is.EqualTo(expectedViewWidth));
        }

        [Test]
        public void Scale_NeverDropsBelowOne()
        {
            Assert.That(Solve(100, 100).Scale, Is.EqualTo(1));
        }

        [Test]
        public void ViewHeight_FillsATallerScreen()
        {
            Assert.That(Solve(1080, 2400).ViewSize, Is.EqualTo(new Vector2Int(270, 600)));
        }

        [Test]
        public void View_RoundsUp_SoNoScreenPixelIsLeftEmpty()
        {
            PixelFrame frame = Solve(1081, 1921);

            Assert.That(frame.ViewSize, Is.EqualTo(new Vector2Int(271, 481)));
        }

        [Test]
        public void RenderSize_IsTheViewPlusTheMarginOnEverySide()
        {
            Assert.That(Solve(1080, 1920).RenderSize, Is.EqualTo(new Vector2Int(272, 482)));
        }

        [Test]
        public void OrthographicSize_KeepsTheWorldScaleOnEveryScreen()
        {
            foreach (PixelFrame frame in new[] { Solve(1080, 1920), Solve(1080, 2400), Solve(720, 1600) })
            {
                float pixelsPerUnit = frame.RenderSize.y / (2f * frame.OrthographicSize);

                Assert.That(pixelsPerUnit, Is.EqualTo(PixelsPerUnit).Within(Tolerance));
            }
        }

        [Test]
        public void Uv_MapsOneGamePixelToExactlyScaleScreenPixels()
        {
            PixelFrame frame = Solve(1080, 2400);

            float screenPixelsPerGamePixelX = 1080f / (frame.UvScale.x * frame.RenderSize.x);
            float screenPixelsPerGamePixelY = 2400f / (frame.UvScale.y * frame.RenderSize.y);

            Assert.That(screenPixelsPerGamePixelX, Is.EqualTo(frame.Scale).Within(Tolerance));
            Assert.That(screenPixelsPerGamePixelY, Is.EqualTo(frame.Scale).Within(Tolerance));
        }

        [Test]
        public void Uv_StartsAfterTheMargin()
        {
            PixelFrame frame = Solve(1080, 1920);

            Assert.That(frame.UvOffset.x * frame.RenderSize.x, Is.EqualTo(Margin).Within(Tolerance));
            Assert.That(frame.UvOffset.y * frame.RenderSize.y, Is.EqualTo(Margin).Within(Tolerance));
        }
    }
}
