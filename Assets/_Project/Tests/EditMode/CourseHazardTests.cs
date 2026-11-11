using System.Collections.Generic;
using System.Linq;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The green, the pin, the hazards and the safe route of generated courses (M3 PR 6b): what the generator guarantees by
    /// construction, over seeds 0 to 49 of each par (the default suite; the Explicit twins run 0 to 199). Courses are generated once and shared by the tests.
    /// </summary>
    public class CourseHazardTests
    {
        private const int Seeds = 50;
        // Decisions compare whole centimetres, so a rule can be missed by half a centimetre in the raw floats.
        private const float Mm = 0.006f;

        private static readonly Dictionary<(int, int), CourseLayout> Cache = new Dictionary<(int, int), CourseLayout>();

        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        public static CourseLayout Course(int par, int seed)
        {
            if (!Cache.TryGetValue((par, seed), out CourseLayout layout))
            {
                layout = CourseGenerator.Generate(seed, par, Settings);
                Cache[(par, seed)] = layout;
            }

            return layout;
        }

        /// <summary>Every hazard of a course with its measured outline, bunkers and ponds in plan order.</summary>
        internal static List<HazardOutline> Outlines(CourseLayout layout)
        {
            var outlines = new List<HazardOutline>();
            foreach (HazardInfo info in layout.Plan.Hazards)
            {
                HazardOutline h = info.IsWater
                    ? HazardOutline.Of(layout.Ponds[info.Index].Outline, true, info.Role)
                    : HazardOutline.Of(layout.Bunkers[info.Index], false, info.Role);
                if (info.IsWater)
                {
                    h.WaterLevel = layout.Ponds[info.Index].WaterLevel;
                }

                outlines.Add(h);
            }

            return outlines;
        }

        private static RouteWorld World(CourseLayout layout, int par)
        {
            IReadOnlyList<Vector2> points = layout.Centreline.Points;
            Vector2 approach = (points[points.Count - 1] - points[points.Count - 2]).normalized;
            return new RouteWorld(new CourseFields(layout), layout.Centreline, Settings, layout.Plan.SafeTarget, approach, par - 1 + Settings.SafeRouteExtraShots);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void SameSeed_GivesTheSameHazards_AndOtherVariantsDiffer(int par)
        {
            for (int seed = 0; seed < 40; seed++)
            {
                string first = CourseGeneratorTests.Describe(CourseGenerator.Generate(seed, par, Settings));
                Assert.That(CourseGeneratorTests.Describe(CourseGenerator.Generate(seed, par, Settings)), Is.EqualTo(first), $"seed {seed}");
            }

            string course = CourseGeneratorTests.Describe(CourseGenerator.Generate(1, par, Settings));
            Assert.That(CourseGeneratorTests.Describe(CourseGenerator.Generate(1, par, Settings, 1)), Is.Not.EqualTo(course));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void HazardsLeaveEveryOtherStep_OfTheSeedAsItWas(int par)
        {
            // Hazards off: no count, no chances. Islands, centreline, fairway, relief, elevation and the green must not move.
            LevelGenSettings bare = Object.Instantiate(Settings);
            try
            {
                var so = new SerializedObject(bare);
                foreach (string rules in new[] { "par3", "par4", "par5" })
                {
                    SerializedProperty p = so.FindProperty(rules);
                    p.FindPropertyRelative("hazardCount").vector2IntValue = Vector2Int.zero;
                    p.FindPropertyRelative("secondGuardChance").intValue = 0;
                    p.FindPropertyRelative("driveZoneChance").intValue = 0;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                for (int seed = 0; seed < 60; seed++)
                {
                    CourseLayout with = CourseGenerator.Generate(seed, par, Settings);
                    CourseLayout without = CourseGenerator.Generate(seed, par, bare);
                    Assert.That(without.Bunkers.Count + without.Ponds.Count, Is.Zero, $"seed {seed}");
                    Assert.That(Outline(without), Is.EqualTo(Outline(with)), $"seed {seed}");
                    var a = new CourseFields(with);
                    var b = new CourseFields(without);
                    for (float x = -20f; x <= 20f; x += 5f)
                    {
                        for (float z = -4f; z <= 4f; z += 2f)
                        {
                            Assert.That(b.Elevation(x, z), Is.EqualTo(a.Elevation(x, z)), $"seed {seed} at {x},{z}");
                            Assert.That(b.BroadHeight(x, z), Is.EqualTo(a.BroadHeight(x, z)), $"seed {seed} at {x},{z}");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        // The layout without its surfaces' hazards, written out exactly.
        private static string Outline(CourseLayout layout)
        {
            var text = new System.Text.StringBuilder();
            foreach (IslandOutline i in layout.Islands)
            {
                text.Append(i.CenterX).Append(' ').Append(i.CenterZ).Append(' ').Append(i.HalfLength).Append(' ').Append(i.HalfWidth).Append(' ').Append(i.NoiseOffset).Append(';');
            }

            foreach (Vector2 p in layout.Centreline.Points)
            {
                text.Append(p.x).Append(',').Append(p.y).Append(';');
            }

            text.Append(layout.Fairway.NoiseOffset).Append(';').Append(layout.Relief.NoiseOffset).Append(';').Append(layout.Greens[0].Outline.Center).Append(layout.Greens[0].Tilt);
            return text.ToString();
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void HazardCount_IsInRange_OrARoleIsMissing(int par)
        {
            HazardCount_IsInRange_OrARoleIsMissing_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void HazardCount_IsInRange_OrARoleIsMissing_FullSweep(int par)
        {
            HazardCount_IsInRange_OrARoleIsMissing_Run(par, 200);
        }

        private void HazardCount_IsInRange_OrARoleIsMissing_Run(int par, int seeds)
        {
            Vector2Int range = Settings.Rules(par).HazardCount;
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                int gap = Settings.GapCountsAsHazard && CourseGeneratorTests.Gaps(layout).Count > 0 ? 1 : 0;
                int count = layout.Plan.Hazards.Count + gap;
                Assert.That(count, Is.LessThanOrEqualTo(range.y), $"seed {seed}");
                Assert.That(count >= range.x || layout.Plan.MissingRoles.Count > 0, Is.True, $"seed {seed}: {count} hazards, none missing");
                Assert.That(layout.Plan.Hazards.Count, Is.EqualTo(layout.Bunkers.Count + layout.Ponds.Count));
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void MostCourses_AreComplete_WithARouteAndEveryRole(int par)
        {
            MostCourses_AreComplete_WithARouteAndEveryRole_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void MostCourses_AreComplete_WithARouteAndEveryRole_FullSweep(int par)
        {
            MostCourses_AreComplete_WithARouteAndEveryRole_Run(par, 200);
        }

        private void MostCourses_AreComplete_WithARouteAndEveryRole_Run(int par, int seeds)
        {
            int complete = 0, noRoute = 0, water = 0, hazards = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                CoursePlan plan = Course(par, seed).Plan;
                complete += plan.Complete ? 1 : 0;
                noRoute += plan.SafeRoute.Count < 2 ? 1 : 0;
                water += Course(par, seed).Ponds.Count;
                hazards += plan.Hazards.Count;
            }

            Debug.Log($"Par {par}: {complete}/{seeds} complete ({noRoute} with no route on the bare course), {hazards / (float)seeds:0.00} hazards and {water / (float)seeds:0.00} ponds per course.");
            Assert.That(complete, Is.GreaterThanOrEqualTo(seeds * 85 / 100));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void EveryHazard_KeepsItsClearances(int par)
        {
            EveryHazard_KeepsItsClearances_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void EveryHazard_KeepsItsClearances_FullSweep(int par)
        {
            EveryHazard_KeepsItsClearances_Run(par, 200);
        }

        private void EveryHazard_KeepsItsClearances_Run(int par, int seeds)
        {
            LevelGenSettings s = Settings;
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                var fields = new CourseFields(layout);
                List<HazardOutline> hazards = Outlines(layout);
                HazardOutline green = HazardOutline.Of(layout.Greens[0].Outline, false, HazardRole.GreenGuard);
                float flat = layout.FlatRadius + layout.FlatBlend;
                List<Vector2> gaps = CourseGeneratorTests.Gaps(layout);

                for (int i = 0; i < hazards.Count; i++)
                {
                    HazardOutline h = hazards[i];
                    string name = $"seed {seed} {h.Role}{(h.IsWater ? " pond" : " bunker")}";
                    Assert.That(h.SignedDistance(layout.Tee), Is.GreaterThanOrEqualTo(Mathf.Max(s.HazardFromTee, flat) - Mm), name + ": tee");
                    Assert.That(h.SignedDistance(layout.Hole), Is.GreaterThanOrEqualTo(Mathf.Max(s.HazardFromHole, flat) - Mm), name + ": hole");
                    Assert.That(h.Gap(green), Is.GreaterThanOrEqualTo((h.IsWater ? s.PondFromGreen : s.BunkerFromGreen) - Mm), name + ": green");
                    for (int j = 0; j < i; j++)
                    {
                        Assert.That(h.Gap(hazards[j]), Is.GreaterThanOrEqualTo(s.HazardSpacing - Mm), name + ": spacing");
                    }

                    Assert.That(fields.Island(Geo.At(h.Centre)), Is.LessThan(0f), name + ": centre on land");
                    if (!h.IsWater)
                    {
                        foreach (Vector2 p in h.Ring)
                        {
                            Assert.That(fields.Island(Geo.At(p)), Is.LessThanOrEqualTo(-s.BunkerFromEdge + Mm), name + ": edge");
                        }
                    }
                    else
                    {
                        int inland = h.Ring.Count(p => fields.Island(Geo.At(p)) <= -s.PondEdgeSliver + Mm);
                        Assert.That(inland, Is.GreaterThanOrEqualTo(HazardOutline.RingPoints / 3), name + ": a third of the rim on land");
                    }

                    foreach (Vector2 gap in gaps)
                    {
                        // The tips of the islands either side, and the corridor just past the far one.
                        if (h.IsWater)
                        {
                            Assert.That(h.SignedDistance(layout.Centreline.PointAt(gap.x)), Is.GreaterThanOrEqualTo(s.PondFromGapEdge - 0.06f), name + ": near gap");
                            Assert.That(h.SignedDistance(layout.Centreline.PointAt(gap.y)), Is.GreaterThanOrEqualTo(s.PondFromGapEdge - 0.06f), name + ": near gap");
                        }

                        for (float t = gap.y; t <= gap.y + s.GapLandingZone; t += 0.5f)
                        {
                            Assert.That(h.SignedDistance(layout.Centreline.PointAt(t)), Is.GreaterThanOrEqualTo(s.ShotCorridorHalfWidth - Mm), name + ": gap landing zone");
                        }
                    }
                }
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void GreenGuard_IsOnTheApproachSide_AndNearTheDirectLine(int par)
        {
            GreenGuard_IsOnTheApproachSide_AndNearTheDirectLine_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void GreenGuard_IsOnTheApproachSide_AndNearTheDirectLine_FullSweep(int par)
        {
            GreenGuard_IsOnTheApproachSide_AndNearTheDirectLine_Run(par, 200);
        }

        private void GreenGuard_IsOnTheApproachSide_AndNearTheDirectLine_Run(int par, int seeds)
        {
            int checkedCourses = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                CoursePlan plan = layout.Plan;
                List<HazardOutline> hazards = Outlines(layout);
                for (int i = 0; i < hazards.Count; i++)
                {
                    if (plan.Hazards[i].Role != HazardRole.GreenGuard)
                    {
                        continue;
                    }

                    Vector2 a = (layout.Hole - plan.DirectOrigin).normalized;
                    Vector2 fromGreen = (hazards[i].Centre - layout.Greens[0].Outline.Center).normalized;
                    Assert.That(Vector2.Angle(fromGreen, -a), Is.LessThanOrEqualTo(60f), $"seed {seed}: guard side");

                    float best = float.MaxValue;
                    float length = Vector2.Distance(plan.DirectOrigin, layout.Hole);
                    for (float t = 0f; t <= length; t += 0.25f)
                    {
                        best = Mathf.Min(best, hazards[i].SignedDistance(Vector2.Lerp(plan.DirectOrigin, layout.Hole, t / length)));
                    }

                    Assert.That(best, Is.LessThanOrEqualTo(Settings.GuardLineReach + Mm), $"seed {seed}: direct line passes near the guard");
                    checkedCourses++;
                }
            }

            Assert.That(checkedCourses, Is.GreaterThan(seeds / 2));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Pin_SitsTowardTheGuard_AndInsideTheGreen(int par)
        {
            Pin_SitsTowardTheGuard_AndInsideTheGreen_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void Pin_SitsTowardTheGuard_AndInsideTheGreen_FullSweep(int par)
        {
            Pin_SitsTowardTheGuard_AndInsideTheGreen_Run(par, 200);
        }

        private void Pin_SitsTowardTheGuard_AndInsideTheGreen_Run(int par, int seeds)
        {
            LevelGenSettings s = Settings;
            float cap = Mathf.Min(s.GreenRadius.x, s.GreenRadius.y) - s.PinFromBorder - s.GreenWobble;
            float worstBorder = float.MaxValue;
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                CoursePlan plan = layout.Plan;
                Vector2 centre = layout.Greens[0].Outline.Center;
                Vector2 shortOfHole = layout.Centreline.PointAt(layout.Centreline.Length - s.GreenCentreShortOfHole);

                // The green moved by the offset, away from the guard: the hole is that far toward it compared to the base position.
                Vector2 offset = shortOfHole - centre;
                Assert.That(offset.magnitude, Is.LessThanOrEqualTo(cap + Mm), $"seed {seed}: within the cap");
                Assert.That(Vector2.Dot(offset, plan.GuardDirection), Is.GreaterThan(0f), $"seed {seed}: toward the guard");

                Assert.That(new CourseFields(layout).Green(Geo.At(layout.Hole)), Is.LessThan(0f), $"seed {seed}: hole on the green");
                float border = -HazardOutline.Of(layout.Greens[0].Outline, false, HazardRole.GreenGuard).SignedDistance(layout.Hole);
                worstBorder = Mathf.Min(worstBorder, border);
            }

            Debug.Log($"Par {par}: the hole is at least {worstBorder:0.00} m inside the green's border over {seeds} seeds.");
            Assert.That(worstBorder, Is.GreaterThan(0.8f));
        }

        [TestCase(4)]
        [TestCase(5)]
        public void CornerCarry_CrossesTheCutLine_AndLeavesTheLandingClear(int par)
        {
            CornerCarry_CrossesTheCutLine_AndLeavesTheLandingClear_Run(par, 50);
        }

        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void CornerCarry_CrossesTheCutLine_AndLeavesTheLandingClear_FullSweep(int par)
        {
            CornerCarry_CrossesTheCutLine_AndLeavesTheLandingClear_Run(par, 200);
        }

        private void CornerCarry_CrossesTheCutLine_AndLeavesTheLandingClear_Run(int par, int seeds)
        {
            LevelGenSettings s = Settings;
            int checkedCorners = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                CoursePlan plan = layout.Plan;
                List<HazardOutline> hazards = Outlines(layout);
                RouteWorld world = World(layout, par);
                foreach (ShotLine line in plan.Lines.Where(l => l.Kind == ShotLineKind.Cut))
                {
                    var corners = new List<HazardOutline>();
                    for (int i = 0; i < hazards.Count; i++)
                    {
                        if (plan.Hazards[i].Role == HazardRole.CornerCarry)
                        {
                            corners.Add(hazards[i]);
                        }
                    }

                    // A corner that found no place is a missing role, which is allowed; but one that was placed must be right.
                    bool carried = false;
                    float length = Vector2.Distance(line.From, line.To);
                    foreach (HazardOutline corner in corners)
                    {
                        float farEdge = -1f;
                        for (float t = 0f; t <= length; t += 0.1f)
                        {
                            if (corner.Contains(Vector2.Lerp(line.From, line.To, t / length)))
                            {
                                farEdge = t;
                            }
                        }

                        if (farEdge >= 0f)
                        {
                            carried = true;
                            Assert.That(farEdge, Is.LessThanOrEqualTo(s.CarryAt(s.CutPower) - s.CutLandingMargin + Mm), $"seed {seed}: far edge");
                            float cutAlong = layout.Centreline.Project(line.To, out _);
                            Assert.That(world.CapsuleClear(cutAlong, hazards), Is.True, $"seed {seed}: landing clear");
                            checkedCorners++;
                        }
                    }

                    Assert.That(carried || plan.MissingRoles.Contains(HazardRole.CornerCarry), Is.True, $"seed {seed}: a cuttable corner without a hazard must be recorded");
                }
            }

            Debug.Log($"Par {par}: {checkedCorners} corner hazards checked.");
            Assert.That(checkedCorners, Is.GreaterThan(seeds >= 200 ? 10 : 1));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void SafeRoute_ExistsOnEveryCompleteCourse_AndKeepsItsRules(int par)
        {
            SafeRoute_ExistsOnEveryCompleteCourse_AndKeepsItsRules_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void SafeRoute_ExistsOnEveryCompleteCourse_AndKeepsItsRules_FullSweep(int par)
        {
            SafeRoute_ExistsOnEveryCompleteCourse_AndKeepsItsRules_Run(par, 200);
        }

        private void SafeRoute_ExistsOnEveryCompleteCourse_AndKeepsItsRules_Run(int par, int seeds)
        {
            LevelGenSettings s = Settings;
            int layup = Geo.Cm(s.CarryAt(s.LayupPower));
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                CoursePlan plan = layout.Plan;
                if (!plan.Complete)
                {
                    continue;
                }

                IReadOnlyList<Vector2> route = plan.SafeRoute;
                List<HazardOutline> hazards = Outlines(layout);
                RouteWorld world = World(layout, par);
                Assert.That(route.Count - 1, Is.InRange(1, par - 1 + s.SafeRouteExtraShots), $"seed {seed}: shots");
                Assert.That(route[0], Is.EqualTo(layout.Tee));
                Assert.That(route[route.Count - 1], Is.EqualTo(plan.SafeTarget));

                for (int i = 1; i < route.Count; i++)
                {
                    Assert.That(Geo.Cm(Vector2.Distance(route[i - 1], route[i])), Is.LessThanOrEqualTo(Geo.Cm(s.SafeStep)), $"seed {seed}: step {i}");
                    if (i < route.Count - 1)
                    {
                        Assert.That(world.CapsuleClear(layout.Centreline.Project(route[i], out _), hazards), Is.True, $"seed {seed}: capsule {i}");
                    }

                    foreach (Vector2 gap in CourseGeneratorTests.Gaps(layout))
                    {
                        float from = layout.Centreline.Project(route[i - 1], out _);
                        float to = layout.Centreline.Project(route[i], out _);
                        if (from < gap.x && to > gap.x)
                        {
                            Assert.That(Geo.Cm(Vector2.Distance(route[i - 1], route[i])), Is.LessThanOrEqualTo(layup), $"seed {seed}: gap step");
                            Assert.That(to, Is.GreaterThanOrEqualTo(gap.y + s.GapLandingMargin - 0.06f), $"seed {seed}: gap landing");
                        }
                    }
                }

                Assert.That(world.FinalClear(hazards), Is.True, $"seed {seed}: final approach");
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Ponds_SitJustBelowALevelEnoughRim_AndBunkersOnGentleGround(int par)
        {
            Ponds_SitJustBelowALevelEnoughRim_AndBunkersOnGentleGround_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void Ponds_SitJustBelowALevelEnoughRim_AndBunkersOnGentleGround_FullSweep(int par)
        {
            Ponds_SitJustBelowALevelEnoughRim_AndBunkersOnGentleGround_Run(par, 200);
        }

        private void Ponds_SitJustBelowALevelEnoughRim_AndBunkersOnGentleGround_Run(int par, int seeds)
        {
            LevelGenSettings s = Settings;
            int ponds = 0;
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = Course(par, seed);
                var fields = new CourseFields(layout);
                foreach (HazardOutline h in Outlines(layout))
                {
                    if (h.IsWater)
                    {
                        Assert.That(HazardPlacer.RimLevel(fields, s, h, out float level, out float spread), Is.True);
                        Assert.That(h.WaterLevel, Is.EqualTo(level).Within(Mm), $"seed {seed}: level");
                        Assert.That(spread, Is.LessThanOrEqualTo(s.PondMaxRimRise + Mm), $"seed {seed}: rim spread");
                        float rimLow = h.Ring.Where(p => fields.Island(Geo.At(p)) < 0f).Min(p => fields.BroadHeight(p.x, p.y));
                        Assert.That(h.WaterLevel, Is.LessThanOrEqualTo(rimLow - s.PondDrop + Mm), $"seed {seed}: below the rim");
                        ponds++;
                    }
                    else
                    {
                        Assert.That(fields.BroadSlope(h.Centre.x, h.Centre.y).magnitude, Is.LessThanOrEqualTo(s.BunkerMaxSlope + Mm), $"seed {seed}: bunker slope");
                    }
                }
            }

            Debug.Log($"Par {par}: {ponds} ponds checked over {seeds} seeds.");
        }

        [Test]
        public void WaterRunningOffTheEdge_IsTaggedAsWater_AndGetsNoTufts()
        {
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            var tufts = AssetDatabase.LoadAssetAtPath<TuftSettings>("Assets/_Project/Settings/IslandTufts.asset");
            int found = 0;
            for (int par = 3; par <= 5 && found < 3; par++)
            {
                for (int seed = 0; seed < Seeds && found < 3; seed++)
                {
                    CourseLayout layout = Course(par, seed);
                    if (layout.Ponds.Count == 0)
                    {
                        continue;
                    }

                    TerrainResult terrain = TerrainBuilder.Build(layout, palette);
                    List<EdgeSegment> water = terrain.Edge.Where(e => e.Inner == EdgeSurface.Water).ToList();
                    if (water.Count == 0)
                    {
                        continue;
                    }

                    found++;
                    var spots = TuftBuilder.Place(terrain.Edge, tufts.MinSpacing, new System.Random(1));
                    foreach (TuftSpot spot in spots)
                    {
                        foreach (EdgeSegment segment in water)
                        {
                            Assert.That(Geo.DistanceToSegment(new Vector2(spot.Position.x, spot.Position.z), new Vector2(segment.A.x, segment.A.z), new Vector2(segment.B.x, segment.B.z)), Is.GreaterThan(0.01f), $"par {par} seed {seed}: a tuft on a water edge");
                        }
                    }
                }
            }

            Assert.That(found, Is.GreaterThan(0), "some generated pond should reach the island's edge");
        }
    }
}
