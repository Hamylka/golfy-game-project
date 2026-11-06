using IsoGolf.Generation;
using NUnit.Framework;

namespace IsoGolf.Tests
{
    public class CourseNoiseTests
    {
        // Pinned once from the implementation. If one changes, every course built from noise changes with it:
        // that has to be a deliberate decision, with the shot sweep re-run.
        [TestCase(3.7f, -1.2f, 0, 0.694991469f)]
        [TestCase(-12.25f, 4.8f, 0, 0.339731038f)]
        [TestCase(70.4f, 1.3f, 0, 0.5703045f)]
        [TestCase(0.5f, 0.5f, 7, 0.286611646f)]
        [TestCase(3.7f, -1.2f, -3, 0.3992708f)]
        public void Value01_MatchesPinnedValues(float x, float y, int seed, float expected)
        {
            Assert.That(CourseNoise.Value01(x, y, seed), Is.EqualTo(expected).Within(1e-6f));
        }

        [Test]
        public void Hash_IsPinned()
        {
            Assert.That(CourseNoise.Hash(1, 2, 3), Is.EqualTo(1599891056u));
            Assert.That(CourseNoise.Hash(0, 0, 0), Is.Not.EqualTo(0u));
        }

        [Test]
        public void Value01_IsHalfOnWholeNumbers()
        {
            for (int x = -5; x <= 5; x++)
            {
                for (int y = -5; y <= 5; y++)
                {
                    Assert.That(CourseNoise.Value01(x, y, 11), Is.EqualTo(0.5f).Within(1e-6f));
                }
            }
        }

        [Test]
        public void Value01_StaysInItsDocumentedRange()
        {
            float low = 1f;
            float high = 0f;
            for (int i = 0; i < 300; i++)
            {
                for (int j = 0; j < 300; j++)
                {
                    float value = CourseNoise.Value01((i * 0.137f) - 20f, (j * 0.173f) - 25f);
                    low = System.Math.Min(low, value);
                    high = System.Math.Max(high, value);
                }
            }

            Assert.That(low, Is.GreaterThanOrEqualTo(0f));
            Assert.That(high, Is.LessThanOrEqualTo(1f));

            // And it uses most of it, so a wobble scaled by it keeps its intended size.
            Assert.That(high - low, Is.GreaterThan(0.8f));
        }

        [Test]
        public void Value01_IsSmooth()
        {
            // The steepest a value can change is bounded, including across cell borders, so a tiny step moves it a tiny amount.
            const float step = 0.001f;
            for (int i = 0; i < 2000; i++)
            {
                float x = (i * 0.0731f) - 40f;
                float y = (i * 0.0419f) - 30f;
                float here = CourseNoise.Value01(x, y);
                Assert.That(System.Math.Abs(CourseNoise.Value01(x + step, y) - here), Is.LessThan(step * 2f));
                Assert.That(System.Math.Abs(CourseNoise.Value01(x, y + step) - here), Is.LessThan(step * 2f));
            }
        }

        [Test]
        public void Value01_IsRepeatableAndDependsOnTheSeed()
        {
            int differ = 0;
            for (int i = 0; i < 100; i++)
            {
                float x = i * 0.37f;
                float y = i * -0.23f;
                Assert.That(CourseNoise.Value01(x, y, 5), Is.EqualTo(CourseNoise.Value01(x, y, 5)));
                if (System.Math.Abs(CourseNoise.Value01(x, y, 5) - CourseNoise.Value01(x, y, 6)) > 0.01f)
                {
                    differ++;
                }
            }

            Assert.That(differ, Is.GreaterThan(50));
        }
    }
}
