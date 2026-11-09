using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The course checks and their deterministic reroll (PR 7). The explicit figures (reroll survey over 200 seeds, the fine ace
    /// sweep over 50) are in <see cref="CourseCheckMeasurements"/>.
    /// </summary>
    public class CourseCheckTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        /// <summary>
        /// (seed, par, variant kept), measured with the settings of this PR. A change to the generator or the shot settings may
        /// move them: that is allowed until the first release, but the figures in the PR and CHANGELOG then need a new survey.
        /// </summary>
        private static readonly int[][] Pinned =
        {
            new[] { 0, 3, 0 }, new[] { 9, 3, 1 },
            new[] { 0, 4, 0 }, new[] { 6, 4, 1 }, new[] { 4, 4, 2 }, new[] { 153, 4, 3 },
            new[] { 0, 5, 0 }, new[] { 19, 5, 1 }, new[] { 77, 5, 2 },
        };

        private static LevelGenSettings Copy(System.Action<SerializedObject> change)
        {
            LevelGenSettings copy = Object.Instantiate(Settings);
            var serialized = new SerializedObject(copy);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        [Test]
        public void PinnedSeeds_KeepTheirVariant()
        {
            foreach (int[] pin in Pinned)
            {
                CheckedCourseResult result = CheckedCourse.Generate(pin[0], pin[1], Settings, Shot);
                Assert.That(result.Variant, Is.EqualTo(pin[2]), $"seed {pin[0]} par {pin[1]}");
                Assert.That(result.Passed, Is.True, $"seed {pin[0]} par {pin[1]}");
                Assert.That(result.Candidates, Is.EqualTo(pin[2] + 1));
            }
        }

        [Test]
        public void SameSeed_GivesTheSameCourse()
        {
            foreach (int[] pin in new[] { new[] { 9, 3 }, new[] { 4, 4 }, new[] { 77, 5 } })
            {
                CheckedCourseResult a = CheckedCourse.Generate(pin[0], pin[1], Settings, Shot);
                CheckedCourseResult b = CheckedCourse.Generate(pin[0], pin[1], Settings, Shot);
                CourseLayout direct = CourseGenerator.Generate(pin[0], pin[1], Settings, a.Variant);

                Assert.That(b.Variant, Is.EqualTo(a.Variant));
                foreach (CourseLayout other in new[] { b.Layout, direct })
                {
                    Assert.That(other.Tee, Is.EqualTo(a.Layout.Tee));
                    Assert.That(other.Hole, Is.EqualTo(a.Layout.Hole));
                    Assert.That(other.Bunkers.Count, Is.EqualTo(a.Layout.Bunkers.Count));
                    Assert.That(other.Ponds.Count, Is.EqualTo(a.Layout.Ponds.Count));
                    Assert.That(other.Plan.SafeRoute, Is.EqualTo(a.Layout.Plan.SafeRoute));
                }
            }
        }

        [Test]
        public void CheckedCourses_PassEveryCheck([Values(3, 4, 5)] int par)
        {
            for (int seed = 0; seed < 30; seed++)
            {
                CheckedCourseResult result = CheckedCourse.Generate(seed, par, Settings, Shot);
                Assert.That(result.Passed, Is.True, $"seed {seed}: {result.Failed}");
                Assert.That(result.Layout.Plan.Complete, Is.True, $"seed {seed}");
                Assert.That(CourseChecks.Run(result.Layout, Shot, Settings), Is.EqualTo(CourseCheck.None), $"seed {seed}");
            }
        }

        [Test]
        public void IncompletePlan_IsRerolled()
        {
            // Seed 4's par 4 misses a role on variants 0 and 1 (see the pinned table).
            CourseLayout first = CourseGenerator.Generate(4, 4, Settings);
            Assert.That(first.Plan.Complete, Is.False);
            Assert.That(CourseChecks.Run(first, Shot, Settings), Is.EqualTo(CourseCheck.IncompletePlan));
            Assert.That(CheckedCourse.Generate(4, 4, Settings, Shot).Layout.Plan.Complete, Is.True);
        }

        [Test]
        public void HandMadeCourse_HasNoPlan_AndFailsAsIncomplete()
        {
            CourseLayout layout = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(CameraCourse.Par3Path).ToLayout();
            Assert.That(CourseChecks.Run(layout, Shot, Settings), Is.EqualTo(CourseCheck.IncompletePlan));
        }

        [Test]
        public void RestNearHole_FailsWhenNoApproachCanReach()
        {
            // Only the weakest approach, straight: from the tee of a par 3 it stops metres short.
            LevelGenSettings weak = Copy(s =>
            {
                s.FindProperty("restCheckPower").vector2Value = new Vector2(0.02f, 0.02f);
                s.FindProperty("restCheckAim").floatValue = 0f;
            });
            CourseLayout layout = CourseGenerator.Generate(0, 3, Settings);
            var ground = new BallGround(layout);

            Assert.That(CourseChecks.RestsNearHole(ground, layout.Plan, Shot, Settings), Is.True);
            Assert.That(CourseChecks.RestsNearHole(ground, layout.Plan, Shot, weak), Is.False);
            Object.DestroyImmediate(weak);
        }

        [Test]
        public void Gap_FailsWhenTheLayUpCannotCarryIt()
        {
            // The first par 4 with a gap on its safe route.
            CourseLayout layout = null;
            for (int seed = 0; seed < 50 && layout == null; seed++)
            {
                CourseLayout candidate = CourseGenerator.Generate(seed, 4, Settings);
                if (candidate.Plan.Complete && CourseGeneratorTests.Gaps(candidate).Count > 0)
                {
                    layout = candidate;
                }
            }

            Assert.That(layout, Is.Not.Null);
            LevelGenSettings weakLayup = Copy(s =>
            {
                s.FindProperty("gapCheckMinPower").floatValue = 0.05f;
                s.FindProperty("layupPower").floatValue = 0.1f;
            });
            var ground = new BallGround(layout);

            Assert.That(CourseChecks.GapsClear(ground, layout.Plan, Shot, Settings), Is.True);
            Assert.That(CourseChecks.GapsClear(ground, layout.Plan, Shot, weakLayup), Is.False);
            Object.DestroyImmediate(weakLayup);
        }

        [Test]
        public void NoVariantPasses_KeepsTheLowestOfTheLeastBad_AndWarns()
        {
            // Every variant fails the approach check (only the weakest straight shot), so all fail exactly one check.
            LevelGenSettings strict = Copy(s =>
            {
                s.FindProperty("restCheckPower").vector2Value = new Vector2(0.02f, 0.02f);
                s.FindProperty("restCheckAim").floatValue = 0f;
                s.FindProperty("checkVariants").intValue = 3;
            });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no variant of 3 passed"));

            CheckedCourseResult result = CheckedCourse.Generate(0, 3, strict, Shot);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.Failed, Is.EqualTo(CourseCheck.NoRestNearHole));
            Assert.That(result.Variant, Is.EqualTo(0));
            Assert.That(result.Candidates, Is.EqualTo(3));
            Object.DestroyImmediate(strict);
        }

        [Test]
        public void HoleInOne_OnPinnedPar3s_StaysUnderOnePercent_AndNeverAtFullPower()
        {
            // The fine sweep the target is defined on, on two pinned par 3s (about 3 s each). The mean over 50 seeds is an
            // explicit measurement (CourseCheckMeasurements.AceSweep_OverCheckedPar3s).
            foreach (int seed in new[] { 0, 9 })
            {
                var ground = new BallGround(CheckedCourse.Generate(seed, 3, Settings, Shot).Layout);
                ShotSweepResult fine = ShotSweep.Run(Shot, ground, ground.Tee, ground.Hole, 6f, 0.1f, 0.5f, 1f, 0.01f);
                ShotSweepResult full = ShotSweep.Run(Shot, ground, ground.Tee, ground.Hole, 6f, 0.1f, 1f, 1f, 1f);

                Assert.That(fine.HoledShare, Is.LessThan(0.01f), $"seed {seed}: {fine.HoledPowers}");
                Assert.That(full.Holed, Is.EqualTo(0), $"seed {seed}");
            }
        }
    }
}
