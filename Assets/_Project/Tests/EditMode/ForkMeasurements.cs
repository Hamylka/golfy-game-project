using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.3 measurements, quoted in the PR (not gates): the fork share per par against the plan's 20% (par 4) and 30% (par 5), why
    /// fork tries fail, and the island bulge forks need (per side, over how many metres). The reroll survey and the ace sweep are
    /// <see cref="CourseCheckMeasurements"/>' own.
    /// </summary>
    public class ForkMeasurements
    {
        [Test, Explicit("Measurement"), Timeout(3600000)]
        public void ForkShare() => Report(200, true);

        /// <summary>The bare draw only, seeds 0-49: a quick look while tuning.</summary>
        [Test, Explicit("Measurement"), Timeout(3600000)]
        public void ForkShare_Quick() => Report(50, false);

        /// <summary>
        /// How often a fork fits when every par 4 and 5 draws one (so the fit rate, not the share), bare variant 0, seeds 0-99, against
        /// the bulge cap, the branch's edge margin and the spine width: what each would give the director if forks miss the target.
        /// </summary>
        [Test, Explicit("Measurement"), Timeout(3600000)]
        public void ForkFitSensitivity()
        {
            const int Seeds = 100;
            var report = new StringBuilder($"FORK_FIT_SENSITIVITY seeds 0-{Seeds - 1}, bare variant 0, every course draws a fork (fit rate; the share is the weight times it)\n");
            var cases = new (float bulge, float margin, Vector2 spine)[]
            {
                (1.5f, 1f, new Vector2(3.5f, 4f)),
                (2f, 1f, new Vector2(3.5f, 4f)),
                (2.5f, 1f, new Vector2(3.5f, 4f)),
                (3f, 1f, new Vector2(3.5f, 4f)),
                (1.5f, 0.5f, new Vector2(3.5f, 3.5f)),
                (2f, 0.5f, new Vector2(3.5f, 3.5f)),
            };

            foreach ((float bulge, float margin, Vector2 spine) in cases)
            {
                LevelGenSettings settings = Object.Instantiate(CourseGeneratorTests.Settings);
                settings.FairwayShapes.SetWeights(4, FairwayShapeWeights.Create(0, 0, 0, 100), 0);
                settings.FairwayShapes.SetWeights(5, FairwayShapeWeights.Create(0, 0, 0, 100), 0);
                settings.FairwayShapes.SetForkRoom(bulge, margin, spine);
                report.Append($"max bulge {bulge:F1} m, edge margin {margin:F1} m, spine {spine.x:F1}-{spine.y:F1} m:");
                foreach (int par in new[] { 4, 5 })
                {
                    int fits = 0, doglegs = 0, doglegFits = 0, noStart = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        CourseLayout layout = CourseGenerator.GenerateBare(seed, par, settings, 0, out FairwayShapeResult shapes);
                        bool fork = shapes.Spine != null;
                        fits += fork ? 1 : 0;
                        doglegs += layout.Plan.Dogleg ? 1 : 0;
                        doglegFits += fork && layout.Plan.Dogleg ? 1 : 0;
                        noStart += !fork && shapes.Rejections.TryGetValue("fork: no valid start", out int n) && n >= settings.FairwayShapes.TriesPerShape ? 1 : 0;
                    }

                    report.Append($" par {par} {fits}% (doglegs {doglegFits}/{doglegs}, no start at all {noStart})");
                }

                report.AppendLine();
                Object.DestroyImmediate(settings);
            }

            Debug.Log(report.ToString());
        }

        private static void Report(int seeds, bool withChecks)
        {
            LevelGenSettings settings = CourseGeneratorTests.Settings;
            var report = new StringBuilder($"FORK_SHARE seeds 0-{seeds - 1}{(withChecks ? "" : " (bare variant 0 only)")}\n");
            foreach (int par in new[] { 4, 5 })
            {
                int drawn = 0, placed = 0, bulged = 0, checkedForks = 0, checkedCourses = 0, missingGuard = 0;
                long ticks = 0;
                var bulges = new List<float>();
                var lengths = new List<float>();
                var rejections = new SortedDictionary<string, int>();
                for (int seed = 0; seed < seeds; seed++)
                {
                    var watch = Stopwatch.StartNew();
                    CourseLayout layout = CourseGenerator.GenerateBare(seed, par, settings, 0, out FairwayShapeResult shapes);
                    ticks += watch.ElapsedTicks;
                    foreach (KeyValuePair<string, int> pair in shapes.Rejections)
                    {
                        if (pair.Key.StartsWith("fork"))
                        {
                            rejections.TryGetValue(pair.Key, out int n);
                            rejections[pair.Key] = n + pair.Value;
                        }
                    }

                    drawn += shapes.FirstDrawn == FairwayShapeKind.Fork || shapes.SecondDrawn == FairwayShapeKind.Fork ? 1 : 0;
                    if (shapes.Spine != null)
                    {
                        placed++;
                        missingGuard += System.Linq.Enumerable.Contains(layout.Plan.MissingRoles, HazardRole.BranchGuard) ? 1 : 0;
                        float most = Mathf.Max(shapes.BulgeSafe, shapes.BulgeRisky);
                        bulges.Add(shapes.BulgeSafe);
                        bulges.Add(shapes.BulgeRisky);
                        bulged += most > 0f ? 1 : 0;
                        lengths.Add(shapes.BulgeStretch.y - shapes.BulgeStretch.x + (2f * settings.FairwayShapes.BulgeEase));
                        report.Append($"  par {par} seed {seed}: bulge safe {shapes.BulgeSafe:F2} m, risky {shapes.BulgeRisky:F2} m, fork {shapes.BulgeStretch.x:F1}-{shapes.BulgeStretch.y:F1} m\n");
                    }

                    if (withChecks)
                    {
                        CheckedCourseResult result = CheckedCourse.Generate(seed, par, settings, CourseCheckMeasurements.Shot);
                        checkedCourses++;
                        foreach (FairwayShape shape in result.Layout.Plan.FairwayShapes)
                        {
                            checkedForks += shape.Kind == FairwayShapeKind.Fork ? 1 : 0;
                        }
                    }
                }

                bulges.Sort();
                lengths.Sort();
                string Quantiles(List<float> v) => v.Count == 0 ? "none" : $"min {v[0]:F2}, median {v[v.Count / 2]:F2}, max {v[v.Count - 1]:F2}";
                report.Append($"par {par}: fork drawn on {drawn}, placed on {placed} of {seeds} bare courses ({100f * placed / seeds:F1}%; target {(par == 4 ? 20 : 30)}%)");
                if (withChecks)
                {
                    report.Append($", checked courses with a fork {checkedForks} of {checkedCourses} ({100f * checkedForks / checkedCourses:F1}%)");
                }

                report.Append($"; bare forks missing their branch guard {missingGuard}\n");
                report.Append($"  bulge per side (both sides of every fork): {Quantiles(bulges)} m; forks with any bulge {bulged} of {placed}\n");
                report.Append($"  bulge length along the centreline, eases included: {Quantiles(lengths)} m\n");
                report.Append($"  fork rejections: {string.Join(", ", rejections)}\n");
                report.Append($"  bare generation {ticks * 1000.0 / Stopwatch.Frequency / seeds:F1} ms per course (Editor)\n");
            }

            Debug.Log(report.ToString());
        }
    }
}
