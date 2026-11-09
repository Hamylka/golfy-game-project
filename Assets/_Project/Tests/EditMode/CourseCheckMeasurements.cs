using System.Diagnostics;
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
    /// Explicit measurements for the PR 7 description (run them by name): the reroll survey over seeds 0-199 per par, and the
    /// fine ace sweep over checked par 3s. The gates that run every time are in <see cref="CourseCheckTests"/>.
    /// </summary>
    public class CourseCheckMeasurements
    {
        public static ShotSettings Shot => AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");

        [Test, Explicit("Measurement: reroll rate, failures and time per par, seeds 0-199"), Timeout(3600000)]
        public void RerollSurvey()
        {
            var report = new StringBuilder();
            foreach (int par in new[] { 3, 4, 5 })
            {
                int rerolled = 0, exhausted = 0, maxVariant = 0;
                int incomplete = 0, gap = 0, rest = 0;
                long candidates = 0;
                var variants = new StringBuilder();
                var watch = Stopwatch.StartNew();
                for (int seed = 0; seed < 200; seed++)
                {
                    CheckedCourseResult result = CheckedCourse.Generate(seed, par, CourseGeneratorTests.Settings, Shot);
                    candidates += result.Candidates;
                    rerolled += result.Variant > 0 ? 1 : 0;
                    exhausted += result.Passed ? 0 : 1;
                    maxVariant = Mathf.Max(maxVariant, result.Variant);
                    if (result.Variant > 0 && variants.Length < 400)
                    {
                        variants.Append($"{seed}:{result.Variant} ");
                    }
                }

                watch.Stop();

                // What variant 0 fails, check by check, for the figures (not part of the timing above).
                for (int seed = 0; seed < 200; seed++)
                {
                    CourseCheck failed = CourseChecks.Run(CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings), Shot, CourseGeneratorTests.Settings);
                    incomplete += (failed & CourseCheck.IncompletePlan) != 0 ? 1 : 0;
                    gap += (failed & CourseCheck.GapNotCleared) != 0 ? 1 : 0;
                    rest += (failed & CourseCheck.NoRestNearHole) != 0 ? 1 : 0;
                }

                report.Append($"par {par}: rerolled {rerolled}/200, exhausted {exhausted}, highest variant {maxVariant}, {candidates / 200f:0.00} candidates per course, " +
                    $"{watch.ElapsedMilliseconds / 200f:0.0} ms per checked course (Editor, Mono). Variant 0 fails: incomplete plan {incomplete}, gap {gap}, no rest near hole {rest}.\n" +
                    $"  rerolled seeds (seed:variant): {variants}\n");
            }

            Debug.Log(report.ToString());
        }

        [Test, Explicit("Measurement: the fine ace sweep over checked par 3s, seeds 0-49"), Timeout(3600000)]
        public void AceSweep_OverCheckedPar3s()
        {
            ShotSettings shot = Shot;
            double sum = 0;
            float max = 0f;
            int maxSeed = -1, fullPowerAces = 0, over = 0;
            var report = new StringBuilder();
            const int Seeds = 50;
            for (int seed = 0; seed < Seeds; seed++)
            {
                CourseLayout layout = CheckedCourse.Generate(seed, 3, CourseGeneratorTests.Settings, shot).Layout;
                var ground = new BallGround(layout);
                ShotSweepResult fine = ShotSweep.Run(shot, ground, ground.Tee, ground.Hole, 6f, 0.1f, 0.5f, 1f, 0.01f);
                ShotSweepResult full = ShotSweep.Run(shot, ground, ground.Tee, ground.Hole, 6f, 0.1f, 1f, 1f, 1f);
                sum += fine.HoledShare;
                over += fine.HoledShare >= 0.01f ? 1 : 0;
                fullPowerAces += full.Holed > 0 ? 1 : 0;
                if (fine.HoledShare > max)
                {
                    max = fine.HoledShare;
                    maxSeed = seed;
                }

                report.Append($"seed {seed}: {fine.HoledShare:P2} ({fine.Holed}/{fine.Shots}), full power {full.Holed}\n");
            }

            Debug.Log($"Ace sweep, checked par 3 seeds 0-{Seeds - 1}: mean {sum / Seeds:P2}, max {max:P2} (seed {maxSeed}), {over} seeds at or over 1%, {fullPowerAces} seeds where full power holes.\n{report}");
        }
    }
}
