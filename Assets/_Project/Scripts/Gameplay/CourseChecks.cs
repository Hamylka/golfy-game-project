using System;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>The checks a generated course can fail. Flags, so a candidate reports every check it failed at once.</summary>
    [Flags]
    public enum CourseCheck
    {
        None = 0,

        /// <summary>A required hazard role found no place, or the bare course has no safe route (<see cref="CoursePlan.Complete"/>).</summary>
        IncompletePlan = 1,

        /// <summary>A safe-route leg across a gap: no shot up to the lay-up power came to rest on land past it.</summary>
        GapNotCleared = 2,

        /// <summary>No approach from the safe route's last rest spot holed or stopped on the green near the hole.</summary>
        NoRestNearHole = 4,
    }

    /// <summary>What <see cref="CheckedCourse.Generate"/> settled on.</summary>
    public sealed class CheckedCourseResult
    {
        public CheckedCourseResult(CourseLayout layout, int variant, int candidates, CourseCheck failed)
        {
            Layout = layout;
            Variant = variant;
            Candidates = candidates;
            Failed = failed;
        }

        public CourseLayout Layout { get; }

        /// <summary>The variant kept: the first that passed, or the least bad one if none did.</summary>
        public int Variant { get; }

        /// <summary>How many variants were generated and checked.</summary>
        public int Candidates { get; }

        /// <summary>The checks the kept variant fails: <see cref="CourseCheck.None"/> unless every variant failed.</summary>
        public CourseCheck Failed { get; }

        public bool Passed => Failed == CourseCheck.None;
    }

    /// <summary>
    /// The course checks that run when a course is generated, on the phone too: cheap ones only (a few hundred simulated
    /// shots, about 15 ms in the Editor). They play the real <see cref="BallSimulation"/> on the real <see cref="BallGround"/>, so
    /// slopes, bounces and rolls count, which the generator's geometric safe route can't see. The hole-in-one rate is not
    /// checked here: its sweep costs seconds per course, so it is held by tuning and gated offline by the tests.
    /// </summary>
    public static class CourseChecks
    {
        // A shot settles in a few seconds; this only guards against settings that never stop the ball.
        private const int StepLimit = 60 * 60;

        // Sampling step along a safe-route leg to find where it is over open sky.
        private const float LegSample = 0.25f;

        public static CourseCheck Run(CourseLayout layout, ShotSettings shot, LevelGenSettings settings)
        {
            CoursePlan plan = layout.Plan;
            if (plan == null || !plan.Complete)
            {
                // Without a safe route there is nothing to play the other checks from.
                return CourseCheck.IncompletePlan;
            }

            var ground = new BallGround(layout);
            CourseCheck failed = CourseCheck.None;
            if (!GapsClear(ground, plan, shot, settings))
            {
                failed |= CourseCheck.GapNotCleared;
            }

            if (!RestsNearHole(ground, plan, shot, settings))
            {
                failed |= CourseCheck.NoRestNearHole;
            }

            return failed;
        }

        /// <summary>
        /// Every leg of the safe route that crosses open sky can be played: from its start, aimed at its end, some power
        /// from <see cref="LevelGenSettings.GapCheckMinPower"/> to the lay-up power leaves the ball at rest past the gap's far
        /// edge. The generator places that leg with the carry model alone; this adds the bounce, the roll and the slopes.
        /// </summary>
        public static bool GapsClear(BallGround ground, CoursePlan plan, ShotSettings shot, LevelGenSettings settings)
        {
            for (int k = 0; k + 1 < plan.SafeRoute.Count; k++)
            {
                Vector2 from = plan.SafeRoute[k];
                Vector2 to = plan.SafeRoute[k + 1];
                if (!FarEdgeOfGap(ground, from, to, out float farEdge))
                {
                    continue;
                }

                Vector2 direction = (to - from).normalized;
                bool cleared = false;
                foreach (float power in Powers(settings.GapCheckMinPower, settings.LayupPower, settings.CheckPowerStep))
                {
                    if (Play(ground, shot, from, direction, power, out Vector2 rest) == BallState.Resting
                        && Vector2.Dot(rest - from, direction) > farEdge)
                    {
                        cleared = true;
                        break;
                    }
                }

                if (!cleared)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A ball can come to rest near the hole: from the safe route's last rest spot (the tee on most par 3s), some approach
        /// at the hole or <see cref="LevelGenSettings.RestCheckAim"/> degrees either side, at a power from the
        /// <see cref="LevelGenSettings.RestCheckPower"/> range, holes or stops on the green within
        /// <see cref="LevelGenSettings.RestNearHole"/> of it.
        /// </summary>
        public static bool RestsNearHole(BallGround ground, CoursePlan plan, ShotSettings shot, LevelGenSettings settings)
        {
            Vector2 from = plan.SafeRoute[plan.SafeRoute.Count - 2];
            Vector2 straight = ground.Hole - from;
            float aim = settings.RestCheckAim;
            float[] aims = aim > 0f ? new[] { 0f, -aim, aim } : new[] { 0f };
            float near = settings.RestNearHole;

            foreach (float angle in aims)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, -angle) * (Vector3)straight;
                foreach (float power in Powers(settings.RestCheckPower.x, settings.RestCheckPower.y, settings.CheckPowerStep))
                {
                    BallState end = Play(ground, shot, from, direction, power, out Vector2 rest);
                    if (end == BallState.Holed)
                    {
                        return true;
                    }

                    if (end == BallState.Resting && ground.Surface(rest.x, rest.y) == CourseSurface.Green
                        && Vector2.Distance(rest, ground.Hole) <= near)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether a leg passes over open sky, and if so how far along it (from its start) the last such point is.</summary>
        private static bool FarEdgeOfGap(BallGround ground, Vector2 from, Vector2 to, out float farEdge)
        {
            float length = Vector2.Distance(from, to);
            int samples = Mathf.CeilToInt(length / LegSample);
            farEdge = -1f;
            for (int i = 0; i <= samples; i++)
            {
                float along = length * i / samples;
                Vector2 p = Vector2.Lerp(from, to, along / length);
                if (!ground.IsOnIsland(p.x, p.y))
                {
                    farEdge = along;
                }
            }

            return farEdge >= 0f;
        }

        /// <summary>Powers from <paramref name="low"/> to <paramref name="high"/>, both included, counted in whole steps so they never drift.</summary>
        private static System.Collections.Generic.IEnumerable<float> Powers(float low, float high, float step)
        {
            int count = Mathf.FloorToInt(((high - low) / step) + 0.001f);
            for (int i = 0; i <= count; i++)
            {
                yield return low + (i * step);
            }
        }

        private static BallState Play(BallGround ground, ShotSettings shot, Vector2 from, Vector2 direction, float power, out Vector2 rest)
        {
            var ball = new BallSimulation(ground, shot, from);
            ball.Shoot(direction, power);
            for (int i = 0; i < StepLimit; i++)
            {
                BallState state = ball.State;
                if (state == BallState.Resting || state == BallState.Holed || state == BallState.InWater || state == BallState.OffIsland)
                {
                    break;
                }

                ball.Step();
            }

            rest = new Vector2(ball.Position.x, ball.Position.z);
            return ball.State;
        }
    }

    /// <summary>
    /// A generated course that passed the checks: tries variant 0, 1, ... of a (seed, par) until one passes
    /// <see cref="CourseChecks"/>, at most <see cref="LevelGenSettings.CheckVariants"/> of them. If none does, it keeps the one that
    /// failed the fewest checks (the lowest variant on a tie) and warns. Every step is deterministic, so a (seed, par) always gives
    /// the same course.
    /// </summary>
    public static class CheckedCourse
    {
        public static CheckedCourseResult Generate(int seed, int par, LevelGenSettings settings, ShotSettings shot)
        {
            CourseLayout best = null;
            CourseCheck bestFailed = CourseCheck.None;
            int bestVariant = 0;
            int bestCount = int.MaxValue;
            int variants = Mathf.Max(1, settings.CheckVariants);

            for (int variant = 0; variant < variants; variant++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, settings, variant);
                CourseCheck failed = CourseChecks.Run(layout, shot, settings);
                if (failed == CourseCheck.None)
                {
                    return new CheckedCourseResult(layout, variant, variant + 1, CourseCheck.None);
                }

                int count = Count(failed);
                if (count < bestCount)
                {
                    best = layout;
                    bestFailed = failed;
                    bestVariant = variant;
                    bestCount = count;
                }
            }

            Debug.LogWarning($"Course seed {seed} par {par}: no variant of {variants} passed the checks; keeping variant {bestVariant}, which fails {bestFailed}.");
            return new CheckedCourseResult(best, bestVariant, variants, bestFailed);
        }

        private static int Count(CourseCheck checks)
        {
            int count = 0;
            for (int bits = (int)checks; bits != 0; bits &= bits - 1)
            {
                count++;
            }

            return count;
        }
    }
}
