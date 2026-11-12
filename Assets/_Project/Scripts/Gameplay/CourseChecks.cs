using System;
using System.Collections.Generic;
using IsoGolf.Generation;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("IsoGolf.Tests.EditMode")]

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

        /// <summary>
        /// A safe-route leg's first flight, at its planned power (<see cref="PlannedArc"/>), struck a prop before landing (strategic
        /// obstacles S1). Checked with props on only; the flag pole doesn't count.
        /// </summary>
        SafeLegHit = 8,
    }

    /// <summary>What <see cref="CheckedCourse.Generate"/> settled on.</summary>
    public sealed class CheckedCourseResult
    {
        public CheckedCourseResult(CourseLayout layout, int variant, int candidates, CourseCheck failed, int droppedProps = 0)
        {
            Layout = layout;
            Variant = variant;
            Candidates = candidates;
            Failed = failed;
            DroppedProps = droppedProps;
        }

        /// <summary>How many placed props were taken off the kept course because the checks failed with them (M4 PR 4.3).</summary>
        public int DroppedProps { get; }

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

        /// <param name="props">
        /// Plays the check shots against the course's props and its flag pole (M4 PR 4.3), as the game does. False plays the bare
        /// course, which is what picks the variant, so props never decide which island is kept.
        /// </param>
        /// <param name="struck">Collects the obstacles every played shot struck (indices into the props' shapes; the pole comes last).</param>
        public static CourseCheck Run(CourseLayout layout, ShotSettings shot, LevelGenSettings settings, bool props = false, List<int> struck = null)
        {
            CoursePlan plan = layout.Plan;
            if (plan == null || !plan.Complete)
            {
                // Without a safe route there is nothing to play the other checks from.
                return CourseCheck.IncompletePlan;
            }

            var ground = new BallGround(layout);
            BallObstacles obstacles = props ? BallObstacles.ForCourse(layout.PropShapes, ground, shot) : null;
            CourseCheck failed = CourseCheck.None;
            if (!GapsClear(ground, plan, shot, settings, obstacles, struck))
            {
                failed |= CourseCheck.GapNotCleared;
            }

            if (!RestsNearHole(ground, plan, shot, settings, obstacles, struck))
            {
                failed |= CourseCheck.NoRestNearHole;
            }

            if (obstacles != null && !SafeLegsFly(ground, plan, shot, settings, obstacles, struck))
            {
                failed |= CourseCheck.SafeLegHit;
            }

            return failed;
        }

        /// <summary>
        /// Every leg of the safe route flies clear (strategic obstacles plan §2): played from its start, aimed at its end, at the power
        /// its planned arc lands with (<see cref="PlannedArc"/>, the model strategic props are kept under), its first flight strikes no
        /// prop before it lands. Bounces and rolls don't count: the landing capsules keep them clear of props, and today some already
        /// reach a prop past the capsule. The flag pole doesn't count either (hub's decision): only a prop can be dropped, and the
        /// pole is the approach check's business. The flight is found by the search <c>Shoot</c> runs (<see cref="BallSimulation.PredictHit"/>),
        /// so no step is played. Every struck prop goes into <paramref name="struck"/>, so <see cref="CheckedCourse.KeepProps"/> drops it.
        /// </summary>
        public static bool SafeLegsFly(BallGround ground, CoursePlan plan, ShotSettings shot, LevelGenSettings settings, BallObstacles obstacles, List<int> struck = null)
        {
            bool clear = true;
            for (int k = 0; k + 1 < plan.SafeRoute.Count; k++)
            {
                Vector2 from = plan.SafeRoute[k];
                Vector2 to = plan.SafeRoute[k + 1];
                var planned = new PlannedArc(from, ground.Height(from.x, from.y), to, ground.Height(to.x, to.y), settings);
                var ball = new BallSimulation(ground, shot, from, obstacles);
                BallisticArc arc = ball.ArcFor(planned.Direction, planned.Power);
                if (ball.PredictHit(arc, ball.PredictLanding(arc), out ArcHit hit) && !hit.IsPole)
                {
                    struck?.Add(hit.Index);
                    clear = false;
                }
            }

            return clear;
        }

        /// <summary>
        /// Every leg of the safe route that crosses open sky can be played: from its start, aimed at its end, some power
        /// from <see cref="LevelGenSettings.GapCheckMinPower"/> to the lay-up power leaves the ball at rest past the gap's far
        /// edge. The generator places that leg with the carry model alone; this adds the bounce, the roll and the slopes.
        /// </summary>
        public static bool GapsClear(BallGround ground, CoursePlan plan, ShotSettings shot, LevelGenSettings settings, BallObstacles obstacles = null, List<int> struck = null)
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
                    if (Play(ground, shot, from, direction, power, obstacles, struck, out Vector2 rest) == BallState.Resting
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
        public static bool RestsNearHole(BallGround ground, CoursePlan plan, ShotSettings shot, LevelGenSettings settings, BallObstacles obstacles = null, List<int> struck = null)
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
                    BallState end = Play(ground, shot, from, direction, power, obstacles, struck, out Vector2 rest);
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

        private static BallState Play(BallGround ground, ShotSettings shot, Vector2 from, Vector2 direction, float power, BallObstacles obstacles, List<int> struck, out Vector2 rest)
        {
            var ball = new BallSimulation(ground, shot, from, obstacles);
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

            struck?.AddRange(ball.Struck);
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

            // The variant is picked on the bare course, as before props existed: props never decide which island is kept.
            for (int variant = 0; variant < variants; variant++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, settings, variant, props: false);
                CourseCheck failed = CourseChecks.Run(layout, shot, settings);
                if (failed == CourseCheck.None)
                {
                    CourseLayout kept = KeepProps(CourseGenerator.WithProps(layout, seed, par, variant, settings), CourseCheck.None, shot, settings, seed, par, out int dropped);
                    return new CheckedCourseResult(kept, variant, variant + 1, CourseCheck.None, dropped);
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
            CourseLayout fallback = KeepProps(CourseGenerator.WithProps(best, seed, par, bestVariant, settings), bestFailed, shot, settings, seed, par, out int droppedFromFallback);
            return new CheckedCourseResult(fallback, bestVariant, variants, bestFailed, droppedFromFallback);
        }

        // Rounds of dropping struck props before every prop goes.
        private const int DropRounds = 3;

        /// <summary>
        /// Plays the checks again with the props and the flag pole, as the game does. A prop that makes a check fail that the bare
        /// course passed is dropped, not the course rerolled (M4 plan §3): every prop a played shot struck goes, and the checks run
        /// again, up to <see cref="DropRounds"/> times; after that, every prop goes. Only the struck props go (director's choice Q1 A),
        /// so a cluster may be left with a single prop, which stays. A run that strikes nothing plays exactly like the bare one.
        /// </summary>
        internal static CourseLayout KeepProps(CourseLayout layout, CourseCheck bareFailed, ShotSettings shot, LevelGenSettings settings, int seed, int par, out int dropped)
        {
            dropped = 0;
            int placed = layout.Props.Count;
            for (int round = 0; layout.Props.Count > 0; round++)
            {
                var struck = new List<int>();
                CourseCheck failed = CourseChecks.Run(layout, shot, settings, true, struck);
                if ((failed & ~bareFailed) == CourseCheck.None)
                {
                    return layout;
                }

                var drop = new HashSet<int>();
                foreach (int shape in struck)
                {
                    // The pole sits after the props' shapes; it never goes.
                    if (shape < layout.PropShapes.Count)
                    {
                        drop.Add(layout.PropOfShape(shape));
                    }
                }

                // A shed or a bench takes its trace (bicycle, laundry, bucket) with it: left alone it would stand against nothing.
                foreach (int i in new List<int>(drop))
                {
                    if (layout.Props[i].Kind == PropKind.Shed || layout.Props[i].Kind == PropKind.Bench)
                    {
                        for (int j = 0; j < layout.Props.Count; j++)
                        {
                            if (layout.Props[j].Cluster == layout.Props[i].Cluster && CourseProp.IsTrace(layout.Props[j].Kind))
                            {
                                drop.Add(j);
                            }
                        }
                    }
                }

                if (drop.Count == 0)
                {
                    Debug.LogWarning($"Course seed {seed} par {par}: the checks fail with the flag pole alone ({failed}); props kept.");
                    return layout;
                }

                var kept = new List<CourseProp>();
                for (int i = 0; i < layout.Props.Count; i++)
                {
                    if (!drop.Contains(i) && round + 1 < DropRounds)
                    {
                        kept.Add(layout.Props[i]);
                    }
                }

                layout = layout.WithProps(kept);
                dropped = placed - kept.Count;
            }

            return layout;
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
