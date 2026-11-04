using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class TerrainBuilderTests
    {
        private const float Tolerance = 0.001f;
        private const float PondBankDistance = 0.05f;

        private CourseShapeSettings _shape;
        private PaletteSettings _palette;
        private CourseFields _fields;
        private TerrainResult _result;

        [SetUp]
        public void SetUp()
        {
            _shape = ScriptableObject.CreateInstance<CourseShapeSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
            _fields = new CourseFields(_shape);
            _result = TerrainBuilder.Build(_shape, _palette);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_shape);
            Object.DestroyImmediate(_palette);
        }

        private static IEnumerable<(Vector3 a, Vector3 b, Vector3 c, Color32 color)> Triangles(TerrainMeshData data)
        {
            for (int i = 0; i < data.Positions.Count; i += 3)
            {
                yield return (data.Positions[i], data.Positions[i + 1], data.Positions[i + 2], data.Colors[i]);
            }
        }

        private static bool Same(Color32 x, Color32 y)
        {
            return x.r == y.r && x.g == y.g && x.b == y.b;
        }

        private List<(Vector3 a, Vector3 b, Vector3 c, Color32 color)> LandOfColor(Color color)
        {
            Color32 wanted = TerrainBuilder.ToVertexColor(color);
            var found = new List<(Vector3, Vector3, Vector3, Color32)>();
            foreach (var triangle in Triangles(_result.Land))
            {
                if (Same(triangle.color, wanted))
                {
                    found.Add(triangle);
                }
            }

            return found;
        }

        [Test]
        public void SameShape_BuildsExactlyTheSameTerrain()
        {
            TerrainResult again = TerrainBuilder.Build(_shape, _palette);

            CollectionAssert.AreEqual(_result.Land.Positions, again.Land.Positions);
            CollectionAssert.AreEqual(_result.Water.Positions, again.Water.Positions);
        }

        [Test]
        public void EveryVertexBelongsToOneTriangle_SoFacetsAreFlat()
        {
            Assert.That(_result.Land.Positions.Count % 3, Is.EqualTo(0));
            Assert.That(_result.Land.Colors.Count, Is.EqualTo(_result.Land.Positions.Count));
            Assert.That(_result.Water.Positions.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LandStaysInsideTheIsland()
        {
            foreach (var triangle in Triangles(_result.Land))
            {
                Vector3 center = (triangle.a + triangle.b + triangle.c) / 3f;
                Assert.That(_fields.Island(center), Is.LessThan(0.05f));
            }
        }

        [Test]
        public void GroundFacesUp_AndWallsFaceSideways()
        {
            foreach (var triangle in Triangles(_result.Land))
            {
                Vector3 normal = Vector3.Cross(triangle.b - triangle.a, triangle.c - triangle.a).normalized;
                bool wall = Same(triangle.color, TerrainBuilder.ToVertexColor(_palette.Earth));
                if (!wall)
                {
                    Assert.That(normal.y, Is.GreaterThan(0.5f));
                }
            }
        }

        [Test]
        public void IslandEdge_IsCutExactlyOnTheBorder_NotOnTheGrid()
        {
            Assert.That(_result.Outline.Count, Is.GreaterThan(20));

            foreach (Vector3 point in _result.Outline)
            {
                // The field isn't quite linear along an edge, so a cut lands very close to zero, not exactly on it.
                Assert.That(Mathf.Abs(_fields.Island(new Vector3(point.x, 0f, point.z))), Is.LessThan(0.05f));
            }
        }

        [Test]
        public void Skirt_FacesOutward()
        {
            int checkedWalls = 0;
            foreach (var triangle in LandOfColor(_palette.Earth))
            {
                Vector3 center = (triangle.a + triangle.b + triangle.c) / 3f;

                // The pond reaches almost to the island edge, and its banks face the water instead.
                if (Mathf.Abs(_fields.Island(center)) > 0.05f || Mathf.Abs(_fields.Pond(center)) < PondBankDistance)
                {
                    continue;
                }

                Vector3 normal = Vector3.Cross(triangle.b - triangle.a, triangle.c - triangle.a).normalized;
                Assert.That(_fields.Island(center + (normal * 0.1f)), Is.GreaterThan(_fields.Island(center)));
                checkedWalls++;
            }

            Assert.That(checkedWalls, Is.GreaterThan(0));
        }

        [Test]
        public void PondBanks_FaceTheWater()
        {
            int checkedWalls = 0;
            foreach (var triangle in LandOfColor(_palette.Earth))
            {
                Vector3 center = (triangle.a + triangle.b + triangle.c) / 3f;

                // Only walls standing above the water line: the outer skirt goes much lower.
                bool bank = center.y > _shape.WaterLevel && Mathf.Abs(_fields.Pond(center)) < PondBankDistance && _fields.Island(center) < -0.1f;
                if (!bank)
                {
                    continue;
                }

                Vector3 normal = Vector3.Cross(triangle.b - triangle.a, triangle.c - triangle.a).normalized;
                Assert.That(_fields.Pond(center + (normal * 0.1f)), Is.LessThan(_fields.Pond(center)));
                checkedWalls++;
            }

            Assert.That(checkedWalls, Is.GreaterThan(0));
        }

        [Test]
        public void Water_IsFlat_AtTheWaterLevel()
        {
            foreach (Vector3 position in _result.Water.Positions)
            {
                Assert.That(position.y, Is.EqualTo(_shape.WaterLevel).Within(Tolerance));
            }
        }

        [Test]
        public void Stripes_RunAlongThePlayAxis_WithNoSharedTriangle()
        {
            var triangles = new List<(Vector3 a, Vector3 b, Vector3 c, Color32 color)>();
            triangles.AddRange(LandOfColor(_palette.FairwayLight));
            triangles.AddRange(LandOfColor(_palette.FairwayDark));
            Assert.That(triangles.Count, Is.GreaterThan(0));

            foreach (var triangle in triangles)
            {
                int stripe = _fields.Stripe((triangle.a.z + triangle.b.z + triangle.c.z) / 3f);
                float low = (stripe * _shape.StripeWidth) - Tolerance;
                float high = ((stripe + 1) * _shape.StripeWidth) + Tolerance;
                Assert.That(triangle.a.z, Is.InRange(low, high));
                Assert.That(triangle.b.z, Is.InRange(low, high));
                Assert.That(triangle.c.z, Is.InRange(low, high));
            }
        }

        [Test]
        public void EverySurface_IsPresent_OnThePar3()
        {
            Assert.That(LandOfColor(_palette.Rough).Count, Is.GreaterThan(0));
            Assert.That(LandOfColor(_palette.FairwayLight).Count, Is.GreaterThan(0));
            Assert.That(LandOfColor(_palette.FairwayDark).Count, Is.GreaterThan(0));
            Assert.That(LandOfColor(_palette.Green).Count, Is.GreaterThan(0));
            Assert.That(LandOfColor(_palette.Sand).Count, Is.GreaterThan(0));
            Assert.That(LandOfColor(_palette.Earth).Count, Is.GreaterThan(0));
        }

        [Test]
        public void LandIsWatertight_ExceptAlongItsOuterBorders()
        {
            // Every edge inside a surface must be shared by exactly two triangles with the same two
            // end points. An edge used once away from the island edge and pond is a T-junction,
            // which shows up on screen as a pinhole with the white background behind it.
            var uses = new Dictionary<(Vector3, Vector3), int>();
            void Count(Vector3 p, Vector3 q)
            {
                bool ordered = p.x < q.x || (p.x == q.x && (p.z < q.z || (p.z == q.z && p.y < q.y)));
                (Vector3, Vector3) key = ordered ? (p, q) : (q, p);
                uses.TryGetValue(key, out int count);
                uses[key] = count + 1;
            }

            foreach (var triangle in Triangles(_result.Land))
            {
                Count(triangle.a, triangle.b);
                Count(triangle.b, triangle.c);
                Count(triangle.c, triangle.a);
            }

            int open = 0;
            foreach (KeyValuePair<(Vector3, Vector3), int> edge in uses)
            {
                Vector3 middle = (edge.Key.Item1 + edge.Key.Item2) * 0.5f;
                bool expected = _fields.Island(middle) > -0.2f || _fields.Pond(middle) < 0.2f || middle.y < _shape.WaterLevel;
                if (edge.Value == 1 && !expected)
                {
                    open++;
                }
            }

            TestContext.WriteLine($"open edges inside the land: {open} of {uses.Count}");
            Assert.That(open, Is.EqualTo(0));
        }

        [Test]
        public void VertexColors_AreConvertedToLinear()
        {
            Color srgb = new Color32(0x56, 0x71, 0x3A, 0xFF);
            Color32 converted = TerrainBuilder.ToVertexColor(srgb);

            Assert.That(converted.r, Is.LessThan(0x56));
            Assert.That(converted.g, Is.LessThan(0x71));
            Assert.That(converted.b, Is.LessThan(0x3A));
        }

        [Test]
        public void TriangleCount_StaysWithinAPhoneBudget()
        {
            TestContext.WriteLine($"land {_result.Land.TriangleCount}, water {_result.Water.TriangleCount}");

            Assert.That(_result.Land.TriangleCount + _result.Water.TriangleCount, Is.LessThan(5000));
        }
    }
}
