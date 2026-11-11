using System.Collections.Generic;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.2 rule tests (split fairway plan §7): rough islands and splits. Each sweeps seeds 0-49 on every par and variants 0-3 (so
    /// enough courses carry a shape), and has an Explicit 0-199 twin. A shaped course is compared with its forced-plain twin: the same
    /// seed, par and variant generated with every shape weight on plain, which is the course the shape was cut into.
    /// </summary>
    public class FairwayShapeTests
    {
        private const int Variants = 4;

        /// <summary>A course that carries at least one shape, with its plain twin.</summary>
        internal sealed class Shaped
        {
            public int Par, Seed, Variant;
            public CourseLayout Layout, Plain;
            public string Name => $"par {Par} seed {Seed} variant {Variant}";
        }

        private static LevelGenSettings _plainSettings;

        /// <summary>A copy of the settings whose every par draws plain, and no second shape.</summary>
        internal static LevelGenSettings PlainSettings
        {
            get
            {
                if (_plainSettings == null)
                {
                    _plainSettings = Object.Instantiate(CourseGeneratorTests.Settings);
                    foreach (int par in new[] { 3, 4, 5 })
                    {
                        _plainSettings.FairwayShapes.SetWeights(par, FairwayShapeWeights.Create(100, 0, 0, 0), 0);
                    }
                }

                return _plainSettings;
            }
        }

        internal static List<Shaped> ShapedCourses(int seeds)
        {
            var list = new List<Shaped>();
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    for (int variant = 0; variant < Variants; variant++)
                    {
                        CourseLayout layout = CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings, variant, props: false);
                        if (layout.Plan.FairwayShapes.Count == 0)
                        {
                            continue;
                        }

                        list.Add(new Shaped
                        {
                            Par = par,
                            Seed = seed,
                            Variant = variant,
                            Layout = layout,
                            Plain = CourseGenerator.Generate(seed, par, PlainSettings, variant, props: false),
                        });
                    }
                }
            }

            return list;
        }

        // ---------------------------------------------------------------------------------------------------------------

        [Test, Timeout(900000)]
        public void RoughIslandsAndBandsNeverTouchACapsule() => NeverTouchACapsule(50);

        [Test, Explicit("Full sweep, seeds 0-199: run by hand when a PR changes these rules and the director asks"), Timeout(3600000)]
        public void RoughIslandsAndBandsNeverTouchACapsule_FullSweep() => NeverTouchACapsule(200);

        /// <summary>
        /// Read from the fields, not the placer's geometry: wherever the plain twin is fairway at a landing capsule's sample (every
        /// rest spot of the safe route) or a final-approach sample, the shaped course is fairway too. So no shape takes landing
        /// ground away from the safe route.
        /// </summary>
        private static void NeverTouchACapsule(int seeds)
        {
            var failures = new StringBuilder();
            List<Shaped> courses = ShapedCourses(seeds);
            Assert.That(courses.Count, Is.GreaterThan(seeds / 10), "Too few shaped courses to test.");
            foreach (Shaped c in courses)
            {
                CoursePlan plan = c.Layout.Plan;
                if (plan.SafeRoute.Count < 2)
                {
                    continue;
                }

                var shaped = new CourseFields(c.Layout);
                var plain = new CourseFields(c.Plain);
                CoursePath path = c.Layout.Centreline;
                Vector2 approach = (path.End - path.Points[path.Points.Count - 2]).normalized;
                LevelGenSettings s = CourseGeneratorTests.Settings;
                var world = new RouteWorld(shaped, path, s, plan.SafeTarget, approach, c.Par);

                var samples = new List<Vector2>();
                for (int i = 1; i < plan.SafeRoute.Count - 1; i++)
                {
                    samples.AddRange(world.CapsuleSamples(path.Project(plan.SafeRoute[i], out _)));
                }

                foreach (KeyValuePair<Vector2, bool> sample in world.FinalSamples())
                {
                    samples.Add(sample.Key);
                }

                foreach (Vector2 p in samples)
                {
                    Vector3 at = new Vector3(p.x, 0f, p.y);
                    if (plain.Fairway(at) < 0f && shaped.Fairway(at) >= 0f)
                    {
                        failures.AppendLine($"{c.Name}: landing sample ({p.x:F2}, {p.y:F2}) is rough because of a shape");
                        break;
                    }
                }
            }

            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------

        [Test, Timeout(900000)]
        public void ShapesKeepClearOfTeeGapsBendsAndEnds() => KeepClear(50);

        [Test, Explicit("Full sweep, seeds 0-199: run by hand when a PR changes these rules and the director asks"), Timeout(3600000)]
        public void ShapesKeepClearOfTeeGapsBendsAndEnds_FullSweep() => KeepClear(200);

        private static void KeepClear(int seeds)
        {
            LevelGenSettings s = CourseGeneratorTests.Settings;
            FairwayShapeRules r = s.FairwayShapes;
            var failures = new StringBuilder();
            foreach (Shaped c in ShapedCourses(seeds))
            {
                CourseLayout layout = c.Layout;
                CoursePlan plan = layout.Plan;
                List<Vector2> gaps = Gaps(layout);
                List<float> bends = Bends(layout.Centreline);
                float bendClearance = plan.Dogleg ? s.GapFromBend : s.GapFromSoftBend;
                FairwayLayout fairway = layout.Fairway;

                for (int i = 0; i < plan.FairwayShapes.Count; i++)
                {
                    FairwayShape shape = plan.FairwayShapes[i];
                    bool split = shape.Kind == FairwayShapeKind.Split;
                    float from = split ? shape.From - s.FairwayCapDepth : shape.From;
                    float to = split ? shape.To + s.FairwayCapDepth : shape.To;
                    string name = $"{c.Name} {shape.Kind} {from:F2}-{to:F2}";

                    if (Cm(from) < Cm(r.FromTee))
                    {
                        failures.AppendLine($"{name}: within {r.FromTee} m of the tee");
                    }

                    if (Cm(from) < Cm(fairway.Start + fairway.StartCap + r.FromFairwayTips) || Cm(to) > Cm(fairway.End - fairway.EndCap - r.FromFairwayTips))
                    {
                        failures.AppendLine($"{name}: within {r.FromFairwayTips} m of a fairway tip ({fairway.Start:F2}..{fairway.End:F2})");
                    }

                    float fromGap = split ? r.SplitFromGap : r.FromGap;
                    for (int g = 0; g < gaps.Count; g++)
                    {
                        bool before = Cm(to) <= Cm(gaps[g].x - fromGap);
                        bool after = Cm(from) >= Cm(gaps[g].y + fromGap);
                        if (!before && !after)
                        {
                            failures.AppendLine($"{name}: within {fromGap} m of the gap {gaps[g].x:F2}-{gaps[g].y:F2}");
                        }

                        if (plan.IslandGreen && g == gaps.Count - 1 && !before)
                        {
                            failures.AppendLine($"{name}: on the island green's island");
                        }
                    }

                    foreach (float bend in bends)
                    {
                        if (Cm(to) > Cm(bend - bendClearance) && Cm(from) < Cm(bend + bendClearance))
                        {
                            failures.AppendLine($"{name}: within {bendClearance} m of the bend at {bend:F2}");
                        }
                    }

                    for (int j = 0; j < i; j++)
                    {
                        FairwayShape other = plan.FairwayShapes[j];
                        if (Cm(from) < Cm(other.To) && Cm(to) > Cm(other.From))
                        {
                            failures.AppendLine($"{name}: overlaps another shape");
                        }
                    }
                }
            }

            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------

        [Test, Timeout(900000)]
        public void ShapedCoursesKeepASafeRoute() => KeepARoute(50);

        [Test, Explicit("Full sweep, seeds 0-199: run by hand when a PR changes these rules and the director asks"), Timeout(3600000)]
        public void ShapedCoursesKeepASafeRoute_FullSweep() => KeepARoute(200);

        /// <summary>Wherever the plain twin has a safe route, the shaped course has one too, with steps of at most the safe step and at most par shots.</summary>
        private static void KeepARoute(int seeds)
        {
            LevelGenSettings s = CourseGeneratorTests.Settings;
            var failures = new StringBuilder();
            foreach (Shaped c in ShapedCourses(seeds))
            {
                if (c.Plain.Plan.SafeRoute.Count < 2)
                {
                    continue;
                }

                IReadOnlyList<Vector2> route = c.Layout.Plan.SafeRoute;
                if (route.Count < 2)
                {
                    failures.AppendLine($"{c.Name}: no safe route");
                    continue;
                }

                if (route.Count - 1 > c.Par - 1 + s.SafeRouteExtraShots)
                {
                    failures.AppendLine($"{c.Name}: {route.Count - 1} shots");
                }

                for (int i = 1; i < route.Count; i++)
                {
                    if (Cm(Vector2.Distance(route[i - 1], route[i])) > Cm(s.SafeStep))
                    {
                        failures.AppendLine($"{c.Name}: a {Vector2.Distance(route[i - 1], route[i]):F2} m step");
                    }
                }
            }

            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------

        [Test, Timeout(900000)]
        public void ShapeStreamNeverMovesIslandsOrHazardCandidates() => NeverMoves(50);

        [Test, Explicit("Full sweep, seeds 0-199: run by hand when a PR changes these rules and the director asks"), Timeout(3600000)]
        public void ShapeStreamNeverMovesIslandsOrHazardCandidates_FullSweep() => NeverMoves(200);

        /// <summary>
        /// The shapes stream draws from neither other stream: a shaped course and its plain twin have the same islands, centreline,
        /// elevation, green, pin, tilt, fairway noise, hazard target and first hazard candidate, bit for bit. (Later candidates may
        /// differ: a shape can reject one, and a slot then draws another.)
        /// </summary>
        private static void NeverMoves(int seeds)
        {
            var failures = new StringBuilder();
            List<Shaped> courses = ShapedCourses(seeds);
            Assert.That(courses.Count, Is.GreaterThan(seeds / 10), "Too few shaped courses to test.");
            foreach (Shaped c in courses)
            {
                CourseLayout a = c.Layout, b = c.Plain;
                var why = new List<string>();
                if (a.Islands.Count != b.Islands.Count)
                {
                    why.Add("island count");
                }
                else
                {
                    for (int i = 0; i < a.Islands.Count; i++)
                    {
                        if (!a.Islands[i].Equals(b.Islands[i]))
                        {
                            why.Add($"island {i}");
                        }
                    }
                }

                for (int i = 0; i < a.Centreline.Points.Count; i++)
                {
                    if (a.Centreline.Points[i] != b.Centreline.Points[i])
                    {
                        why.Add("centreline");
                    }
                }

                var fa = new CourseFields(a);
                var fb = new CourseFields(b);
                for (float along = 0f; along <= a.Centreline.Length; along += 3f)
                {
                    Vector2 p = a.Centreline.PointAt(along);
                    if (fa.Elevation(p.x, p.y) != fb.Elevation(p.x, p.y))
                    {
                        why.Add($"elevation at {along}");
                        break;
                    }
                }

                if (!a.Greens[0].Outline.Equals(b.Greens[0].Outline) || a.Greens[0].Tilt != b.Greens[0].Tilt || a.Hole != b.Hole || a.Tee != b.Tee)
                {
                    why.Add("green, pin or tilt");
                }

                if (a.Fairway.NoiseOffset != b.Fairway.NoiseOffset)
                {
                    why.Add("fairway noise");
                }

                if (a.Plan.HazardTarget != b.Plan.HazardTarget || a.Plan.SafeTarget != b.Plan.SafeTarget || a.Plan.GuardDirection != b.Plan.GuardDirection)
                {
                    why.Add("hazard target or green plan");
                }

                EllipseShape? firstA = FirstCandidate(c.Seed, c.Par, c.Variant, CourseGeneratorTests.Settings);
                EllipseShape? firstB = FirstCandidate(c.Seed, c.Par, c.Variant, PlainSettings);
                if (!Equals(firstA, firstB))
                {
                    why.Add("first hazard candidate");
                }

                if (why.Count > 0)
                {
                    failures.AppendLine($"{c.Name}: {string.Join(", ", why)}");
                }
            }

            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        private static EllipseShape? FirstCandidate(int seed, int par, int variant, LevelGenSettings settings)
        {
            EllipseShape? first = null;
            HazardPlacer.CandidateObserver = shape => first ??= shape;
            try
            {
                CourseGenerator.Generate(seed, par, settings, variant, props: false);
            }
            finally
            {
                HazardPlacer.CandidateObserver = null;
            }

            return first;
        }

        // ---------------------------------------------------------------------------------------------------------------

        [Test, Timeout(900000)]
        public void ABallOnARoughIslandIsOnRough() => OnRough(50);

        [Test, Explicit("Full sweep, seeds 0-199: run by hand when a PR changes these rules and the director asks"), Timeout(3600000)]
        public void ABallOnARoughIslandIsOnRough_FullSweep() => OnRough(200);

        /// <summary>
        /// The ball's ground: rough at a rough island's centre and in a band's middle, and fairway across the 2 m beside every point
        /// of a rough island's outline (the "2 m of fairway either side" rule, measured on the drawn field).
        /// </summary>
        private static void OnRough(int seeds)
        {
            var failures = new StringBuilder();
            int islands = 0, bands = 0;
            foreach (Shaped c in ShapedCourses(seeds))
            {
                var ground = new BallGround(c.Layout);
                CoursePath path = c.Layout.Centreline;
                foreach (FairwayShape shape in c.Layout.Plan.FairwayShapes)
                {
                    if (shape.Kind == FairwayShapeKind.Split)
                    {
                        bands++;
                        Vector2 middle = path.PointAt((shape.From + shape.To) * 0.5f);
                        if (ground.Surface(middle.x, middle.y) != CourseSurface.Rough)
                        {
                            failures.AppendLine($"{c.Name}: the band's middle is {ground.Surface(middle.x, middle.y)}");
                        }

                        continue;
                    }

                    islands++;
                    EllipseShape oval = OvalOf(c.Layout, shape);
                    if (ground.Surface(oval.Center.x, oval.Center.y) != CourseSurface.Rough)
                    {
                        failures.AppendLine($"{c.Name}: the rough island's centre is {ground.Surface(oval.Center.x, oval.Center.y)}");
                    }

                    HazardOutline outline = HazardOutline.Of(oval, false, HazardRole.Flank);
                    foreach (Vector2 ring in outline.Ring)
                    {
                        float along = path.Project(ring, out float side);
                        Vector2 left = Geo.Left(Geo.PathDirection(path, along));
                        foreach (float sign in Mathf.Abs(side) < 0.05f ? new[] { -1f, 1f } : new[] { Mathf.Sign(side) })
                        {
                            for (float d = 0.05f; d <= 1.99f; d += 0.1f)
                            {
                                Vector2 p = ring + (left * (sign * d));
                                if (outline.Contains(p))
                                {
                                    continue;
                                }

                                if (ground.Surface(p.x, p.y) != CourseSurface.Fairway)
                                {
                                    failures.AppendLine($"{c.Name}: {d:F2} m beside the rough island is {ground.Surface(p.x, p.y)}");
                                    goto nextShape;
                                }
                            }
                        }
                    }

                    nextShape:;
                }
            }

            Assert.That(islands, Is.GreaterThan(0), "No rough island to test.");
            Assert.That(bands, Is.GreaterThan(0), "No split to test.");
            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Helpers

        private static int Cm(float metres) => Mathf.RoundToInt(metres * 100f);

        /// <summary>The rough oval behind a rough island shape: the one whose centre lies within its stretch.</summary>
        internal static EllipseShape OvalOf(CourseLayout layout, FairwayShape shape)
        {
            foreach (EllipseShape oval in layout.Fairway.RoughOvals)
            {
                float along = layout.Centreline.Project(oval.Center, out _);
                if (along >= shape.From && along <= shape.To)
                {
                    return oval;
                }
            }

            Assert.Fail($"No oval for the rough island at {shape.From:F2}-{shape.To:F2}");
            return default;
        }

        /// <summary>The stretches of the centreline off every island, as the route world finds them.</summary>
        internal static List<Vector2> Gaps(CourseLayout layout)
        {
            var fields = new CourseFields(layout);
            var gaps = new List<Vector2>();
            float start = -1f;
            for (float s = 0f; s <= layout.Centreline.Length; s += 0.05f)
            {
                Vector2 p = layout.Centreline.PointAt(s);
                bool off = fields.Island(new Vector3(p.x, 0f, p.y)) >= 0f;
                if (off && start < 0f)
                {
                    start = s;
                }
                else if (!off && start >= 0f)
                {
                    gaps.Add(new Vector2(start, s));
                    start = -1f;
                }
            }

            return gaps;
        }

        /// <summary>Where the centreline bends, in metres from the tee.</summary>
        internal static List<float> Bends(CoursePath path)
        {
            var bends = new List<float>();
            float along = 0f;
            for (int i = 1; i < path.Points.Count - 1; i++)
            {
                along += Vector2.Distance(path.Points[i - 1], path.Points[i]);
                bends.Add(along);
            }

            return bends;
        }
    }
}
