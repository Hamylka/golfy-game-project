using System.Collections.Generic;
using System.Globalization;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Pins the Main par 3 as it ships: its terrain and tufts. Multi-island work must not move one vertex of it.
    /// If a change is meant to alter the par 3, update the expected summary and say why in the changelog.
    /// </summary>
    public class Par3PinTests
    {
        private const string ExpectedLand = "1522 tris 3792.06/-669.14/-182.96";
        private const string ExpectedWater = "85 tris -357.27/-56.10/761.89";
        private const int ExpectedOutlinePoints = 350;
        private const string ExpectedTufts = "2020 tris 287.76/22.53/-225.91";

        private CourseLayout _layout;
        private PaletteSettings _palette;
        private TuftSettings _tufts;

        [SetUp]
        public void SetUp()
        {
            _layout = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset").ToLayout();
            _palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            _tufts = AssetDatabase.LoadAssetAtPath<TuftSettings>("Assets/_Project/Settings/IslandTufts.asset");
        }

        [Test]
        public void Land_IsUnchanged()
        {
            Assert.That(Summary(TerrainBuilder.Build(_layout, _palette).Land), Is.EqualTo(ExpectedLand));
        }

        [Test]
        public void Water_IsUnchanged()
        {
            Assert.That(Summary(TerrainBuilder.Build(_layout, _palette).Water), Is.EqualTo(ExpectedWater));
        }

        [Test]
        public void Outline_IsUnchanged()
        {
            TerrainResult terrain = TerrainBuilder.Build(_layout, _palette);

            Assert.That(terrain.Outline.Count, Is.EqualTo(ExpectedOutlinePoints));
        }

        [Test]
        public void Tufts_AreUnchanged()
        {
            TerrainResult terrain = TerrainBuilder.Build(_layout, _palette);

            Assert.That(Summary(TuftBuilder.Build(_tufts, _palette, terrain.Edge)), Is.EqualTo(ExpectedTufts));
        }

        // Triangle count plus the sum of every coordinate, rounded to a millimetre: any moved vertex shows.
        private static string Summary(TerrainMeshData mesh)
        {
            double x = 0;
            double y = 0;
            double z = 0;
            foreach (Vector3 p in mesh.Positions)
            {
                x += p.x;
                y += p.y;
                z += p.z;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} tris {1:F2}/{2:F2}/{3:F2}", mesh.TriangleCount, x, y, z);
        }
    }
}
