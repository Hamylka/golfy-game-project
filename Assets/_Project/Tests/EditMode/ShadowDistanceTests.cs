using IsoGolf.Generation;
using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Taller screens push the framed camera further back, so the far end of the island gets further away.
    /// URP fades shadows out between (1 - border) of the shadow distance and the full distance, measured
    /// from the camera, so the whole island has to stay nearer than where that fade starts.
    /// </summary>
    public class ShadowDistanceTests
    {
        private const string PipelinePath = "Assets/_Project/Rendering/IsoGolf_RPAsset.asset";
        private const string ShapePath = "Assets/_Project/Settings/Par3CourseShape.asset";
        private const string PalettePath = "Assets/_Project/Settings/Palette.asset";
        private const string RigPath = "Assets/_Project/Settings/CameraRigSettings.asset";

        [TestCase(3f, 4f, TestName = "Tablet 3:4")]
        [TestCase(9f, 16f, TestName = "Older phone 9:16")]
        [TestCase(9f, 19.5f, TestName = "Common phone 9:19.5")]
        [TestCase(1280f, 2800f, TestName = "Honor 500 Pro")]
        [TestCase(9f, 21f, TestName = "Tall phone 9:21")]
        public void WholeParThreeIslandKeepsFullShadows(float width, float height)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(ShapePath);
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>(PalettePath);
            var rig = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(RigPath);

            TerrainResult terrain = TerrainBuilder.Build(shape.ToLayout(), palette);
            var view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, width / height);
            Vector3 camera = CameraFraming.Fit(terrain.Outline, view, rig.ScreenFill);

            float farthest = 0f;
            foreach (Vector3 point in terrain.Outline)
            {
                farthest = Mathf.Max(farthest, Vector3.Distance(camera, point));
            }

            float fadeStart = pipeline.shadowDistance * (1f - pipeline.cascadeBorder);
            Assert.That(farthest, Is.LessThan(fadeStart));
        }
    }
}
