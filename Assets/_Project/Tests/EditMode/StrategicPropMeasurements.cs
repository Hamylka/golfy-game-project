using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Strategic obstacles S2: the figures quoted in the PR. Manual measurements only (`Explicit`), never in the default suite.
    /// </summary>
    public class StrategicPropMeasurements
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;
        private static ShotSettings Shot => CourseCheckMeasurements.Shot;

        /// <summary>
        /// Variant 0, seeds 0-199 per par (as PR 4.4 and F3.4 measured): the share of courses with a strategic group, per zone (of
        /// the courses that have that zone's shape), members and kinds, groups that fully block a line, props per course, and the
        /// generation time with props.
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Category("Measurement")]
        [Timeout(3600000)]
        public void StrategicShareAndProps()
        {
            var report = new StringBuilder("STRATEGIC_SHARE");
            foreach (int par in new[] { 3, 4, 5 })
            {
                int courses = 0, groups = 0, members = 0, props = 0, blocks = 0;
                var zoneCourses = new Dictionary<StrategicZone, int>();
                var zoneGroups = new Dictionary<StrategicZone, int>();
                var kinds = new Dictionary<PropKind, int>();
                var clock = new Stopwatch();
                for (int seed = 0; seed < 200; seed++)
                {
                    clock.Start();
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    clock.Stop();
                    courses++;
                    props += layout.Props.Count;
                    foreach (StrategicZone zone in ZonesOf(layout, par))
                    {
                        zoneCourses[zone] = zoneCourses.TryGetValue(zone, out int n) ? n + 1 : 1;
                    }

                    var feet = new List<StrategicFoot>();
                    StrategicZone placed = StrategicZone.None;
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (!prop.Strategic)
                        {
                            continue;
                        }

                        placed = prop.Zone;
                        members++;
                        kinds[prop.Kind] = kinds.TryGetValue(prop.Kind, out int k) ? k + 1 : 1;
                        feet.AddRange(StrategicPropTests.Feet(prop));
                    }

                    if (placed == StrategicZone.None)
                    {
                        continue;
                    }

                    groups++;
                    zoneGroups[placed] = zoneGroups.TryGetValue(placed, out int g) ? g + 1 : 1;
                    blocks += Blocks(layout, feet) ? 1 : 0;
                }

                report.Append($" | par {par}: {groups}/{courses} courses with a group ({100f * groups / courses:F1}%), {(float)members / Mathf.Max(1, groups):F2} props per group, ");
                report.Append($"{blocks} full blocks ({100f * blocks / courses:F1}% of courses), props per course {(float)props / courses:F2}, {clock.Elapsed.TotalMilliseconds / courses:F1} ms per course; zones");
                foreach (StrategicZone zone in new[] { StrategicZone.Spine, StrategicZone.Split, StrategicZone.RoughIsland, StrategicZone.CutCorner })
                {
                    int have = zoneCourses.TryGetValue(zone, out int h) ? h : 0;
                    int got = zoneGroups.TryGetValue(zone, out int n) ? n : 0;
                    report.Append($" {zone} {got}/{have}");
                }

                report.Append("; kinds");
                foreach (KeyValuePair<PropKind, int> kind in kinds)
                {
                    report.Append($" {kind.Key} {kind.Value}");
                }
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Checked courses, seeds 0-49 per par: props kept per course, props dropped by the checks (all, and strategic), and the
        /// checked generation time; then the phone review seeds: every checked course with a strategic group, labelled with its
        /// zone, its kinds and whether it fully blocks a line, and whether it has a spine cypress whose shadow crosses a corridor.
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Category("Measurement")]
        [Timeout(3600000)]
        public void CheckedDropsAndReviewSeeds()
        {
            var report = new StringBuilder("STRATEGIC_CHECKED");
            var seeds = new StringBuilder("STRATEGIC_REVIEW_SEEDS");
            foreach (int par in new[] { 3, 4, 5 })
            {
                int props = 0, dropped = 0, strategicDropped = 0, strategicKept = 0;
                var clock = new Stopwatch();
                for (int seed = 0; seed < 50; seed++)
                {
                    clock.Start();
                    CheckedCourseResult result = CheckedCourse.Generate(seed, par, Settings, Shot);
                    clock.Stop();
                    CourseLayout placed = CourseGenerator.Generate(seed, par, Settings, result.Variant);
                    int sPlaced = 0, sKept = 0;
                    foreach (CourseProp prop in placed.Props)
                    {
                        sPlaced += prop.Strategic ? 1 : 0;
                    }

                    var feet = new List<StrategicFoot>();
                    var kinds = new List<string>();
                    StrategicZone zone = StrategicZone.None;
                    bool spineCypress = false;
                    foreach (CourseProp prop in result.Layout.Props)
                    {
                        spineCypress |= prop.Cluster == PropPlacement.SpineBase && prop.Kind == PropKind.Cypress;
                        if (prop.Strategic)
                        {
                            sKept++;
                            zone = prop.Zone;
                            kinds.Add(prop.Kind.ToString());
                            feet.AddRange(StrategicPropTests.Feet(prop));
                        }
                    }

                    props += result.Layout.Props.Count;
                    dropped += result.DroppedProps;
                    strategicDropped += sPlaced - sKept;
                    strategicKept += sKept;
                    if (zone != StrategicZone.None || spineCypress)
                    {
                        string block = zone != StrategicZone.None && Blocks(result.Layout, feet) ? " FULL BLOCK" : string.Empty;
                        string what = zone != StrategicZone.None ? $"{zone} ({string.Join("+", kinds)}){block}" : "no group";
                        seeds.Append($" | par {par} seed {seed}: {what}{(spineCypress ? ", spine cypress" : string.Empty)}");
                    }
                }

                report.Append($" | par {par}: props per course {props / 50f:F2}, dropped {dropped} ({strategicDropped} strategic, {strategicKept} strategic kept), {clock.Elapsed.TotalMilliseconds / 50:F1} ms per checked course");
            }

            Debug.Log(report.ToString());
            Debug.Log(seeds.ToString());
        }

        /// <summary>Whether the group leaves some direct or cut line it reaches with no playable way past it (Q5).</summary>
        internal static bool Blocks(CourseLayout layout, List<StrategicFoot> feet)
        {
            var zones = new PropZones(layout, Settings);
            for (int i = 0; i < zones.Lines.Count; i++)
            {
                if (!zones.IsStrategicLine(i))
                {
                    continue;
                }

                bool reaches = false;
                foreach (StrategicFoot foot in feet)
                {
                    reaches |= zones.Intrudes(i, foot.Centre, foot.Radius);
                }

                if (reaches && !zones.Playable(i, feet))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The strategic zones this course's shapes and lines offer (whether or not any slot fits): a fork, a split, an oval, a cut line.</summary>
        private static IEnumerable<StrategicZone> ZonesOf(CourseLayout layout, int par)
        {
            var zones = new HashSet<StrategicZone>();
            if (layout.Plan == null)
            {
                return zones;
            }

            foreach (FairwayShape shape in layout.Plan.FairwayShapes)
            {
                if (shape.Kind == FairwayShapeKind.RoughIsland)
                {
                    zones.Add(StrategicZone.RoughIsland);
                }
                else if (par > 3)
                {
                    zones.Add(shape.Kind == FairwayShapeKind.Fork ? StrategicZone.Spine : StrategicZone.Split);
                }
            }

            foreach (ShotLine line in layout.Plan.Lines)
            {
                if (line.Kind == ShotLineKind.Cut && par > 3)
                {
                    zones.Add(StrategicZone.CutCorner);
                }
            }

            return zones;
        }
    }
}
