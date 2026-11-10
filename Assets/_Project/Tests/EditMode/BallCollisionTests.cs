using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The ball striking props and the flag pole (M4 PR 4.2), on exact planes and on Sandbox_Par3's course.</summary>
    public class BallCollisionTests
    {
        private const int StepLimit = 60 * 120;
        private const float NearHole = 0.25f;

        private ShotSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
        }

        private static bool Settled(BallSimulation ball) => ball.State == BallState.Resting || ball.State == BallState.Holed;

        private static void RunUntilSettled(BallSimulation ball, List<Vector3> path = null)
        {
            for (int i = 0; i < StepLimit && !Settled(ball); i++)
            {
                ball.Step();
                path?.Add(ball.Position);
            }

            Assert.That(Settled(ball), $"Still {ball.State} after {StepLimit} steps");
        }

        /// <summary>The ball's center at a share of the way along the arc of this shot, for placing a shape on it.</summary>
        private Vector3 CenterOnArc(BallSimulation ball, float power, float share)
        {
            BallisticArc arc = ball.ArcFor(Vector2.right, power);
            float time = ball.PredictLanding(arc).Time * share;
            return arc.PositionAt(time) + (Vector3.up * _settings.BallRadius);
        }

        private BallSimulation Fairway(params PropShape[] shapes)
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway);
            var obstacles = new BallObstacles(shapes, _settings.BallRadius, null, ground);
            return new BallSimulation(ground, _settings, Vector2.zero, obstacles);
        }

        // --- Each shape and material ---

        [Test]
        public void TrunkInFlight_SendsTheBallBack()
        {
            var probe = Fairway();
            Vector3 on = CenterOnArc(probe, 0.6f, 0.5f);
            var trunkFoot = new Vector3(on.x + 0.5f, 0f, 0f);
            PropShape trunk = PropShape.Cylinder(trunkFoot, 0.15f, 8f, PropMaterial.Wood);
            var ball = Fairway(trunk);

            ball.Shoot(Vector2.right, 0.6f);
            RunUntilSettled(ball);

            Assert.That(ball.Hits, Is.EqualTo(1));
            Assert.That(ball.LastHit.Material, Is.EqualTo(PropMaterial.Wood));
            Assert.That(ball.LastHit.Normal.x, Is.LessThan(-0.99f), "Struck head on, from -X");
            float fromAxis = new Vector2(ball.LastHit.Contact.x - trunkFoot.x, ball.LastHit.Contact.z - trunkFoot.z).magnitude;
            Assert.That(fromAxis, Is.EqualTo(0.15f).Within(1e-3f), "The mark sits on the trunk's bark");
            Assert.That(ball.Position.x, Is.LessThan(trunkFoot.x), "Hard: it bounced back off the trunk");
        }

        [Test]
        public void RockInFlight_ContactIsOnTheRockAndTheBallBounces()
        {
            var probe = Fairway();
            Vector3 on = CenterOnArc(probe, 0.6f, 0.6f);
            var center = new Vector3(on.x + 0.6f, on.y, 0f);
            PropShape rock = PropShape.Ellipsoid(center, Vector3.one * 0.5f, PropMaterial.Stone);
            var ball = Fairway(rock);

            ball.Shoot(Vector2.right, 0.6f);
            Vector3 before = Vector3.zero;
            for (int i = 0; i < StepLimit && ball.Hits == 0; i++)
            {
                before = ball.Velocity;
                ball.Step();
            }

            Assert.That(ball.Hits, Is.EqualTo(1));
            Assert.That(ball.LastHit.Material, Is.EqualTo(PropMaterial.Stone));
            Assert.That((ball.LastHit.Contact - center).magnitude, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(Vector3.Dot(ball.Velocity, ball.LastHit.Normal), Is.GreaterThan(0f), "Leaving the rock");
            Assert.That(ball.Velocity.magnitude, Is.LessThan(before.magnitude), "A hit never adds speed");
        }

        [TestCase(0f)]
        [TestCase(0.4f)]
        public void WallInFlight_NormalIsTheFaceItStrikes(float yaw)
        {
            var probe = Fairway();
            Vector3 on = CenterOnArc(probe, 0.6f, 0.5f);
            var center = new Vector3(on.x + 1f, 0.5f, 0f);
            PropShape wall = PropShape.Box(center, new Vector3(0.2f, 4f, 3f), yaw, PropMaterial.Stone);
            var ball = Fairway(wall);

            ball.Shoot(Vector2.right, 0.6f);
            RunUntilSettled(ball);

            Assert.That(ball.Hits, Is.EqualTo(1));
            var face = new Vector3(-Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
            Assert.That(Vector3.Dot(ball.LastHit.Normal, face), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(wall.Level(ball.LastHit.Contact), Is.EqualTo(1f).Within(2e-3f), "On the wall's face");
            Assert.That(ball.Position.x, Is.LessThan(center.x));
        }

        [Test]
        public void GlancingHit_TurnsTheBallOnlyALittle()
        {
            var probe = Fairway();
            Vector3 on = CenterOnArc(probe, 0.6f, 0.5f);

            // The grown sphere reaches 0.62 m from its middle: 0.58 m to the side leaves a graze.
            var center = new Vector3(on.x + 1f, on.y, 0.58f);
            PropShape rock = PropShape.Ellipsoid(center, Vector3.one * 0.5f, PropMaterial.Stone);
            var ball = Fairway(rock);

            ball.Shoot(Vector2.right, 0.6f);
            Vector3 before = Vector3.zero;
            for (int i = 0; i < StepLimit && ball.Hits == 0; i++)
            {
                before = ball.Velocity;
                ball.Step();
            }

            Assert.That(ball.Hits, Is.EqualTo(1));
            Vector2 was = new Vector2(before.x, before.z);
            Vector2 now = new Vector2(ball.Velocity.x, ball.Velocity.z);
            Assert.That(now.y, Is.LessThan(0f), "Pushed away from the rock's side");
            Assert.That(Vector2.Angle(was, now), Is.InRange(0.5f, 40f));
        }

        // --- Soft crowns ---

        private (PropShape trunk, PropShape crown) Olive(Vector3 crownCenter)
        {
            var foot = new Vector3(crownCenter.x, 0f, crownCenter.z);
            PropShape trunk = PropShape.Cylinder(foot, 0.12f, crownCenter.y, PropMaterial.Wood);
            PropShape crown = PropShape.Ellipsoid(crownCenter, new Vector3(1.1f, 0.8f, 1.1f), PropMaterial.Foliage);
            return (trunk, crown);
        }

        [Test]
        public void SoftCrown_TakesMostOfTheSpeedAndTheBallDropsUnderTheTree()
        {
            var probe = Fairway();
            Vector3 on = CenterOnArc(probe, 0.6f, 0.6f);
            var (trunk, crown) = Olive(new Vector3(on.x + 1f, Mathf.Max(on.y, 1.8f), 0f));
            var ball = Fairway(trunk, crown);

            ball.Shoot(Vector2.right, 0.6f);
            Vector3 before = Vector3.zero;
            for (int i = 0; i < StepLimit && ball.Hits == 0; i++)
            {
                before = ball.Velocity;
                ball.Step();
            }

            Assert.That(ball.LastHit.Material, Is.EqualTo(PropMaterial.Foliage));
            Assert.That(ball.Velocity.magnitude, Is.LessThan(before.magnitude * 0.25f), "Most of the speed is lost in the leaves");

            // Where it first meets the ground after the leaves.
            for (int i = 0; i < StepLimit && ball.State == BallState.Flying && ball.Bounces == 0; i++)
            {
                ball.Step();
            }

            float landed = new Vector2(ball.Position.x - trunk.Center.x, ball.Position.z - trunk.Center.z).magnitude;
            Assert.That(landed, Is.LessThan(crown.Extents.x), "Falls through the leaves to the ground under the crown");

            RunUntilSettled(ball);
            float rests = new Vector2(ball.Position.x - trunk.Center.x, ball.Position.z - trunk.Center.z).magnitude;
            Assert.That(rests, Is.LessThan(crown.Extents.x + 1f), "Then only a short hop and roll");
            Debug.Log($"Soft crown: landed {landed:F2} m from the trunk, rests {rests:F2} m");
        }

        /// <summary>
        /// The review's question: the ball drops out of a soft crown, lands and hops. The hop may strike the same
        /// crown or a neighbour's from below. Falling through the leaves, the ball may meet its own tree's trunk
        /// (hard, inside the crown), but its landing is too slow for any hop to reach a crown again.
        /// </summary>
        [Test]
        public void DropOutOfACrown_HopsDontReachTheCrownsAgain()
        {
            var probe = Fairway();
            Vector3 on = CenterOnArc(probe, 0.6f, 0.6f);
            var (trunk, crown) = Olive(new Vector3(on.x + 1f, Mathf.Max(on.y, 1.8f), 0f));
            var (otherTrunk, otherCrown) = Olive(new Vector3(on.x + 3f, Mathf.Max(on.y, 1.8f), 0.6f));
            var ball = Fairway(trunk, crown, otherTrunk, otherCrown);

            ball.Shoot(Vector2.right, 0.6f);
            var materials = new List<PropMaterial>();
            var bouncesAtHit = new List<int>();
            for (int i = 0; i < StepLimit && !Settled(ball); i++)
            {
                int hits = ball.Hits;
                ball.Step();
                if (ball.Hits != hits)
                {
                    materials.Add(ball.LastHit.Material);
                    bouncesAtHit.Add(ball.Bounces);
                }
            }

            Assert.That(materials[0], Is.EqualTo(PropMaterial.Foliage));
            Assert.That(materials.FindAll(m => m == PropMaterial.Foliage).Count, Is.EqualTo(1), "Only the first crown, once");
            Assert.That(bouncesAtHit.TrueForAll(b => b == 0), "Every hit before the first landing: no hop reaches a crown");
            Debug.Log($"Drop out of a crown: hits {string.Join(", ", materials)}, rests at {ball.Position}");
        }

        /// <summary>The other half of it: a ball punched up from under a crown meets the leaves from below, and drops.</summary>
        [Test]
        public void ShotFromUnderACrown_IsCaughtByItFromBelow()
        {
            // A low crown, and the ball resting under it beside the trunk.
            var (trunk, crown) = Olive(new Vector3(1.5f, 1.1f, 0f));
            var ground = PlaneBallGround.All(CourseSurface.Fairway);
            var ball = new BallSimulation(ground, _settings, new Vector2(1.5f, 0.45f),
                new BallObstacles(new[] { trunk, crown }, _settings.BallRadius, null, ground));

            ball.Shoot(Vector2.right, 0.5f);
            for (int i = 0; i < StepLimit && ball.Hits == 0; i++)
            {
                ball.Step();
            }

            Assert.That(ball.LastHit.Material, Is.EqualTo(PropMaterial.Foliage));
            Assert.That(ball.LastHit.Normal.y, Is.LessThan(0f), "Struck from below");

            RunUntilSettled(ball);
            Assert.That(ball.Position.x, Is.LessThan(crown.Center.x + crown.Extents.x + 1.5f), "Dropped near the tree rather than flying on");
        }

        // --- The cap on hits ---

        /// <summary>
        /// Between two walls that give back all their speed, the ball would bounce from one to the other until it
        /// fell. At the cap, the last hit takes all its speed: it drops straight down, past every obstacle, and
        /// rests right under that hit.
        /// </summary>
        [Test]
        public void HitCap_LastHitDropsTheBallStraightDown()
        {
            ShotSettings bouncy = Object.Instantiate(_settings);
            try
            {
                using (var serialized = new SerializedObject(bouncy))
                {
                    serialized.FindProperty("stone.restitution").floatValue = 1f;
                    serialized.FindProperty("stone.alongKeep").floatValue = 1f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PropShape left = PropShape.Box(new Vector3(10f, 3f, 0.6f), new Vector3(15f, 3f, 0.2f), 0f, PropMaterial.Stone);
                PropShape right = PropShape.Box(new Vector3(10f, 3f, -0.6f), new Vector3(15f, 3f, 0.2f), 0f, PropMaterial.Stone);
                var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Fairway), bouncy, Vector2.zero,
                    new BallObstacles(new[] { left, right }, bouncy.BallRadius));

                ball.Shoot(new Vector2(1f, 0.6f), 1f);
                RunUntilSettled(ball);

                Assert.That(ball.Hits, Is.EqualTo(bouncy.MaxHits));
                Assert.That(ball.Position.x, Is.EqualTo(ball.LastHit.Point.x).Within(1e-4f));
                Assert.That(ball.Position.z, Is.EqualTo(ball.LastHit.Point.z).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(bouncy);
            }
        }

        // --- Rolling ---

        [Test]
        public void RollingIntoATrunk_NeverEntersItAndComesBack()
        {
            var foot = new Vector3(2f, 0f, 0f);
            PropShape trunk = PropShape.Cylinder(foot, 0.15f, 2f, PropMaterial.Wood);
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Green), _settings, Vector2.zero,
                new BallObstacles(new[] { trunk }, _settings.BallRadius));

            Assert.That(ball.IsPutt);
            ball.Shoot(Vector2.right, 0.6f);
            var path = new List<Vector3>();
            RunUntilSettled(ball, path);

            float closest = float.MaxValue;
            foreach (Vector3 p in path)
            {
                closest = Mathf.Min(closest, new Vector2(p.x - foot.x, p.z - foot.z).magnitude);
            }

            Assert.That(closest, Is.GreaterThanOrEqualTo(0.15f + _settings.BallRadius - 1e-4f));
            Assert.That(ball.Position.x, Is.LessThan(foot.x - 0.27f));
            Assert.That(ball.Hits, Is.EqualTo(0), "Rolling contacts aren't flight hits");
        }

        [Test]
        public void RollingIntoATrunkOffCentre_IsTurnedAside()
        {
            var foot = new Vector3(2f, 0f, 0.15f);
            PropShape trunk = PropShape.Cylinder(foot, 0.15f, 2f, PropMaterial.Wood);
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Green), _settings, Vector2.zero,
                new BallObstacles(new[] { trunk }, _settings.BallRadius));

            ball.Shoot(Vector2.right, 0.6f);
            RunUntilSettled(ball);

            Assert.That(ball.Position.z, Is.LessThan(-0.05f), "Deflected away from the trunk's side");
        }

        /// <summary>A slope pushing the ball into a wall would strike it on every step; it rests against it instead.</summary>
        [Test]
        public void SlopeIntoAWall_TheBallRestsAgainstIt()
        {
            PropShape wall = PropShape.Box(new Vector3(1.5f, 0.3f, 0f), new Vector3(0.2f, 0.4f, 3f), 0f, PropMaterial.Stone);
            var ground = PlaneBallGround.All(CourseSurface.Green, new Vector2(-0.2f, 0f));
            var ball = new BallSimulation(ground, _settings, Vector2.zero, new BallObstacles(new[] { wall }, _settings.BallRadius));

            ball.Shoot(Vector2.right, 0.3f);
            RunUntilSettled(ball);

            Assert.That(ball.State, Is.EqualTo(BallState.Resting));
            Assert.That(ball.Position.x, Is.EqualTo(1.5f - 0.2f - _settings.BallRadius).Within(0.01f));
        }

        // --- The flag pole ---

        private PlaneBallGround GreenAround(Vector2 hole)
        {
            var ground = new PlaneBallGround(Vector2.zero,
                (x, z) => new Vector2(x - hole.x, z - hole.y).magnitude < 4f ? CourseSurface.Green : CourseSurface.Fairway);
            ground.Hole = hole;
            return ground;
        }

        private static readonly PropShape[] NoProps = new PropShape[0];

        /// <summary>
        /// Light to strong shots at a pin 6 m away. Some strike the pole: a few of those, reaching it low and
        /// already slowed by their landing, drop in; most bounce away. So the pole makes a chip-in possible,
        /// at the right power only, and a pole hit never guarantees one.
        /// </summary>
        [Test]
        public void Pole_ChipInIsPossibleButDependsOnPower()
        {
            var ground = GreenAround(new Vector2(6f, 0f));
            BallObstacles pole = BallObstacles.ForCourse(NoProps, ground, _settings);
            int poleHits = 0, holedOffThePole = 0, highHitsHoled = 0, highHits = 0;
            var holedPowers = new List<float>();

            for (int p = 0; p <= 400; p++)
            {
                float power = p / 400f * 0.5f;
                var ball = new BallSimulation(ground, _settings, Vector2.zero, pole);
                ball.Shoot(Vector2.right, power);
                RunUntilSettled(ball);
                if (ball.Hits == 0)
                {
                    continue;
                }

                Assert.That(ball.LastHit.IsPole);
                poleHits++;
                bool holed = ball.State == BallState.Holed;
                if (holed)
                {
                    holedOffThePole++;
                    holedPowers.Add(power);
                }

                // Higher up, the ball is still fast: it bounces off.
                if (ball.LastHit.Point.y > 0.3f)
                {
                    highHits++;
                    highHitsHoled += holed ? 1 : 0;
                }
            }

            Assert.That(holedOffThePole, Is.GreaterThan(0), "A chip-in off the pole is possible");
            Assert.That(holedOffThePole, Is.LessThan(poleHits / 2), "Most pole hits don't drop");
            Assert.That(highHits, Is.GreaterThan(0));
            Assert.That(highHitsHoled, Is.EqualTo(0), "A fast ball striking the pole high bounces away");
        }

        [Test]
        public void Pole_SlowBallFallsBesideItAndTheRimDecides()
        {
            var ground = GreenAround(new Vector2(6f, 0f));
            BallObstacles pole = BallObstacles.ForCourse(NoProps, ground, _settings);

            for (int p = 0; p <= 400; p++)
            {
                float power = p / 400f * 0.5f;
                var ball = new BallSimulation(ground, _settings, Vector2.zero, pole);
                ball.Shoot(Vector2.right, power);
                RunUntilSettled(ball);
                if (ball.State == BallState.Holed && ball.Hits > 0)
                {
                    // Bounced out of the pole into the hole, by the rule any ball over it follows.
                    Assert.That(new Vector2(ball.LastHit.Point.x - 6f, ball.LastHit.Point.z).magnitude, Is.LessThan(_settings.HoleCaptureRadius));
                    return;
                }
            }

            Assert.Fail("No shot dropped off the pole");
        }

        /// <summary>
        /// A hop on the green can strike the pole. Where the ball would otherwise have rolled over the rim and
        /// lipped out, the hop strikes the pole instead, and plays out the same way every time.
        /// </summary>
        [Test]
        public void Pole_HopThatWouldHaveLippedOut_IsDeterministic()
        {
            var hole = new Vector2(6f, 0f);
            var ground = GreenAround(hole);
            BallObstacles pole = BallObstacles.ForCourse(NoProps, ground, _settings);
            int found = 0;

            for (int p = 0; p <= 800 && found < 3; p++)
            {
                float power = p / 800f * 0.5f;

                // Without the pole: it rolls over the hole and stays out, a lip-out.
                var without = new BallSimulation(ground, _settings, Vector2.zero);
                without.Shoot(Vector2.right, power);
                bool rolledOverHole = false;
                for (int i = 0; i < StepLimit && !Settled(without); i++)
                {
                    without.Step();
                    rolledOverHole |= without.State == BallState.Rolling &&
                        new Vector2(without.Position.x - hole.x, without.Position.z - hole.y).magnitude < NearHole;
                }

                if (!rolledOverHole || without.State == BallState.Holed)
                {
                    continue;
                }

                var first = new BallSimulation(ground, _settings, Vector2.zero, pole);
                var second = new BallSimulation(ground, _settings, Vector2.zero, pole);
                first.Shoot(Vector2.right, power);
                second.Shoot(Vector2.right, power);
                int bouncesAtHit = -1;
                var firstPath = new List<Vector3>();
                for (int i = 0; i < StepLimit && !Settled(first); i++)
                {
                    first.Step();
                    firstPath.Add(first.Position);
                    if (first.Hits > 0 && bouncesAtHit < 0)
                    {
                        bouncesAtHit = first.Bounces;
                    }
                }

                if (bouncesAtHit <= 0)
                {
                    continue;
                }

                var secondPath = new List<Vector3>();
                RunUntilSettled(second, secondPath);
                CollectionAssert.AreEqual(firstPath, secondPath);
                Assert.That(second.State, Is.EqualTo(first.State));
                found++;
            }

            Assert.That(found, Is.GreaterThan(0), "Some hop strikes the pole where the ball would have lipped out");
        }

        [Test]
        public void Putting_IgnoresThePole()
        {
            var hole = new Vector2(3f, 0f);
            var ground = GreenAround(hole);
            BallObstacles pole = BallObstacles.ForCourse(NoProps, ground, _settings);

            foreach (float side in new[] { 0f, 0.05f, 0.12f, 0.2f })
            {
                for (int p = 1; p <= 20; p++)
                {
                    float power = p / 20f;
                    var direction = new Vector2(3f, side);
                    var plain = new BallSimulation(ground, _settings, Vector2.zero);
                    var withPole = new BallSimulation(ground, _settings, Vector2.zero, pole);
                    Assert.That(plain.IsPutt && withPole.IsPutt);
                    plain.Shoot(direction, power);
                    withPole.Shoot(direction, power);

                    var a = new List<Vector3>();
                    var b = new List<Vector3>();
                    RunUntilSettled(plain, a);
                    RunUntilSettled(withPole, b);
                    CollectionAssert.AreEqual(a, b, $"side {side} power {power}");
                    Assert.That(withPole.State, Is.EqualTo(plain.State));
                }
            }
        }

        // --- Nothing changes without props ---

        [Test]
        public void NoProps_PlaysExactlyAsBefore()
        {
            var ground = PlaneBallGround.All(CourseSurface.Fairway, new Vector2(0.03f, -0.02f));
            PropShape faraway = PropShape.Ellipsoid(new Vector3(1000f, 0f, 1000f), Vector3.one, PropMaterial.Stone);
            var empty = new BallObstacles(NoProps, _settings.BallRadius);
            var far = new BallObstacles(new[] { faraway }, _settings.BallRadius);

            foreach (float power in new[] { 0.1f, 0.5f, 1f })
            {
                var paths = new List<List<Vector3>>();
                foreach (BallObstacles obstacles in new[] { null, empty, far })
                {
                    var ball = new BallSimulation(ground, _settings, Vector2.zero, obstacles);
                    ball.Shoot(new Vector2(1f, 0.3f), power);
                    var path = new List<Vector3>();
                    RunUntilSettled(ball, path);
                    paths.Add(path);
                }

                CollectionAssert.AreEqual(paths[0], paths[1]);
                CollectionAssert.AreEqual(paths[0], paths[2]);
            }
        }

        [Test]
        public void SameShotIntoTrees_PlaysTheSameEveryTimeAndAtAnyFrameRate()
        {
            SandboxCourse course = SandboxCourse.Load();
            Vector2 toHole = (course.Ground.Hole - course.Ground.Tee).normalized;
            var byStep = new BallSimulation(course.Ground, _settings, course.Ground.Tee, course.Obstacles);
            var again = new BallSimulation(course.Ground, _settings, course.Ground.Tee, course.Obstacles);
            var byFrames = new BallSimulation(course.Ground, _settings, course.Ground.Tee, course.Obstacles);
            Vector2 aim = Quaternion.Euler(0f, 0f, 12f) * toHole;
            byStep.Shoot(aim, 0.9f);
            again.Shoot(aim, 0.9f);
            byFrames.Shoot(aim, 0.9f);

            var a = new List<Vector3>();
            var b = new List<Vector3>();
            RunUntilSettled(byStep, a);
            RunUntilSettled(again, b);
            CollectionAssert.AreEqual(a, b);

            float[] frames = { 0.016f, 0.033f, 0.007f, 0.05f, 0.021f };
            for (int i = 0; i < StepLimit && !Settled(byFrames); i++)
            {
                byFrames.Advance(frames[i % frames.Length]);
            }

            Assert.That(byFrames.Position, Is.EqualTo(byStep.Position));
        }

        // --- The aim preview never disagrees ---

        /// <summary>
        /// A fan of shots from the sandbox's tee, into its trees and at the pin. For each, the preview's hit (or
        /// none) is compared with the played shot: the step it first strikes something leaves the ball exactly at
        /// the predicted point, and a shot predicted clear lands before striking anything.
        /// </summary>
        [Test]
        public void Preview_MatchesThePlayedShot_IntoTreesAndThePole()
        {
            SandboxCourse course = SandboxCourse.Load();
            Vector2 tee = course.Ground.Tee;
            Vector2 toHole = (course.Ground.Hole - tee).normalized;
            int treeHits = 0, poleHits = 0, clear = 0;

            for (int angle = -40; angle <= 40; angle += 2)
            {
                Vector2 aim = Quaternion.Euler(0f, 0f, angle) * toHole;
                for (int p = 2; p <= 20; p++)
                {
                    float power = p / 20f;
                    var ball = new BallSimulation(course.Ground, _settings, tee, course.Obstacles);
                    BallisticArc arc = ball.ArcFor(aim, power);
                    ArcLanding landing = ball.PredictLanding(arc);
                    bool predicted = ball.PredictHit(arc, landing, out ArcHit hit);

                    ball.Shoot(aim, power);
                    for (int i = 0; i < StepLimit && ball.State == BallState.Flying && ball.Hits == 0 && ball.Bounces == 0; i++)
                    {
                        ball.Step();
                    }

                    if (predicted)
                    {
                        Assert.That(ball.Hits, Is.EqualTo(1), $"angle {angle} power {power}");
                        Assert.That(ball.LastHit.Point, Is.EqualTo(hit.Point));
                        Assert.That(ball.Position, Is.EqualTo(hit.Point));
                        Assert.That(ball.LastHit.Index, Is.EqualTo(hit.Index));
                        if (hit.IsPole)
                        {
                            poleHits++;
                        }
                        else
                        {
                            treeHits++;
                        }
                    }
                    else
                    {
                        Assert.That(ball.Hits, Is.EqualTo(0), $"angle {angle} power {power}");
                        clear++;
                    }
                }
            }

            Assert.That(treeHits, Is.GreaterThan(0), "Some shots strike the trees");
            Assert.That(clear, Is.GreaterThan(0));
            Debug.Log($"Preview fan: {treeHits} tree hits, {poleHits} pole hits, {clear} clear");
        }

        [Test]
        public void Preview_MatchesThePole()
        {
            var ground = GreenAround(new Vector2(6f, 0f));
            BallObstacles pole = BallObstacles.ForCourse(NoProps, ground, _settings);
            int hits = 0;
            for (int p = 0; p <= 400; p++)
            {
                float power = p / 400f * 0.5f;
                var ball = new BallSimulation(ground, _settings, Vector2.zero, pole);
                BallisticArc arc = ball.ArcFor(Vector2.right, power);
                bool predicted = ball.PredictHit(arc, ball.PredictLanding(arc), out ArcHit hit);
                ball.Shoot(Vector2.right, power);
                for (int i = 0; i < StepLimit && ball.State == BallState.Flying && ball.Hits == 0 && ball.Bounces == 0; i++)
                {
                    ball.Step();
                }

                Assert.That(ball.Hits, Is.EqualTo(predicted ? 1 : 0));
                if (predicted)
                {
                    Assert.That(ball.Position, Is.EqualTo(hit.Point));
                    hits++;
                }
            }

            Assert.That(hits, Is.GreaterThan(0));
        }

        // --- The grid ---

        [Test]
        public void Grid_FindsTheSameFirstHitAsTestingEveryShape()
        {
            var random = new System.Random(3);
            float Next(float min, float max) => min + ((float)random.NextDouble() * (max - min));

            var shapes = new List<PropShape>();
            for (int i = 0; i < 60; i++)
            {
                var at = new Vector3(Next(-15f, 15f), Next(0f, 2f), Next(-15f, 15f));
                PropMaterial material = (PropMaterial)random.Next(3);
                switch (random.Next(3))
                {
                    case 0:
                        shapes.Add(PropShape.Cylinder(at, Next(0.1f, 0.6f), Next(0.5f, 4f), material));
                        break;
                    case 1:
                        shapes.Add(PropShape.Ellipsoid(at, new Vector3(Next(0.3f, 1.5f), Next(0.3f, 1.5f), Next(0.3f, 1.5f)), material));
                        break;
                    default:
                        shapes.Add(PropShape.Box(at, new Vector3(Next(0.2f, 3f), Next(0.2f, 1f), Next(0.1f, 0.4f)), Next(0f, 6.28f), material));
                        break;
                }
            }

            var all = new BallObstacles(shapes, 0.12f);
            var single = new BallObstacles[shapes.Count];
            for (int i = 0; i < shapes.Count; i++)
            {
                single[i] = new BallObstacles(new[] { shapes[i] }, 0.12f);
            }

            int hits = 0;
            for (int n = 0; n < 4000; n++)
            {
                var from = new Vector3(Next(-17f, 17f), Next(0f, 4f), Next(-17f, 17f));
                Vector3 to = from + new Vector3(Next(-3f, 3f), Next(-1f, 1f), Next(-3f, 3f));
                bool rolling = random.Next(2) == 0;

                float best = float.MaxValue;
                int bestIndex = BallObstacles.None;
                for (int i = 0; i < shapes.Count; i++)
                {
                    if (single[i].FirstHit(from, to, rolling, BallObstacles.None, out ChordHit one) && one.Fraction < best)
                    {
                        best = one.Fraction;
                        bestIndex = i;
                    }
                }

                bool found = all.FirstHit(from, to, rolling, BallObstacles.None, out ChordHit hit);
                Assert.That(found, Is.EqualTo(bestIndex != BallObstacles.None));
                if (found)
                {
                    hits++;
                    Assert.That(hit.Index, Is.EqualTo(bestIndex));
                    Assert.That(hit.Fraction, Is.EqualTo(best));
                }
            }

            Assert.That(hits, Is.GreaterThan(100));
        }

        // --- The simulation's sizes match the drawing ---

        [Test]
        public void ShotSettings_BallAndPoleMatchTheDrawnPieces()
        {
            var pieces = AssetDatabase.LoadAssetAtPath<PlayPieceSettings>("Assets/_Project/Settings/PlayPieces.asset");
            Assert.That(_settings.BallRadius, Is.EqualTo(pieces.BallRadius));
            Assert.That(_settings.PoleRadius, Is.EqualTo(pieces.PoleRadius));
            Assert.That(_settings.PoleHeight, Is.EqualTo(pieces.PoleHeight));
        }

        /// <summary>Sandbox_Par3's hand-made course with its hand-placed trees and the pole, as the scene plays it.</summary>
        private sealed class SandboxCourse
        {
            public BallGround Ground { get; private set; }
            public BallObstacles Obstacles { get; private set; }

            public static SandboxCourse Load()
            {
                var shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
                var trees = AssetDatabase.LoadAssetAtPath<TreeSettings>("Assets/_Project/Settings/SandboxTrees.asset");
                var kit = AssetDatabase.LoadAssetAtPath<PropKitSettings>("Assets/_Project/Settings/PropKit.asset");
                var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
                var shot = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");

                CourseLayout layout = shape.ToLayout();
                var shapes = new List<PropShape>();
                PropBuilder.BuildTrees(trees, kit, palette, layout, shapes);
                Assert.That(shapes.Count, Is.GreaterThan(0));

                var ground = new BallGround(layout);
                return new SandboxCourse { Ground = ground, Obstacles = BallObstacles.ForCourse(shapes, ground, shot) };
            }
        }
    }
}
