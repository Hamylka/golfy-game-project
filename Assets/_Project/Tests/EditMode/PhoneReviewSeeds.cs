using System.Text;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace IsoGolf.Tests
{
    /// <summary>
    /// F3.4's phone review: the fairway shapes of each checked course (the variant the game plays), seeds 0-49 of each par, with
    /// where each shape starts from the tee and how many props stand on a fork's spine. Read to pick the APK's seed list.
    /// </summary>
    public class PhoneReviewSeeds
    {
        [Test]
        [Explicit("Measurement")]
        [Timeout(1800000)]
        public void FairwayShapesOfCheckedCourses()
        {
            var report = new StringBuilder("PHONE_SEEDS\n");
            foreach (int par in new[] { 3, 4, 5 })
            {
                report.Append($"par {par}:");
                for (int seed = 0; seed < 50; seed++)
                {
                    CheckedCourseResult result = CheckedCourse.Generate(seed, par, CourseGeneratorTests.Settings, CourseCheckMeasurements.Shot);
                    CourseLayout layout = result.Layout;
                    if (layout.Plan == null || layout.Plan.FairwayShapes.Count == 0)
                    {
                        continue;
                    }

                    int spine = 0;
                    foreach (CourseProp prop in layout.Props)
                    {
                        spine += prop.Cluster == PropPlacement.SpineBase ? 1 : 0;
                    }

                    report.Append($" {seed}v{result.Variant}[");
                    foreach (FairwayShape shape in layout.Plan.FairwayShapes)
                    {
                        string kind = shape.Kind == FairwayShapeKind.RoughIsland ? "I" : (shape.Kind == FairwayShapeKind.Split ? "S" : "F");
                        report.Append($"{kind}{shape.From:F0} ");
                    }

                    report.Append(spine > 0 ? $"sp{spine}]" : "]");
                }

                report.Append("\n");
            }

            Debug.Log(report.ToString());
        }
    }
}
