using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>Guards the art direction rules behind the look chosen on the phone in M1.3.</summary>
    public class LookSettingsTests
    {
        private const string PalettePath = "Assets/_Project/Settings/Palette.asset";
        private const string TerrainMaterialPath = "Assets/_Project/Art/Materials/Terrain.mat";
        private const string BackgroundMaterialPath = "Assets/_Project/Art/Materials/Background.mat";

        [Test]
        public void RoughStaysDarkerThanTheFairway()
        {
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>(PalettePath);

            Assert.That(palette.Rough.grayscale, Is.LessThan(palette.FairwayDark.grayscale));
            Assert.That(palette.Rough.grayscale, Is.LessThan(palette.FairwayLight.grayscale));
        }

        [Test]
        public void ShadowTintIsCoolAndNeverBlack()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            Color tint = material.GetColor("_ShadowTint");

            Assert.That(tint.b, Is.GreaterThan(tint.r), "Blue must lead, so shadows lean violet or blue-grey.");
            Assert.That(tint.b, Is.GreaterThan(tint.g));
            Assert.That(tint.maxColorComponent, Is.GreaterThan(0f));
        }

        [Test]
        public void BackgroundFadesFromLightGreyAndStaysOnScreen()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);
            Color bottom = material.GetColor("_BottomColor");

            // ART_DIRECTION.md asks for #E4E4E4 at the bottom of the screen, fading to white above.
            Assert.That(bottom.r, Is.EqualTo(0xE4 / 255f).Within(0.01f));
            Assert.That(bottom.g, Is.EqualTo(bottom.r).Within(0.001f));
            Assert.That(bottom.b, Is.EqualTo(bottom.r).Within(0.001f));
            Assert.That(material.GetFloat("_FadeHeight"), Is.InRange(0.05f, 1f));
        }
    }
}
