using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// M4 PR 4.5: the mesh side of the ambient motion. The shader reads UV1 (x weight, y phase or glint hash); these tests
    /// pin what the builders write there: still vertices stay exactly still, the pennant is rooted to its pole, phases and
    /// glint hashes are deterministic.
    /// </summary>
    public class AmbientMotionTests
    {
        private PlayPieceSettings _settings;
        private PaletteSettings _palette;
        private PropKitSettings _kitSettings;
        private CourseShapeSettings _shape;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PlayPieceSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
            _kitSettings = ScriptableObject.CreateInstance<PropKitSettings>();
            _shape = ScriptableObject.CreateInstance<CourseShapeSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_palette);
            Object.DestroyImmediate(_kitSettings);
            Object.DestroyImmediate(_shape);
        }

        [Test]
        public void Flag_PoleIsStill_AndThePennantRootEdgeIsRooted()
        {
            TerrainMeshData flag = PlayPieceBuilder.BuildFlag(_settings, _palette);
            Color32 pole = TerrainBuilder.ToVertexColor(_palette.FlagPole);
            Assert.That(flag.Sway, Is.Not.Null);

            float highest = 0f;
            for (int i = 0; i < flag.Positions.Count; i++)
            {
                if (flag.Colors[i].Equals(pole))
                {
                    Assert.That(flag.Sway[i], Is.EqualTo(0f), "the pole never moves");
                    continue;
                }

                Vector3 p = flag.Positions[i];
                if (Mathf.Approximately(p.x, 0f) && Mathf.Approximately(p.z, 0f))
                {
                    Assert.That(flag.Sway[i], Is.EqualTo(0f), "a pennant corner on the pole never detaches");
                }

                highest = Mathf.Max(highest, flag.Sway[i]);
            }

            Assert.That(highest, Is.EqualTo(1f), "the free end flutters fully");
        }

        [Test]
        public void Flag_WeightRisesWithDistanceFromThePole()
        {
            TerrainMeshData flag = PlayPieceBuilder.BuildFlag(_settings, _palette);
            Color32 pole = TerrainBuilder.ToVertexColor(_palette.FlagPole);

            for (int i = 0; i < flag.Positions.Count; i++)
            {
                if (flag.Colors[i].Equals(pole))
                {
                    continue;
                }

                float reach = -flag.Positions[i].z / _settings.FlagLength;
                if (reach > 0.99f)
                {
                    Assert.That(flag.Sway[i], Is.EqualTo(1f), "the tip");
                }
                else if (Mathf.Abs(flag.Positions[i].x) > 0f)
                {
                    Assert.That(flag.Sway[i], Is.GreaterThan(0f).And.LessThan(1f), "the crease sits in between");
                }
            }
        }

        [Test]
        public void Flag_BuiltTwiceIsIdentical()
        {
            TerrainMeshData a = PlayPieceBuilder.BuildFlag(_settings, _palette);
            TerrainMeshData b = PlayPieceBuilder.BuildFlag(_settings, _palette);
            CollectionAssert.AreEqual(a.Sway, b.Sway);
            CollectionAssert.AreEqual(a.Phase, b.Phase);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Trees_PhaseIsPerTree_AndOnlyOnSwayingVertices(int seed)
        {
            var builder = new PropBuilder(_kitSettings, _palette);
            builder.AddOlive(new Vector3(3f, 1f, -2f), 1f, new System.Random(seed));
            builder.AddOlive(new Vector3(9f, 1f, 5f), 1f, new System.Random(seed));
            TerrainMeshData mesh = builder.Mesh;

            var phases = new HashSet<float>();
            for (int i = 0; i < mesh.Positions.Count; i++)
            {
                if (mesh.Sway[i] > 0f)
                {
                    phases.Add(mesh.Phase[i]);
                    Assert.That(mesh.Phase[i], Is.InRange(0f, 1f));
                }
                else
                {
                    Assert.That(mesh.Phase[i], Is.EqualTo(0f), "a still vertex stays (0, 0), never read as water");
                }
            }

            Assert.That(phases.Count, Is.EqualTo(2), "one phase per tree");
        }

        [Test]
        public void Props_BuiltTwiceHaveTheSameChannels()
        {
            TerrainMeshData Build()
            {
                var builder = new PropBuilder(_kitSettings, _palette);
                builder.AddCypress(new Vector3(1f, 0f, 1f), 1f, new System.Random(5));
                builder.AddBush(new Vector3(4f, 0f, 2f), 1f, new System.Random(6));
                return builder.Mesh;
            }

            TerrainMeshData a = Build();
            TerrainMeshData b = Build();
            CollectionAssert.AreEqual(a.Sway, b.Sway);
            CollectionAssert.AreEqual(a.Phase, b.Phase);
        }

        [Test]
        public void StonesAndWalls_CarryNoMotionChannel()
        {
            var builder = new PropBuilder(_kitSettings, _palette);
            builder.AddRock(Vector3.zero, 1f, new System.Random(1));
            builder.AddWall(Vector3.zero, new Vector3(4f, 0f, 1f), new System.Random(2));
            Assert.That(builder.Mesh.Sway, Is.Null);
            Assert.That(builder.Mesh.Phase, Is.Null);
        }

        [Test]
        public void Land_CarriesNoMotionChannel()
        {
            TerrainResult result = TerrainBuilder.Build(_shape.ToLayout(), _palette);
            Assert.That(result.Land.Sway, Is.Null, "no sway weight, no glint: the land mesh gets no UV1");
            Assert.That(result.Land.Phase, Is.Null);
        }

        [Test]
        public void Water_EveryFacetCarriesOneGlintHash_AndNoSway()
        {
            TerrainResult result = TerrainBuilder.Build(_shape.ToLayout(), _palette);
            TerrainMeshData water = result.Water;
            Assert.That(water.Positions.Count, Is.GreaterThan(0));
            Assert.That(water.Phase, Has.Count.EqualTo(water.Positions.Count));

            for (int i = 0; i < water.Positions.Count; i += 3)
            {
                Assert.That(water.Sway[i], Is.EqualTo(0f));
                Assert.That(water.Phase[i], Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(water.Phase[i + 1], Is.EqualTo(water.Phase[i]), "one tone per facet");
                Assert.That(water.Phase[i + 2], Is.EqualTo(water.Phase[i]), "one tone per facet");
            }
        }

        [Test]
        public void Water_GlintHashesAreDeterministic()
        {
            TerrainResult a = TerrainBuilder.Build(_shape.ToLayout(), _palette);
            TerrainResult b = TerrainBuilder.Build(_shape.ToLayout(), _palette);
            CollectionAssert.AreEqual(a.Water.Phase, b.Water.Phase);
        }

        [Test]
        public void Water_IsDarkerAtTheMiddleThanAtTheRim_AndOneToneEach()
        {
            CourseLayout layout = _shape.ToLayout();
            var fields = new CourseFields(layout);
            TerrainMeshData water = TerrainBuilder.Build(layout, _palette).Water;
            double deep = 0, rim = 0;
            int deepCount = 0, rimCount = 0;
            var tones = new HashSet<int>();

            for (int i = 0; i < water.Positions.Count; i += 3)
            {
                Assert.That(water.Colors[i + 1], Is.EqualTo(water.Colors[i]), "one tone per facet");
                Assert.That(water.Colors[i + 2], Is.EqualTo(water.Colors[i]), "one tone per facet");

                Vector3 centre = (water.Positions[i] + water.Positions[i + 1] + water.Positions[i + 2]) / 3f;
                float depth = Mathf.Clamp01(-fields.Pond(centre));
                Color32 c = water.Colors[i];
                double light = c.r + c.g + c.b;
                tones.Add((c.r << 16) | (c.g << 8) | c.b);
                if (depth > 0.5f)
                {
                    deep += light;
                    deepCount++;
                }
                else
                {
                    rim += light;
                    rimCount++;
                }
            }

            Assert.That(deepCount, Is.GreaterThan(0));
            Assert.That(rimCount, Is.GreaterThan(0));
            Assert.That(deep / deepCount, Is.LessThan(rim / rimCount), "darker toward the middle");
            Assert.That(tones.Count, Is.GreaterThan(3), "a mosaic of several tones, not one flat colour");
        }

        [Test]
        public void AmbientHash_IsInRange_AndSpreadsAcrossTheCycle()
        {
            int below = 0;
            const int count = 400;
            for (int i = 0; i < count; i++)
            {
                float h = AmbientHash.Unit(i * 0.37f, i * -0.91f);
                Assert.That(h, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                if (h < 0.18f)
                {
                    below++;
                }
            }

            // About 18% fall under the default glint share (generous bounds: it is a hash, not a quota).
            Assert.That(below, Is.InRange(40, 110));
        }

        [Test]
        public void GlintFacet_CarriesItsHashOnAllCorners()
        {
            var data = new TerrainMeshData();
            data.AddGlint(Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, new Color32(1, 2, 3, 255), 0.4f);
            Assert.That(data.Phase, Is.All.EqualTo(0.4f));
            Assert.That(data.Sway, Is.All.EqualTo(0f));
        }
    }
}
