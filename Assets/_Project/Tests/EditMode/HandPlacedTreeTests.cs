using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class HandPlacedTreeTests
    {
        private const string TreesAssetPath = "Assets/_Project/Settings/SandboxTrees.asset";
        private const string ShapeAssetPath = "Assets/_Project/Settings/Par3CourseShape.asset";
        private const float MaxSink = 0.5f;

        private CourseShapeSettings _shape;
        private PaletteSettings _palette;
        private TreeSettings _trees;
        private PropKitSettings _kit;

        [SetUp]
        public void SetUp()
        {
            _shape = ScriptableObject.CreateInstance<CourseShapeSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
            _trees = ScriptableObject.CreateInstance<TreeSettings>();
            _kit = ScriptableObject.CreateInstance<PropKitSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_shape);
            Object.DestroyImmediate(_palette);
            Object.DestroyImmediate(_trees);
            Object.DestroyImmediate(_kit);
        }

        private void Place(int seed, TreeKind kind)
        {
            using var serialized = new SerializedObject(_trees);
            serialized.FindProperty("seed").intValue = seed;
            SerializedProperty list = serialized.FindProperty("placements");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            entry.FindPropertyRelative("position").vector2Value = new Vector2(-8f, 4f);
            entry.FindPropertyRelative("scale").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TestCase(TreeKind.Cauliflower)]
        [TestCase(TreeKind.Cypress)]
        public void SameSeedBuildsTheSameTree(TreeKind kind)
        {
            Place(5, kind);
            TerrainMeshData first = PropBuilder.BuildTrees(_trees, _kit, _palette, _shape.ToLayout());
            TerrainMeshData second = PropBuilder.BuildTrees(_trees, _kit, _palette, _shape.ToLayout());

            CollectionAssert.AreEqual(first.Positions, second.Positions);
            CollectionAssert.AreEqual(first.Colors, second.Colors);
        }

        [TestCase(TreeKind.Cauliflower)]
        [TestCase(TreeKind.Cypress)]
        public void DifferentSeedBuildsADifferentTree(TreeKind kind)
        {
            Place(5, kind);
            List<Vector3> first = PropBuilder.BuildTrees(_trees, _kit, _palette, _shape.ToLayout()).Positions;
            Place(6, kind);
            List<Vector3> second = PropBuilder.BuildTrees(_trees, _kit, _palette, _shape.ToLayout()).Positions;

            CollectionAssert.AreNotEqual(first, second);
        }

        [TestCase(TreeKind.Cauliflower)]
        [TestCase(TreeKind.Cypress)]
        public void TreeStandsOnTheGround(TreeKind kind)
        {
            Place(5, kind);
            TerrainMeshData mesh = PropBuilder.BuildTrees(_trees, _kit, _palette, _shape.ToLayout());
            float ground = new CourseFields(_shape.ToLayout()).Height(-8f, 4f);

            float lowest = float.MaxValue;
            foreach (Vector3 position in mesh.Positions)
            {
                lowest = Mathf.Min(lowest, position.y);
            }

            Assert.That(lowest, Is.LessThan(ground));
            Assert.That(lowest, Is.GreaterThan(ground - MaxSink));
        }

        [TestCase(TreeKind.Cauliflower)]
        [TestCase(TreeKind.Cypress)]
        public void TreeUsesOnlyTreeColors(TreeKind kind)
        {
            Place(5, kind);
            TerrainMeshData mesh = PropBuilder.BuildTrees(_trees, _kit, _palette, _shape.ToLayout());
            var allowed = new[]
            {
                TerrainBuilder.ToVertexColor(_palette.FoliageLight),
                TerrainBuilder.ToVertexColor(_palette.OliveLight),
                TerrainBuilder.ToVertexColor(_palette.FoliageMid),
                TerrainBuilder.ToVertexColor(_palette.FoliageDark),
                TerrainBuilder.ToVertexColor(_palette.Trunk),
            };

            foreach (Color32 color in mesh.Colors)
            {
                CollectionAssert.Contains(allowed, color);
            }
        }

        /// <summary>
        /// Pinned before the kit replaced TreeBuilder (develop at 474c2a3): Sandbox_Par3's ten hand-placed trees
        /// were 2260 triangles, and the TreeBuilder code and the kit's agree on this FNV-1a hash of every vertex
        /// position rounded to 0.1 mm. Only the olive crowns' light shade changed since (OliveLight), never a position.
        /// </summary>
        [Test]
        public void SandboxTreesKeepTheirShapeThroughTheKit()
        {
            var trees = AssetDatabase.LoadAssetAtPath<TreeSettings>(TreesAssetPath);
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(ShapeAssetPath);
            TerrainMeshData mesh = PropBuilder.BuildTrees(trees, _kit, _palette, shape.ToLayout());

            // Rounded to 0.1 mm: the kit's code path differs from TreeBuilder's by a last float bit on 10 of 6780 vertices.
            ulong hash = 14695981039346656037UL;
            foreach (Vector3 p in mesh.Positions)
            {
                foreach (float f in new[] { p.x, p.y, p.z })
                {
                    hash ^= (ulong)(long)System.Math.Round(f * 10000.0);
                    hash *= 1099511628211UL;
                }
            }

            Assert.That(mesh.TriangleCount, Is.EqualTo(2260));
            Assert.That(hash, Is.EqualTo(17899946963706251753UL));
        }

        [Test]
        public void SandboxTreesStayOffThePlayedSurfaces()
        {
            var trees = AssetDatabase.LoadAssetAtPath<TreeSettings>(TreesAssetPath);
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(ShapeAssetPath);
            Assert.That(trees, Is.Not.Null);
            Assert.That(shape, Is.Not.Null);
            Assert.That(trees.Placements, Is.Not.Empty);
            var fields = new CourseFields(shape.ToLayout());

            foreach (TreePlacement placement in trees.Placements)
            {
                var point = new Vector3(placement.Position.x, 0f, placement.Position.y);
                Assert.That(fields.Island(point), Is.LessThan(0f), $"{placement.Position} is off the island");
                Assert.That(fields.Fairway(point), Is.GreaterThan(0f), $"{placement.Position} is on the fairway");
                Assert.That(fields.Green(point), Is.GreaterThan(0f), $"{placement.Position} is on the green");
                Assert.That(fields.Bunker(point), Is.GreaterThan(0f), $"{placement.Position} is in the bunker");
                Assert.That(fields.Pond(point), Is.GreaterThan(0f), $"{placement.Position} is in the pond");
            }
        }
    }
}
