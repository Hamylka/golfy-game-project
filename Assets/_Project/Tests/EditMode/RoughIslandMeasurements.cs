using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.2 measurements, quoted in the PR (not gates): how often each shape is drawn and placed per par (so how often each window
    /// yields a place), the shaped share of the checked courses against the plan's targets, and which pinned courses moved. The reroll
    /// survey and the ace sweep are <see cref="CourseCheckMeasurements"/>' own.
    /// </summary>
    public class RoughIslandMeasurements
    {
        [Test, Explicit("Measurement"), Timeout(3600000)]
        public void ShapeShares()
        {
            const int Seeds = 200;
            LevelGenSettings settings = CourseGeneratorTests.Settings;
            var report = new StringBuilder($"FAIRWAY_SHAPE_SHARES seeds 0-{Seeds - 1}\n");
            foreach (int par in new[] { 3, 4, 5 })
            {
                var drawn = new Dictionary<FairwayShapeKind, int>();
                var placed = new Dictionary<FairwayShapeKind, int>();
                var placedDogleg = new Dictionary<FairwayShapeKind, int>();
                var drawnDogleg = new Dictionary<FairwayShapeKind, int>();
                int secondDrawn = 0, secondPlaced = 0, shapedBare = 0, shapedChecked = 0, checkedIsland = 0, checkedSplit = 0;
                long ticks = 0;
                var rejections = new SortedDictionary<string, int>();
                foreach (FairwayShapeKind k in new[] { FairwayShapeKind.RoughIsland, FairwayShapeKind.Split, FairwayShapeKind.Fork })
                {
                    drawn[k] = placed[k] = placedDogleg[k] = drawnDogleg[k] = 0;
                }

                for (int seed = 0; seed < Seeds; seed++)
                {
                    var watch = Stopwatch.StartNew();
                    CourseLayout layout = CourseGenerator.GenerateBare(seed, par, settings, 0, out FairwayShapeResult shapes);
                    ticks += watch.ElapsedTicks;
                    foreach (KeyValuePair<string, int> pair in shapes.Rejections)
                    {
                        rejections.TryGetValue(pair.Key, out int n);
                        rejections[pair.Key] = n + pair.Value;
                    }

                    bool dogleg = layout.Plan.Dogleg;
                    if (shapes.FirstDrawn != null)
                    {
                        FairwayShapeKind k = shapes.FirstDrawn.Value;
                        drawn[k]++;
                        drawnDogleg[k] += dogleg ? 1 : 0;
                    }

                    secondDrawn += shapes.SecondDrawn != null && shapes.SecondDrawn != FairwayShapeKind.Fork ? 1 : 0;
                    foreach (FairwayShape shape in layout.Plan.FairwayShapes)
                    {
                        // The first shape's window starts at the tee; a second (par 5) lies a full reach further on.
                        bool second = shapes.SecondDrawn != null && shape.From > settings.FullReach + 10f;
                        if (second)
                        {
                            secondPlaced++;
                        }
                        else
                        {
                            placed[shape.Kind]++;
                            placedDogleg[shape.Kind] += dogleg ? 1 : 0;
                        }
                    }

                    shapedBare += layout.Plan.FairwayShapes.Count > 0 ? 1 : 0;

                    CheckedCourseResult result = CheckedCourse.Generate(seed, par, settings, CourseCheckMeasurements.Shot);
                    IReadOnlyList<FairwayShape> kept = result.Layout.Plan.FairwayShapes;
                    shapedChecked += kept.Count > 0 ? 1 : 0;
                    foreach (FairwayShape shape in kept)
                    {
                        checkedIsland += shape.Kind == FairwayShapeKind.RoughIsland ? 1 : 0;
                        checkedSplit += shape.Kind == FairwayShapeKind.Split ? 1 : 0;
                    }
                }

                FairwayShapeWeights w = settings.FairwayShapes.Weights(par);
                report.Append($"par {par} (weights plain/island/split/fork {w.Plain}/{w.RoughIsland}/{w.Split}/{w.Fork}):\n");
                foreach (FairwayShapeKind k in new[] { FairwayShapeKind.RoughIsland, FairwayShapeKind.Split })
                {
                    report.Append($"  {k}: drawn {drawn[k]}, placed {placed[k]} ({Share(placed[k], drawn[k])} of draws yield a place; " +
                        $"doglegs {placedDogleg[k]}/{drawnDogleg[k]}, others {placed[k] - placedDogleg[k]}/{drawn[k] - drawnDogleg[k]}), " +
                        $"share of courses {Share(placed[k], Seeds)} against {Share(k == FairwayShapeKind.RoughIsland ? w.RoughIsland : w.Split, w.Total)}\n");
                }

                report.Append($"  fork draws (plain until F3.3): {drawn[FairwayShapeKind.Fork]}\n");
                if (par == 5)
                {
                    report.Append($"  second shape: drawn {secondDrawn}, placed {secondPlaced}\n");
                }

                var why = new StringBuilder();
                foreach (KeyValuePair<string, int> pair in rejections)
                {
                    why.Append($"{pair.Key} {pair.Value}; ");
                }

                report.Append($"  failed tries: {why}\n");
                report.Append($"  variant 0 with a shape: {shapedBare}/{Seeds} ({Share(shapedBare, Seeds)}); checked courses with a shape: {shapedChecked}/{Seeds} " +
                    $"(rough islands {checkedIsland}, splits {checkedSplit}); bare generation {ticks * 1000.0 / Stopwatch.Frequency / Seeds:0.0} ms per course (Editor)\n");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Which pinned courses changed: each one's checked course is hashed and compared with <see cref="PlainCourseReference"/> (taken
        /// before F3.1), and its shapes are listed. A pin that moved needs re-pinning, keeping its purpose.
        /// </summary>
        [Test, Explicit("Measurement"), Timeout(1800000)]
        public void MovedPins()
        {
            var pins = new (int Seed, int Par, string Purpose)[]
            {
                (1, 3, "MainSceneTests: Main's seed and par"),
                (6, 5, "CameraHeadingTests: F1 par 5 seed 6 heading"),
                (33, 5, "CameraCourse.LongPath: CameraPathTests, ShadowDistanceTests (phone par 5)"),
                (120, 5, "PR 4.4 captures (bucket, lighthouse)"),
                (103, 5, "PR 4.4 phone review (lighthouse)"),
                (4, 4, "CourseCheckTests: seed 4 par 4"),
                (0, 3, "CourseCheckTests / PropPlacementTests: seed 0 par 3"),
                (42, 4, "CourseGeneratorTests: seed 42 par 4"),
            };

            var report = new StringBuilder("MOVED_PINS (checked courses against the pre-F3.1 reference)\n");
            foreach (var pin in pins)
            {
                CheckedCourseResult result = CheckedCourse.Generate(pin.Seed, pin.Par, CourseGeneratorTests.Settings, CourseCheckMeasurements.Shot);
                ulong[] now = PlainCourseReferenceTests.Hashes(result);
                int index = (((pin.Par - 3) * PlainCourseReferenceTests.ReferenceSeeds) + pin.Seed) * 3;
                bool same = now[0] == PlainCourseReference.Generated[index] && now[1] == PlainCourseReference.Generated[index + 1] && now[2] == PlainCourseReference.Generated[index + 2];
                var shapes = new StringBuilder();
                foreach (FairwayShape shape in result.Layout.Plan.FairwayShapes)
                {
                    shapes.Append($"{shape.Kind} {shape.From:F1}-{shape.To:F1} ");
                }

                report.Append($"par {pin.Par} seed {pin.Seed} ({pin.Purpose}): {(same ? "unchanged" : "MOVED")}, variant {result.Variant}, shapes: {(shapes.Length > 0 ? shapes.ToString() : "none")}\n");
            }

            Debug.Log(report.ToString());
        }

        private static string Share(int count, int total) => total <= 0 ? "-" : $"{100f * count / total:0.0}%";
    }
}
