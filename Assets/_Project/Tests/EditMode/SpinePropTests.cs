using System.Collections.Generic;
using System.Text;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.4 (split fairway plan §4): the props of a fork's spine, and the guards that keep every other prop where it was. Courses are
    /// generated at variant 0, seeds 0-49 of each par (the references' courses).
    /// </summary>
    public class SpinePropTests
    {
        /// <summary>
        /// Courses with no fork keep every prop of F3.3, bit for bit: the fork capsule fix and the spine theme change fork courses
        /// only. Against <c>PropReferenceBefore</c>, captured on F3.3's code.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void CoursesWithoutAForkKeepTheirProps()
        {
            Dictionary<(int, int), (bool fork, List<ulong> props)> reference = PropReferenceTests.Read(PropReferenceBefore.Courses);
            var failures = new StringBuilder();
            int compared = 0;
            foreach (KeyValuePair<(int, int), (bool fork, List<ulong> props)> row in reference)
            {
                if (row.Value.fork)
                {
                    continue;
                }

                List<ulong> now = PropReferenceTests.Row(row.Key.Item2, row.Key.Item1, out CourseLayout layout);
                Assert.IsFalse(PropReferenceTests.IsFork(layout), $"par {row.Key.Item1} seed {row.Key.Item2} drew a fork it did not have");
                // The strategic group (S2) is new on any course; every other prop stays.
                int none = 0;
                if (!Same(Others(layout, ref none, false), row.Value.props))
                {
                    failures.AppendLine($"par {row.Key.Item1} seed {row.Key.Item2}: props changed ({row.Value.props.Count} -> {now.Count - 4})");
                }

                compared++;
            }

            Assert.Greater(compared, 100);
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        /// <summary>
        /// How many props of fork courses the capsule fix moved (props in F3.3's reference with no identical prop now, and the
        /// reverse), per par. Quoted in the F3.4 PR and the changelog.
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Timeout(900000)]
        public void ForkCapsuleFixMovedProps()
        {
            Dictionary<(int, int), (bool fork, List<ulong> props)> reference = PropReferenceTests.Read(PropReferenceBefore.Courses);
            var report = new StringBuilder("FORK_CAPSULE_FIX");
            foreach (int par in new[] { 4, 5 })
            {
                int forks = 0, before = 0, after = 0, gone = 0, added = 0, changed = 0;
                for (int seed = 0; seed < PropReferenceTests.Seeds; seed++)
                {
                    (bool fork, List<ulong> props) old = reference[(par, seed)];
                    if (!old.fork)
                    {
                        continue;
                    }

                    List<ulong> now = PropReferenceTests.Row(seed, par, out CourseLayout layout);
                    List<ulong> props = now.GetRange(4, now.Count - 4);
                    var left = new List<ulong>(old.props);
                    int kept = 0;
                    foreach (ulong h in props)
                    {
                        if (left.Remove(h))
                        {
                            kept++;
                        }
                    }

                    forks++;
                    before += old.props.Count;
                    after += props.Count;
                    gone += old.props.Count - kept;
                    added += props.Count - kept;
                    changed += old.props.Count - kept > 0 || props.Count - kept > 0 ? 1 : 0;
                }

                report.Append($" | par {par}: {forks} forks, {changed} with props changed, props {before} -> {after}, {gone} gone, {added} new");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Every spine prop stands on its fork's spine (the held stretch, between the branch centres) and breaks no keep-out, read by
        /// <see cref="PropPlacementTests.BrokenKeepOut"/> from the true outlines and the safe route's own capsule samples: corridors and
        /// shadows, capsules, fairway plus its margin, the tee, the green, hazards, the edge, the end.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void SpinePropsStayOutOfCorridorsCapsulesAndFairway() => SpinePropsStayOut(PropReferenceTests.Seeds);

        [Test]
        [Explicit("Full sweep, seeds 0-199: run by hand when a PR changes the prop rules and the director asks")]
        [Timeout(1800000)]
        public void SpinePropsStayOutOfCorridorsCapsulesAndFairway_FullSweep() => SpinePropsStayOut(200);

        /// <summary>
        /// Without its spine cluster, every course keeps exactly the props of <c>PropReferenceCapsuleFix</c> (captured after the
        /// capsule fix, before the spine theme), in order and bit for bit.
        /// </summary>
        [Test]
        [Timeout(900000)]
        public void SpineThemeLeavesOtherClustersBitIdentical()
        {
            Dictionary<(int, int), (bool fork, List<ulong> props)> reference = PropReferenceTests.Read(PropReferenceCapsuleFix.Courses);
            var failures = new StringBuilder();
            int spines = 0;
            foreach (KeyValuePair<(int, int), (bool fork, List<ulong> props)> row in reference)
            {
                CourseLayout layout = CourseGenerator.Generate(row.Key.Item2, row.Key.Item1, CourseGeneratorTests.Settings);
                List<ulong> others = Others(layout, ref spines);
                if (!Same(others, row.Value.props))
                {
                    failures.AppendLine($"par {row.Key.Item1} seed {row.Key.Item2}: other props changed ({row.Value.props.Count} -> {others.Count})");
                }
            }

            Assert.Greater(spines, 0, "no spine cluster on seeds 0-49: the guard proves nothing");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        /// <summary>The same guard on seeds 0-199, against the same code with the spine theme off (<c>spineChance</c> 0).</summary>
        [Test]
        [Explicit("Full sweep, seeds 0-199: run by hand when a PR changes the prop rules and the director asks")]
        [Timeout(1800000)]
        public void SpineThemeLeavesOtherClustersBitIdentical_FullSweep()
        {
            LevelGenSettings off = NoSpine();
            var failures = new StringBuilder();
            int spines = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    List<ulong> others = Others(CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings), ref spines);
                    int none = 0;
                    List<ulong> bare = Others(CourseGenerator.Generate(seed, par, off), ref none);
                    Assert.AreEqual(0, none);
                    if (!Same(others, bare))
                    {
                        failures.AppendLine($"par {par} seed {seed}: other props changed");
                    }
                }
            }

            Object.DestroyImmediate(off);
            Assert.Greater(spines, 0);
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        /// <summary>
        /// Props per course per par with the spine theme and without it (variant 0, seeds 0-199, as PR 4.4 measured), the share of
        /// forks that get a spine cluster, props per spine and their kinds, and the seeds with spine props (for the phone review).
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Timeout(1800000)]
        public void SpinePropsMeasurement()
        {
            LevelGenSettings off = NoSpine();
            var report = new StringBuilder("SPINE_PROPS\n");
            foreach (int par in new[] { 3, 4, 5 })
            {
                int with = 0, without = 0, forks = 0, spined = 0, spineProps = 0;
                long onTicks = 0, offTicks = 0, forkOn = 0, forkOff = 0;
                var kinds = new Dictionary<PropKind, int>();
                var seeds = new StringBuilder();
                for (int seed = 0; seed < 200; seed++)
                {
                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    CourseLayout layout = CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings);
                    long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                    with += layout.Props.Count;
                    without += CourseGenerator.Generate(seed, par, off).Props.Count;
                    long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                    onTicks += t1 - t0;
                    offTicks += t2 - t1;
                    if (!PropReferenceTests.IsFork(layout))
                    {
                        continue;
                    }

                    forks++;
                    forkOn += t1 - t0;
                    forkOff += t2 - t1;
                    int here = 0;
                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Cluster == PropPlacement.SpineBase)
                        {
                            here++;
                            kinds[prop.Kind] = kinds.TryGetValue(prop.Kind, out int k) ? k + 1 : 1;
                        }
                    }

                    if (here > 0)
                    {
                        spined++;
                        spineProps += here;
                        seeds.Append($" {seed}({here})");
                    }
                }

                var kindText = new StringBuilder();
                foreach (KeyValuePair<PropKind, int> kind in kinds)
                {
                    kindText.Append($" {kind.Key} {kind.Value}");
                }

                report.Append($"par {par}: props/course {with / 200f:F2} (no spine {without / 200f:F2}); forks {forks}, with a spine cluster {spined}" +
                    $" ({(forks > 0 ? 100f * spined / forks : 0f):F1}%), props per spine {(spined > 0 ? spineProps / (float)spined : 0f):F2};{kindText}\n" +
                    $"  generation ms per course (Editor, bare + props): all {Ms(onTicks, 200)} (no spine {Ms(offTicks, 200)}), forks {Ms(forkOn, forks)} (no spine {Ms(forkOff, forks)})\n" +
                    $"  spine seeds:{seeds}\n");
            }

            Object.DestroyImmediate(off);
            Debug.Log(report.ToString());
        }

        private static string Ms(long ticks, int count)
        {
            return count > 0 ? (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / count).ToString("F1") : "-";
        }

        /// <summary>
        /// Why a spine is bare: over the held stretch of every fork (variant 0, seeds 0-199), points every 0.25 m along and 0.1 m
        /// across between the branch centres; the share that is rough ground off the fairway margin, and of those the share each
        /// keep-out takes (a point may fall in several), and the share free for a prop's foot. Per course, the widest free strip.
        /// </summary>
        [Test]
        [Explicit("Measurement")]
        [Timeout(1800000)]
        public void SpineRoomMeasurement()
        {
            LevelGenSettings settings = CourseGeneratorTests.Settings;
            var report = new StringBuilder("SPINE_ROOM\n");
            foreach (int par in new[] { 4, 5 })
            {
                int forks = 0, points = 0, ground = 0, free = 0, withRoom = 0;
                var taken = new Dictionary<string, int>();
                var fitting = new Dictionary<string, int>();
                var widest = new List<int>();
                for (int seed = 0; seed < 200; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, settings, 0, props: false);
                    FairwayShape? found = null;
                    foreach (FairwayShape shape in layout.Plan.FairwayShapes)
                    {
                        if (shape.Kind == FairwayShapeKind.Fork)
                        {
                            found = shape;
                        }
                    }

                    if (found == null)
                    {
                        continue;
                    }

                    forks++;
                    FairwayShape fork = found.Value;
                    var zones = new PropZones(layout, settings);
                    var fields = new CourseFields(layout);
                    float ease = settings.FairwayShapes.ForkEase;
                    float low = Mathf.Min(fork.SafeOffset, fork.RiskyOffset);
                    float high = Mathf.Max(fork.SafeOffset, fork.RiskyOffset);
                    int best = 0;
                    for (float s = fork.From + ease; s <= fork.To - ease; s += 0.25f)
                    {
                        Vector2 centre = layout.Centreline.PointAt(s);
                        Vector2 side = Geo.Left(Geo.PathDirection(layout.Centreline, s));
                        int run = 0;
                        for (float a = low + 0.1f; a < high; a += 0.1f)
                        {
                            points++;
                            Vector2 p = centre + (side * a);
                            if (fields.Surface(Geo.At(p)) != CourseSurface.Rough || zones.OverFairway(p, 0.05f))
                            {
                                run = 0;
                                continue;
                            }

                            ground++;
                            bool clear = true;
                            foreach (ShotLine line in layout.Plan.Lines)
                            {
                                if (Geo.DistanceToSegment(p, line.From, line.To) < zones.Corridor + 0.05f)
                                {
                                    string key = line.Kind + " corridor";
                                    taken[key] = taken.TryGetValue(key, out int n) ? n + 1 : 1;
                                    clear = false;
                                }
                            }

                            foreach (Vector2[] capsule in zones.Capsules)
                            {
                                if (PropZones.PolylineDistance(p, capsule) < zones.CapsuleHalfWidth + 0.05f)
                                {
                                    taken["capsule"] = taken.TryGetValue("capsule", out int n) ? n + 1 : 1;
                                    clear = false;
                                    break;
                                }
                            }

                            bool fits = zones.Fits(p, 0.05f, 0f, false);
                            if (clear && !fits)
                            {
                                taken["other"] = taken.TryGetValue("other", out int n) ? n + 1 : 1;
                            }

                            free += fits ? 1 : 0;
                            run = fits ? run + 1 : 0;
                            best = Mathf.Max(best, run);
                        }
                    }

                    widest.Add(best);
                    withRoom += best > 0 ? 1 : 0;

                    // Whether a prop of each kind's median reach and height (measured below on the course's own props) fits anywhere.
                    foreach (KeyValuePair<PropKind, Vector2> kind in MedianReach(par))
                    {
                        bool any = false;
                        for (float s = fork.From + ease; s <= fork.To - ease && !any; s += 0.25f)
                        {
                            Vector2 centre = layout.Centreline.PointAt(s);
                            Vector2 side = Geo.Left(Geo.PathDirection(layout.Centreline, s));
                            for (float a = low + 0.1f; a < high && !any; a += 0.1f)
                            {
                                any = zones.Fits(centre + (side * a), kind.Value.x, kind.Value.y, false);
                            }
                        }

                        string key = $"fits {kind.Key} r{kind.Value.x:F2} h{kind.Value.y:F2}";
                        fitting[key] = (fitting.TryGetValue(key, out int f) ? f : 0) + (any ? 1 : 0);
                    }
                }

                foreach (KeyValuePair<string, int> f in fitting)
                {
                    report.Append($"  {f.Key}: {f.Value}/{forks} forks\n");
                }

                widest.Sort();
                var text = new StringBuilder();
                foreach (KeyValuePair<string, int> t in taken)
                {
                    text.Append($" {t.Key} {100f * t.Value / Mathf.Max(1, ground):F0}%");
                }

                report.Append($"par {par}: {forks} forks; spine points {points}, rough off the margin {100f * ground / Mathf.Max(1, points):F0}%, taken:{text};" +
                    $" free for a foot {100f * free / Mathf.Max(1, ground):F1}%; forks with any free point {withRoom}; widest free strip (x0.1 m) median {(widest.Count > 0 ? widest[widest.Count / 2] : 0)}, max {(widest.Count > 0 ? widest[widest.Count - 1] : 0)}\n");
            }

            Debug.Log(report.ToString());
        }

        private static Dictionary<int, Dictionary<PropKind, Vector2>> _medianReach = new Dictionary<int, Dictionary<PropKind, Vector2>>();

        /// <summary>The median ground reach and height of bushes, rocks, cypresses and olives on this par's courses, seeds 0-49.</summary>
        private static Dictionary<PropKind, Vector2> MedianReach(int par)
        {
            if (_medianReach.TryGetValue(par, out Dictionary<PropKind, Vector2> cached))
            {
                return cached;
            }

            var radii = new Dictionary<PropKind, List<float>>();
            var heights = new Dictionary<PropKind, List<float>>();
            for (int seed = 0; seed < 50; seed++)
            {
                foreach (CourseProp prop in CourseGenerator.Generate(seed, par, CourseGeneratorTests.Settings).Props)
                {
                    if (prop.Kind != PropKind.Bush && prop.Kind != PropKind.Rock && prop.Kind != PropKind.Cypress && prop.Kind != PropKind.Olive)
                    {
                        continue;
                    }

                    var shapes = new List<PropShape>(prop.Shapes);
                    PropPlacement.Reach(prop.Root, prop.End, shapes, out float r, out float h);
                    if (!radii.ContainsKey(prop.Kind))
                    {
                        radii[prop.Kind] = new List<float>();
                        heights[prop.Kind] = new List<float>();
                    }

                    radii[prop.Kind].Add(r);
                    heights[prop.Kind].Add(h);
                }
            }

            var result = new Dictionary<PropKind, Vector2>();
            foreach (PropKind kind in radii.Keys)
            {
                radii[kind].Sort();
                heights[kind].Sort();
                result[kind] = new Vector2(radii[kind][radii[kind].Count / 2], heights[kind][heights[kind].Count / 2]);
            }

            _medianReach[par] = result;
            return result;
        }

        private static void SpinePropsStayOut(int seeds)
        {
            LevelGenSettings settings = CourseGeneratorTests.Settings;
            var failures = new StringBuilder();
            int spineProps = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, settings);
                    FairwayShape? fork = null;
                    foreach (FairwayShape shape in layout.Plan.FairwayShapes)
                    {
                        if (shape.Kind == FairwayShapeKind.Fork)
                        {
                            fork = shape;
                        }
                    }

                    foreach (CourseProp prop in layout.Props)
                    {
                        if (prop.Cluster != PropPlacement.SpineBase)
                        {
                            continue;
                        }

                        spineProps++;
                        if (fork == null)
                        {
                            failures.AppendLine($"par {par} seed {seed}: a spine prop on a course with no fork");
                            continue;
                        }

                        var root = new Vector2(prop.Root.x, prop.Root.z);
                        float along = layout.Centreline.Project(root, out _);
                        Vector2 centre = layout.Centreline.PointAt(along);
                        float across = Vector2.Dot(root - centre, Geo.Left(Geo.PathDirection(layout.Centreline, along)));
                        float ease = settings.FairwayShapes.ForkEase;
                        float low = Mathf.Min(fork.Value.SafeOffset, fork.Value.RiskyOffset);
                        float high = Mathf.Max(fork.Value.SafeOffset, fork.Value.RiskyOffset);
                        if (along < fork.Value.From + ease - 0.01f || along > fork.Value.To - ease + 0.01f || across <= low || across >= high)
                        {
                            failures.AppendLine($"par {par} seed {seed}: {prop.Kind} off the spine (along {along:F2}, across {across:F2})");
                        }

                        string broken = PropPlacementTests.BrokenKeepOut(layout, prop);
                        if (broken != null)
                        {
                            failures.AppendLine($"par {par} seed {seed}: {prop.Kind} at ({prop.Root.x:F2}, {prop.Root.z:F2}) breaks {broken}");
                        }
                    }
                }
            }

            Assert.Greater(spineProps, 0, "no spine prop: the rule proves nothing");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
        }

        /// <summary>The course's prop hashes, in order, leaving out its spine cluster (counted into <paramref name="spines"/>).</summary>
        /// <summary>
        /// The hashes of a course's props, in order, leaving out the strategic group (S2) and, with <paramref name="withoutSpine"/>, the
        /// spine cluster; <paramref name="spines"/> counts courses that have a spine cluster.
        /// </summary>
        internal static List<ulong> Others(CourseLayout layout, ref int spines, bool withoutSpine = true)
        {
            var others = new List<ulong>();
            bool spine = false;
            foreach (CourseProp prop in layout.Props)
            {
                if (prop.Strategic)
                {
                    continue;
                }

                if (prop.Cluster == PropPlacement.SpineBase && withoutSpine)
                {
                    spine = true;
                }
                else
                {
                    others.Add(CourseFingerprint.Prop(prop));
                }
            }

            spines += spine ? 1 : 0;
            return others;
        }

        /// <summary>The main settings with the spine theme off.</summary>
        private static LevelGenSettings NoSpine()
        {
            LevelGenSettings copy = Object.Instantiate(CourseGeneratorTests.Settings);
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("props.spineChance").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        internal static bool Same(List<ulong> a, List<ulong> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
