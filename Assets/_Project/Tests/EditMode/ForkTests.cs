using System.Collections.Generic;
using System.Text;
using IsoGolf.Generation;
using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.3 rule tests (split fairway plan §7): forks, their island bulge and the camera's scale. Forks are rare on the shipped weights,
    /// so the rules are swept on a copy of the settings where every par 4 and 5 draws a fork (<see cref="ForkSettings"/>), seeds 0-49
    /// and variants 0-1, each with an Explicit 0-199 twin. A fork that finds no place leaves its course plain, which these skip.
    /// </summary>
    public class ForkTests
    {
        private const int Variants = 2;

        internal sealed class Forked
        {
            public int Par, Seed, Variant;
            public CourseLayout Layout;
            public FairwayShapeResult Shapes;
            public FairwayShape Shape;
            public string Name => $"par {Par} seed {Seed} variant {Variant}";
        }

        private static LevelGenSettings _forkSettings;

        /// <summary>A copy of the shipped settings whose every par 4 and 5 draws a fork first, and no second shape.</summary>
        internal static LevelGenSettings ForkSettings
        {
            get
            {
                if (_forkSettings == null)
                {
                    _forkSettings = Object.Instantiate(CourseGeneratorTests.Settings);
                    foreach (int par in new[] { 4, 5 })
                    {
                        _forkSettings.FairwayShapes.SetWeights(par, FairwayShapeWeights.Create(0, 0, 0, 100), 0);
                    }
                }

                return _forkSettings;
            }
        }

        private static readonly Dictionary<int, List<Forked>> Cache = new Dictionary<int, List<Forked>>();

        internal static List<Forked> ForkCourses(int seeds)
        {
            if (Cache.TryGetValue(seeds, out List<Forked> cached))
            {
                return cached;
            }

            var list = new List<Forked>();
            foreach (int par in new[] { 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    for (int variant = 0; variant < Variants; variant++)
                    {
                        CourseLayout layout = CourseGenerator.GenerateBare(seed, par, ForkSettings, variant, out FairwayShapeResult shapes);
                        if (shapes.Spine == null)
                        {
                            continue;
                        }

                        FairwayShape fork = default;
                        foreach (FairwayShape shape in layout.Plan.FairwayShapes)
                        {
                            fork = shape.Kind == FairwayShapeKind.Fork ? shape : fork;
                        }

                        list.Add(new Forked { Par = par, Seed = seed, Variant = variant, Layout = layout, Shapes = shapes, Shape = fork });
                    }
                }
            }

            Debug.Log($"FORK_TESTS {list.Count} fork courses over par 4 and 5, seeds 0-{seeds - 1}, variants 0-{Variants - 1}");
            Cache[seeds] = list;
            return list;
        }

        private static FairwayShapeRules Rules => CourseGeneratorTests.Settings.FairwayShapes;

        /// <summary>The fork's spine on the course as built (its own fields), so the tests read what is drawn.</summary>
        private static GroundKeepOut Spine(Forked f) =>
            GroundKeepOut.OfSpine(new CourseFields(f.Layout), f.Shapes.SafeBranch, f.Shapes.RiskyBranch, f.Shape.From, f.Shape.To);

        private static void Check(int seeds, System.Action<Forked, StringBuilder> rule)
        {
            List<Forked> forks = ForkCourses(seeds);
            Assert.That(forks.Count, Is.GreaterThan(0), "No fork was placed: nothing to test.");
            var failures = new StringBuilder();
            foreach (Forked f in forks)
            {
                rule(f, failures);
            }

            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        // ---------------------------------------------------------------------------------------------------------------------------

        [Test, Timeout(900000)]
        public void ForkBranchesKeepTheirWidths() => Check(50, BranchWidths);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void ForkBranchesKeepTheirWidths_FullSweep() => Check(200, BranchWidths);

        /// <summary>
        /// Along the spine's held stretch, measured on the drawn fairway field every 25 cm: each branch is at least its range's lowest
        /// width, and the spine (rough between them) at least its lowest width (whole cm).
        /// </summary>
        private static void BranchWidths(Forked f, StringBuilder failures)
        {
            var fields = new CourseFields(f.Layout);
            CoursePath path = f.Layout.Centreline;
            float ease = Rules.ForkEase;
            for (float s = f.Shape.From + ease; s <= f.Shape.To - ease + 0.001f; s += 0.25f)
            {
                Vector2 centre = path.PointAt(s);
                Vector2 left = Geo.Left(Geo.PathDirection(path, s));
                float safe = f.Shapes.SafeBranch.OffsetAt(s), risky = f.Shapes.RiskyBranch.OffsetAt(s);
                float Field(float across) => fields.Fairway(Geo.At(centre + (left * across)));

                float safeIn = Edge(Field, safe, -Mathf.Sign(safe)), safeOut = Edge(Field, safe, Mathf.Sign(safe));
                float riskyIn = Edge(Field, risky, -Mathf.Sign(risky)), riskyOut = Edge(Field, risky, Mathf.Sign(risky));
                int safeWidth = Geo.Cm(Mathf.Abs(safeOut - safeIn)), riskyWidth = Geo.Cm(Mathf.Abs(riskyOut - riskyIn)), spine = Geo.Cm(Mathf.Abs(safeIn - riskyIn));
                if (safeWidth < Geo.Cm(Rules.SafeBranchWidth.x) || riskyWidth < Geo.Cm(Rules.RiskyBranchWidth.x) || spine < Geo.Cm(Rules.SpineWidth.x))
                {
                    failures.AppendLine($"{f.Name} at {s:F2} m: safe {safeWidth} cm, risky {riskyWidth} cm, spine {spine} cm");
                    return;
                }
            }
        }

        /// <summary>Where the fairway field crosses zero going from a branch's centre in one direction (bisection, to a millimetre).</summary>
        private static float Edge(System.Func<float, float> field, float from, float direction)
        {
            float inside = 0f, outside = 0.05f;
            while (field(from + (direction * outside)) < 0f && outside < 6f)
            {
                inside = outside;
                outside += 0.05f;
            }

            for (int i = 0; i < 12; i++)
            {
                float m = (inside + outside) * 0.5f;
                if (field(from + (direction * m)) < 0f)
                {
                    inside = m;
                }
                else
                {
                    outside = m;
                }
            }

            return from + (direction * inside);
        }

        [Test, Timeout(900000)]
        public void ForksRejoinBeforeTheEndTip() => Check(50, Rejoin);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void ForksRejoinBeforeTheEndTip_FullSweep() => Check(200, Rejoin);

        /// <summary>
        /// The fork ends at least <see cref="FairwayShapeRules.ForkBeforeEndTip"/> before the fairway's end tip starts, and from its end
        /// to that tip the centreline is fairway again (one strip): the end tip, the run into the green and the apron are untouched.
        /// </summary>
        private static void Rejoin(Forked f, StringBuilder failures)
        {
            FairwayLayout fairway = f.Layout.Fairway;
            float endTip = fairway.End - fairway.EndCap;
            if (Geo.Cm(f.Shape.To) > Geo.Cm(endTip - Rules.ForkBeforeEndTip))
            {
                failures.AppendLine($"{f.Name}: the fork ends at {f.Shape.To:F2} m, the end tip starts at {endTip:F2} m");
                return;
            }

            var fields = new CourseFields(f.Layout);
            for (float s = f.Shape.To; s <= endTip; s += 0.25f)
            {
                if (Geo.Cm(fields.Fairway(Geo.At(f.Layout.Centreline.PointAt(s)))) >= 0)
                {
                    failures.AppendLine($"{f.Name}: rough on the centreline at {s:F2} m, after the fork rejoined");
                    return;
                }
            }
        }

        [Test, Timeout(900000)]
        public void NoShotLineCrossesASpine() => Check(50, LinesOffSpine);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void NoShotLineCrossesASpine_FullSweep() => Check(200, LinesOffSpine);

        private static void LinesOffSpine(Forked f, StringBuilder failures)
        {
            GroundKeepOut spine = Spine(f);
            foreach (ShotLine line in f.Layout.Plan.Lines)
            {
                if (spine.Crosses(line.From, line.To, f.Layout.Centreline))
                {
                    failures.AppendLine($"{f.Name}: a {line.Kind} line from {line.From} to {line.To} crosses the spine");
                }
            }
        }

        [Test, Timeout(900000)]
        public void SafeRouteRestsOnTheSafeBranch() => Check(50, RestsOnSafeBranch);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void SafeRouteRestsOnTheSafeBranch_FullSweep() => Check(200, RestsOnSafeBranch);

        /// <summary>
        /// Every rest spot along the fork lies on the safe branch's side, on fairway, with its capsule off the spine; and no step
        /// jumps over the fork from before it to after it (it would cross the spine).
        /// </summary>
        private static void RestsOnSafeBranch(Forked f, StringBuilder failures)
        {
            CoursePath path = f.Layout.Centreline;
            var fields = new CourseFields(f.Layout);
            GroundKeepOut spine = Spine(f);
            IReadOnlyList<Vector2> route = f.Layout.Plan.SafeRoute;
            float sign = Mathf.Sign(f.Shape.SafeOffset);
            for (int i = 1; i < route.Count; i++)
            {
                float a = path.Project(route[i - 1], out _);
                float b = path.Project(route[i], out float side);
                if (Geo.Cm(a) < Geo.Cm(f.Shape.From) && Geo.Cm(b) > Geo.Cm(f.Shape.To))
                {
                    failures.AppendLine($"{f.Name}: the safe route steps over the fork from {a:F1} m to {b:F1} m");
                }

                bool rest = i < route.Count - 1;
                float ease = Rules.ForkEase;
                if (!rest || b <= f.Shape.From + ease || b >= f.Shape.To - ease)
                {
                    continue;
                }

                if (Mathf.Sign(side) != sign || fields.Surface(Geo.At(route[i])) != CourseSurface.Fairway || spine.Blocks(route[i], path))
                {
                    failures.AppendLine($"{f.Name}: rest spot {i} at {b:F1} m sits {side:F2} m across ({fields.Surface(Geo.At(route[i]))}), not on the safe branch");
                }
            }
        }

        [Test, Timeout(900000)]
        public void BranchGuardIsBesideTheRiskyLine() => Check(50, GuardBesideRisky);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void BranchGuardIsBesideTheRiskyLine_FullSweep() => Check(200, GuardBesideRisky);

        /// <summary>
        /// A fork's plan has a branch guard or records it missing (a reroll); a placed one lies on the risky side, the risky branch's
        /// centre line passes within the guard line reach of it, and it is the only role replacing the drive zone (no drive zone).
        /// </summary>
        private static void GuardBesideRisky(Forked f, StringBuilder failures)
        {
            CoursePlan plan = f.Layout.Plan;
            int guards = 0;
            foreach (HazardInfo info in plan.Hazards)
            {
                if (info.Role == HazardRole.DriveZone)
                {
                    failures.AppendLine($"{f.Name}: a drive zone on a fork course");
                }

                if (info.Role != HazardRole.BranchGuard)
                {
                    continue;
                }

                guards++;
                EllipseShape shape = info.IsWater ? f.Layout.Ponds[info.Index].Outline : f.Layout.Bunkers[info.Index];
                HazardOutline outline = HazardOutline.Of(shape, info.IsWater, info.Role);
                CoursePath path = f.Layout.Centreline;
                path.Project(shape.Center, out float side);
                float best = float.MaxValue;
                FairwayPiece risky = f.Shapes.RiskyBranch;
                for (float s = risky.From; s <= risky.To + 0.001f; s += 0.25f)
                {
                    best = Mathf.Min(best, outline.SignedDistance(path.PointAt(s) + (Geo.Left(Geo.PathDirection(path, s)) * risky.OffsetAt(s)), 3f));
                }

                if (Mathf.Sign(side) != Mathf.Sign(risky.Offset) || Geo.Cm(best) > Geo.Cm(CourseGeneratorTests.Settings.GuardLineReach))
                {
                    failures.AppendLine($"{f.Name}: branch guard {side:F2} m across, {best:F2} m from the risky branch's centre line");
                }
            }

            bool missing = System.Linq.Enumerable.Contains(plan.MissingRoles, HazardRole.BranchGuard);
            if (guards != (missing ? 0 : 1))
            {
                failures.AppendLine($"{f.Name}: {guards} branch guards, missing recorded {missing}");
            }
        }

        [Test, Timeout(900000)]
        public void NoHazardOnASpineOrOval() => Check(50, NoHazardOnShape);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void NoHazardOnASpineOrOval_FullSweep() => Check(200, NoHazardOnShape);

        /// <summary>No spine sample (25 cm grid) and no point of a rough oval (20 cm grid) is sand or water.</summary>
        private static void NoHazardOnShape(Forked f, StringBuilder failures)
        {
            var fields = new CourseFields(f.Layout);
            bool Hazard(Vector2 p) => Geo.Cm(fields.Bunker(Geo.At(p))) < 0 || Geo.Cm(fields.Pond(Geo.At(p))) < 0;
            foreach (Vector2 p in Spine(f).SpineSamples(f.Layout.Centreline))
            {
                if (Hazard(p))
                {
                    failures.AppendLine($"{f.Name}: a hazard on the spine at {p}");
                    return;
                }
            }

            foreach (EllipseShape oval in f.Layout.Fairway.RoughOvals)
            {
                float r = Mathf.Max(oval.Radius.x, oval.Radius.y) * 1.3f;
                for (float x = -r; x <= r; x += 0.2f)
                {
                    for (float z = -r; z <= r; z += 0.2f)
                    {
                        Vector2 p = oval.Center + new Vector2(x, z);
                        if (CourseFields.Ellipse(Geo.At(p), oval) < 0f && Hazard(p))
                        {
                            failures.AppendLine($"{f.Name}: a hazard on a rough oval at {p}");
                            return;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------------
        // The bulge (director: "A, renflement")

        [Test, Timeout(900000)]
        public void BulgeOnlyOnForkCourses_KeepsTheEdgeMargin() => BulgeRule(50);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void BulgeOnlyOnForkCourses_KeepsTheEdgeMargin_FullSweep() => BulgeRule(200);

        /// <summary>
        /// On the shipped settings, every par: a course without a fork has no bulged island. On fork courses: each branch's outer edge
        /// keeps <see cref="FairwayShapeRules.ForkEdgeMargin"/> inside the island at every 25 cm of the fork, and the bulge is at most
        /// <see cref="FairwayShapeRules.MaxBulge"/> on either side.
        /// </summary>
        private static void BulgeRule(int seeds)
        {
            var failures = new StringBuilder();
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.GenerateBare(seed, par, CourseGeneratorTests.Settings, 0, out FairwayShapeResult shapes);
                    if (shapes.Spine != null)
                    {
                        continue;
                    }

                    foreach (IslandOutline island in layout.Islands)
                    {
                        if (island.HasBulge)
                        {
                            failures.AppendLine($"par {par} seed {seed}: a bulged island without a fork");
                        }
                    }
                }
            }

            foreach (Forked f in ForkCourses(seeds))
            {
                if (Geo.Cm(f.Shapes.BulgeSafe) > Geo.Cm(Rules.MaxBulge) || Geo.Cm(f.Shapes.BulgeRisky) > Geo.Cm(Rules.MaxBulge))
                {
                    failures.AppendLine($"{f.Name}: bulge {f.Shapes.BulgeSafe:F2} / {f.Shapes.BulgeRisky:F2} m over the maximum");
                }

                var fields = new CourseFields(f.Layout);
                CoursePath path = f.Layout.Centreline;
                FairwayLayout fairway = f.Layout.Fairway;
                for (float s = f.Shape.From; s <= f.Shape.To + 0.001f; s += 0.25f)
                {
                    Vector2 centre = path.PointAt(s);
                    Vector2 left = Geo.Left(Geo.PathDirection(path, s));
                    foreach (FairwayPiece branch in new[] { f.Shapes.SafeBranch, f.Shapes.RiskyBranch })
                    {
                        float sign = Mathf.Sign(branch.Offset);
                        float outer = Edge(across => fields.Fairway(Geo.At(centre + (left * across))), branch.OffsetAt(s), sign);
                        if (Geo.Cm(fields.Island(Geo.At(centre + (left * outer)))) > -Geo.Cm(Rules.ForkEdgeMargin))
                        {
                            failures.AppendLine($"{f.Name} at {s:F2} m: a branch's outer edge within {Rules.ForkEdgeMargin} m of the island edge");
                            break;
                        }
                    }
                }
            }

            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        /// <summary>The same course with every island's bulge left out: the outline its fork widened.</summary>
        internal static CourseLayout WithoutBulge(CourseLayout l)
        {
            var islands = new List<IslandOutline>();
            foreach (IslandOutline i in l.Islands)
            {
                islands.Add(new IslandOutline(i.HalfLength, i.HalfWidth, i.WidthWobble, i.WidthNoiseScale, i.CenterX, i.CenterZ, i.Heading, i.NoiseOffset));
            }

            return new CourseLayout(islands, l.Centreline, l.Fairway, l.Greens, l.Bunkers, l.Ponds, l.Swells, l.WaterLevel, l.Relief, l.Tee, l.Hole,
                l.FlatRadius, l.FlatBlend, l.Edge, l.AlongCentreline, l.Elevation, l.Plan);
        }

        [Test, Timeout(900000)]
        public void CameraScaleIgnoresTheBulge() => CameraScale(50);

        [Test, Explicit("Full sweep, seeds 0-199"), Timeout(3600000)]
        public void CameraScaleIgnoresTheBulge_FullSweep() => CameraScale(200);

        /// <summary>
        /// The zoomed-out camera of a fork course, framed as `Main` frames it (<see cref="CourseFields.FramingOutline"/>), sits where it
        /// sits on the same course without the bulge, within <see cref="ScaleTolerance"/>: a bulge never changes the scale every par
        /// shares. What is left is the terrain's own cut: the framed outline points are the bulged outline's mesh vertices moved back,
        /// not the unbulged mesh's vertices, so the two differ by the chords' few centimetres (about 0.3% of the camera's height).
        /// </summary>
        private const float ScaleTolerance = 0.15f;

        private static void CameraScale(int seeds)
        {
            var failures = new StringBuilder();
            float worst = 0f, worstRaw = 0f, worstShare = 0f;
            int bulged = 0;
            foreach (Forked f in ForkCourses(seeds))
            {
                if (f.Shapes.BulgeSafe <= 0f && f.Shapes.BulgeRisky <= 0f)
                {
                    continue;
                }

                bulged++;
                var withBulge = new CameraCourse(f.Layout, 1280f / 2800f);
                var without = new CameraCourse(WithoutBulge(f.Layout), 1280f / 2800f);
                Vector3 raw = CameraFraming.FitCourse(withBulge.Outline, withBulge.Centreline, withBulge.View, withBulge.Rig.ScreenFill, withBulge.Rig.FramedLength);
                float delta = Vector3.Distance(withBulge.Home, without.Home);
                worst = Mathf.Max(worst, delta);
                worstRaw = Mathf.Max(worstRaw, Vector3.Distance(raw, without.Home));
                worstShare = Mathf.Max(worstShare, Mathf.Abs(withBulge.Home.y - without.Home.y) / without.Home.y);
                if (delta > ScaleTolerance)
                {
                    failures.AppendLine($"{f.Name}: the zoomed-out camera moved {delta * 100f:F1} cm (height {withBulge.Home.y:F2} against {without.Home.y:F2} m)");
                }
            }

            Debug.Log($"CAMERA_SCALE {bulged} bulged fork courses: the camera moves at most {worst * 100f:F2} cm framed without the bulge (height {worstShare * 100f:F2}% at most), {worstRaw * 100f:F1} cm if it framed the bulge");
            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }

        [Test, Timeout(1800000)]
        public void ForkCoursesKeepTheirShadowReach() => ShadowReach(12);

        /// <summary>
        /// On 9:21, the worst framed stretch of a fork course stays under the generated par 4 and 5 ceilings of
        /// <see cref="GeneratedShadowTests"/> (75 and 85 m: past the 56.8 m fade start by design, director in M3). The growth against
        /// the same course without its bulge is logged for the PR.
        /// </summary>
        private static void ShadowReach(int seeds)
        {
            var failures = new StringBuilder();
            float growth = 0f;
            int count = 0;
            foreach (Forked f in ForkCourses(seeds))
            {
                var course = new CameraCourse(f.Layout, 9f / 21f);
                float far = ShadowDistanceTests.WorstFramedStretch(course, out _);
                float bare = ShadowDistanceTests.WorstFramedStretch(new CameraCourse(WithoutBulge(f.Layout), 9f / 21f), out _);
                growth = Mathf.Max(growth, far - bare);
                count++;
                float ceiling = f.Par == 4 ? 75f : 85f;
                if (far >= ceiling)
                {
                    failures.AppendLine($"{f.Name}: worst framed stretch {far:F2} m, ceiling {ceiling} m");
                }
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Rendering/IsoGolf_RPAsset.asset");
            Debug.Log($"FORK_SHADOW {count} fork courses at 9:21: the bulge adds at most {growth:F2} m to the worst framed stretch (fade starts at {pipeline.shadowDistance * (1f - pipeline.cascadeBorder):F1} m)");
            Assert.That(failures.Length, Is.Zero, failures.ToString());
        }
    }
}
