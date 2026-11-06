using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// A course with a gap: two islands of one course with open sky between them. The fixture is
    /// GapCourseShape: the first island spans x -19 to 1, the second 5.5 to 22.5, the pond is on the first and the green on the second.
    /// </summary>
    public class MultiIslandTests
    {
        private const string GapShapePath = "Assets/_Project/Settings/GapCourseShape.asset";

        // The tips of the two islands, on the play axis.
        private const float FirstTip = 1f;
        private const float SecondTip = 5.5f;

        // Tufts flare out a little past the edge, so the check keeps this far from the tips.
        private const float TuftMargin = 0.6f;

        private CourseShapeSettings _shape;
        private CourseLayout _layout;
        private CourseFields _fields;
        private BallGround _ground;
        private PaletteSettings _palette;
        private TuftSettings _tufts;
        private ShotSettings _shot;
        private TerrainResult _terrain;

        [SetUp]
        public void SetUp()
        {
            _shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(GapShapePath);
            _layout = _shape.ToLayout();
            _fields = new CourseFields(_layout);
            _ground = new BallGround(_layout);
            _palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            _tufts = AssetDatabase.LoadAssetAtPath<TuftSettings>("Assets/_Project/Settings/IslandTufts.asset");
            _shot = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            _terrain = TerrainBuilder.Build(_layout, _palette);
        }

        [Test]
        public void Layout_HoldsTwoIslandsThatCoverBothEnds()
        {
            Assert.That(_layout.Islands.Count, Is.EqualTo(2));
            Assert.That(_layout.HalfExtent.x, Is.EqualTo(22.5f).Within(0.001f));
        }

        [Test]
        public void AGapPoint_IsOffTheIsland_OnTheFieldAndOnTheBallsGround()
        {
            for (float x = FirstTip + 0.5f; x < SecondTip - 0.5f; x += 0.5f)
            {
                Assert.That(_fields.Island(new Vector3(x, 0f, 0f)), Is.GreaterThan(0f), $"x {x}");
                Assert.That(_ground.Surface(x, 0f), Is.EqualTo(CourseSurface.OffIsland), $"x {x}");
                Assert.That(_ground.IsOnIsland(x, 0f), Is.False, $"x {x}");
            }
        }

        [Test]
        public void BothIslands_AreLand()
        {
            Assert.That(_ground.Surface(-5f, 0f), Is.Not.EqualTo(CourseSurface.OffIsland));
            Assert.That(_ground.Surface(10f, 0f), Is.Not.EqualTo(CourseSurface.OffIsland));
            Assert.That(_ground.Surface(_shape.Tee.x, _shape.Tee.y), Is.EqualTo(CourseSurface.Fairway));
            Assert.That(_ground.Surface(_shape.Hole.x, _shape.Hole.y), Is.EqualTo(CourseSurface.Green));
        }

        [Test]
        public void Terrain_HasNoGeometryInTheGap()
        {
            foreach (TerrainMeshData mesh in new[] { _terrain.Land, _terrain.Water })
            {
                foreach (Vector3 p in mesh.Positions)
                {
                    // Every vertex, walls and earth band included, lies on or inside an island's outline
                    // (the cut lands within a quarter of a millimetre of it).
                    Assert.That(_fields.Island(p), Is.LessThan(0.01f), $"vertex {p}");
                    Assert.That(p.x < FirstTip + 0.01f || p.x > SecondTip - 0.01f, Is.True, $"vertex {p} is in the gap");
                }
            }
        }

        [Test]
        public void EachIsland_HasItsOwnLandEdgeAndEarthBand()
        {
            AssertBothSides(_terrain.Land.Positions, "land");
            AssertBothSides(_terrain.Outline, "earth band");

            // The edge around each tip faces the gap.
            var facingTheGap = new[] { 0, 0 };
            foreach (EdgeSegment segment in _terrain.Edge)
            {
                float middle = (segment.A.x + segment.B.x) * 0.5f;
                if (middle > FirstTip - 1f && middle < FirstTip + 0.1f && segment.Outward.x > 0.5f)
                {
                    facingTheGap[0]++;
                }

                if (middle < SecondTip + 1f && middle > SecondTip - 0.1f && segment.Outward.x < -0.5f)
                {
                    facingTheGap[1]++;
                }
            }

            Assert.That(facingTheGap[0], Is.GreaterThan(0), "no edge at the first island's tip");
            Assert.That(facingTheGap[1], Is.GreaterThan(0), "no edge at the second island's tip");
        }

        [Test]
        public void Tufts_GrowAroundEachIsland_AndNeverAcrossTheGap()
        {
            TerrainMeshData tufts = TuftBuilder.Build(_tufts, _palette, _terrain.Edge);

            var first = new List<Vector3>();
            var second = new List<Vector3>();
            foreach (Vector3 p in tufts.Positions)
            {
                Assert.That(p.x < FirstTip + TuftMargin || p.x > SecondTip - TuftMargin, Is.True, $"tuft vertex {p} is in the gap");
                (p.x < FirstTip + TuftMargin ? first : second).Add(p);
            }

            AssertBothSides(tufts.Positions, "tufts");

            // And none of the island's own edge is bare: tufts sit beside the tips too.
            Assert.That(first.Exists(p => p.x > FirstTip - 1.5f), Is.True, "no tufts at the first tip");
            Assert.That(second.Exists(p => p.x < SecondTip + 1.5f), Is.True, "no tufts at the second tip");
        }

        [Test]
        public void EdgeSegments_AreTaggedByTheSurfaceInside()
        {
            // The fixture's pond reaches the first island's edge, so it has both kinds. A water piece lies
            // at the water level, with the pond just inside it; a land piece has dry ground inside.
            int land = 0;
            int water = 0;
            foreach (EdgeSegment segment in _terrain.Edge)
            {
                Vector3 inside = ((segment.A + segment.B) * 0.5f) - (segment.Outward.normalized * 0.05f);
                if (segment.Inner == EdgeSurface.Water)
                {
                    water++;
                    Assert.That(segment.A.y, Is.EqualTo(_layout.WaterLevel).Within(0.0001f));
                    Assert.That(segment.B.y, Is.EqualTo(_layout.WaterLevel).Within(0.0001f));
                    Assert.That(_ground.Surface(inside.x, inside.z), Is.EqualTo(CourseSurface.Water).Or.EqualTo(CourseSurface.OffIsland), $"water edge at {inside}");
                }
                else
                {
                    land++;
                    Assert.That(_ground.Surface(inside.x, inside.z), Is.Not.EqualTo(CourseSurface.Water), $"land edge at {inside}");
                }
            }

            Assert.That(land, Is.GreaterThan(20));
            Assert.That(water, Is.GreaterThan(0), "the fixture's pond reaches the island edge");
        }

        [Test]
        public void EdgeSegments_TheParThreesPondStaysClearOfItsEdge_SoItsTuftsAreUnchanged()
        {
            var par3 = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset").ToLayout();

            foreach (EdgeSegment segment in TerrainBuilder.Build(par3, _palette).Edge)
            {
                Assert.That(segment.Inner, Is.EqualTo(EdgeSurface.Land));
            }
        }

        [Test]
        public void Tufts_SkipWaterEdges_AndKeepTheLandOnesUntouched()
        {
            var land = new EdgeSegment(Vector3.zero, Vector3.right * 2f, Vector3.forward);
            var wet = new EdgeSegment(Vector3.right * 5f, Vector3.right * 7f, Vector3.forward, EdgeSurface.Water);

            List<TuftSpot> withWater = TuftBuilder.Place(new[] { land, wet }, 0.2f, new System.Random(3));
            List<TuftSpot> landOnly = TuftBuilder.Place(new[] { land }, 0.2f, new System.Random(3));

            Assert.That(landOnly.Count, Is.GreaterThan(0));
            Assert.That(TuftBuilder.Place(new[] { wet }, 0.2f, new System.Random(3)), Is.Empty);
            Assert.That(withWater.Count, Is.EqualTo(landOnly.Count));
            for (int i = 0; i < landOnly.Count; i++)
            {
                Assert.That(withWater[i].Position, Is.EqualTo(landOnly[i].Position));
            }
        }

        [Test]
        public void ACarry_OverTheGap_LandsOnTheSecondIsland()
        {
            float power = FindPowerLandingBetween(9f, 16f);
            var ball = new BallSimulation(_ground, _shot, new Vector2(-3f, 0f));

            Assert.That(ball.Shoot(Vector2.right, power), Is.True);
            RunUntilStill(ball);

            Assert.That(ball.State, Is.EqualTo(BallState.Resting).Or.EqualTo(BallState.Holed));
            Assert.That(ball.Position.x, Is.GreaterThan(SecondTip), "the ball should end on the second island");
            Assert.That(_ground.IsOnIsland(ball.Position.x, ball.Position.z), Is.True);
        }

        [Test]
        public void AShortShot_LandsInTheGap_IsLost_AndReturnsToWhereItWasPlayed()
        {
            var start = new Vector2(-3f, 0f);
            float power = FindPowerLandingBetween(FirstTip + 1.5f, SecondTip - 1.5f);
            var ball = new BallSimulation(_ground, _shot, start);

            Assert.That(ball.Shoot(Vector2.right, power), Is.True);
            bool wasLost = false;
            for (int i = 0; i < 60 * 30 && ball.State != BallState.Resting; i++)
            {
                ball.Step();
                wasLost |= ball.State == BallState.OffIsland;
            }

            Assert.That(wasLost, Is.True, "a ball landing in the gap falls");
            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
            Assert.That(ball.Position.x, Is.EqualTo(start.x).Within(0.001f));
            Assert.That(ball.Position.z, Is.EqualTo(start.y).Within(0.001f));
        }

        [Test]
        public void TheFloatOffset_NeverChangesWhereAShotOverTheGapLands()
        {
            float power = FindPowerLandingBetween(9f, 16f);
            Vector3 still = LandingInLocalSpace(power, 0f);

            foreach (float offset in new[] { 0.15f, -0.15f, 0.07f })
            {
                Vector3 floating = LandingInLocalSpace(power, offset);
                Assert.That(Vector3.Distance(floating, still), Is.EqualTo(0f).Within(0.0001f), $"offset {offset}");
            }
        }

        // The ball is simulated in the container's local space and drawn through its transform, as in the game.
        private Vector3 LandingInLocalSpace(float power, float floatOffset)
        {
            var course = new GameObject("Course");
            try
            {
                course.transform.localPosition = Vector3.up * floatOffset;
                var ball = new BallSimulation(_ground, _shot, new Vector2(-3f, 0f));
                ball.Shoot(Vector2.right, power);
                RunUntilStill(ball);

                // Out to world space for drawing, and back: what the views and the camera see.
                Vector3 world = course.transform.TransformPoint(ball.Position);
                Assert.That(world.y - ball.Position.y, Is.EqualTo(floatOffset).Within(0.0001f));
                return course.transform.InverseTransformPoint(world);
            }
            finally
            {
                Object.DestroyImmediate(course);
            }
        }

        private float FindPowerLandingBetween(float from, float to)
        {
            var ball = new BallSimulation(_ground, _shot, new Vector2(-3f, 0f));
            for (float power = 0.05f; power <= 1f; power += 0.01f)
            {
                ArcLanding landing = ball.ArcFor(Vector2.right, power).FindLanding(_ground, _shot.LandingSearchStep);
                if (landing.Point.x >= from && landing.Point.x <= to)
                {
                    return power;
                }
            }

            Assert.Fail($"no power lands between x {from} and {to}");
            return 0f;
        }

        private static void RunUntilStill(BallSimulation ball)
        {
            for (int i = 0; i < 60 * 120 && ball.State != BallState.Resting && ball.State != BallState.Holed; i++)
            {
                ball.Step();
            }
        }

        private static void AssertBothSides(IReadOnlyList<Vector3> points, string what)
        {
            Assert.That(points.Count, Is.GreaterThan(0), what);
            bool first = false;
            bool second = false;
            foreach (Vector3 p in points)
            {
                first |= p.x < FirstTip;
                second |= p.x > SecondTip;
            }

            Assert.That(first, Is.True, $"{what}: nothing on the first island");
            Assert.That(second, Is.True, $"{what}: nothing on the second island");
        }
    }
}
