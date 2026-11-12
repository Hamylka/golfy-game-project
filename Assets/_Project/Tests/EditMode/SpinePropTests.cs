using System.Collections.Generic;
using System.Text;
using IsoGolf.Generation;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

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
                if (!Same(now.GetRange(4, now.Count - 4), row.Value.props))
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
