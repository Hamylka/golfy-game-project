using System;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F2: a piece of ground is coloured by the side of each border cut it fell on, so the colour always agrees with the drawn
    /// border. Before, it came from the true outline read at the piece's centroid: against a chord across a 1 m cell, a bunker
    /// of about 1 m radius gave sand wedges outside its drawn border and gaps inside it.
    /// </summary>
    public class SurfaceColourTests
    {
        // A cut crossing is within about a millimetre of the true border, so a vertex this close counts as on it.
        private const float OnBorder = 0.02f;

        private PaletteSettings _palette;

        [SetUp]
        public void SetUp() => _palette = ScriptableObject.CreateInstance<PaletteSettings>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_palette);

        private static CourseLayout Course(int seed, int par) => CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings);

        /// <summary>Triangles of the land mesh whose corners disagree with the colour: sand with a corner outside the border, or
        /// another colour with every corner inside it. Returns how many.</summary>
        private int Disagreements(CourseLayout layout, Func<CourseFields, Func<Vector3, float>> field, Color paletteColour)
        {
            var fields = new CourseFields(layout);
            Func<Vector3, float> inside = field(fields);
            Color32 target = TerrainBuilder.ToVertexColor(paletteColour);
            TerrainMeshData land = TerrainBuilder.Build(layout, _palette).Land;
            int bad = 0;
            for (int i = 0; i + 2 < land.Positions.Count; i += 3)
            {
                float a = inside(land.Positions[i]), b = inside(land.Positions[i + 1]), c = inside(land.Positions[i + 2]);
                bool coloured = land.Colors[i].Equals(target);
                float highest = Mathf.Max(a, Mathf.Max(b, c));
                if (coloured && highest > OnBorder)
                {
                    bad++;
                }
                else if (!coloured && highest < -OnBorder && !IsEarth(land.Colors[i]))
                {
                    // All three corners well inside, yet another colour. (Fairway stripes and the green share an area with no bunker,
                    // so this reading only makes sense for the topmost surface; the callers pass the bunker.)
                    bad++;
                }
            }

            return bad;
        }

        private bool IsEarth(Color32 colour) => colour.Equals(TerrainBuilder.ToVertexColor(_palette.Earth));

        [TestCase(4, 4)]
        [TestCase(6, 4)]
        [TestCase(3, 4)]
        [TestCase(5, 5)]
        public void BunkerSand_FollowsTheDrawnBorder(int seed, int par)
        {
            CourseLayout layout = Course(seed, par);
            Assume.That(layout.Bunkers.Count, Is.GreaterThan(0));
            Assert.That(Disagreements(layout, f => f.Bunker, _palette.Sand), Is.EqualTo(0), $"seed {seed} par {par}");
        }

        [Test]
        public void BunkerSand_FollowsTheDrawnBorder_OnSeeds0To29()
        {
            int bad = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 30; seed++)
                {
                    bad += Disagreements(Course(seed, par), f => f.Bunker, _palette.Sand);
                }
            }

            Assert.That(bad, Is.EqualTo(0));
        }

        [Test]
        public void Colouring_IsDeterministic()
        {
            CourseLayout layout = Course(6, 4);
            TerrainMeshData a = TerrainBuilder.Build(layout, _palette).Land;
            TerrainMeshData b = TerrainBuilder.Build(layout, _palette).Land;
            Assert.That(b.Colors, Is.EqualTo(a.Colors));
            Assert.That(b.Positions, Is.EqualTo(a.Positions));
        }
    }
}
