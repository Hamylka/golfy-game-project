using System.Collections.Generic;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// M4 PR 4.6: the perched birds. Their perches are a view-time layer (nothing in generation reads them, so props stay bit-identical,
    /// which <see cref="StrategicPropTests.OtherPropsBitIdentical"/> checks), deterministic from (seed, par, variant), and never in a
    /// hazard or on the green; the flight is a small state machine driven by the ball's position.
    /// </summary>
    public class BirdTests
    {
        private static LevelGenSettings Settings => CourseGeneratorTests.Settings;

        private BirdSettings _birds;
        private PaletteSettings _palette;

        [SetUp]
        public void SetUp()
        {
            _birds = ScriptableObject.CreateInstance<BirdSettings>();
            _palette = ScriptableObject.CreateInstance<PaletteSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_birds);
            Object.DestroyImmediate(_palette);
        }

        private static string Describe(List<BirdPerch> perches)
        {
            var text = new System.Text.StringBuilder();
            foreach (BirdPerch p in perches)
            {
                text.Append($"{Mathf.RoundToInt(p.Position.x * 100)},{Mathf.RoundToInt(p.Position.y * 100)},{Mathf.RoundToInt(p.Position.z * 100)},{p.YawDegrees},{p.PathDegrees},{p.TakeOffDelay},{p.ReturnDelay},{p.Phase};");
            }

            return text.ToString();
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void SameSeed_GivesTheSamePerchesAndPaths(int par)
        {
            int different = 0;
            string previous = null;
            for (int seed = 0; seed < 20; seed++)
            {
                CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                string a = Describe(BirdPerches.Place(layout, seed, par, 0, _birds));
                string b = Describe(BirdPerches.Place(CourseGenerator.Generate(seed, par, Settings), seed, par, 0, _birds));
                Assert.AreEqual(a, b, $"par {par} seed {seed}");
                if (previous != null && a != previous)
                {
                    different++;
                }

                previous = a;
            }

            Assert.Greater(different, 10, "different seeds should give different flocks");
        }

        [Test]
        public void Perches_AreInTheCountRange_AndNeverInAHazardOrOnTheGreen()
        {
            int courses = 0;
            int onProps = 0;
            int total = 0;
            foreach (int par in new[] { 3, 4, 5 })
            {
                for (int seed = 0; seed < 50; seed++)
                {
                    CourseLayout layout = CourseGenerator.Generate(seed, par, Settings);
                    var fields = new CourseFields(layout);
                    List<BirdPerch> perches = BirdPerches.Place(layout, seed, par, 0, _birds);
                    if (perches.Count == 0)
                    {
                        continue;
                    }

                    courses++;
                    Assert.That(perches.Count, Is.InRange(_birds.Count.x, _birds.Count.y), $"par {par} seed {seed}");
                    foreach (BirdPerch perch in perches)
                    {
                        total++;
                        var foot = new Vector3(perch.Position.x, 0f, perch.Position.z);
                        CourseSurface surface = fields.Surface(foot);
                        Assert.That(surface == CourseSurface.Rough || surface == CourseSurface.Fairway, $"par {par} seed {seed}: {surface}");
                        Assert.GreaterOrEqual(Vector2.Distance(new Vector2(foot.x, foot.z), layout.Tee), _birds.TeeClearance - 0.5f, $"par {par} seed {seed} near the tee");
                        Assert.LessOrEqual(perch.Position.x, layout.Hole.x + 0.5f, $"par {par} seed {seed} past the hole");

                        // Clear ground all round: no green, hazard or void within the clearance (a prop's top is up to its radius off its foot,
                        // so the ring is tested at the perch itself, which is a little stricter than at the foot).
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI * 0.25f;
                            Vector3 q = foot + (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (_birds.PerchClearance * 0.5f));
                            CourseSurface around = fields.Surface(q);
                            Assert.AreNotEqual(CourseSurface.Green, around, $"par {par} seed {seed} green beside a perch");
                            Assert.AreNotEqual(CourseSurface.Water, around, $"par {par} seed {seed} water beside a perch");
                            Assert.AreNotEqual(CourseSurface.Bunker, around, $"par {par} seed {seed} bunker beside a perch");
                        }

                        float ground = fields.Height(foot.x, foot.z);
                        if (perch.Position.y > ground + 0.2f)
                        {
                            onProps++;
                        }
                    }
                }
            }

            Assert.Greater(courses, 100, "most courses have a flock");
            // A rock or a wall top stands under 0.2 m over the ground, so this undercounts: it only shows props are the main perch.
            TestContext.Out.WriteLine($"{courses} flocks, {total} birds, {onProps} over 0.2 m above the ground");
            Assert.Greater(onProps, total / 4, "birds perch on props first");
        }

        [Test]
        public void BirdsStream_DoesNotMoveAnyProp()
        {
            // Placement reads nothing of the birds: the props of a course are the same whether or not a flock is placed.
            CourseLayout layout = CourseGenerator.Generate(33, 5, Settings);
            var before = new List<Vector3>();
            foreach (CourseProp prop in layout.Props)
            {
                before.Add(prop.Root);
            }

            BirdPerches.Place(layout, 33, 5, 0, _birds);
            Assert.AreEqual(before.Count, layout.Props.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i], layout.Props[i].Root);
            }

            Assert.AreEqual(9, BirdPerches.BirdsStream);
        }

        private static BirdPerch Perch(Vector3 at, float path = 0f) => new BirdPerch(at, 90f, path, 0f, 10f, 0);

        private static readonly Vector3 FarBall = new Vector3(100f, 0f, 100f);

        [Test]
        public void Trigger_NeedsAMovingBallWithinTheRadius()
        {
            var bird = new BirdFlight(Perch(Vector3.zero), _birds);
            float radius = _birds.TriggerRadius;

            bird.Update(0.1f, new Vector3(radius - 0.1f, 0f, 0f), false);
            Assert.AreEqual(BirdState.Perched, bird.State, "a resting ball does not trigger");

            bird.Update(0.1f, new Vector3(radius + 0.1f, 0f, 0f), true);
            Assert.AreEqual(BirdState.Perched, bird.State, "outside the radius");

            bird.Update(0.1f, new Vector3(radius - 0.1f, 5f, 0f), true);
            Assert.AreEqual(BirdState.TakeOff, bird.State, "inside, height ignored");
        }

        [Test]
        public void StateMachine_PerchedTakeOffAwayReturnPerched()
        {
            var bird = new BirdFlight(Perch(new Vector3(2f, 1f, 3f)), _birds);
            Vector3 near = new Vector3(3f, 0f, 3f);
            bird.Update(0.02f, near, true);
            Assert.AreEqual(BirdState.TakeOff, bird.State);

            float flight = _birds.PathLength / _birds.FlightSpeed;
            float seen = 0f;
            bool lifted = false;
            while (bird.State == BirdState.TakeOff && seen < flight + 5f)
            {
                bird.Update(0.05f, FarBall, false);
                seen += 0.05f;
                lifted |= bird.Pose.Position.y > 2f;
            }

            Assert.AreEqual(BirdState.Away, bird.State);
            Assert.IsTrue(lifted, "it climbs");
            Assert.IsFalse(bird.Pose.Visible, "gone, shrunk to nothing, when away");

            // It waits out its return delay, and while the ball is close.
            bird.Update(5f, FarBall, false);
            Assert.AreEqual(BirdState.Away, bird.State);
            bird.Update(6f, near, false);
            Assert.AreEqual(BirdState.Away, bird.State, "the ball is near: it waits");
            bird.Update(0.1f, FarBall, false);
            Assert.AreEqual(BirdState.Return, bird.State);

            for (int i = 0; i < 400 && bird.State == BirdState.Return; i++)
            {
                bird.Update(0.05f, FarBall, false);
            }

            Assert.AreEqual(BirdState.Perched, bird.State);
            Assert.AreEqual(new Vector3(2f, 1f, 3f), bird.Pose.Position, "back on the same perch");
        }

        [Test]
        public void Flight_NeverPopsNearTheCourse_AndPathIsAPureFunctionOfTime()
        {
            // Run twice with different step sizes: the same path, and the bird is always either full size or shrinking, never
            // vanishing close to its perch.
            Vector3[] ends = new Vector3[2];
            for (int run = 0; run < 2; run++)
            {
                float dt = run == 0 ? 0.01f : 0.05f;
                var bird = new BirdFlight(Perch(Vector3.zero, 40f), _birds);
                bird.Update(dt, new Vector3(1f, 0f, -1f), true);
                float t = 0f;
                while (t < 1.6f)
                {
                    bird.Update(dt, FarBall, false);
                    t += dt;
                    float fromPerch = Vector2.Distance(Vector2.zero, new Vector2(bird.Pose.Position.x, bird.Pose.Position.z));
                    if (!bird.Pose.Visible)
                    {
                        Assert.Greater(fromPerch, _birds.PathLength * 0.9f, "it only disappears at the far end of its path");
                    }
                }

                ends[run] = bird.Pose.Position;
            }

            Assert.AreEqual(ends[0].x, ends[1].x, 0.2f);
            Assert.AreEqual(ends[0].y, ends[1].y, 0.2f);
        }

        [Test]
        public void TakeOff_HeadsAwayFromTheBall()
        {
            var bird = new BirdFlight(Perch(Vector3.zero, 0f), _birds);
            bird.Update(0.02f, new Vector3(2f, 0f, 0f), true);
            for (int i = 0; i < 20; i++)
            {
                bird.Update(0.05f, FarBall, false);
            }

            Assert.Less(bird.Pose.Position.x, -1f, "the ball stood on the +X side of its path: it leaves toward -X");
        }

        [Test]
        public void Mesh_HasConstantVertexCount_NoBlackNoNeon_AndFlapMovesOnlyTheWings()
        {
            BirdMeshKit kit = BirdMeshKit.Build(_palette);
            Assert.Greater(kit.VertexCount, 0);
            Assert.AreEqual(0, kit.VertexCount % 3);
            Assert.Less(kit.VertexCount / 3, 80, "tiny: under 80 triangles");
            foreach (Color32 color in kit.Colors)
            {
                // Vertex colours are linear: the darkest palette tone used (slate dark, #5C5D5C) is about 82 in sum.
                Assert.Greater(color.r + color.g + color.b, 60, "no pure black");
                Assert.Less(Mathf.Max(color.r, Mathf.Max(color.g, color.b)) - Mathf.Min(color.r, Mathf.Min(color.g, color.b)), 140, "no neon");
            }

            var still = new Vector3[kit.VertexCount];
            var flapped = new Vector3[kit.VertexCount];
            kit.Apply(new BirdPose(Vector3.zero, 0f, 0f, 0f, 1f, 1f), 1f, still, 0);
            kit.Apply(new BirdPose(Vector3.zero, 0f, 0f, 0.8f, 1f, 1f), 1f, flapped, 0);
            int moved = 0;
            for (int i = 0; i < kit.VertexCount; i++)
            {
                bool differs = (still[i] - flapped[i]).sqrMagnitude > 1e-8f;
                if (differs)
                {
                    moved++;
                    Assert.IsTrue(kit.IsWing[i], "only wing vertices move with the flap");
                }
            }

            Assert.Greater(moved, 0, "the wings move");

            // Folded wings are narrower than spread ones.
            var folded = new Vector3[kit.VertexCount];
            kit.Apply(new BirdPose(Vector3.zero, 0f, 0f, 0f, 0f, 1f), 1f, folded, 0);
            float spreadWidth = 0f, foldedWidth = 0f;
            for (int i = 0; i < kit.VertexCount; i++)
            {
                spreadWidth = Mathf.Max(spreadWidth, Mathf.Abs(still[i].z));
                foldedWidth = Mathf.Max(foldedWidth, Mathf.Abs(folded[i].z));
            }

            Assert.Less(foldedWidth, spreadWidth * 0.7f);
        }

        [Test]
        public void View_OnePerchedFlockInOneMesh_AndStaysInSettingsRange()
        {
            CourseLayout layout = CourseGenerator.Generate(33, 5, Settings);
            var go = new GameObject("Birds");
            try
            {
                var view = go.AddComponent<BirdsView>();
                var serialized = new UnityEditor.SerializedObject(view);
                serialized.FindProperty("settings").objectReferenceValue = _birds;
                serialized.FindProperty("palette").objectReferenceValue = _palette;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.Build(layout, 33, 5, 0);
                Assert.That(view.Birds.Count, Is.InRange(_birds.Count.x, _birds.Count.y));
                Assert.AreEqual(1, go.GetComponents<MeshFilter>().Length, "one mesh for the whole flock");

                // A resting ball far away: nothing moves.
                view.Advance(0.1f, FarBall, false);
                foreach (BirdFlight bird in view.Birds)
                {
                    Assert.AreEqual(BirdState.Perched, bird.State);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
