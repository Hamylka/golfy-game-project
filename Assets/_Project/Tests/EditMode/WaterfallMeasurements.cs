using System.Text;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>M4 PR 4.8 measurement, quoted in the PR (not a gate): how often a pond reaches the island edge on generated courses.</summary>
    public class WaterfallMeasurements
    {
        [Test, Explicit("Measurement"), Timeout(3600000)]
        public void WaterEdgeFrequency()
        {
            const int Seeds = 200;
            LevelGenSettings settings = CourseGeneratorTests.Settings;
            var palette = ScriptableObject.CreateInstance<PaletteSettings>();
            var report = new StringBuilder($"WATERFALL_FREQUENCY variant 0, bare (no props), seeds 0-{Seeds - 1}\n");
            for (int par = 3; par <= 5; par++)
            {
                int courses = 0;
                int withWater = 0;
                float totalLength = 0f;
                float longest = 0f;
                var seeds = new StringBuilder();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, settings, 0, false);
                    TerrainResult terrain = TerrainBuilder.Build(layout, palette);
                    float length = 0f;
                    foreach (EdgeSegment segment in terrain.Edge)
                    {
                        if (segment.Inner == EdgeSurface.Water)
                        {
                            length += Vector3.Distance(segment.A, segment.B);
                        }
                    }

                    courses++;
                    if (length > 0f)
                    {
                        withWater++;
                        totalLength += length;
                        longest = Mathf.Max(longest, length);
                        seeds.Append($" {seed}:{length:F1}");
                    }
                }

                report.Append($"par {par}: {withWater}/{courses} courses with a water edge, mean {(withWater > 0 ? totalLength / withWater : 0f):F1} m, longest {longest:F1} m\n  seeds:{seeds}\n");
            }

            Object.DestroyImmediate(palette);
            Debug.Log(report.ToString());
        }
    }
}
