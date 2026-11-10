using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F2: where a generated fairway ends. Its tips are rounded (no flat cut at the tee or at the green), and by the green's guard it
    /// either runs into the green (guard beside it) or stops short behind a rough apron (guard in front, or an island green).
    /// </summary>
    public class FairwayJunctionTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        private static CourseLayout Course(int seed, int par) => CheckedCourse.Generate(seed, par, Settings, CourseCheckMeasurements.Shot).Layout;

        private static float Length(CourseLayout layout) => layout.Centreline.Length;

        private static Vector2 Approach(CourseLayout layout)
        {
            var points = layout.Centreline.Points;
            return (points[points.Count - 1] - points[points.Count - 2]).normalized;
        }

        /// <summary>True when the fairway stops short of the hole (the rough apron look), false when it runs into the green.</summary>
        private static bool IsApron(CourseLayout layout) => layout.Fairway.End < Length(layout) - 1f;

        [Test]
        public void TeeEnd_IsRoundedOnEveryCourse()
        {
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 30; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    var points = layout.Centreline.Points;
                    Vector2 dir = (points[1] - points[0]).normalized;
                    Vector2 left = Geo.Left(dir);

                    // 5 cm inside the start, a metre to the side (inside the strip's narrowest half width, 1.7 m): a flat cut holds this
                    // point, a rounded tip is already narrower than a metre there.
                    Vector2 p = points[0] + (dir * (layout.Fairway.Start + 0.05f)) + left;
                    Assert.That(new CourseFields(layout).Fairway(Geo.At(p)), Is.GreaterThan(0f), $"seed {seed} par {par}");
                }
            }
        }

        [Test]
        public void StripBetweenTipsKeepsItsWidth()
        {
            CourseLayout layout = Course(4, 4);
            var fields = new CourseFields(layout);
            var points = layout.Centreline.Points;
            Vector2 dir = (points[1] - points[0]).normalized;
            Vector2 p = points[0] + (dir * 8f);
            Assert.That(fields.Fairway(Geo.At(p)), Is.LessThan(-1.5f));
        }

        [Test]
        public void GuardBesideTheGreen_RunsIntoIt_WithNoRoughOnTheLine()
        {
            int beside = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 30; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    float angle = Vector2.Angle(layout.Plan.GuardDirection, -Approach(layout));
                    if (layout.Plan.IslandGreen || Geo.Cm(angle) < Geo.Cm(Settings.FairwayIntoGreenGuardAngle))
                    {
                        continue;
                    }

                    beside++;
                    Assert.That(IsApron(layout), Is.False, $"seed {seed} par {par}");
                    var fields = new CourseFields(layout);
                    float length = Length(layout);
                    for (float s = length - 8f; s <= length - 0.5f; s += 0.25f)
                    {
                        Vector2 p = layout.Centreline.PointAt(s);
                        Assert.That(fields.Surface(Geo.At(p)), Is.Not.EqualTo(CourseSurface.Rough), $"seed {seed} par {par} at {s:0.00} m");
                    }
                }
            }

            Assert.That(beside, Is.GreaterThan(5), "the survey has to meet courses with a guard beside the green");
        }

        [Test]
        public void GuardInFront_StopsShort_AndKeepsAnApron()
        {
            int front = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 30; seed++)
                {
                    CourseLayout layout = Course(seed, par);
                    float angle = Vector2.Angle(layout.Plan.GuardDirection, -Approach(layout));
                    bool expectApron = layout.Plan.IslandGreen || Geo.Cm(angle) < Geo.Cm(Settings.FairwayIntoGreenGuardAngle);
                    Assert.That(IsApron(layout), Is.EqualTo(expectApron), $"seed {seed} par {par}");
                    if (expectApron)
                    {
                        front++;
                        Assert.That(layout.Fairway.End, Is.EqualTo(Length(layout) - Settings.FairwayShortOfHole).Within(1e-4f));
                    }
                }
            }

            Assert.That(front, Is.GreaterThan(5));
        }

        [Test]
        public void BothLooksAppearOnPar4()
        {
            int apron = 0, into = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                if (IsApron(Course(seed, 4)))
                {
                    apron++;
                }
                else
                {
                    into++;
                }
            }

            Assert.That(apron, Is.GreaterThan(0));
            Assert.That(into, Is.GreaterThan(0));
        }

        [Test]
        public void Fairway_IsDeterministic()
        {
            CourseLayout a = Course(4, 4);
            CourseLayout b = Course(4, 4);
            Assert.That(b.Fairway.End, Is.EqualTo(a.Fairway.End));
            Assert.That(b.Fairway.EndCap, Is.EqualTo(a.Fairway.EndCap));
        }

        [Test, Explicit("Measurement: share of fairway looks per par, seeds 0-199")]
        public void LookShare()
        {
            var report = new StringBuilder();
            foreach (int par in new[] { 3, 4, 5 })
            {
                int apron = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    apron += IsApron(Course(seed, par)) ? 1 : 0;
                }

                report.Append($"par {par}: rough apron {apron}/200, runs into the green {200 - apron}/200\n");
            }

            Debug.Log(report.ToString());
        }
    }
}
