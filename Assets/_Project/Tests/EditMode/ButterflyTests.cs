using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// M4 PR 4.7: the butterflies and the cloud shadow's path. Butterflies are a view-time layer on their own stream (salt 10), so props
    /// stay bit-identical; they are deterministic from (seed, par, variant), at most one per bush, and loop within a bounded radius of
    /// their bush. A course with no bush has none.
    /// </summary>
    public class ButterflyTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        private ButterflySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<ButterflySettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        private static string Describe(List<ButterflySpec> list)
        {
            var text = new System.Text.StringBuilder();
            foreach (ButterflySpec b in list)
            {
                text.Append($"{Mathf.RoundToInt(b.Centre.x * 100)},{Mathf.RoundToInt(b.Centre.z * 100)},{Mathf.RoundToInt(b.Radius * 100)},{Mathf.RoundToInt(b.Speed * 100)},{Mathf.RoundToInt(b.Phase * 100)},{b.Yellow};");
            }

            return text.ToString();
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void SameSeed_GivesTheSameButterflies(int par)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                string a = Describe(ButterflyPlacement.Place(layout, seed, par, 0, _settings));
                string b = Describe(ButterflyPlacement.Place(layout, seed, par, 0, _settings));
                Assert.AreEqual(a, b, $"seed {seed} par {par}");
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ButterflyCount_IsInRange_AndOnePerBushAtMost(int par)
        {
            int withAny = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                int bushes = 0;
                foreach (CourseProp prop in layout.Props)
                {
                    if (prop.Kind == PropKind.Bush)
                    {
                        bushes++;
                    }
                }

                List<ButterflySpec> list = ButterflyPlacement.Place(layout, seed, par, 0, _settings);
                Assert.LessOrEqual(list.Count, Mathf.Min(bushes, _settings.Count.y), $"seed {seed} par {par}");
                if (bushes == 0)
                {
                    Assert.AreEqual(0, list.Count, $"a course with no bush has none (seed {seed} par {par})");
                }

                if (bushes >= _settings.Count.x)
                {
                    Assert.GreaterOrEqual(list.Count, _settings.Count.x, $"seed {seed} par {par}");
                }

                withAny += list.Count > 0 ? 1 : 0;
            }

            Assert.Greater(withAny, 0, "some course in the sweep has butterflies");
        }

        [Test]
        public void EveryButterfly_LoopsAroundItsBush_WithinTheLoopBounds()
        {
            CourseLayout layout = CourseGenerator.Generate(33, 5, Settings);
            List<ButterflySpec> list = ButterflyPlacement.Place(layout, 33, 5, 0, _settings);
            Assert.IsNotEmpty(list);
            foreach (ButterflySpec b in list)
            {
                bool nearABush = false;
                foreach (CourseProp prop in layout.Props)
                {
                    if (prop.Kind == PropKind.Bush && Vector3.Distance(prop.Root + (Vector3.up * _settings.LoopHeight), b.Centre) < 0.001f)
                    {
                        nearABush = true;
                    }
                }

                Assert.IsTrue(nearABush, "the loop's centre sits over a bush");
                for (float t = 0f; t < 60f; t += 0.5f)
                {
                    Vector3 offset = b.PositionAt(t) - b.Centre;
                    // Each horizontal axis swings by at most the radius, on its own rate, so the loop is a box, not a circle.
                    Assert.LessOrEqual(Mathf.Abs(offset.x), b.Radius + 0.001f);
                    Assert.LessOrEqual(Mathf.Abs(offset.z), b.Radius + 0.001f);
                    Assert.LessOrEqual(Mathf.Abs(offset.y), b.Rise + 0.001f);
                }
            }
        }

        [Test]
        public void ButterflySettings_DefaultsKeepEveryRangeOrdered()
        {
            Assert.LessOrEqual(_settings.LoopRadius.x, _settings.LoopRadius.y);
            Assert.LessOrEqual(_settings.Rise.x, _settings.Rise.y);
            Assert.LessOrEqual(_settings.LoopSpeed.x, _settings.LoopSpeed.y);
            Assert.GreaterOrEqual(_settings.Count.x, 1);
        }

        [Test]
        public void CloudShadowPath_StaysBetweenTeeAndHole_AndStartsHalfway()
        {
            var tee = new Vector2(-20f, 3f);
            var hole = new Vector2(24f, -5f);
            Vector2 start = CloudShadowView.PathAt(tee, hole, 0f, 125f);
            Assert.AreEqual(Vector2.Lerp(tee, hole, 0.5f).x, start.x, 0.001f);
            for (float t = 0f; t < 250f; t += 5f)
            {
                Vector2 p = CloudShadowView.PathAt(tee, hole, t, 125f);
                Assert.GreaterOrEqual(p.x, tee.x - 0.001f);
                Assert.LessOrEqual(p.x, hole.x + 0.001f);
            }
        }
    }
}
