using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class TuftBuilderTests
    {
        private const string ShapeAssetPath = "Assets/_Project/Settings/Par3CourseShape.asset";
        private const string TuftsAssetPath = "Assets/_Project/Settings/IslandTufts.asset";

        // A candidate can sit half a scan step from the point being checked.
        private const float ScanSlack = 0.05f;

        // Well above the expected count, but it fails if a change doubles it.
        private const int MaxTriangles = 2500;

        private CourseShapeSettings _shape;
        private PaletteSettings _palette;
        private TuftSettings _tufts;
        private CourseFields _fields;
        private TerrainResult _terrain;

        [SetUp]
        public void SetUp()
        {
            _shape = ScriptableObject.CreateInstance<CourseShapeSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
            _tufts = ScriptableObject.CreateInstance<TuftSettings>();
            _fields = new CourseFields(_shape);
            _terrain = TerrainBuilder.Build(_shape, _palette);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_shape);
            Object.DestroyImmediate(_palette);
            Object.DestroyImmediate(_tufts);
        }

        private void SetSeed(int seed)
        {
            using var serialized = new SerializedObject(_tufts);
            serialized.FindProperty("seed").intValue = seed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private List<TuftSpot> Spots(int seed = 1)
        {
            return TuftBuilder.Place(_terrain.Edge, _tufts.MinSpacing, new System.Random(seed));
        }

        private static float FlatDistance(Vector3 p, Vector3 q)
        {
            return new Vector2(p.x - q.x, p.z - q.z).magnitude;
        }

        [Test]
        public void SameSeed_BuildsExactlyTheSameTufts()
        {
            TerrainMeshData first = TuftBuilder.Build(_tufts, _palette, _terrain.Edge);
            TerrainMeshData second = TuftBuilder.Build(_tufts, _palette, _terrain.Edge);

            CollectionAssert.AreEqual(first.Positions, second.Positions);
            CollectionAssert.AreEqual(first.Colors, second.Colors);
        }

        [Test]
        public void DifferentSeed_BuildsDifferentTufts()
        {
            TerrainMeshData first = TuftBuilder.Build(_tufts, _palette, _terrain.Edge);
            SetSeed(2);
            TerrainMeshData second = TuftBuilder.Build(_tufts, _palette, _terrain.Edge);

            CollectionAssert.AreNotEqual(first.Positions, second.Positions);
        }

        [Test]
        public void TerrainRecordsTheEdge_OnDryGroundOnly()
        {
            Assert.That(_terrain.Edge.Count, Is.GreaterThan(20));

            foreach (EdgeSegment segment in _terrain.Edge)
            {
                // Only the end points are cut on the border. In between, a piece is a straight chord.
                Assert.That(Mathf.Abs(_fields.Island(segment.A)), Is.LessThan(0.05f));
                Assert.That(Mathf.Abs(_fields.Island(segment.B)), Is.LessThan(0.05f));
                Assert.That(_fields.Pond((segment.A + segment.B) * 0.5f), Is.GreaterThanOrEqualTo(-0.01f));
            }
        }

        [Test]
        public void EdgeOutward_PointsOffTheIsland()
        {
            foreach (EdgeSegment segment in _terrain.Edge)
            {
                Vector3 middle = (segment.A + segment.B) * 0.5f;
                Assert.That(_fields.Island(middle + (segment.Outward * 0.1f)), Is.GreaterThan(_fields.Island(middle)));
            }
        }

        [Test]
        public void TuftsKeepTheirMinimumSpacing()
        {
            List<TuftSpot> spots = Spots();

            for (int i = 0; i < spots.Count; i++)
            {
                for (int j = i + 1; j < spots.Count; j++)
                {
                    Assert.That(FlatDistance(spots[i].Position, spots[j].Position), Is.GreaterThanOrEqualTo(_tufts.MinSpacing * 0.999f));
                }
            }
        }

        [Test]
        public void NoPointOnTheEdge_IsFarFromATuft_SoNoCleanStretchShows()
        {
            float limit = _tufts.MinSpacing + ScanSlack;

            foreach (int seed in new[] { 1, 2, 3 })
            {
                List<TuftSpot> spots = Spots(seed);
                foreach (EdgeSegment segment in _terrain.Edge)
                {
                    float nearest = float.MaxValue;
                    Vector3 middle = (segment.A + segment.B) * 0.5f;
                    foreach (TuftSpot spot in spots)
                    {
                        nearest = Mathf.Min(nearest, FlatDistance(spot.Position, middle));
                    }

                    Assert.That(nearest, Is.LessThan(limit));
                }
            }
        }

        [Test]
        public void TuftsSitOnTheEdge()
        {
            foreach (TuftSpot spot in Spots())
            {
                float nearest = float.MaxValue;
                foreach (EdgeSegment segment in _terrain.Edge)
                {
                    Vector3 along = segment.B - segment.A;
                    float t = Mathf.Clamp01(Vector3.Dot(spot.Position - segment.A, along) / Mathf.Max(along.sqrMagnitude, 1e-8f));
                    nearest = Mathf.Min(nearest, Vector3.Distance(spot.Position, segment.A + (along * t)));
                }

                Assert.That(nearest, Is.LessThan(0.001f));
            }
        }

        [Test]
        public void SpacingIsIrregular_NotAFixedRhythm()
        {
            List<TuftSpot> spots = Spots();
            float shortest = float.MaxValue;
            float longest = 0f;

            foreach (TuftSpot spot in spots)
            {
                float nearest = float.MaxValue;
                foreach (TuftSpot other in spots)
                {
                    if (!other.Position.Equals(spot.Position))
                    {
                        nearest = Mathf.Min(nearest, FlatDistance(spot.Position, other.Position));
                    }
                }

                shortest = Mathf.Min(shortest, nearest);
                longest = Mathf.Max(longest, nearest);
            }

            Assert.That(longest - shortest, Is.GreaterThan(_tufts.MinSpacing * 0.25f));
        }

        [Test]
        public void TopFacetsFaceUp_AndHangingFacetsTiltUpAndOutward()
        {
            // An edge shorter than the spacing holds one tuft, so each seed gives one shape to check.
            var edge = new List<EdgeSegment> { new EdgeSegment(Vector3.zero, Vector3.right * 0.1f, Vector3.forward) };

            for (int seed = 0; seed < 100; seed++)
            {
                // Half the seeds use a sharp tip and half the widest blunt one, which adds a facet.
                bool blunt = seed % 2 == 1;
                using (var serialized = new SerializedObject(_tufts))
                {
                    serialized.FindProperty("tipWidth").floatValue = blunt ? 0.6f : 0f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                SetSeed(seed);
                TerrainMeshData mesh = TuftBuilder.Build(_tufts, _palette, edge);
                Assert.That(mesh.TriangleCount, Is.EqualTo(blunt ? 5 : 4));

                for (int facet = 0; facet < mesh.TriangleCount; facet++)
                {
                    Vector3 a = mesh.Positions[facet * 3];
                    Vector3 normal = Vector3.Cross(mesh.Positions[(facet * 3) + 1] - a, mesh.Positions[(facet * 3) + 2] - a);

                    // A hanging facet that tilted down would fall in shade, and read as a dark shard.
                    Assert.That(normal.y, Is.GreaterThan(0f), $"seed {seed}, facet {facet}");
                    if (facet >= 2)
                    {
                        Assert.That(normal.z, Is.GreaterThan(0f), $"seed {seed}, facet {facet}");
                    }
                }
            }
        }

        [Test]
        public void SavedSpacing_LetsNeighbouringTuftsOverlap()
        {
            // Neighbours can be up to twice the spacing apart, and each reaches at least its smallest
            // half width toward the other, so this is what keeps the fringe free of gaps.
            var saved = AssetDatabase.LoadAssetAtPath<TuftSettings>(TuftsAssetPath);

            Assert.That(saved.HalfWidth.x, Is.GreaterThanOrEqualTo(saved.MinSpacing));
        }

        [Test]
        public void TuftsDontCastShadows_ByDefault()
        {
            Assert.That(_tufts.CastShadows, Is.False);
        }

        [Test]
        public void ColorsComeOnlyFromTheTuftPalette()
        {
            var allowed = new HashSet<Color32>
            {
                TerrainBuilder.ToVertexColor(_palette.TuftLight),
                TerrainBuilder.ToVertexColor(_palette.TuftMid),
                TerrainBuilder.ToVertexColor(_palette.TuftDark),
            };

            foreach (Color32 color in TuftBuilder.Build(_tufts, _palette, _terrain.Edge).Colors)
            {
                Assert.That(allowed.Contains(color), Is.True);
            }
        }

        [Test]
        public void SavedSettings_OnThePar3_StayWithinAPhoneBudget()
        {
            var savedShape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(ShapeAssetPath);
            var savedTufts = AssetDatabase.LoadAssetAtPath<TuftSettings>(TuftsAssetPath);
            Assert.That(savedShape, Is.Not.Null);
            Assert.That(savedTufts, Is.Not.Null);

            TerrainResult terrain = TerrainBuilder.Build(savedShape, _palette);
            TerrainMeshData mesh = TuftBuilder.Build(savedTufts, _palette, terrain.Edge);
            TestContext.WriteLine($"tuft triangles: {mesh.TriangleCount}");

            Assert.That(mesh.TriangleCount, Is.GreaterThan(0));
            Assert.That(mesh.TriangleCount, Is.LessThan(MaxTriangles));
        }
    }
}
