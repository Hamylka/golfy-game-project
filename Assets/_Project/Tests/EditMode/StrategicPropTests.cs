using System.Collections.Generic;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Strategic obstacles S2 (`feature/strategic-props`): the strategic group on the direct and cut lines (plan §1-§5). Seeds 0-49
    /// of each par; the rule tests read the courses at variant 0 (the placement's own output), the play tests read checked courses.
    /// </summary>
    public class StrategicPropTests
    {
        private const int Seeds = 50;
        private const float Tolerance = 0.011f;
        private static readonly int[] Pars = { 3, 4, 5 };

        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        private static readonly Dictionary<(int, int), CheckedCourseResult> CheckedCache = new Dictionary<(int, int), CheckedCourseResult>();
        private static readonly Dictionary<(int, int), CourseLayout> PlacedCache = new Dictionary<(int, int), CourseLayout>();

        internal static CheckedCourseResult Checked(int seed, int par)
        {
            if (!CheckedCache.TryGetValue((seed, par), out CheckedCourseResult result))
            {
                result = CheckedCourse.Generate(seed, par, Settings, Shot);
                CheckedCache[(seed, par)] = result;
            }

            return result;
        }

        internal static CourseLayout Placed(int seed, int par)
        {
            if (!PlacedCache.TryGetValue((seed, par), out CourseLayout layout))
            {
                layout = CourseGenerator.Generate(seed, par, Settings);
                PlacedCache[(seed, par)] = layout;
            }

            return layout;
        }

        /// <summary>The footprint circles of a prop, as the placement tests them (a wall or a bridge every 0.25 m), with its reach and top.</summary>
        internal static List<StrategicFoot> Feet(CourseProp prop)
        {
            PropPlacement.Reach(prop.Root, prop.End, prop.Shapes, out float radius, out float height);
            float top = Mathf.Min(prop.Root.y, prop.End.y) + height;
            var from = new Vector2(prop.Root.x, prop.Root.z);
            var to = new Vector2(prop.End.x, prop.End.z);
            int samples = CourseProp.IsLine(prop.Kind) ? Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.25f)) : 0;
            var feet = new List<StrategicFoot>();
            for (int i = 0; i <= samples; i++)
            {
                feet.Add(new StrategicFoot(samples == 0 ? from : Vector2.Lerp(from, to, (float)i / samples), radius, top));
            }

            return feet;
        }

        internal static bool IsFork(CourseLayout layout) => PropReferenceTests.IsFork(layout);

        // --- Keep-outs and safe flights ----------------------------------------------------------------------------------------

        /// <summary>
        /// On every checked course, every strategic prop keeps out of the capsules, the tee, the green and its margin, the hazards,
        /// the +X end, the edge and the fairway with its margin (the oval's on a rough island), and of every safe leg's corridor
        /// except under the leg's planned arc; read from the true outlines by <see cref="PropPlacementTests.BrokenKeepOut"/>. Then
        /// every safe leg's first flight, played with every prop, strikes nothing (<see cref="CourseChecks.SafeLegsFly"/>).
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void StrategicPropsStayOutOfCapsulesAndSafeFlights()
        {
            var failures = new StringBuilder();
            int strategic = 0;
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Checked(seed, par).Layout;
                    bool any = false;
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (!prop.Strategic)
                        {
                            continue;
                        }

                        any = true;
                        strategic++;
                        string broken = PropPlacementTests.BrokenKeepOut(layout, prop);
                        if (broken != null)
                        {
                            failures.AppendLine($"par {par} seed {seed}: {prop.Kind} on {prop.Zone} breaks {broken}");
                        }
                    }

                    if (any)
                    {
                        var ground = new BallGround(layout);
                        BallObstacles obstacles = BallObstacles.ForCourse(layout.PropShapes, ground, Shot);
                        if (!CourseChecks.SafeLegsFly(ground, layout.Plan, Shot, Settings, obstacles))
                        {
                            failures.AppendLine($"par {par} seed {seed}: a safe leg's first flight strikes a prop");
                        }
                    }
                }
            }

            Assert.Greater(strategic, 0, "no strategic prop on seeds 0-49: the test proves nothing");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        // --- The block rule (Q5) -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Every strategic group stands on a line (a member reaches into a direct or cut corridor), and every line it reaches stays
        /// playable (a 1.5 m free run across the corridor past what a full-power shot can't loft) unless a full block is allowed
        /// there: never on a par 3, only on a cut line or a fork's direct line, only where the safe route reaches the green in at
        /// most par − 1 shots, never on a straight course's tee shot. The conditions are re-read here from the plan, not from
        /// <see cref="PropZones.FullBlockAllowed"/>.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void StrategicGroupLeavesALineOrBlocksItOnlyWhereAllowed()
        {
            var failures = new StringBuilder();
            int groups = 0, blocks = 0;
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Placed(seed, par);
                    var feet = new List<StrategicFoot>();
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Strategic)
                        {
                            feet.AddRange(Feet(prop));
                        }
                    }

                    if (feet.Count == 0)
                    {
                        continue;
                    }

                    groups++;
                    var zones = new PropZones(layout, Settings);
                    CoursePlan plan = layout.Plan;
                    bool onALine = false;
                    for (int i = 0; i < zones.Lines.Count; i++)
                    {
                        ShotLineKind kind = zones.Kinds[i];
                        if (kind == ShotLineKind.Safe)
                        {
                            continue;
                        }

                        bool reaches = false;
                        foreach (StrategicFoot foot in feet)
                        {
                            reaches |= Geo.DistanceToSegment(foot.Centre, zones.Lines[i][0], zones.Lines[i][1]) < zones.Corridor + foot.Radius - Tolerance;
                        }

                        onALine |= reaches;
                        if (!reaches || zones.Playable(i, feet))
                        {
                            continue;
                        }

                        blocks++;
                        bool fromTee = Vector2.Distance(zones.Lines[i][0], layout.Tee) < 0.01f;
                        string why = par == 3 ? "on a par 3"
                            : !(kind == ShotLineKind.Cut || (kind == ShotLineKind.Direct && IsFork(layout))) ? $"on a {kind} line off a fork"
                            : plan.SafeRoute.Count - 1 > par - 1 ? "where the safe route takes more than par - 1 shots"
                            : !plan.Dogleg && fromTee ? "on a straight course's tee shot"
                            : null;
                        if (why != null)
                        {
                            failures.AppendLine($"par {par} seed {seed}: line {i} fully blocked {why}");
                        }
                    }

                    if (!onALine)
                    {
                        failures.AppendLine($"par {par} seed {seed}: the strategic group stands on no direct or cut line");
                    }
                }
            }

            Debug.Log($"STRATEGIC_BLOCKS {groups} groups, {blocks} fully blocked lines (seeds 0-49, variant 0)");
            Assert.Greater(groups, 0);
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        /// <summary>
        /// B3 at 100%, then "Pour l'instant A" (director): on a course with a zone that reaches a line where a full block is allowed,
        /// the group goes there with tall kinds (olive, cypress, wall, shed) standing in that line's corridor, unless none fits, when a
        /// group by the normal rule stands in the same zone. So every course with a block zone has its group in a block zone, never
        /// on a rough island, and some par 4 or 5 has a tall group on a block line.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void B3GroupsStandOnTheBlockLineWithTallKinds()
        {
            var failures = new StringBuilder();
            var tallGroups = new Dictionary<int, int> { { 4, 0 }, { 5, 0 } };
            var fallbacks = new Dictionary<int, int> { { 4, 0 }, { 5, 0 } };
            foreach (int par in new[] { 4, 5 })
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Placed(seed, par);
                    var zones = new PropZones(layout, Settings);
                    List<PropPlacement.StrategicArea> blockAreas = PropPlacement.BlockAreas(PropPlacement.StrategicAreas(layout, par, Settings, zones, Settings.Props));
                    var group = new List<CourseProp>();
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Strategic)
                        {
                            group.Add(prop);
                        }
                    }

                    if (group.Count == 0 || blockAreas.Count == 0)
                    {
                        continue;
                    }

                    // A tall obstacle closes a block line before any zone is drawn (director Q5 B): TallObstacleTests hold it.
                    StrategicZone zone = group[0].Zone;
                    if (zone == StrategicZone.BlockLine)
                    {
                        continue;
                    }

                    bool inABlockZone = false;
                    foreach (PropPlacement.StrategicArea area in blockAreas)
                    {
                        inABlockZone |= area.Zone == zone;
                    }

                    if (!inABlockZone)
                    {
                        failures.AppendLine($"par {par} seed {seed}: the group stands in {zone}, not in a block zone");
                        continue;
                    }

                    bool allTall = true, onBlockLine = false;
                    foreach (CourseProp prop in group)
                    {
                        allTall &= prop.Kind == PropKind.Olive || prop.Kind == PropKind.Cypress || prop.Kind == PropKind.Wall || prop.Kind == PropKind.Shed;
                        foreach (StrategicFoot foot in Feet(prop))
                        {
                            for (int i = 0; i < zones.Lines.Count; i++)
                            {
                                onBlockLine |= zones.FullBlockAllowed(i, par) && zones.Intrudes(i, foot.Centre, foot.Radius);
                            }
                        }
                    }

                    if (allTall && onBlockLine)
                    {
                        tallGroups[par]++;
                    }
                    else
                    {
                        fallbacks[par]++;
                    }
                }
            }

            Debug.Log($"STRATEGIC_B3 tall groups on a block line: par 4 {tallGroups[4]}, par 5 {tallGroups[5]}; fell back to the normal rule: par 4 {fallbacks[4]}, par 5 {fallbacks[5]} (seeds 0-49, variant 0)");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
            // On seeds 0-49 par 4 has none (its few block zones lie over fairway, where a tall crown can't stand): the count is
            // quoted in the PR rather than pinned per par.
            Assert.Greater(tallGroups[4] + tallGroups[5], 0, "some par 4 or 5 has a B3 tall group on a block line");
        }

        // --- Rough islands (Q2 B, Q8 A) ----------------------------------------------------------------------------------------

        /// <summary>On a rough island only a rock or a low bush stands, reaching at most 0.5 m; a par 3's strategic props stand only there.</summary>
        [Test]
        [Timeout(900000)]
        public void StrategicPropsOnlySmallOnRoughIslands()
        {
            var failures = new StringBuilder();
            int island = 0;
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    foreach (CourseProp prop in Placed(seed, par).Props)
                    {
                        if (par == 3 && prop.Strategic && prop.Zone != StrategicZone.RoughIsland)
                        {
                            failures.AppendLine($"par 3 seed {seed}: a strategic {prop.Kind} on {prop.Zone}");
                        }

                        if (prop.Zone != StrategicZone.RoughIsland)
                        {
                            continue;
                        }

                        island++;
                        PropPlacement.Reach(prop.Root, prop.End, prop.Shapes, out float radius, out _);
                        if ((prop.Kind != PropKind.Bush && prop.Kind != PropKind.Rock) || radius > Settings.Props.StrategicSmallReach + Tolerance)
                        {
                            failures.AppendLine($"par {par} seed {seed}: a {prop.Kind} reaching {radius:F2} m on a rough island");
                        }
                    }
                }
            }

            Assert.Greater(island, 0, "no strategic prop on a rough island on seeds 0-49");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        // --- Bit identity ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Every prop but the strategic group and the spine stays bit for bit. (a) At variant 0, against the same settings with
        /// <c>strategicChance</c> 0: a course with no fork keeps every prop; on a fork course, the props before the spine (placed
        /// last) stay in order. (b) Every checked course with no fork, its strategic props and their drops left out, equals the
        /// checked course of those settings: variant, candidates, failed checks, other drops and every other prop. (Until 4.7 the
        /// anchors were <c>PropReferenceS1</c> and <c>CheckedCourseReferenceBefore</c>, stored hashes.)
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void OtherPropsBitIdentical()
        {
            // Re-anchored in 4.7: it compared with PropReferenceS1 and CheckedCourseReferenceBefore (stored hashes), which the bush
            // jitter change moved for every bush. The same settings with strategicChance 0, run live, are the twin: no group, no tall
            // obstacle, so every other prop and every check outcome must be the same.
            LevelGenSettings off = SpinePropTests.NoStrategic();
            var failures = new StringBuilder();
            int forks = 0, plain = 0;
            try
            {
            for (int p = 0; p < Pars.Length; p++)
            {
                int par = Pars[p];
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Placed(seed, par);
                    CourseLayout bare = CourseGenerator.Generate(seed, par, off);
                    bool fork = IsFork(layout);
                    Assert.AreEqual(fork, IsFork(bare), $"par {par} seed {seed}: fork flag");
                    int none = 0;
                    List<ulong> others = SpinePropTests.Others(layout, ref none, fork);
                    List<ulong> every = SpinePropTests.Others(bare, ref none, false);
                    List<ulong> expected = fork ? every.GetRange(0, Mathf.Min(others.Count, every.Count)) : every;
                    if (!SpinePropTests.Same(others, expected))
                    {
                        failures.AppendLine($"par {par} seed {seed}: other props changed at variant 0");
                    }

                    if (fork)
                    {
                        forks++;
                        continue;
                    }

                    // (b) The checked course, strategic props and their drops aside.
                    CheckedCourseResult result = Checked(seed, par);
                    CheckedCourseResult bareResult = CheckedCourse.Generate(seed, par, off, Shot);
                    CourseLayout placed = CourseGenerator.Generate(seed, par, Settings, result.Variant);
                    int strategicPlaced = 0, strategicKept = 0;
                    var kept = new List<CourseProp>();
                    foreach (CourseProp prop in placed.Props)
                    {
                        strategicPlaced += prop.Strategic ? 1 : 0;
                    }

                    foreach (CourseProp prop in result.Layout.Props)
                    {
                        if (prop.Strategic)
                        {
                            strategicKept++;
                        }
                        else
                        {
                            kept.Add(prop);
                        }
                    }

                    ulong hash = CheckedCourseReferenceTests.HashOf(result, kept, result.DroppedProps - (strategicPlaced - strategicKept));
                    if (hash != CheckedCourseReferenceTests.HashOf(bareResult, bareResult.Layout.Props, bareResult.DroppedProps))
                    {
                        failures.AppendLine($"par {par} seed {seed}: checked course changed");
                    }

                    plain++;
                }
            }
            }
            finally
            {
                Object.DestroyImmediate(off);
            }

            Debug.Log($"OTHER_PROPS compared {plain} checked courses with no fork and {forks} fork courses at variant 0");
            Assert.Greater(forks, 0);
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        // --- Spine cypresses (Q7 B) --------------------------------------------------------------------------------------------

        /// <summary>
        /// Every spine cypress stands out of every corridor, and its shadow out of every safe leg's corridor and every capsule; on
        /// some forks its shadow crosses a direct or cut corridor, which the rule now allows (else the test proves nothing).
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void SpineCypressShadowRule()
        {
            var failures = new StringBuilder();
            int cypresses = 0, crossing = 0;
            PropRules rules = Settings.Props;
            foreach (int par in new[] { 4, 5 })
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Placed(seed, par);
                    float corridor = layout.Plan.ShotCorridorHalfWidth;
                    Vector2 shadow = Geo.Dir(Geo.HeadingOf(layout.Centreline.Points.Count > 2 ? layout.Hole - layout.Tee : Vector2.right) + rules.ShadowHeading);
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Cluster != PropPlacement.SpineBase || prop.Kind != PropKind.Cypress)
                        {
                            continue;
                        }

                        cypresses++;
                        PropPlacement.Reach(prop.Root, prop.End, prop.Shapes, out float radius, out float height);
                        var root = new Vector2(prop.Root.x, prop.Root.z);
                        bool crosses = false;
                        foreach (ShotLine line in layout.Plan.Lines)
                        {
                            if (Geo.DistanceToSegment(root, line.From, line.To) < corridor + radius - Tolerance)
                            {
                                failures.AppendLine($"par {par} seed {seed}: a spine cypress stands in a {line.Kind} corridor");
                            }

                            for (int k = 1; k <= 10; k++)
                            {
                                Vector2 s = root + (shadow * (height * rules.ShadowLength * k / 10f));
                                if (Geo.DistanceToSegment(s, line.From, line.To) < corridor + radius - Tolerance)
                                {
                                    if (line.Kind == ShotLineKind.Safe)
                                    {
                                        failures.AppendLine($"par {par} seed {seed}: a spine cypress's shadow crosses a safe leg's corridor");
                                    }
                                    else
                                    {
                                        crosses = true;
                                    }
                                }
                            }
                        }

                        crossing += crosses ? 1 : 0;
                        string broken = PropPlacementTests.BrokenKeepOut(layout, prop);
                        if (broken != null)
                        {
                            failures.AppendLine($"par {par} seed {seed}: a spine cypress breaks {broken}");
                        }
                    }
                }
            }

            Debug.Log($"SPINE_CYPRESS {cypresses} spine cypresses, {crossing} with a shadow across a direct or cut corridor (seeds 0-49)");
            Assert.Greater(crossing, 0, "no spine cypress casts its shadow across a direct or cut corridor: the rule is not exercised");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        // --- Aim preview (§4) --------------------------------------------------------------------------------------------------

        /// <summary>
        /// Shots from a line's start, and from a few metres short of the prop, into the strategic props of generated courses: whenever
        /// the preview (<see cref="BallSimulation.PredictHit"/>) finds a hit, the played ball's first strike is that hit (same time,
        /// point and shape), and a shot predicted clear lands before striking anything. A crown (soft, the ball falls through) and a
        /// hard prop are each struck first at least once. A tree's trunk stands under its own crown (an olive's from 0 to about 1 m
        /// inside a crown from 0.26 m, a cypress's likewise), so a flight always meets the crown first; the hard first strikes are
        /// rocks, walls and sheds, which the preview marks the same way.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void PreviewStopsAtStrategicTree()
        {
            int crowns = 0, hard = 0, clear = 0;
            foreach (int par in Pars)
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    CourseLayout layout = Placed(seed, par);
                    var ground = new BallGround(layout);
                    BallObstacles obstacles = null;
                    foreach (CourseProp tree in layout.Props)
                    {
                        if (!tree.Strategic)
                        {
                            continue;
                        }

                        obstacles ??= BallObstacles.ForCourse(layout.PropShapes, ground, Shot);
                        var at = new Vector2(tree.Root.x, tree.Root.z);
                        foreach (ShotLine line in layout.Plan.Lines)
                        {
                            if (line.Kind == ShotLineKind.Safe || Geo.DistanceToSegment(at, line.From, line.To) > layout.Plan.ShotCorridorHalfWidth + 2f)
                            {
                                continue;
                            }

                            // From the line's start, and from a few metres short of the tree on that line, where a low shot comes
                            // down under the crown into the trunk.
                            Vector2 toTree = (at - line.From).normalized;
                            for (int start = 0; start <= 5; start++)
                            for (int angle = -2; angle <= 2; angle += 2)
                            {
                                Vector2 from = start == 0 ? line.From : at - (toTree * (1f + start));
                                Vector2 aim = Quaternion.Euler(0f, 0f, angle) * toTree;
                                for (int p = start == 0 ? 2 : 0; p <= 40; p++)
                                {
                                    float power = start == 0 ? p / 40f : p / 160f;
                                    var ball = new BallSimulation(ground, Shot, from, obstacles);
                                    BallisticArc arc = ball.ArcFor(aim, power);
                                    bool predicted = ball.PredictHit(arc, ball.PredictLanding(arc), out ArcHit hit);
                                    ball.Shoot(aim, power);
                                    for (int i = 0; i < 60 * 60 && ball.State == BallState.Flying && ball.Hits == 0 && ball.Bounces == 0; i++)
                                    {
                                        ball.Step();
                                    }

                                    string where = $"par {par} seed {seed} start {start} angle {angle} power {power}";
                                    if (!predicted)
                                    {
                                        Assert.That(ball.Hits, Is.EqualTo(0), where);
                                        clear++;
                                        continue;
                                    }

                                    Assert.That(ball.Hits, Is.EqualTo(1), where);
                                    Assert.That(ball.LastHit.Time, Is.EqualTo(hit.Time), where);
                                    Assert.That(ball.LastHit.Point, Is.EqualTo(hit.Point), where);
                                    Assert.That(ball.LastHit.Index, Is.EqualTo(hit.Index), where);
                                    Assert.That(ball.Position, Is.EqualTo(hit.Point), where);
                                    if (!hit.IsPole && layout.Props[layout.PropOfShape(hit.Index)].Strategic)
                                    {
                                        if (hit.Material == PropMaterial.Foliage)
                                        {
                                            crowns++;
                                        }
                                        else
                                        {
                                            hard++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"STRATEGIC_PREVIEW {crowns} crown hits, {hard} hard hits, {clear} clear shots");
            Assert.Greater(crowns, 0, "no shot struck a strategic crown first");
            Assert.Greater(hard, 0, "no shot struck a hard strategic prop first");
        }
    }
}
