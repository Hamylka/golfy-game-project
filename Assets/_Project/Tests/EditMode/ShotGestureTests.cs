using IsoGolf.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class ShotGestureTests
    {
        private const float Width = 1000f;
        private const float RadiusShare = 0.08f;
        private const float FullShare = 0.3f;
        private const float Radius = RadiusShare * Width;
        private const float Full = FullShare * Width;
        private const float Frame = 1f / 60f;

        private static readonly Vector2 Ball = new Vector2(500f, 500f);

        private static ShotGesture Gesture(float holdSeconds = 0f) => new ShotGesture(RadiusShare, FullShare, holdSeconds);

        private static bool Press(ShotGesture gesture, Vector2 position, bool ballCanBeGrabbed = true, int fingers = 1)
        {
            var pointer = new ShotPointer { Pressed = true, Position = position, Fingers = fingers };
            return gesture.Update(pointer, Ball, Width, ballCanBeGrabbed, Frame);
        }

        private static bool Lift(ShotGesture gesture, bool canceled = false)
        {
            return gesture.Update(new ShotPointer { Canceled = canceled }, Ball, Width, true, Frame);
        }

        private static Vector2 Pulled(float pixels) => Ball + (Vector2.down * pixels);

        [Test]
        public void Grab_APressOnTheBallTakesTheTouch()
        {
            ShotGesture gesture = Gesture();

            Press(gesture, Ball + (Vector2.left * (Radius - 1f)));

            Assert.That(gesture.IsActive, Is.True);
            Assert.That(gesture.IsAiming, Is.False);
        }

        [Test]
        public void Grab_APressElsewhereLeavesItAlone()
        {
            ShotGesture gesture = Gesture();

            Press(gesture, Ball + (Vector2.left * (Radius + 1f)));

            Assert.That(gesture.IsActive, Is.False);
        }

        [Test]
        public void Grab_AMovingBallCantBeGrabbed()
        {
            ShotGesture gesture = Gesture();

            Press(gesture, Ball, ballCanBeGrabbed: false);

            Assert.That(gesture.IsActive, Is.False);
        }

        [Test]
        public void Grab_AFingerSlidingOntoTheBallDoesntGrabIt()
        {
            ShotGesture gesture = Gesture();

            Press(gesture, Pulled(Full));
            Press(gesture, Ball);

            Assert.That(gesture.IsActive, Is.False, "That finger is moving the camera.");
        }

        [Test]
        public void Grab_TwoFingersLandingTogetherArentAShot()
        {
            ShotGesture gesture = Gesture();

            Press(gesture, Ball, fingers: 2);

            Assert.That(gesture.IsActive, Is.False);
        }

        [Test]
        public void Release_BeyondTheRadiusShoots()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);
            Press(gesture, Pulled(Radius + 1f));

            Assert.That(gesture.IsAiming, Is.True);
            Assert.That(Lift(gesture), Is.True);
            Assert.That(gesture.IsActive, Is.False);
            Assert.That(Lift(gesture), Is.False, "A release happens only once.");
        }

        [Test]
        public void Release_ATapOnTheEdgeOfTheGrabAreaCancels()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Pulled(Radius - 1f));

            Assert.That(Lift(gesture), Is.False);
            Assert.That(gesture.IsActive, Is.False);
        }

        [Test]
        public void Release_BackOnTheBallCancels()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);
            Press(gesture, Pulled(Full));
            Press(gesture, Ball);

            Assert.That(gesture.IsAiming, Is.False);
            Assert.That(Lift(gesture), Is.False);
        }

        [Test]
        public void Power_GrowsFromTheRadiusToTheFullPull()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);

            Press(gesture, Pulled(Radius + 0.001f));
            Assert.That(gesture.Power, Is.EqualTo(0f).Within(0.001f));

            Press(gesture, Pulled((Radius + Full) * 0.5f));
            Assert.That(gesture.Power, Is.EqualTo(0.5f).Within(0.001f));

            Press(gesture, Pulled(Full));
            Assert.That(gesture.Power, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Power_StopsAtFull()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);

            Press(gesture, Pulled(Full * 3f));

            Assert.That(gesture.Power, Is.EqualTo(1f));
        }

        [Test]
        public void Power_IsTheSameInEveryDirection()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);

            Press(gesture, Pulled(200f));
            float down = gesture.Power;
            Press(gesture, Ball + (Vector2.right * 200f));

            Assert.That(gesture.Power, Is.EqualTo(down).Within(0.0001f));
        }

        [Test]
        public void Power_IsAShareOfTheScreenWidth()
        {
            ShotGesture gesture = Gesture();
            const float WiderScreen = Width * 2f;
            gesture.Update(new ShotPointer { Pressed = true, Position = Ball, Fingers = 1 }, Ball, WiderScreen, true, Frame);

            gesture.Update(new ShotPointer { Pressed = true, Position = Pulled(Full * 2f), Fingers = 1 }, Ball, WiderScreen, true, Frame);

            Assert.That(gesture.Power, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Release_KeepsThePowerItLastRead()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);
            Press(gesture, Pulled(Full));

            Lift(gesture);

            Assert.That(gesture.Power, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SecondFinger_IsIgnoredWhileAiming()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);
            Press(gesture, Pulled(Full));

            Press(gesture, Pulled(Full), fingers: 2);

            Assert.That(gesture.IsAiming, Is.True);
            Assert.That(Lift(gesture), Is.True);
        }

        [Test]
        public void SystemCancel_NeverShoots()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);
            Press(gesture, Pulled(Full));

            Assert.That(Lift(gesture, canceled: true), Is.False);
            Assert.That(gesture.IsActive, Is.False);
        }

        [Test]
        public void Cancel_DropsTheShotAndTheFingerMustLift()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);
            Press(gesture, Pulled(Full));

            gesture.Cancel();

            Assert.That(gesture.IsAiming, Is.False);
            Press(gesture, Ball);
            Assert.That(gesture.IsActive, Is.False, "The same finger can't grab it again.");
            Assert.That(Lift(gesture), Is.False);

            Press(gesture, Ball);
            Assert.That(gesture.IsActive, Is.True);
        }

        [Test]
        public void Hold_NoHoldAimsAtOnce()
        {
            ShotGesture gesture = Gesture();
            Press(gesture, Ball);

            Press(gesture, Pulled(Full));

            Assert.That(gesture.IsAiming, Is.True);
        }

        [Test]
        public void Hold_PullingTooSoonDropsTheShot()
        {
            ShotGesture gesture = Gesture(holdSeconds: 1f);
            Press(gesture, Ball);

            Press(gesture, Pulled(Full));

            Assert.That(gesture.IsActive, Is.False);
            Assert.That(Lift(gesture), Is.False);
        }

        [Test]
        public void Hold_AfterTheWaitAPullAims()
        {
            ShotGesture gesture = Gesture(holdSeconds: Frame * 2f);
            Press(gesture, Ball);
            Press(gesture, Ball);
            Press(gesture, Ball);

            Press(gesture, Pulled(Full));

            Assert.That(gesture.IsAiming, Is.True);
        }

        [Test]
        public void Aim_GoesFromTheFingerThroughTheBall()
        {
            var ball = new Vector3(2f, 1f, 3f);
            var camera = new Vector3(-20f, 15f, 1f);
            Vector3 fingerOnGround = ball + new Vector3(-4f, 0f, 3f);
            var ray = new Ray(camera, fingerOnGround - camera);

            Assert.That(ShotGesture.TryAim(ray, ball, out Vector3 direction), Is.True);

            Vector3 expected = new Vector3(4f, 0f, -3f).normalized;
            Assert.That(Vector3.Distance(direction, expected), Is.LessThan(0.0001f));
        }

        [Test]
        public void Aim_AFingerAboveTheHorizonGivesNoDirection()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), new Vector3(1f, 0.2f, 0f));

            Assert.That(ShotGesture.TryAim(ray, Vector3.zero, out _), Is.False);
        }

        [Test]
        public void RealSettings_FullPowerIsBeyondTheTouchRadius()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");

            Assert.That(settings.FullPowerPull, Is.GreaterThan(settings.TouchRadius));
            Assert.That(settings.HoldSeconds, Is.EqualTo(0f), "No hold time by default.");
        }
    }
}
