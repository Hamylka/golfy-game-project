using System;
using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>M4 PR 4.1: the procedural prop kit. Determinism, budgets, colliders, palette, flat normals, sway weights.</summary>
    public class PropKitTests
    {
        public enum Kind
        {
            Olive,
            Cypress,
            Bush,
            Rock,
            RoundRock,
            Wall,
        }

        // Triangle budgets (M4 plan section 1: an olive is about 400, a par 5 stays near 20k in all).
        // The measured counts are logged by TriangleCountsStayInBudget.
        private static readonly Dictionary<Kind, int> Budget = new Dictionary<Kind, int>
        {
            { Kind.Olive, 440 },
            { Kind.Cypress, 40 },
            { Kind.Bush, 130 },
            { Kind.Rock, 20 },
            { Kind.RoundRock, 80 },
            { Kind.Wall, 400 },
        };

        private static readonly Vector3 Root = new Vector3(3f, 1.5f, -2f);

        private PropKitSettings _settings;
        private PaletteSettings _palette;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PropKitSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_settings);
            UnityEngine.Object.DestroyImmediate(_palette);
        }

        private (TerrainMeshData mesh, PropShape[] shapes) Build(Kind kind, int seed, float size = 1f)
        {
            var builder = new PropBuilder(_settings, _palette);
            var random = new System.Random(seed);
            PropShape[] shapes;
            switch (kind)
            {
                case Kind.Olive: shapes = builder.AddOlive(Root, size, random); break;
                case Kind.Cypress: shapes = builder.AddCypress(Root, size, random); break;
                case Kind.Bush: shapes = builder.AddBush(Root, size, random); break;
                case Kind.Rock: shapes = builder.AddRock(Root, size, random); break;
                case Kind.RoundRock: shapes = builder.AddRoundRock(Root, size, random); break;
                default: shapes = builder.AddWall(Root, Root + new Vector3(4f, 0.3f, 2f), random); break;
            }

            return (builder.Mesh, shapes);
        }

        private HashSet<Color32> Allowed(Kind kind)
        {
            Color[] colors;
            switch (kind)
            {
                case Kind.Olive:
                    colors = new[] { _palette.FoliageMid, _palette.FoliageDark, _palette.OliveLight, _palette.Trunk };
                    break;
                case Kind.Cypress:
                    colors = new[] { _palette.CypressMid, _palette.CypressDark, _palette.FoliageDark, _palette.Trunk };
                    break;
                case Kind.Bush:
                    colors = new[] { _palette.FoliageLight, _palette.FoliageMid, _palette.FoliageDark };
                    break;
                default:
                    colors = new[] { _palette.StoneLight, _palette.StoneMid, _palette.StoneDark };
                    break;
            }

            var set = new HashSet<Color32>();
            foreach (Color c in colors)
            {
                set.Add(TerrainBuilder.ToVertexColor(c));
            }

            return set;
        }

        [TestCase(Kind.Olive)]
        [TestCase(Kind.Cypress)]
        [TestCase(Kind.Bush)]
        [TestCase(Kind.Rock)]
        [TestCase(Kind.RoundRock)]
        [TestCase(Kind.Wall)]
        public void SameSeedBuildsTheSameProp(Kind kind)
        {
            var first = Build(kind, 11, 0.8f);
            var second = Build(kind, 11, 0.8f);

            CollectionAssert.AreEqual(first.mesh.Positions, second.mesh.Positions);
            CollectionAssert.AreEqual(first.mesh.Colors, second.mesh.Colors);
            CollectionAssert.AreEqual(first.shapes, second.shapes);
        }

        [TestCase(Kind.Olive)]
        [TestCase(Kind.Cypress)]
        [TestCase(Kind.Bush)]
        [TestCase(Kind.Rock)]
        [TestCase(Kind.RoundRock)]
        [TestCase(Kind.Wall)]
        public void DifferentSeedBuildsADifferentProp(Kind kind)
        {
            CollectionAssert.AreNotEqual(Build(kind, 11).mesh.Positions, Build(kind, 12).mesh.Positions);
        }

        [TestCase(Kind.Olive)]
        [TestCase(Kind.Cypress)]
        [TestCase(Kind.Bush)]
        [TestCase(Kind.Rock)]
        [TestCase(Kind.RoundRock)]
        [TestCase(Kind.Wall)]
        public void TriangleCountsStayInBudget(Kind kind)
        {
            int worst = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                worst = Mathf.Max(worst, Build(kind, seed).mesh.TriangleCount);
            }

            Debug.Log($"[PropKit] {kind}: {Build(kind, 0).mesh.TriangleCount} triangles (seed 0), worst of 20 seeds {worst}, budget {Budget[kind]}");
            Assert.That(worst, Is.LessThanOrEqualTo(Budget[kind]));
            Assert.That(worst, Is.GreaterThan(0));
        }

        [TestCase(Kind.Olive)]
        [TestCase(Kind.Cypress)]
        [TestCase(Kind.Bush)]
        [TestCase(Kind.Rock)]
        [TestCase(Kind.RoundRock)]
        [TestCase(Kind.Wall)]
        public void PropUsesOnlyItsPaletteColors(Kind kind)
        {
            HashSet<Color32> allowed = Allowed(kind);
            foreach (Color32 color in Build(kind, 3).mesh.Colors)
            {
                Assert.That(allowed, Does.Contain(color));
            }
        }

        [TestCase(Kind.Olive)]
        [TestCase(Kind.Cypress)]
        [TestCase(Kind.Bush)]
        [TestCase(Kind.Rock)]
        [TestCase(Kind.RoundRock)]
        [TestCase(Kind.Wall)]
        public void NormalsAreFlatAndTurnedOutward(Kind kind)
        {
            TerrainMeshData data = Build(kind, 5).mesh;
            Mesh mesh = data.ToMesh("test");
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                var middle = mesh.bounds.center;
                Assert.That(vertices.Length, Is.EqualTo(data.TriangleCount * 3), "vertices are never shared");

                for (int i = 0; i < vertices.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]).normalized;
                    for (int k = 0; k < 3; k++)
                    {
                        Assert.That(Vector3.Dot(normals[i + k], face), Is.GreaterThan(0.999f), "flat normal");
                    }

                    // Single blobs are star-shaped about their bounds' middle. The others (trunk inside foliage,
                    // several blobs, a long wall) are not, so only their flat normals are checked.
                    if (kind == Kind.Rock || kind == Kind.RoundRock)
                    {
                        Vector3 centroid = (vertices[i] + vertices[i + 1] + vertices[i + 2]) / 3f;
                        Assert.That(Vector3.Dot(face, centroid - middle), Is.GreaterThan(-0.05f), "facet turned inward");
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(Kind.Olive, 1f)]
        [TestCase(Kind.Olive, 1.4f)]
        [TestCase(Kind.Cypress, 1f)]
        [TestCase(Kind.Bush, 1f)]
        [TestCase(Kind.Rock, 0.3f)]
        [TestCase(Kind.Rock, 1.2f)]
        [TestCase(Kind.RoundRock, 0.5f)]
        [TestCase(Kind.Wall, 1f)]
        public void CollidersEncloseTheMeshAndStayCloseToIt(Kind kind, float size)
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var (mesh, shapes) = Build(kind, seed, size);
                Assert.That(shapes, Is.Not.Empty);
                var bounds = new Bounds(mesh.Positions[0], Vector3.zero);

                foreach (Vector3 point in mesh.Positions)
                {
                    bounds.Encapsulate(point);

                    // The foot below the ground line is never reached by the ball.
                    if (point.y < Root.y - 0.001f)
                    {
                        continue;
                    }

                    float best = float.MaxValue;
                    foreach (PropShape shape in shapes)
                    {
                        best = Mathf.Min(best, shape.Level(point));
                    }

                    Assert.That(best, Is.LessThanOrEqualTo(1.001f), $"{kind} seed {seed}: a vertex sticks out of every collider");
                }

                // Not absurdly big either: no shape reaches further than 1.6 times the mesh's own half size
                // (a box is exact, a fitted ellipsoid is looser).
                foreach (PropShape shape in shapes)
                {
                    if (shape.Kind == PropShapeKind.Ellipsoid)
                    {
                        Assert.That(shape.Extents.x, Is.LessThanOrEqualTo(bounds.extents.x * 1.6f));
                        Assert.That(shape.Extents.y, Is.LessThanOrEqualTo(bounds.extents.y * 1.6f));
                        Assert.That(shape.Extents.z, Is.LessThanOrEqualTo(bounds.extents.z * 1.6f));
                    }
                }
            }
        }

        [TestCase(Kind.Olive, new[] { PropMaterial.Wood, PropMaterial.Foliage })]
        [TestCase(Kind.Cypress, new[] { PropMaterial.Wood, PropMaterial.Foliage })]
        [TestCase(Kind.Bush, new[] { PropMaterial.Foliage })]
        [TestCase(Kind.Rock, new[] { PropMaterial.Stone })]
        [TestCase(Kind.RoundRock, new[] { PropMaterial.Stone })]
        [TestCase(Kind.Wall, new[] { PropMaterial.Stone })]
        public void CollidersCarryTheirMaterial(Kind kind, PropMaterial[] expected)
        {
            var materials = new List<PropMaterial>();
            foreach (PropShape shape in Build(kind, 1).shapes)
            {
                materials.Add(shape.Material);
            }

            CollectionAssert.AreEqual(expected, materials);
        }

        [Test]
        public void ShapeKindsMatchTheirProps()
        {
            Assert.That(Build(Kind.Olive, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Cylinder), "trunk");
            Assert.That(Build(Kind.Olive, 1).shapes[1].Kind, Is.EqualTo(PropShapeKind.Ellipsoid), "crown");
            Assert.That(Build(Kind.Wall, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Box));
            Assert.That(Build(Kind.Rock, 1).shapes[0].Kind, Is.EqualTo(PropShapeKind.Ellipsoid));
        }

        [TestCase(Kind.Olive, true)]
        [TestCase(Kind.Cypress, true)]
        [TestCase(Kind.Bush, true)]
        [TestCase(Kind.Rock, false)]
        [TestCase(Kind.RoundRock, false)]
        [TestCase(Kind.Wall, false)]
        public void OnlyFoliageSways(Kind kind, bool sways)
        {
            TerrainMeshData mesh = Build(kind, 2).mesh;
            if (!sways)
            {
                Assert.That(mesh.Sway, Is.Null, "no sway channel at all on stone");
                return;
            }

            Assert.That(mesh.Sway, Has.Count.EqualTo(mesh.Positions.Count));
            Color32 trunk = TerrainBuilder.ToVertexColor(_palette.Trunk);
            float highest = 0f;

            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                Assert.That(mesh.Sway[i], Is.InRange(0f, 1f));
                if (mesh.Colors[i].Equals(trunk))
                {
                    Assert.That(mesh.Sway[i], Is.EqualTo(0f), "a trunk never sways");
                }

                highest = Mathf.Max(highest, mesh.Sway[i]);
            }

            Assert.That(highest, Is.GreaterThan(0.8f), "the tip of the foliage reaches near full weight");
        }

        [Test]
        public void SwayChannelFollowsTheTriangleWhenItIsFlipped()
        {
            var data = new TerrainMeshData();
            Vector3 a = Vector3.zero;
            Vector3 b = Vector3.right;
            Vector3 c = Vector3.forward;

            // Normal of (a, b, c) is -Y here; facing up forces the flip, which swaps b and c.
            data.Add(a, b, c, Vector3.up, new Color32(1, 2, 3, 255), 0.1f, 0.2f, 0.3f);

            Assert.That(data.Positions[1], Is.EqualTo(c));
            Assert.That(data.Sway[1], Is.EqualTo(0.3f));
            Assert.That(data.Sway[2], Is.EqualTo(0.2f));
        }

        [Test]
        public void TheTerrainMeshesCarryNoSwayChannel()
        {
            var data = new TerrainMeshData();
            data.Add(Vector3.zero, Vector3.right, Vector3.forward, Vector3.down, new Color32(1, 2, 3, 255));
            Assert.That(data.Sway, Is.Null);
        }

        [Test]
        public void RocksStayWithinTheirSize()
        {
            foreach (float size in new[] { 0.3f, 0.6f, 1.2f })
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    foreach (Kind kind in new[] { Kind.Rock, Kind.RoundRock })
                    {
                        TerrainMeshData mesh = Build(kind, seed, size).mesh;
                        var bounds = new Bounds(mesh.Positions[0], Vector3.zero);
                        foreach (Vector3 p in mesh.Positions)
                        {
                            bounds.Encapsulate(p);
                        }

                        Assert.That(bounds.size.x, Is.LessThanOrEqualTo(size + 0.001f));
                        Assert.That(bounds.size.z, Is.LessThanOrEqualTo(size + 0.001f));
                        Assert.That(bounds.size.y, Is.LessThanOrEqualTo(size + 0.001f), "flatter than it is wide");
                    }
                }
            }
        }

        [Test]
        public void WallIsMadeOfBlocksOfDifferentTones()
        {
            TerrainMeshData mesh = Build(Kind.Wall, 4).mesh;
            var tones = new HashSet<Color32>(mesh.Colors);
            Assert.That(tones.Count, Is.GreaterThanOrEqualTo(2));

            // Between 18 and 50 blocks over 4.5 m in 3 courses, each at most 10 triangles (no underside above the first course).
            Assert.That(mesh.TriangleCount, Is.InRange(150, 400));
        }

        [Test]
        public void WallStandsOnItsLineAndHasTheDrawnHeight()
        {
            TerrainMeshData mesh = Build(Kind.Wall, 4).mesh;
            float top = float.MinValue;
            foreach (Vector3 p in mesh.Positions)
            {
                top = Mathf.Max(top, p.y);
            }

            float expectedMin = Root.y + (_settings.WallCourses * _settings.WallBlockHeight) - 0.1f;
            float expectedMax = Root.y + 0.3f + (_settings.WallCourses * _settings.WallBlockHeight) + 0.1f;
            Assert.That(top, Is.InRange(expectedMin, expectedMax));
        }

        [Test]
        public void GableRoofHasTwoSlopesAndTwoEnds()
        {
            var data = new TerrainMeshData();
            var kit = new PropMeshKit(data);
            kit.AddGableRoof(Vector3.zero, 3f, 2f, 0.8f, 0.4f, new Color32(10, 20, 30, 255), new Color32(30, 20, 10, 255));

            Assert.That(data.TriangleCount, Is.EqualTo(6));
            float highest = float.MinValue;
            foreach (Vector3 p in data.Positions)
            {
                highest = Mathf.Max(highest, p.y);
            }

            Assert.That(highest, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void ConeAndPrismTriangleCounts()
        {
            var data = new TerrainMeshData();
            var kit = new PropMeshKit(data);
            kit.AddCone(Vector3.zero, 1f, 2f, 7, 0f, new Color32(1, 1, 1, 255), false);
            Assert.That(data.TriangleCount, Is.EqualTo(7));
            kit.AddCone(Vector3.zero, 1f, 2f, 7, 0f, new Color32(1, 1, 1, 255), true);
            Assert.That(data.TriangleCount, Is.EqualTo(7 + 14));
            kit.AddPrism(Vector3.zero, 1f, 1f, 6, 0f, new Color32(1, 1, 1, 255), 0.1f);
            Assert.That(data.TriangleCount, Is.EqualTo(7 + 14 + 12));
        }

        [Test]
        public void BoxHasTwelveTrianglesOrTenWithoutItsUnderside()
        {
            var data = new TerrainMeshData();
            var kit = new PropMeshKit(data);
            kit.AddBox(Vector3.zero, Vector3.one, 0.3f, 0.05f, new System.Random(1), new Color32(1, 1, 1, 255), false);
            Assert.That(data.TriangleCount, Is.EqualTo(12));
            kit.AddBox(Vector3.zero, Vector3.one, 0.3f, 0.05f, new System.Random(1), new Color32(1, 1, 1, 255), true);
            Assert.That(data.TriangleCount, Is.EqualTo(12 + 10));
        }

        [Test]
        public void ShapeLevelIsOneOnTheSurface()
        {
            var cylinder = PropShape.Cylinder(Vector3.zero, 0.5f, 2f, PropMaterial.Wood);
            Assert.That(cylinder.Level(new Vector3(0.5f, 1f, 0f)), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(cylinder.Level(new Vector3(0f, 2f, 0f)), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(cylinder.Level(new Vector3(0f, 1f, 0f)), Is.EqualTo(0f).Within(1e-5f));

            var ellipsoid = PropShape.Ellipsoid(new Vector3(1f, 1f, 1f), new Vector3(2f, 1f, 0.5f), PropMaterial.Foliage);
            Assert.That(ellipsoid.Level(new Vector3(3f, 1f, 1f)), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(ellipsoid.Level(new Vector3(1f, 1f, 1.5f)), Is.EqualTo(1f).Within(1e-5f));

            // Turned a quarter turn, the box's long side runs along z.
            var box = PropShape.Box(Vector3.zero, new Vector3(2f, 1f, 0.5f), Mathf.PI * 0.5f, PropMaterial.Stone);
            Assert.That(box.Level(new Vector3(0f, 0f, 2f)), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(box.Level(new Vector3(0.5f, 0f, 0f)), Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
