using System.Diagnostics;
using System.IO;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Explicit measurements for the PR description (run them by name): the hole-in-one sweep over generated par 3s, the time
    /// to generate a course, and the strategy maps. None gates anything.
    /// </summary>
    public class CourseHazardMeasurements
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        [Test, Explicit("Measurement: the ace sweep over generated par 3s, seeds 0 to 49")]
        public void AceSweep_OverGeneratedPar3s()
        {
            var shot = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            double sum = 0;
            float max = 0f;
            int maxSeed = -1, fullPowerAces = 0, over = 0, count = 0;
            var report = new StringBuilder();
            for (int seed = 0; seed < 50; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, 3, Settings);
                var ground = new BallGround(layout);
                ShotSweepResult fine = ShotSweep.Run(shot, ground, ground.Tee, ground.Hole, 6f, 0.1f, 0.5f, 1f, 0.01f);
                ShotSweepResult full = ShotSweep.Run(shot, ground, ground.Tee, ground.Hole, 6f, 0.1f, 1f, 1f, 1f);
                sum += fine.HoledShare;
                count++;
                over += fine.HoledShare >= 0.01f ? 1 : 0;
                fullPowerAces += full.Holed > 0 ? 1 : 0;
                if (fine.HoledShare > max)
                {
                    max = fine.HoledShare;
                    maxSeed = seed;
                }

                report.Append($"seed {seed}: {fine.HoledShare:P2} ({fine.Holed}/{fine.Shots}), full power {full.Holed}\n");
            }

            Debug.Log($"Ace sweep, par 3 seeds 0-49: mean {sum / count:P2}, max {max:P2} (seed {maxSeed}), {over} seeds at or over 1%, {fullPowerAces} seeds where full power holes.\n{report}");
        }

        [Test, Explicit("Survey: which seeds have which kind of hazard, to pick the capture seeds")]
        public void SeedSurvey()
        {
            var report = new StringBuilder();
            foreach (int par in new[] { 3, 4, 5 })
            {
                var lists = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>();
                void Tag(string tag, int seed)
                {
                    if (!lists.TryGetValue(tag, out var list))
                    {
                        lists[tag] = list = new System.Collections.Generic.List<int>();
                    }

                    if (list.Count < 12)
                    {
                        list.Add(seed);
                    }
                }

                for (int seed = 0; seed < 200; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    CoursePlan plan = layout.Plan;
                    if (!plan.Complete)
                    {
                        continue;
                    }

                    var fields = new CourseFields(layout);
                    System.Collections.Generic.List<HazardOutline> hazards = CourseHazardTests.Outlines(layout);
                    int bends = layout.Centreline.Points.Count - 2;
                    bool gap = CourseGeneratorTests.Gaps(layout).Count > 0;
                    for (int i = 0; i < hazards.Count; i++)
                    {
                        HazardInfo info = plan.Hazards[i];
                        bool runsOff = hazards[i].IsWater && System.Linq.Enumerable.Any(hazards[i].Ring, p => fields.Island(Geo.At(p)) >= 0f);
                        Vector2 u = plan.GuardDirection;
                        string side = Vector2.SignedAngle(-(layout.Hole - plan.DirectOrigin).normalized, hazards[i].Centre - layout.Greens[0].Outline.Center) > 0 ? "left" : "right";
                        Tag($"{info.Role} {(info.IsWater ? "water" : "sand")}{(runsOff ? " off-edge" : string.Empty)} {(info.Role == HazardRole.GreenGuard ? side : string.Empty)}", seed);
                    }

                    Tag($"bends {bends}{(gap ? " gap" : string.Empty)}{(plan.IslandGreen ? " island-green" : string.Empty)}", seed);
                }

                report.Append($"par {par}\n");
                foreach (var pair in lists)
                {
                    report.Append($"  {pair.Key}: {string.Join(",", pair.Value)}\n");
                }
            }

            Debug.Log(report.ToString());
        }

        [Test, Explicit("Measurement: generation time per par over 50 seeds")]
        public void GenerationTime()
        {
            var report = new StringBuilder();
            foreach (int par in new[] { 3, 4, 5 })
            {
                var watch = Stopwatch.StartNew();
                for (int seed = 0; seed < 50; seed++)
                {
                    CourseGenerator.Generate(seed, par, Settings);
                }

                watch.Stop();
                report.Append($"par {par}: {watch.ElapsedMilliseconds / 50f:0.0} ms per course (Editor, Mono)\n");
            }

            Debug.Log(report.ToString());
        }

        [Test, Explicit("Writes the strategy maps to Docs/Screenshots/m3-surfaces-hazards/strategy-map")]
        public void StrategyMaps()
        {
            const float Pixels = 6f;
            const int Columns = 2, Rows = 6;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Screenshots/m3-surfaces-hazards/strategy-map"));
            Directory.CreateDirectory(folder);
            foreach (int par in new[] { 3, 4, 5 })
            {
                int cellW = Mathf.RoundToInt((par == 3 ? 40f : par == 4 ? 70f : 100f) * Pixels);
                int cellH = Mathf.RoundToInt(36f * Pixels);
                var sheet = new Texture2D(cellW * Columns, cellH * Rows, TextureFormat.RGB24, false);
                var fill = new Color32[sheet.width * sheet.height];
                for (int i = 0; i < fill.Length; i++)
                {
                    fill[i] = new Color32(255, 255, 255, 255);
                }

                sheet.SetPixels32(fill);
                for (int seed = 0; seed < Columns * Rows; seed++)
                {
                    DrawCourse(sheet, CourseGenerator.Generate(seed, par, Settings), (seed % Columns) * cellW, (Rows - 1 - (seed / Columns)) * cellH, cellW, cellH, Pixels);
                }

                File.WriteAllBytes(Path.Combine(folder, $"par{par}-seeds-0-11.png"), sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
            }
        }

        private static Color32 SurfaceColour(CourseSurface surface, int stripe)
        {
            switch (surface)
            {
                case CourseSurface.Fairway: return stripe % 2 == 0 ? new Color32(143, 168, 90, 255) : new Color32(126, 154, 72, 255);
                case CourseSurface.Green: return new Color32(176, 204, 118, 255);
                case CourseSurface.Rough: return new Color32(106, 133, 65, 255);
                case CourseSurface.Bunker: return new Color32(232, 214, 160, 255);
                case CourseSurface.Water: return new Color32(104, 170, 205, 255);
                default: return new Color32(214, 232, 245, 255);
            }
        }

        private static void DrawCourse(Texture2D sheet, CourseLayout layout, int originX, int originY, int width, int height, float pixels)
        {
            var fields = new CourseFields(layout);
            Vector2 middle = (layout.Tee + layout.Hole) * 0.5f;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    var p = new Vector3(middle.x + ((px - (width * 0.5f)) / pixels), 0f, middle.y + ((py - (height * 0.5f)) / pixels));
                    sheet.SetPixel(originX + px, originY + py, SurfaceColour(fields.Surface(p), fields.Stripe(fields.Across(p))));
                }
            }

            Vector2Int ToPixel(Vector2 p) => new Vector2Int(Mathf.RoundToInt(((p.x - middle.x) * pixels) + (width * 0.5f)), Mathf.RoundToInt(((p.y - middle.y) * pixels) + (height * 0.5f)));
            void Dot(Vector2Int c, Color32 color)
            {
                if (c.x >= 0 && c.x < width && c.y >= 0 && c.y < height)
                {
                    sheet.SetPixel(originX + c.x, originY + c.y, color);
                }
            }

            foreach (ShotLine line in layout.Plan.Lines)
            {
                Color32 color = line.Kind == ShotLineKind.Direct ? new Color32(200, 40, 40, 255) : line.Kind == ShotLineKind.Cut ? new Color32(230, 140, 20, 255) : new Color32(255, 255, 255, 255);
                Vector2Int a = ToPixel(line.From), b = ToPixel(line.To);
                int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 1);
                for (int i = 0; i <= steps; i++)
                {
                    Dot(Vector2Int.RoundToInt(Vector2.Lerp(a, b, i / (float)steps)), color);
                }
            }

            foreach (Vector2 spot in layout.Plan.SafeRoute)
            {
                Vector2Int c = ToPixel(spot);
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        Dot(new Vector2Int(c.x + dx, c.y + dy), new Color32(255, 255, 255, 255));
                    }
                }
            }
        }
    }
}
