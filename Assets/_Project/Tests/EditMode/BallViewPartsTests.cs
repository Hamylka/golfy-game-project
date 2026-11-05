using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The plain pieces behind the ball's view and the temporary test shots.</summary>
    public class BallViewPartsTests
    {
        private const float MaxSeconds = 0.3f;
        private const float MaxTravel = 20f;

        private static TapGesture Gesture() => new TapGesture(MaxSeconds, MaxTravel);

        [Test]
        public void Spin_AQuarterOfTheCircumferenceIsAQuarterTurn()
        {
            const float Radius = 0.15f;
            var movement = new Vector3(2f * Mathf.PI * Radius * 0.25f, 0f, 0f);

            Quaternion turned = BallSpin.Roll(Quaternion.identity, movement, Radius);

            Assert.That(Quaternion.Angle(Quaternion.identity, turned), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void Spin_TheTopOfTheBallMovesTheWayItRolls()
        {
            Quaternion turned = BallSpin.Roll(Quaternion.identity, new Vector3(0.01f, 0f, 0f), 0.15f);

            Assert.That((turned * Vector3.up).x, Is.GreaterThan(0f));
        }

        [Test]
        public void Spin_NoHorizontalMovementLeavesItAlone()
        {
            Quaternion start = Quaternion.Euler(10f, 20f, 30f);

            Assert.That(BallSpin.Roll(start, Vector3.up, 0.15f), Is.EqualTo(start));
        }

        [Test]
        public void Tap_AQuickStillPressIsATap()
        {
            TapGesture tap = Gesture();

            Assert.That(tap.Update(true, Vector2.zero, 1, 0.1f), Is.False);
            Assert.That(tap.Update(false, Vector2.zero, 0, 0.1f), Is.True);
            Assert.That(tap.Update(false, Vector2.zero, 0, 0.1f), Is.False, "A tap ends only once.");
        }

        [Test]
        public void Tap_ADragIsNotATap()
        {
            TapGesture tap = Gesture();

            tap.Update(true, Vector2.zero, 1, 0.05f);
            tap.Update(true, new Vector2(MaxTravel + 1f, 0f), 1, 0.05f);
            tap.Update(true, Vector2.zero, 1, 0.05f);

            Assert.That(tap.Update(false, Vector2.zero, 0, 0.05f), Is.False, "Coming back doesn't undo a drag.");
        }

        [Test]
        public void Tap_ALongPressIsNotATap()
        {
            TapGesture tap = Gesture();

            tap.Update(true, Vector2.zero, 1, MaxSeconds + 0.01f);

            Assert.That(tap.Update(false, Vector2.zero, 0, 0.01f), Is.False);
        }

        [Test]
        public void Tap_APinchIsNotATap()
        {
            TapGesture tap = Gesture();

            tap.Update(true, Vector2.zero, 1, 0.01f);
            tap.Update(true, Vector2.zero, 2, 0.01f);
            tap.Update(true, Vector2.zero, 1, 0.01f);

            Assert.That(tap.Update(false, Vector2.zero, 0, 0.01f), Is.False);
        }

        [Test]
        public void Aim_PowerPlaysTheArcOverTheDistance()
        {
            ShotSettings settings = RealSettings();
            const float Distance = 10f;
            var ball = new BallSimulation(PlaneBallGround.All(CourseSurface.Fairway), settings, Vector2.zero);

            float power = TestShotAim.Power(settings, CourseSurface.Fairway, Distance);
            ArcLanding landing = ball.ArcFor(Vector2.right, power).FindLanding(PlaneBallGround.All(CourseSurface.Fairway), settings.LandingSearchStep);

            Assert.That(landing.Point.x, Is.EqualTo(Distance).Within(0.01f));
        }

        [Test]
        public void Aim_TheLieIsMadeUpFor()
        {
            ShotSettings settings = RealSettings();
            IBallGround rough = PlaneBallGround.All(CourseSurface.Rough);
            var ball = new BallSimulation(rough, settings, Vector2.zero);

            float power = TestShotAim.Power(settings, CourseSurface.Rough, 10f);
            ArcLanding landing = ball.ArcFor(Vector2.right, power).FindLanding(rough, settings.LandingSearchStep);

            Assert.That(landing.Point.x, Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void Aim_FromTheGreenAPuttUsesPuttDistances()
        {
            ShotSettings settings = RealSettings();

            Assert.That(TestShotAim.Power(settings, CourseSurface.Green, settings.PuttDistanceFor(0.5f)), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Aim_TooFarIsFullPower()
        {
            Assert.That(TestShotAim.Power(RealSettings(), CourseSurface.Fairway, 1000f), Is.EqualTo(1f));
        }

        private static ShotSettings RealSettings()
        {
            return AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
        }
    }
}
