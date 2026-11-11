using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.1 measurements for the split fairway plan, quoted in the PR (not gates). <see cref="ForkFitShare"/> is the Q4 decision
    /// input: how often a fork fits on today's islands with no outline change. <see cref="FairwayFieldCost"/> is what pieces cost.
    /// </summary>
    public class FairwayShapeMeasurements
    {
        // Plan §1: a spine at least 3.5 m wide and 8 m long; branches 2.6 m (safe) and 2.2 m (risky) wide; the branches open and
        // close over 4 m (the plan's ease length); 1 m of rough between a branch's outer edge and the island edge (hub, Q3).
        private const int SpineHalfCm = 175;
        private const int SafeBranchCm = 260;
        private const int RiskyBranchCm = 220;
        private const int EdgeMarginCm = 100;
        private const float SpineLength = 8f;
        private const float Ease = 4f;

        // Plan §1: no shape within 6 m of the tee or of a gap's tips, none within 2 m of the fairway's start tip, and a fork rejoins
        // at least the cap depth before the fairway's end.
        private const float ClearOfTeeAndGaps = 6f;
        private const float ClearOfStartTip = 2f;
        private const float Step = 0.5f;

        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        [Test]
        [Explicit("Measurement")]
        [Timeout(1800000)]
        public void ForkFitShare()
        {
            LevelGenSettings settings = Settings;
            var report = new StringBuilder("FORK_FIT_SHARE seeds 0-199, bare variant 0, spine 3.5 m x 8 m, branches 2.6/2.2 m, edge margin 1.0 m\n");
            foreach (int par in new[] { 3, 4, 5 })
            {
                int guarded = 0, softExcluded = 0, eitherSide = 0, doglegs = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, settings, 0, props: false);
                    var fit = new ForkFit(layout, settings);
                    doglegs += layout.Plan.Dogleg ? 1 : 0;
                    guarded += fit.Fits(guardSides: true, excludeSoftBends: false) ? 1 : 0;
                    softExcluded += fit.Fits(guardSides: true, excludeSoftBends: true) ? 1 : 0;
                    eitherSide += fit.Fits(guardSides: false, excludeSoftBends: false) ? 1 : 0;
                }

                report.AppendLine($"par {par}: fits {Percent(guarded)} (safe side from the guard, dogleg bends excluded); {Percent(softExcluded)} with soft bends excluded too; {Percent(eitherSide)} either side (upper bound); doglegs {doglegs}/200");
            }

            // Sensitivity for the director's Q4 decision: the same test (safe side from the guard, dogleg bends excluded) with a
            // narrower edge margin, and with Q4 C's narrow spine (2.6 m). Not plan values, only what each would give.
            report.AppendLine("sensitivity (safe side from the guard, dogleg bends excluded):");
            foreach (int spineHalf in new[] { SpineHalfCm, 130 })
            {
                foreach (int margin in new[] { EdgeMarginCm, 50, 0 })
                {
                    report.Append($"  spine {spineHalf * 2 / 100f:F1} m, margin {margin / 100f:F1} m:");
                    foreach (int par in new[] { 4, 5 })
                    {
                        int count = 0;
                        for (int seed = 0; seed < 200; seed++)
                        {
                            CourseLayout layout = CourseGenerator.Generate(seed, par, settings, 0, props: false);
                            count += new ForkFit(layout, settings, spineHalf, margin).Fits(guardSides: true, excludeSoftBends: false) ? 1 : 0;
                        }

                        report.Append($" par {par} {Percent(count)}");
                    }

                    report.AppendLine();
                }
            }

            Debug.Log(report.ToString());
        }

        [Test]
        [Explicit("Measurement")]
        [Timeout(1800000)]
        public void FairwayFieldCost()
        {
            LevelGenSettings settings = Settings;
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            var report = new StringBuilder("FAIRWAY_FIELD_COST seeds 0-9 bare variant 0, grid 0.25 m over the course, best of 3 (Editor)\n");
            foreach (int par in new[] { 3, 4, 5 })
            {
                var plain = new List<CourseLayout>();
                var shaped = new List<CourseLayout>();
                for (int seed = 0; seed < 10; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, settings, 0, props: false);
                    plain.Add(layout);
                    shaped.Add(WithSyntheticShapes(layout));
                }

                report.AppendLine($"par {par}: Fairway() plain {FieldNs(plain):F0} ns/call, 4 pieces + 2 ovals {FieldNs(shaped):F0} ns/call; terrain build plain {BuildMs(plain, palette):F1} ms, shaped {BuildMs(shaped, palette):F1} ms per course");
            }

            Debug.Log(report.ToString());
        }

        private static string Percent(int count) => $"{count}/200 = {count * 0.5f:F1}%";

        private static double FieldNs(List<CourseLayout> layouts)
        {
            double best = double.MaxValue;
            float sink = 0f;
            long calls = 0;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                calls = 0;
                var clock = Stopwatch.StartNew();
                foreach (CourseLayout layout in layouts)
                {
                    var fields = new CourseFields(layout);
                    Vector2 extent = layout.HalfExtent;
                    for (float x = -extent.x; x <= extent.x; x += 0.25f)
                    {
                        for (float z = -extent.y; z <= extent.y; z += 0.25f)
                        {
                            sink += fields.Fairway(new Vector3(x, 0f, z));
                            calls++;
                        }
                    }
                }

                best = System.Math.Min(best, clock.Elapsed.TotalMilliseconds);
            }

            Assert.That(float.IsNaN(sink), Is.False);
            return best * 1e6 / calls;
        }

        private static double BuildMs(List<CourseLayout> layouts, PaletteSettings palette)
        {
            double best = double.MaxValue;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                var clock = Stopwatch.StartNew();
                foreach (CourseLayout layout in layouts)
                {
                    TerrainBuilder.Build(layout, palette);
                }

                best = System.Math.Min(best, clock.Elapsed.TotalMilliseconds / layouts.Count);
            }

            return best;
        }

        /// <summary>
        /// The course with a worst-case fairway for the cost: a main piece, two offset branches round a spine in the middle and a
        /// main piece after (4 pieces), and two rough ovals. Its look is not a planned shape, only its cost is measured.
        /// </summary>
        private static CourseLayout WithSyntheticShapes(CourseLayout layout)
        {
            FairwayLayout plain = layout.Fairway;
            float middle = (plain.Start + plain.End) * 0.5f;
            var pieces = new[]
            {
                new FairwayPiece(plain.Start, middle - 4f, plain.HalfWidth, plain.StartCap),
                new FairwayPiece(middle - 6f, middle + 6f, 1.3f, offset: 3.05f, offsetEase: 4f),
                new FairwayPiece(middle - 6f, middle + 6f, 1.1f, offset: -2.85f, offsetEase: 4f),
                new FairwayPiece(middle + 4f, plain.End, plain.HalfWidth, toCap: plain.EndCap),
            };
            Vector2 a = layout.Centreline.PointAt(plain.Start + 6f);
            Vector2 b = layout.Centreline.PointAt(plain.End - 8f);
            var ovals = new[] { new EllipseShape(a, new Vector2(1.5f, 0.8f), 0.1f), new EllipseShape(b, new Vector2(2f, 0.7f), 0.1f) };
            var fairway = new FairwayLayout(plain.Start, plain.End, plain.HalfWidth, plain.Wobble, plain.StripeWidth, plain.NoiseOffset, plain.StartCap, plain.EndCap, pieces, ovals);
            return new CourseLayout(
                layout.Islands, layout.Centreline, fairway, layout.Greens, layout.Bunkers, layout.Ponds, layout.Swells, layout.WaterLevel, layout.Relief,
                layout.Tee, layout.Hole, layout.FlatRadius, layout.FlatBlend, layout.Edge, layout.AlongCentreline, layout.Elevation, layout.Plan);
        }

        /// <summary>
        /// Whether a fork fits on a course's island as it is (Q4 A): a stretch where the branches open over 4 m, run beside an 8 m
        /// spine at full width, and close over 4 m, with the plan's clearances, and an island wide enough on both sides all along the
        /// spine, in whole centimetres. The branches' eased ends are narrower than the full width, so only the spine stretch is
        /// tested for width.
        /// </summary>
        private sealed class ForkFit
        {
            private readonly CourseLayout _layout;
            private readonly CourseFields _fields;
            private readonly List<float> _bends = new List<float>();
            private readonly float _bendClearance;
            private readonly float _softBendClearance;
            private readonly bool _dogleg;
            private readonly float _last;
            private readonly bool _guardOnLeft;
            private readonly int _spineHalfCm;
            private readonly int _marginCm;

            public ForkFit(CourseLayout layout, LevelGenSettings settings, int spineHalfCm = SpineHalfCm, int marginCm = EdgeMarginCm)
            {
                _spineHalfCm = spineHalfCm;
                _marginCm = marginCm;
                _layout = layout;
                _fields = new CourseFields(layout);
                _dogleg = layout.Plan.Dogleg;
                _bendClearance = settings.GapFromBend;
                _softBendClearance = settings.GapFromSoftBend;
                IReadOnlyList<Vector2> points = layout.Centreline.Points;
                float along = 0f;
                for (int i = 1; i < points.Count - 1; i++)
                {
                    along += Vector2.Distance(points[i - 1], points[i]);
                    _bends.Add(along);
                }

                _last = layout.Fairway.End - settings.FairwayCapDepth;

                // The guard's side seen along the approach: the safe branch takes the other side (plan §3).
                Vector2 approach = (points[points.Count - 1] - points[points.Count - 2]).normalized;
                Vector2 guard = layout.Plan.GuardDirection;
                _guardOnLeft = ((approach.x * guard.y) - (approach.y * guard.x)) > 0f;
            }

            public bool Fits(bool guardSides, bool excludeSoftBends)
            {
                float first = Mathf.Max(ClearOfTeeAndGaps, _layout.Fairway.Start + ClearOfStartTip);
                float length = Ease + SpineLength + Ease;
                for (float start = first; start + length <= _last; start += Step)
                {
                    if (Fits(start, start + length, guardSides, excludeSoftBends))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool Fits(float from, float to, bool guardSides, bool excludeSoftBends)
            {
                bool bendsCount = _dogleg || excludeSoftBends;
                float clearance = _dogleg ? _bendClearance : _softBendClearance;
                if (bendsCount)
                {
                    foreach (float bend in _bends)
                    {
                        if (bend > from - clearance && bend < to + clearance)
                        {
                            return false;
                        }
                    }
                }

                // On land well beyond both ends: clear of a gap's tips, and never on an island green's own island.
                for (float s = from - ClearOfTeeAndGaps; s <= to + ClearOfTeeAndGaps; s += Step)
                {
                    if (Geo.Cm(_fields.Island(Geo.At(_layout.Centreline.PointAt(s)))) >= 0)
                    {
                        return false;
                    }
                }

                int safeNeed = _spineHalfCm + SafeBranchCm + _marginCm;
                int riskyNeed = _spineHalfCm + RiskyBranchCm + _marginCm;
                int leftNeed = _guardOnLeft ? riskyNeed : safeNeed;
                int rightNeed = _guardOnLeft ? safeNeed : riskyNeed;
                bool asGuarded = true, swapped = true;
                for (float s = from + Ease; s <= to - Ease; s += Step)
                {
                    asGuarded &= Wide(s, leftNeed, rightNeed);
                    if (!guardSides)
                    {
                        swapped &= Wide(s, rightNeed, leftNeed);
                    }

                    if (!asGuarded && (guardSides || !swapped))
                    {
                        return false;
                    }
                }

                return asGuarded || (!guardSides && swapped);
            }

            private bool Wide(float along, int leftCm, int rightCm)
            {
                Vector2 centre = _layout.Centreline.PointAt(along);
                Vector2 direction = (_layout.Centreline.PointAt(along + 0.05f) - _layout.Centreline.PointAt(along - 0.05f)).normalized;
                Vector2 left = Geo.Left(direction);
                return Geo.Cm(_fields.Island(Geo.At(centre + (left * (leftCm * 0.01f))))) < 0
                    && Geo.Cm(_fields.Island(Geo.At(centre - (left * (rightCm * 0.01f))))) < 0;
            }
        }
    }
}
