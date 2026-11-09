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

        /// <summary>
        /// The farthest point of the course's framed stretch over every heading the camera can take on it (the twist,
        /// PR 3d, is not in): the course's last stretch along each heading, framed at the one zoomed-out scale, as the
        /// par 3 is framed whole above. Earth band included.
        /// </summary>
        public static float WorstFramedStretch(CameraCourse course, out float atYaw)
        {
            float farthest = 0f;
            atYaw = course.StartYaw;
            foreach (float yaw in course.Yaws())
            {
                float far = course.FarthestFramedStretchPoint(yaw);
                if (far > farthest)
                {
                    farthest = far;
                    atYaw = yaw;
                }
            }

            return farthest;
        }

        [TestCase(9f, 16f)]
        [TestCase(1280f, 2800f)]
        public void ParThree_EveryHeading_FramedWhole_KeepsFullShadows(float width, float height)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var course = new CameraCourse(CameraCourse.Par3Path, width / height);

            Assert.That(WorstFramedStretch(course, out _), Is.LessThan(pipeline.shadowDistance * (1f - pipeline.cascadeBorder)));
        }

        // Measured when the camera got headings (feature/m3-camera-yaw), in metres: the worst heading, then the starting
        // heading alone (as before headings). The fade starts at 56.8 m. Past it: the par 3 turned at 9:21 by 0.2 m,
        // and the three sandbox fixtures on Honor and 9:21 screens, already at their starting heading. Their framing
        // was never checked before this PR. PR 4 moved the dogleg by up to 0.17 m: its fairway and the calm band of its relief
        // now follow the centreline, which changes its earth band a little. Generated par 4s and 5s reach past the fade start
        // too, by design (GeneratedShadowTests).
        [TestCase(CameraCourse.Par3Path, 9f, 16f, 49.07f, 48.13f)]
        [TestCase(CameraCourse.Par3Path, 1280f, 2800f, 54.93f, 53.95f)]
        [TestCase(CameraCourse.Par3Path, 9f, 21f, 57.00f, 56.06f)]
        // The long course is the generated par 5 seed 33 since M3 PR 8 (the hand-made one measured 68.03/58.58, 76.17/67.84
        // and 79.17/71.11). Worst heading is 150 on all three; at the starting heading it is under the fade start on 9:16.
        [TestCase(CameraCourse.LongPath, 9f, 16f, 69.14f, 55.02f)]
        [TestCase(CameraCourse.LongPath, 1280f, 2800f, 76.21f, 63.36f)]
        [TestCase(CameraCourse.LongPath, 9f, 21f, 78.83f, 66.37f)]
        [TestCase(CameraCourse.GapPath, 9f, 16f, 56.80f, 55.44f)]
        [TestCase(CameraCourse.GapPath, 1280f, 2800f, 65.30f, 63.75f)]
        [TestCase(CameraCourse.GapPath, 9f, 21f, 68.33f, 66.73f)]
        [TestCase(CameraCourse.DoglegPath, 9f, 16f, 63.68f, 55.95f)]
        [TestCase(CameraCourse.DoglegPath, 1280f, 2800f, 72.33f, 65.20f)]
        [TestCase(CameraCourse.DoglegPath, 9f, 21f, 75.49f, 68.52f)]
        public void EveryHeading_FramedStretch_StaysAtItsMeasuredDistance(string path, float width, float height, float worst, float atStart)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var course = new CameraCourse(path, width / height);
            float fadeStart = pipeline.shadowDistance * (1f - pipeline.cascadeBorder);

            float farthest = WorstFramedStretch(course, out float yaw);
            float straight = course.FarthestFramedStretchPoint(course.StartYaw);

            Debug.Log($"Shadow fade, {System.IO.Path.GetFileNameWithoutExtension(path)} at {width}:{height}: framed stretch {farthest:F2} m at worst (yaw {yaw}), {straight:F2} m at the starting heading; the fade starts at {fadeStart:F1} m.");
            Assert.That(farthest, Is.EqualTo(worst).Within(0.05f));
            Assert.That(straight, Is.EqualTo(atStart).Within(0.05f));
        }

        [Explicit("A measurement for the camera rotation PR: slow, and it does not hold with today's limits.")]
        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.LongPath)]
        [TestCase(CameraCourse.GapPath)]
        [TestCase(CameraCourse.DoglegPath)]
        public void Report_FarthestIslandPointOnScreen_AnyViewTheLimitsAllow(string path)
        {
            // The strict figure: fully zoomed out on 9:21 and at the default zoom, the view's centre anywhere the limits
            // allow, every heading, and every island point on screen, not only a framed stretch.
            var course = new CameraCourse(path, 9f / 21f);
            var start = new System.Collections.Generic.List<float> { course.StartYaw };
            foreach (float zoom in new[] { 0f, course.Rig.DefaultZoom })
            {
                float any = course.FarthestFramedPoint(course.Yaws(), out float yaw, out Vector2 centre, zoom);
                float straight = course.FarthestFramedPoint(start, out _, out Vector2 straightCentre, zoom);
                Debug.Log($"Strict, {System.IO.Path.GetFileNameWithoutExtension(path)} at 9:21, zoom {zoom}: {any:F1} m at yaw {yaw}, view centre {centre}; at the starting yaw only {straight:F1} m, view centre {straightCentre}.");
            }

            Assert.Pass();
        }
    }
}
