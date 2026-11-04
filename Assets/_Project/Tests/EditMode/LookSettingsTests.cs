using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IsoGolf.Tests
{
    /// <summary>Guards the art direction rules behind the look chosen on the phone in M1.3.</summary>
    public class LookSettingsTests
    {
        private const string PalettePath = "Assets/_Project/Settings/Palette.asset";
        private const string TerrainMaterialPath = "Assets/_Project/Art/Materials/Terrain.mat";
        private const string BackgroundMaterialPath = "Assets/_Project/Art/Materials/Background.mat";
        private const string PipelinePath = "Assets/_Project/Rendering/IsoGolf_RPAsset.asset";

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
        public void SkyIsLightBlueAndPalerAtTheBottomOfTheScreen()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);
            Color top = material.GetColor("_TopColor");
            Color bottom = material.GetColor("_BottomColor");

            Assert.That(top.b, Is.GreaterThan(top.g));
            Assert.That(top.g, Is.GreaterThan(top.r));
            Assert.That(bottom.grayscale, Is.GreaterThan(top.grayscale));
            Assert.That(material.GetFloat("_FadeHeight"), Is.InRange(0.05f, 1f));
        }

        [Test]
        public void DepthTextureStaysOff()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            // Measured on the phone in M1.5: it costs about 39% of the GPU frame. Turn it back on only for
            // an effect that reads depth, and measure that effect with its cost.
            Assert.That(pipeline.supportsCameraDepthTexture, Is.False);
        }

        [Test]
        public void CloudsStayLighterThanTheSky()
        {
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>(PalettePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);

            // Even the shaded side of a cloud must stand out against the blue at the top of the screen.
            Assert.That(palette.CloudShade.grayscale, Is.GreaterThan(material.GetColor("_TopColor").grayscale));
            Assert.That(palette.CloudLight.grayscale, Is.GreaterThan(palette.CloudShade.grayscale));
        }
    }
}
