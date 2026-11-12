using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The seeded outline generator: determinism, lengths, shapes, gaps and the fairway along the centreline.</summary>
    public class CourseGeneratorTests
    {
        public const string SettingsPath = "Assets/_Project/Settings/LevelGenSettings.asset";

        // Enough seeds for the shares (dogleg, gap, island green) to sit well inside their bands.
        private const int ShareSeeds = 400;

        public static LevelGenSettings Settings => AssetDatabase.LoadAssetAtPath<LevelGenSettings>(SettingsPath);

        /// <summary>Every number in a layout, written out exactly, so two layouts compare as text.</summary>
        public static string Describe(CourseLayout layout)
        {
            var text = new StringBuilder();
            void Add(float value) => text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(' ');
            foreach (IslandOutline island in layout.Islands)
            {
                Add(island.CenterX); Add(island.CenterZ); Add(island.Heading); Add(island.HalfLength); Add(island.HalfWidth); Add(island.WidthWobble); Add(island.NoiseOffset);
            }

            foreach (Vector2 point in layout.Centreline.Points)
            {
                Add(point.x); Add(point.y);
            }

            Add(layout.Fairway.Start); Add(layout.Fairway.End); Add(layout.Fairway.NoiseOffset); Add(layout.Relief.NoiseOffset);
            foreach (GreenLayout green in layout.Greens)
            {
                Add(green.Outline.Center.x); Add(green.Outline.Center.y);
            }

            foreach (EllipseShape bunker in layout.Bunkers)
            {
                Add(bunker.Center.x); Add(bunker.Center.y); Add(bunker.Radius.x); Add(bunker.Radius.y); Add(bunker.Wobble); Add(bunker.Heading);
            }

            foreach (PondLayout pond in layout.Ponds)
            {
                Add(pond.Outline.Center.x); Add(pond.Outline.Center.y); Add(pond.Outline.Radius.x); Add(pond.Outline.Radius.y); Add(pond.Outline.Wobble); Add(pond.Outline.Heading); Add(pond.WaterLevel);
            }

            if (layout.Plan != null)
            {
                foreach (HazardInfo hazard in layout.Plan.Hazards)
                {
                    text.Append((int)hazard.Role).Append(hazard.IsWater ? 'w' : 's').Append(hazard.Index).Append(' ');
                }

                foreach (HazardRole missing in layout.Plan.MissingRoles)
                {
                    text.Append("missing").Append((int)missing).Append(' ');
                }

                foreach (Vector2 spot in layout.Plan.SafeRoute)
                {
                    Add(spot.x); Add(spot.y);
                }
            }

            return text.ToString();
        }

        /// <summary>Stretches of the centreline (in metres from the tee, tee to hole) that lie off every island.</summary>
        public static List<Vector2> Gaps(CourseLayout layout)
        {
            var fields = new CourseFields(layout);
            var gaps = new List<Vector2>();
            const float Step = 0.05f;
            float length = layout.Centreline.Length;
            float start = -1f;
            for (float s = 0f; s <= length; s += Step)
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

        /// <summary>The headings of the centreline's legs, in degrees from +X.</summary>
        public static List<float> Headings(CourseLayout layout)
        {
            var headings = new List<float>();
            IReadOnlyList<Vector2> points = layout.Centreline.Points;
            for (int i = 1; i < points.Count; i++)
            {
                Vector2 leg = points[i] - points[i - 1];
                headings.Add(Mathf.Atan2(leg.y, leg.x) * Mathf.Rad2Deg);
            }

            return headings;
        }

        private static float MaxTurn(CourseLayout layout)
        {
            List<float> headings = Headings(layout);
            float turn = 0f;
            for (int i = 1; i < headings.Count; i++)
            {
                turn = Mathf.Max(turn, Mathf.Abs(Mathf.DeltaAngle(headings[i - 1], headings[i])));
            }

            return turn;
        }

        [TestCase(3, 7)]
        [TestCase(4, 12345)]
        [TestCase(5, -9)]
        public void SameSeed_GivesTheSameCourse(int par, int seed)
        {
            string first = Describe(CourseGenerator.Generate(seed, par, Settings));
            string second = Describe(CourseGenerator.Generate(seed, par, Settings));
            Assert.That(second, Is.EqualTo(first));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void OtherSeedOrVariant_GivesAnotherCourse(int par)
        {
            string course = Describe(CourseGenerator.Generate(1, par, Settings));
            Assert.That(Describe(CourseGenerator.Generate(2, par, Settings)), Is.Not.EqualTo(course));
            Assert.That(Describe(CourseGenerator.Generate(1, par, Settings, 1)), Is.Not.EqualTo(course));
        }

        [Test]
        public void Seed_IsPinned()
        {
            // A seed's course may change with the generator until release; when it does, this pin is updated on purpose.
            CourseLayout layout = CourseGenerator.Generate(42, 4, Settings);
            Debug.Log($"Seed 42, par 4: {Describe(layout)}");

            // A soft bend of 4 degrees, two islands, no gap.
            Assert.That(layout.Centreline.Length, Is.EqualTo(51.11f).Within(0.001f));
            Assert.That(layout.Centreline.Points.Count, Is.EqualTo(3));
            Assert.That(layout.Islands.Count, Is.EqualTo(2));
            Assert.That(layout.Tee.x, Is.EqualTo(-26.0391f).Within(0.001f));
            Assert.That(layout.Tee.y, Is.EqualTo(0.6059f).Within(0.001f));
            Assert.That(layout.Hole.x, Is.EqualTo(25.0397f).Within(0.001f));
            Assert.That(layout.Hole.y, Is.EqualTo(0.0708f).Within(0.001f));
            Assert.That(layout.Islands[0].HalfWidth, Is.EqualTo(4.75f).Within(0.001f));
            Assert.That(Gaps(layout), Is.Empty);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Length_TeeToHole_StaysInItsRange(int par)
        {
            Length_TeeToHole_StaysInItsRange_Run(par, 50);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void Length_TeeToHole_StaysInItsRange_FullSweep(int par)
        {
            Length_TeeToHole_StaysInItsRange_Run(par, 200);
        }

        private void Length_TeeToHole_StaysInItsRange_Run(int par, int seeds)
        {
            Vector2 range = Settings.Rules(par).Length;
            for (int seed = 0; seed < seeds; seed++)
            {
                float length = CourseGenerator.Generate(seed, par, Settings).Centreline.Length;
                Assert.That(length, Is.InRange(range.x - 0.001f, range.y + 0.001f), $"seed {seed}");
            }
        }

        [Test]
        public void ParThree_IsStraightAlongX()
        {
            ParThree_IsStraightAlongX_Run(50);
        }

        [Test]
        [Explicit("Full sweep over seeds 0-199, run by hand before a merge")]
        public void ParThree_IsStraightAlongX_FullSweep()
        {
            ParThree_IsStraightAlongX_Run(200);
        }

        private void ParThree_IsStraightAlongX_Run(int seeds)
        {
            for (int seed = 0; seed < seeds; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, 3, Settings);
                Assert.That(layout.Centreline.Points.Count, Is.EqualTo(2), $"seed {seed}");
                Assert.That(layout.Hole.y, Is.EqualTo(layout.Tee.y), $"seed {seed}");
                Assert.That(layout.Hole.x, Is.GreaterThan(layout.Tee.x), $"seed {seed}");
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        public void EveryLeg_KeepsHeadingTowardPlusX(int par)
        {
            for (int seed = 0; seed < ShareSeeds; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                foreach (float heading in Headings(layout))
                {
                    Assert.That(Mathf.Abs(heading), Is.LessThanOrEqualTo(Settings.MaxHeading + 0.01f), $"seed {seed}");
                }

                Assert.That(MaxTurn(layout), Is.LessThanOrEqualTo(Settings.DoglegTurn.y + 0.01f), $"seed {seed}");
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        public void AboutHalf_AreDoglegs_TheOthersBendSoftly(int par)
        {
            int doglegs = 0, twice = 0;
            for (int seed = 0; seed < ShareSeeds; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                float turn = MaxTurn(layout);
                if (turn >= Settings.DoglegTurn.x - 0.01f)
                {
                    doglegs++;
                    twice += layout.Centreline.Points.Count == 4 ? 1 : 0;
                }
                else
                {
                    Assert.That(turn, Is.InRange(Settings.SoftTurn.x - 0.01f, Settings.SoftTurn.y + 0.01f), $"seed {seed}: a course that isn't a dogleg bends softly");
                }
            }

            Debug.Log($"Par {par}: {doglegs} doglegs in {ShareSeeds}, {twice} turning twice.");
            Assert.That(doglegs, Is.InRange(ShareSeeds * 0.4f, ShareSeeds * 0.6f));
            if (par == 5)
            {
                Assert.That(twice, Is.InRange(doglegs * 0.35f, doglegs * 0.65f));
            }
            else
            {
                Assert.That(twice, Is.Zero);
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        public void AboutOneInThree_HasAGap_FourToTenMetres_NeverByTheTee(int par)
        {
            int withGap = 0;
            for (int seed = 0; seed < ShareSeeds; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                List<Vector2> gaps = Gaps(layout);
                Assert.That(gaps.Count, Is.LessThanOrEqualTo(1), $"seed {seed}");
                if (gaps.Count == 0)
                {
                    continue;
                }

                withGap++;
                float width = gaps[0].y - gaps[0].x;
                Assert.That(width, Is.InRange(Settings.GapWidth.x - 0.1f, Settings.GapWidth.y + 0.1f), $"seed {seed}");
                Assert.That(gaps[0].x, Is.GreaterThanOrEqualTo(Settings.GapFromTee - 0.1f), $"seed {seed}");
                Assert.That(gaps[0].y, Is.LessThanOrEqualTo(layout.Centreline.Length - Settings.GapFromHole + 0.1f), $"seed {seed}");
            }

            Debug.Log($"Par {par}: {withGap} gaps in {ShareSeeds}.");
            Assert.That(withGap, Is.InRange(ShareSeeds * 0.25f, ShareSeeds * 0.42f));
        }

        [Test]
        public void ParThree_NowAndThen_HasAnIslandGreen()
        {
            int islandGreens = 0;
            for (int seed = 0; seed < ShareSeeds; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, 3, Settings);
                List<Vector2> gaps = Gaps(layout);
                Assert.That(gaps.Count, Is.LessThanOrEqualTo(1), $"seed {seed}");
                if (gaps.Count == 0)
                {
                    continue;
                }

                islandGreens++;
                Assert.That(gaps[0].y - gaps[0].x, Is.InRange(Settings.IslandGreenGapWidth.x - 0.1f, Settings.IslandGreenGapWidth.y + 0.1f), $"seed {seed}");
                Assert.That(gaps[0].x, Is.GreaterThanOrEqualTo(Settings.IslandGreenFromTee - 0.1f), $"seed {seed}");
                Assert.That(gaps[0].y, Is.EqualTo(layout.Centreline.Length - Settings.IslandGreenBeforeHole).Within(0.1f), $"seed {seed}");
            }

            Debug.Log($"Par 3: {islandGreens} island greens in {ShareSeeds}.");
            Assert.That(islandGreens, Is.InRange(ShareSeeds * 0.07f, ShareSeeds * 0.25f));
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void TeeHoleAndGreen_AreOnLand_TheFairwayRunsAlongTheCentreline(int par)
        {
            for (int seed = 0; seed < 100; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                var fields = new CourseFields(layout);
                Vector3 tee = new Vector3(layout.Tee.x, 0f, layout.Tee.y);
                Vector3 hole = new Vector3(layout.Hole.x, 0f, layout.Hole.y);
                Assert.That(fields.Surface(tee), Is.EqualTo(CourseSurface.Fairway), $"seed {seed}: tee");
                Assert.That(fields.Surface(hole), Is.EqualTo(CourseSurface.Green), $"seed {seed}: hole");

                // The fairway starts inside the first island's tip, behind the tee, with ground around it.
                Vector2 start = layout.Centreline.PointAt(layout.Fairway.Start);
                Assert.That(fields.Island(new Vector3(start.x, 0f, start.y)), Is.LessThan(-1f), $"seed {seed}: fairway start");

                // Every point of the centreline from the tee to the green's front is fairway where there is land, except under a
                // fairway shape (F3.2, F3.3), which puts rough there by design: a rough island's oval, a split's band and the tips
                // either side of it, a fork's spine.
                for (float s = 0f; s < layout.Fairway.End; s += 0.5f)
                {
                    bool underShape = false;
                    foreach (FairwayShape shape in layout.Plan.FairwayShapes)
                    {
                        float reach = shape.Kind == FairwayShapeKind.Split ? Settings.FairwayCapDepth : 0f;
                        underShape |= s >= shape.From - reach && s <= shape.To + reach;
                    }

                    if (underShape)
                    {
                        continue;
                    }

                    Vector2 p = layout.Centreline.PointAt(s);
                    CourseSurface surface = fields.Surface(new Vector3(p.x, 0f, p.y));
                    Assert.That(surface, Is.EqualTo(CourseSurface.Fairway).Or.EqualTo(CourseSurface.OffIsland).Or.EqualTo(CourseSurface.Green).Or.EqualTo(CourseSurface.Bunker).Or.EqualTo(CourseSurface.Water), $"seed {seed} at {s} m");
                }

                // And the fairway's width, across the centreline, is the same on every leg.
                Vector2 middle = layout.Centreline.PointAt(layout.Centreline.Length * 0.5f);
                float across = layout.Fairway.HalfWidth - (layout.Fairway.Wobble * 0.5f) - 0.05f;
                List<float> headings = Headings(layout);
                float heading = headings[headings.Count / 2] * Mathf.Deg2Rad;
                Vector2 side = new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading)) * across;
                if (Gaps(layout).Count == 0 && layout.Centreline.Points.Count == 2)
                {
                    Assert.That(fields.Fairway(new Vector3(middle.x + side.x, 0f, middle.y + side.y)), Is.LessThan(0f), $"seed {seed}");
                }
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Course_IsCentredOnTheOrigin_AndBuildsAsTerrain(int par)
        {
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            for (int seed = 0; seed < 12; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                TerrainResult terrain = TerrainBuilder.Build(layout, palette);
                Assert.That(terrain.Land.TriangleCount, Is.GreaterThan(0), $"seed {seed}");
                Assert.That(terrain.Water.TriangleCount > 0, Is.EqualTo(layout.Ponds.Count > 0), $"seed {seed}: water only where there is a pond");

                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (EdgeSegment edge in terrain.Edge)
                {
                    Assert.That(edge.Inner == EdgeSurface.Water ? layout.Ponds.Count : 1, Is.GreaterThan(0), $"seed {seed}: water edges only where there is a pond");
                    minX = Mathf.Min(minX, edge.A.x);
                    maxX = Mathf.Max(maxX, edge.A.x);
                    minZ = Mathf.Min(minZ, edge.A.z);
                    maxZ = Mathf.Max(maxZ, edge.A.z);
                }

                // Centred within the wobble: the box is laid around the widest the islands can reach.
                Assert.That(Mathf.Abs(minX + maxX), Is.LessThan(4f), $"seed {seed}");
                Assert.That(Mathf.Abs(minZ + maxZ), Is.LessThan(4f), $"seed {seed}");
            }
        }

        [Test]
        public void HandMadeCourses_KeepTheAxisFairway_ButTheDogleg()
        {
            foreach (string path in CameraCourse.ScenePaths)
            {
                CourseLayout layout = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(path).ToLayout();
                Assert.That(layout.AlongCentreline, Is.EqualTo(path == CameraCourse.DoglegPath), path);
                Assert.That(layout.Islands[0].NoiseOffset, Is.Zero, path);
            }
        }

        [Test]
        public void DoglegFixture_FairwayFollowsBothLegs()
        {
            CourseLayout layout = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(CameraCourse.DoglegPath).ToLayout();
            var fields = new CourseFields(layout);
            for (float s = 0f; s < layout.Fairway.End - 1f; s += 0.5f)
            {
                Vector2 p = layout.Centreline.PointAt(s);
                Assert.That(fields.Fairway(new Vector3(p.x, 0f, p.y)), Is.LessThan(0f), $"{s} m along");
            }
        }

        [Test]
        public void CoursePath_ProjectsAlongAndAcross()
        {
            var path = new CoursePath(new[] { Vector2.zero, new Vector2(10f, 0f), new Vector2(20f, 10f) });
            Assert.That(path.Project(new Vector2(5f, 2f), out float across), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(across, Is.EqualTo(2f).Within(1e-4f), "left of +X is +Z");
            Assert.That(path.Project(new Vector2(-3f, -1f), out across), Is.EqualTo(-3f).Within(1e-4f), "the first leg runs on behind the start");
            Assert.That(across, Is.EqualTo(-1f).Within(1e-4f));
            Vector2 second = path.PointAt(10f + 5f);
            Assert.That(second.x, Is.EqualTo(10f + (5f / Mathf.Sqrt(2f))).Within(1e-4f));
            Assert.That(path.Project(second, out across), Is.EqualTo(15f).Within(1e-3f));
            Assert.That(across, Is.EqualTo(0f).Within(1e-3f));
            Vector2 past = path.PointAt(path.Length + 2f);
            Assert.That(Vector2.Distance(past, new Vector2(20f, 10f)), Is.EqualTo(2f).Within(1e-4f), "the last leg runs on past the end");
        }
    }
}
