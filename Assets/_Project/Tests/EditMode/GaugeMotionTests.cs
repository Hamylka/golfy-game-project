using IsoGolf.UI;
using NUnit.Framework;

namespace IsoGolf.Tests
{
    public class GaugeMotionTests
    {
        private static float Run(float start, float target, float smoothTime, float seconds, int frames)
        {
            float value = start;
            for (int i = 0; i < frames; i++)
            {
                value = GaugeMotion.Toward(value, target, smoothTime, seconds / frames);
            }

            return value;
        }

        [Test]
        public void MovesPartWayInOneFrame()
        {
            float value = GaugeMotion.Toward(0f, 1f, 0.08f, 1f / 60f);

            Assert.That(value, Is.GreaterThan(0f));
            Assert.That(value, Is.LessThan(0.5f));
        }

        [Test]
        public void IsTheSameAtAnyFrameRate()
        {
            Assert.That(Run(0.2f, 0.9f, 0.08f, 0.1f, 3), Is.EqualTo(Run(0.2f, 0.9f, 0.08f, 0.1f, 12)).Within(0.0001f));
        }

        [Test]
        public void FallsWhenPowerIsRemoved()
        {
            float value = GaugeMotion.Toward(1f, 0.2f, 0.08f, 1f / 60f);

            Assert.That(value, Is.LessThan(1f));
            Assert.That(value, Is.GreaterThan(0.2f));
        }

        [Test]
        public void ReachesThePowerWithoutOvershooting()
        {
            float value = 0f;
            for (int i = 0; i < 120; i++)
            {
                value = GaugeMotion.Toward(value, 0.7f, 0.08f, 1f / 60f);
                Assert.That(value, Is.LessThanOrEqualTo(0.7f));
            }

            Assert.That(value, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void NoSmoothTime_FollowsAtOnce()
        {
            Assert.That(GaugeMotion.Toward(0f, 0.7f, 0f, 1f / 60f), Is.EqualTo(0.7f));
        }
    }
}
