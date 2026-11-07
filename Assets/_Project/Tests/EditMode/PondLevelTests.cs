using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class PondLevelTests
    {
        private const float LowLevel = -0.2f;
        private const float HighLevel = 0.6f;
        private const float Tolerance = 0.0005f;

        private PaletteSettings _palette;

        // Two ponds inland, at different levels: the second sits on a rise.
        private static readonly EllipseShape LowPond = new EllipseShape(new Vector2(-8f, 0f), new Vector2(2.5f, 2f), 0.1f);
        private static readonly EllipseShape HighPond = new EllipseShape(new Vector2(8f, 0f), new Vector2(2.5f, 2f), 0.1f);

        [SetUp]
        public void SetUp() => _palette = ScriptableObject.CreateInstance<PaletteSettings>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_palette);

        private static CourseLayout TwoPonds()
        {
            return Layout(
                new[] { new PondLayout(LowPond, LowLevel), new PondLayout(HighPond, HighLevel) },
                new[] { new GroundSwell(new Vector2(8f, 0f), 5f, 1.5f) });
        }

        [Test]
        public void WaterTriangles_SitAtTheirOwnPondsLevel()
        {
            TerrainResult terrain = TerrainBuilder.Build(TwoPonds(), _palette);

            int low = 0;
            int high = 0;
            foreach (Vector3 p in terrain.Water.Positions)
            {
                bool nearLow = p.x < 0f;
                Assert.That(p.y, Is.EqualTo(nearLow ? LowLevel : HighLevel).Within(Tolerance), $"water vertex {p}");
                if (nearLow)
                {
                    low++;
                }
                else
                {
                    high++;
                }
            }

            Assert.That(low, Is.GreaterThan(0));
            Assert.That(high, Is.GreaterThan(0));
        }

        [Test]
        public void BankWalls_GoDownToTheirOwnPondsLevel()
        {
            CourseLayout layout = TwoPonds();
            var fields = new CourseFields(layout);
            TerrainResult terrain = TerrainBuilder.Build(layout, _palette);

            // A wall's lower vertices lie on the pond's border at the water level.
            foreach ((float side, float level) in new[] { (-1f, LowLevel), (1f, HighLevel) })
            {
                bool found = false;
                foreach (Vector3 p in terrain.Land.Positions)
                {
                    if (p.x * side > 0f && Mathf.Abs(fields.Pond(p)) < 0.01f && Mathf.Abs(p.y - level) < Tolerance)
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True, $"no bank wall reaches {level} on side {side}");
            }
        }

        [Test]
        public void TheBall_LandsAndIsDrawnAtTheLevelOfThePondItIsIn()
        {
            var ground = new BallGround(TwoPonds());

            Assert.That(ground.Surface(-8f, 0f), Is.EqualTo(CourseSurface.Water));
            Assert.That(ground.Surface(8f, 0f), Is.EqualTo(CourseSurface.Water));
            Assert.That(ground.LandingHeight(-8f, 0f), Is.EqualTo(LowLevel));
            Assert.That(ground.LandingHeight(8f, 0f), Is.EqualTo(HighLevel));
            Assert.That(ground.DrawnHeight(-8f, 0f), Is.EqualTo(LowLevel));
            Assert.That(ground.DrawnHeight(8f, 0f), Is.EqualTo(HighLevel));
        }

        [Test]
        public void WaterLevelAt_IsTheNearestPondsLevel_AndTheLayoutsDefaultWithNoPond()
        {
            var fields = new CourseFields(TwoPonds());

            Assert.That(fields.WaterLevelAt(new Vector3(-8f, 0f, 0f)), Is.EqualTo(LowLevel));
            Assert.That(fields.WaterLevelAt(new Vector3(8f, 0f, 0f)), Is.EqualTo(HighLevel));
            Assert.That(fields.WaterLevelAt(new Vector3(-3f, 0f, 0f)), Is.EqualTo(LowLevel), "nearest pond");
            Assert.That(fields.WaterLevelAt(new Vector3(3f, 0f, 0f)), Is.EqualTo(HighLevel), "nearest pond");

            var none = new CourseFields(Layout(new PondLayout[0], new GroundSwell[0], -0.37f));
            Assert.That(none.WaterLevelAt(new Vector3(1f, 0f, 1f)), Is.EqualTo(-0.37f));
        }

        [Test]
        public void APondAtTheIslandEdge_EmitsWaterEdgeSegmentsAtItsOwnLevel()
        {
            const float EdgeLevel = 0.3f;
            var edgePond = new EllipseShape(new Vector2(0f, 4f), new Vector2(3f, 2.5f), 0f);
            CourseLayout layout = Layout(new[] { new PondLayout(edgePond, EdgeLevel) }, new GroundSwell[0]);
            TerrainResult terrain = TerrainBuilder.Build(layout, _palette);

            var water = new List<EdgeSegment>();
            foreach (EdgeSegment segment in terrain.Edge)
            {
                if (segment.Inner == EdgeSurface.Water)
                {
                    water.Add(segment);
                }
            }

            Assert.That(water.Count, Is.GreaterThan(0), "the pond crosses the island edge");
            foreach (EdgeSegment segment in water)
            {
                Assert.That(segment.A.y, Is.EqualTo(EdgeLevel).Within(Tolerance));
                Assert.That(segment.B.y, Is.EqualTo(EdgeLevel).Within(Tolerance));
            }
        }

        [Test]
        public void SandOnAGeneratedCourse_IsCalm_AndOnAHandMadeOneItIsNot()
        {
            var bunker = new EllipseShape(new Vector2(0f, 3f), new Vector2(3f, 2f), 0f);
            var generated = new CourseFields(SandLayout(bunker, true));
            var handMade = new CourseFields(SandLayout(bunker, false));

            float handMadeWorst = 0f;
            for (float x = -1.5f; x <= 1.5f; x += 0.25f)
            {
                for (float z = 2.2f; z <= 3.8f; z += 0.25f)
                {
                    Assert.That(generated.Height(x, z), Is.EqualTo(generated.BroadHeight(x, z)).Within(0.001f), $"calm sand at {x}, {z}");
                    Assert.That(generated.FeltHeight(x, z), Is.EqualTo(generated.BroadHeight(x, z)).Within(0.001f));
                    handMadeWorst = Mathf.Max(handMadeWorst, Mathf.Abs(handMade.Height(x, z) - handMade.BroadHeight(x, z)));
                }
            }

            Assert.That(handMadeWorst, Is.GreaterThan(0.01f), "the hand-made path keeps its facets");

            // Well outside the bunker the generated rough keeps its facets.
            float outside = 0f;
            for (float x = -4f; x <= 4f; x += 0.5f)
            {
                outside = Mathf.Max(outside, Mathf.Abs(generated.Height(x, 8f) - generated.BroadHeight(x, 8f)));
            }

            Assert.That(outside, Is.GreaterThan(0.01f));
        }

        private static CourseLayout SandLayout(EllipseShape bunker, bool alongCentreline)
        {
            var tee = new Vector2(-30f, 0f);
            var hole = new Vector2(30f, 0f);
            return new CourseLayout(
                new[] { new IslandOutline(40f, 20f, 0f, 0.2f) },
                new CoursePath(new[] { tee, hole }),
                new FairwayLayout(-30f, 30f, 2f, 0f, 1f),
                new GreenLayout[0],
                new[] { bunker },
                new PondLayout[0],
                new[] { new GroundSwell(new Vector2(0f, 3f), 4f, 0.5f) },
                -0.2f,
                new GroundRelief(1f, 0.1f, 0.4f, 4f, 0.5f),
                tee,
                hole,
                1f,
                1f,
                new EdgeLayout(Vector2.one, 1f),
                alongCentreline);
        }

        private static CourseLayout Layout(PondLayout[] ponds, GroundSwell[] swells, float defaultLevel = -0.2f)
        {
            var tee = new Vector2(-14f, 0f);
            var hole = new Vector2(14f, 0f);
            return new CourseLayout(
                new[] { new IslandOutline(20f, 6f, 0f, 0.2f) },
                new CoursePath(new[] { tee, hole }),
                new FairwayLayout(-14f, 14f, 1.5f, 0f, 1f),
                new GreenLayout[0],
                new EllipseShape[0],
                ponds,
                swells,
                defaultLevel,
                new GroundRelief(1f, 0f, 0f, 4f, 0.5f),
                tee,
                hole,
                1f,
                1f,
                new EdgeLayout(new Vector2(0.9f, 1.8f), 0.7f));
        }
    }
}
