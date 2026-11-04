using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CloudBuilderTests
    {
        private static readonly Rect Visible = Rect.MinMaxRect(-20f, -30f, 100f, 30f);
        private static readonly Vector3 ToSun = new Vector3(0.45f, 0.79f, 0.41f);

        // The tallest a cluster can rise above its centre, from the largest blob settings.
        private const float BlobHeadroom = 6f;

        private CloudSettings _settings;
        private PaletteSettings _palette;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<CloudSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void SameSeedBuildsTheSameClouds()
        {
            TerrainMeshData first = CloudBuilder.Build(_settings, _palette, Visible, ToSun);
            TerrainMeshData second = CloudBuilder.Build(_settings, _palette, Visible, ToSun);

            Assert.That(first.TriangleCount, Is.GreaterThan(0));
            Assert.That(second.Positions, Is.EqualTo(first.Positions));
            Assert.That(second.Colors, Is.EqualTo(first.Colors));
        }

        [Test]
        public void CloudsStayWellBelowTheIsland()
        {
            TerrainMeshData mesh = CloudBuilder.Build(_settings, _palette, Visible, ToSun);

            foreach (Vector3 position in mesh.Positions)
            {
                Assert.That(position.y, Is.LessThan(_settings.Highest + BlobHeadroom));
                Assert.That(position.y, Is.LessThan(0f));
            }
        }

        [Test]
        public void CloudsCoverTheViewAtEitherEndOfTheDrift()
        {
            TerrainMeshData mesh = CloudBuilder.Build(_settings, _palette, Visible, ToSun);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 position in mesh.Positions)
            {
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
            }

            Assert.That(minX, Is.LessThan(Visible.xMin));
            Assert.That(maxX, Is.GreaterThan(Visible.xMax));
            Assert.That(minZ + _settings.DriftRange, Is.LessThan(Visible.yMin));
            Assert.That(maxZ - _settings.DriftRange, Is.GreaterThan(Visible.yMax));
        }

        [Test]
        public void FacetsStayBetweenTheShadeAndLightColors()
        {
            TerrainMeshData mesh = CloudBuilder.Build(_settings, _palette, Visible, ToSun);
            Color32 light = TerrainBuilder.ToVertexColor(_palette.CloudLight);
            Color32 shade = TerrainBuilder.ToVertexColor(_palette.CloudShade);

            foreach (Color32 color in mesh.Colors)
            {
                Assert.That(color.r, Is.InRange(shade.r - 1, light.r + 1));
                Assert.That(color.g, Is.InRange(shade.g - 1, light.g + 1));
                Assert.That(color.b, Is.InRange(shade.b - 1, light.b + 1));
            }
        }

        [Test]
        public void FacetsFacingTheSunAreLighterOnAverage()
        {
            TerrainMeshData mesh = CloudBuilder.Build(_settings, _palette, Visible, ToSun);
            float sunward = 0f, away = 0f;
            int sunwardCount = 0, awayCount = 0;
            for (int i = 0; i < mesh.Positions.Count; i += 3)
            {
                // Same winding rule as TerrainMeshData: this cross product points out of a front face.
                Vector3 normal = Vector3.Cross(mesh.Positions[i + 1] - mesh.Positions[i], mesh.Positions[i + 2] - mesh.Positions[i]);
                if (Vector3.Dot(normal, ToSun) > 0f)
                {
                    sunward += mesh.Colors[i].r;
                    sunwardCount++;
                }
                else
                {
                    away += mesh.Colors[i].r;
                    awayCount++;
                }
            }

            Assert.That(sunward / sunwardCount, Is.GreaterThan(away / awayCount));
        }

        [TestCase(0f)]
        [TestCase(100f)]
        [TestCase(10000f)]
        public void DriftNeverLeavesItsRange(float time)
        {
            float offset = CloudBuilder.DriftOffset(time, _settings.DriftSpeed, _settings.DriftRange);

            Assert.That(Mathf.Abs(offset), Is.LessThanOrEqualTo(_settings.DriftRange));
        }

        [Test]
        public void NoRangeMeansNoDrift()
        {
            Assert.That(CloudBuilder.DriftOffset(50f, 0.35f, 0f), Is.EqualTo(0f));
        }
    }
}
