using System;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IsoGolf.Tests
{
    public class CourseLayoutTests
    {
        private const string Par3Path = "Assets/_Project/Settings/Par3CourseShape.asset";

        private PaletteSettings _palette;

        [SetUp]
        public void SetUp()
        {
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void ToLayout_CarriesTheHandMadeShape()
        {
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(Par3Path);
            CourseLayout layout = shape.ToLayout();

            Assert.That(layout.Islands.Count, Is.EqualTo(1));
            Assert.That(layout.Islands[0].HalfLength, Is.EqualTo(shape.HalfLength));
            Assert.That(layout.Islands[0].HalfWidth, Is.EqualTo(shape.HalfWidth));
            Assert.That(layout.Islands[0].WidthWobble, Is.EqualTo(shape.WidthWobble));
            Assert.That(layout.Fairway.Start, Is.EqualTo(shape.FairwayStart));
            Assert.That(layout.Fairway.End, Is.EqualTo(shape.FairwayEnd));
            Assert.That(layout.Fairway.StripeWidth, Is.EqualTo(shape.StripeWidth));
            Assert.That(layout.Greens.Count, Is.EqualTo(1));
            Assert.That(layout.Greens[0].Outline.Center, Is.EqualTo(shape.Green.Center));
            Assert.That(layout.Greens[0].Tilt, Is.EqualTo(shape.GreenTilt));
            Assert.That(layout.Bunkers[0].Center, Is.EqualTo(shape.Bunker.Center));
            Assert.That(layout.Ponds[0].Center, Is.EqualTo(shape.Pond.Center));
            Assert.That(layout.Swells.Count, Is.EqualTo(shape.Swells.Length));
            Assert.That(layout.WaterLevel, Is.EqualTo(shape.WaterLevel));
            Assert.That(layout.Relief.CellSize, Is.EqualTo(shape.CellSize));
            Assert.That(layout.Tee, Is.EqualTo(shape.Tee));
            Assert.That(layout.Hole, Is.EqualTo(shape.Hole));
            Assert.That(layout.FlatRadius, Is.EqualTo(shape.FlatRadius));
            Assert.That(layout.Edge.SkirtDepth, Is.EqualTo(shape.SkirtDepth));
        }

        [Test]
        public void ToLayout_RunsTheCentrelineFromTeeToHole()
        {
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(Par3Path);
            CoursePath path = shape.ToLayout().Centreline;

            Assert.That(path.Start, Is.EqualTo(shape.Tee));
            Assert.That(path.End, Is.EqualTo(shape.Hole));
            Assert.That(path.Length, Is.EqualTo(Vector2.Distance(shape.Tee, shape.Hole)).Within(1e-4f));
        }

        [Test]
        public void Path_LengthAddsEveryLeg()
        {
            var path = new CoursePath(new[] { Vector2.zero, new Vector2(3f, 0f), new Vector2(3f, 4f) });
            Assert.That(path.Length, Is.EqualTo(7f).Within(1e-5f));
            Assert.That(path.Points.Count, Is.EqualTo(3));
        }

        [Test]
        public void Layout_NeedsAnIslandAndAPath()
        {
            Assert.Throws<ArgumentException>(() => Build(new IslandOutline[0], new EllipseShape[0], new EllipseShape[0]));
            Assert.Throws<ArgumentException>(() => new CoursePath(new[] { Vector2.zero }));
        }

        [Test]
        public void Layout_KeepsItsOwnCopyOfTheLists()
        {
            var bunkers = new[] { new EllipseShape(new Vector2(0f, 2f), Vector2.one, 0f) };
            CourseLayout layout = Build(new[] { Island() }, bunkers, new EllipseShape[0]);
            bunkers[0] = new EllipseShape(new Vector2(5f, 5f), Vector2.one, 0f);

            Assert.That(layout.Bunkers[0].Center, Is.EqualTo(new Vector2(0f, 2f)));
        }

        [Test]
        public void HalfExtent_CoversTheWidestIsland()
        {
            CourseLayout layout = Build(
                new[] { new IslandOutline(10f, 3f, 1f, 0.2f), new IslandOutline(6f, 5f, 2f, 0.2f) },
                new EllipseShape[0],
                new EllipseShape[0]);

            Assert.That(layout.HalfExtent, Is.EqualTo(new Vector2(10f, 6f)));
        }

        [Test]
        public void Fields_JoinEveryPieceOfASurface()
        {
            var left = new EllipseShape(new Vector2(-3f, 1.5f), new Vector2(0.8f, 0.8f), 0f);
            var right = new EllipseShape(new Vector2(3f, -1.5f), new Vector2(0.8f, 0.8f), 0f);
            var fields = new CourseFields(Build(new[] { Island() }, new[] { left, right }, new EllipseShape[0]));

            Assert.That(fields.Surface(new Vector3(-3f, 0f, 1.5f)), Is.EqualTo(CourseSurface.Bunker));
            Assert.That(fields.Surface(new Vector3(3f, 0f, -1.5f)), Is.EqualTo(CourseSurface.Bunker));
            Assert.That(fields.Surface(new Vector3(0f, 0f, 0f)), Is.Not.EqualTo(CourseSurface.Bunker));
        }

        [Test]
        public void Terrain_BuildsWithNoPondsOrBunkers()
        {
            CourseLayout layout = Build(new[] { Island() }, new EllipseShape[0], new EllipseShape[0]);
            TerrainResult terrain = TerrainBuilder.Build(layout, _palette);

            Assert.That(terrain.Land.TriangleCount, Is.GreaterThan(0));
            Assert.That(terrain.Water.TriangleCount, Is.EqualTo(0));
            foreach (Vector3 point in terrain.Outline)
            {
                Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z), Is.False);
            }
        }

        private static IslandOutline Island() => new IslandOutline(10f, 4f, 1f, 0.2f);

        private static CourseLayout Build(IslandOutline[] islands, EllipseShape[] bunkers, EllipseShape[] ponds)
        {
            var tee = new Vector2(-7f, 0f);
            var hole = new Vector2(7f, 0f);
            return new CourseLayout(
                islands,
                new CoursePath(new[] { tee, hole }),
                new FairwayLayout(-8f, 5f, 1.5f, 0.5f, 1f),
                new[] { new GreenLayout(new EllipseShape(hole, new Vector2(2f, 2f), 0f), Vector2.zero) },
                bunkers,
                ponds,
                new GroundSwell[0],
                -0.2f,
                new GroundRelief(1f, 0.05f, 0.3f, 4f, 0.5f),
                tee,
                hole,
                1f,
                0.8f,
                new EdgeLayout(new Vector2(0.9f, 1.8f), 0.7f));
        }
    }
}
