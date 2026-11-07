using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The shadow fade check of <see cref="ShadowDistanceTests"/> on generated courses: along every heading the camera can
    /// take, a par 3's framed stretch, earth band included, stays nearer than where URP starts fading shadows, up to 9:21.
    /// Longer courses reach past it by design (see below).
    /// </summary>
    public class GeneratedShadowTests
    {
        private const string PipelinePath = "Assets/_Project/Rendering/IsoGolf_RPAsset.asset";

        // Seeds tried per par and screen. Each one builds a terrain and walks every heading, so this stays small.
        private const int Seeds = 12;

        private static float FadeStart
        {
            get
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                return pipeline.shadowDistance * (1f - pipeline.cascadeBorder);
            }
        }

        [TestCase(9f, 16f)]
        [TestCase(1280f, 2800f)]
        [TestCase(9f, 21f)]
        public void ParThree_EveryHeading_FramedStretch_KeepsFullShadows(float width, float height)
        {
            Assert.That(WorstOverSeeds(3, width, height), Is.LessThan(FadeStart));
        }

        // Decided with the director (PR 4): a par 4 or 5 is long enough that its far ground lies past the fade start
        // wherever the camera is, even at the default zoom (about 67 m on 9:21), because the top of the screen looks far
        // ahead at a 40° pitch. Shadows fade there; whether it shows is judged on the phone in PR 9. Measured on 9:21 over
        // 40 seeds: par 4 66–73 m, par 5 72–82 m. This only keeps it from growing.
        [TestCase(4, 9f, 21f, 75f)]
        [TestCase(5, 9f, 21f, 85f)]
        public void LongCourses_EveryHeading_FramedStretch_StaysWithinItsMeasuredReach(int par, float width, float height, float ceiling)
        {
            Assert.That(WorstOverSeeds(par, width, height), Is.LessThan(ceiling));
        }

        private static float WorstOverSeeds(int par, float width, float height)
        {
            float fadeStart = FadeStart;
            float worst = 0f;
            int worstSeed = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                var course = new CameraCourse(CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings), width / height);
                float far = ShadowDistanceTests.WorstFramedStretch(course, out _);
                if (far > worst)
                {
                    worst = far;
                    worstSeed = seed;
                }
            }

            Debug.Log($"Shadow fade, generated par {par} at {width}:{height}: worst framed stretch {worst:F2} m (seed {worstSeed}); the fade starts at {fadeStart:F1} m.");
            return worst;
        }

        [Explicit("A measurement for tuning LevelGenSettings: slow.")]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Report_FramedStretch_OverManySeeds(int par)
        {
            float fadeStart = FadeStart;
            var text = new System.Text.StringBuilder($"Generated par {par} at 9:21, worst heading then starting heading (fade at {fadeStart:F1} m):");
            int past = 0;
            float worst = 0f;
            for (int seed = 0; seed < 40; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings);
                var course = new CameraCourse(layout, 9f / 21f);
                float far = ShadowDistanceTests.WorstFramedStretch(course, out float yaw);
                float start = course.FarthestFramedStretchPoint(course.StartYaw);
                worst = Mathf.Max(worst, far);
                past += far >= fadeStart ? 1 : 0;
                text.Append($"\n seed {seed}: {far:F1} (yaw {yaw:F0}) / {start:F1}, bends {layout.Centreline.Points.Count - 2}, gaps {CourseGeneratorTests.Gaps(layout).Count}, half width {layout.Islands[0].HalfWidth:F2} + {layout.Islands[0].WidthWobble:F2}");
            }

            text.Append($"\n worst {worst:F1} m, {past} of 40 past the fade start.");
            Debug.Log(text.ToString());
            Assert.Pass();
        }
    }
}
