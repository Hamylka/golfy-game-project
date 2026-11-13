using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Tall obstacles: the figures quoted in the PR. Manual measurements only (`Explicit`), never in the default suite. Each writes
    /// its report to the console and to <c>Temp/IsoGolfTall*.txt</c>.
    /// </summary>
    public class TallObstacleMeasurements
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        /// <summary>
        /// Variant 0, seeds 0-199 per par: courses with a tall obstacle (full blocks) per kind, the strategic groups they replaced
        /// (S2's zone, from a run with the tall weights at zero), props per course, and generation time with props, now and S2.
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Category("Measurement")]
        [Timeout(3600000)]
        public void TallShareAndCost()
        {
            LevelGenSettings s2 = TallObstacleTests.WithoutTall();
            var report = new StringBuilder("TALL_SHARE variant 0, seeds 0-199");
            var changed = new StringBuilder("TALL_CHANGED");
            try
            {
                foreach (int par in new[] { 3, 4, 5 })
                {
                    int props = 0, propsBefore = 0, tall = 0, blockLine = 0;
                    var kinds = new Dictionary<PropKind, int>();
                    var replaced = new Dictionary<StrategicZone, int>();
                    var now = new Stopwatch();
                    var before = new Stopwatch();
                    for (int seed = 0; seed < 200; seed++)
                    {
                        before.Start();
                        CourseLayout old = CourseGenerator.Generate(seed, par, s2);
                        before.Stop();
                        now.Start();
                        CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                        now.Stop();
                        props += layout.Props.Count;
                        propsBefore += old.Props.Count;

                        var zones = new PropZones(layout, Settings);
                        for (int i = 0; i < zones.Lines.Count; i++)
                        {
                            if (zones.IsStrategicLine(i) && zones.FullBlockAllowed(i, par))
                            {
                                blockLine++;
                                break;
                            }
                        }

                        foreach (CourseProp prop in layout.Props)
                        {
                            if (prop.Zone != StrategicZone.BlockLine)
                            {
                                continue;
                            }

                            tall++;
                            kinds[prop.Kind] = kinds.TryGetValue(prop.Kind, out int k) ? k + 1 : 1;
                            StrategicZone was = StrategicZone.None;
                            string members = "";
                            foreach (CourseProp p in old.Props)
                            {
                                if (p.Strategic)
                                {
                                    was = was == StrategicZone.None ? p.Zone : was;
                                    members += $"{p.Kind} ";
                                }
                            }

                            replaced[was] = replaced.TryGetValue(was, out int r) ? r + 1 : 1;
                            changed.Append($" | par {par} seed {seed}: {prop.Kind} replaces {(was == StrategicZone.None ? "no group" : $"{was} ({members.Trim()})")}");
                        }
                    }

                    report.Append($" | par {par}: block line on {blockLine}/200, tall obstacle on {tall}/200 ({100f * tall / 200f:F1}%):");
                    foreach (KeyValuePair<PropKind, int> kind in kinds)
                    {
                        report.Append($" {kind.Key} {kind.Value}");
                    }

                    report.Append("; replaced S2 groups:");
                    foreach (KeyValuePair<StrategicZone, int> zone in replaced)
                    {
                        report.Append($" {zone.Key} {zone.Value}");
                    }

                    report.Append($"; props per course {(float)props / 200:F2} (S2 {(float)propsBefore / 200:F2}); generation with props {now.Elapsed.TotalMilliseconds / 200:F1} ms per course (S2 {before.Elapsed.TotalMilliseconds / 200:F1})");
                }
            }
            finally
            {
                Object.DestroyImmediate(s2);
            }

            Write("Temp/IsoGolfTallShare.txt", report + "\n" + changed);
        }

        /// <summary>
        /// Checked courses, seeds 0-49 per par: the reroll survey (candidates tried, failed checks), props kept per course, props
        /// dropped (all, and tall), checked generation time; then the phone review seeds, a checked course with a tall obstacle kept,
        /// labelled with its kind.
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Category("Measurement")]
        [Timeout(3600000)]
        public void CheckedTallAndReviewSeeds()
        {
            var report = new StringBuilder("TALL_CHECKED seeds 0-49");
            var seeds = new StringBuilder("TALL_REVIEW_SEEDS");
            foreach (int par in new[] { 3, 4, 5 })
            {
                int props = 0, dropped = 0, tallKept = 0, tallPlaced = 0, rerolls = 0, failed = 0;
                var clock = new Stopwatch();
                for (int seed = 0; seed < 50; seed++)
                {
                    clock.Start();
                    CheckedCourseResult result = CheckedCourse.Generate(seed, par, Settings, Shot);
                    clock.Stop();
                    props += result.Layout.Props.Count;
                    dropped += result.DroppedProps;
                    rerolls += result.Candidates - 1;
                    failed += result.Failed != CourseCheck.None ? 1 : 0;
                    foreach (CourseProp prop in CourseGenerator.Generate(seed, par, Settings, result.Variant).Props)
                    {
                        tallPlaced += prop.Zone == StrategicZone.BlockLine ? 1 : 0;
                    }

                    foreach (CourseProp prop in result.Layout.Props)
                    {
                        if (prop.Zone == StrategicZone.BlockLine)
                        {
                            tallKept++;
                            seeds.Append($" | par {par} seed {seed} variant {result.Variant}: {prop.Kind}");
                        }
                    }
                }

                report.Append($" | par {par}: rerolls {rerolls}, failed {failed}, props per course {(float)props / 50:F2}, dropped {dropped}, tall placed {tallPlaced} kept {tallKept}, checked generation {clock.Elapsed.TotalMilliseconds / 50:F1} ms per course");
            }

            Write("Temp/IsoGolfTallChecked.txt", report + "\n" + seeds);
        }

        private static void Write(string path, string text)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(path, text);
            Debug.Log(text);
        }
    }
}
