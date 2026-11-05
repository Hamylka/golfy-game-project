using IsoGolf.UI;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class ResetViewButtonTests
    {
        private static readonly Rect Hit = new Rect(900f, 2300f, 150f, 150f);
        private static readonly Vector2 On = new Vector2(950f, 2350f);
        private static readonly Vector2 Off = new Vector2(100f, 100f);

        private ResetViewButtonState _state;

        [SetUp]
        public void SetUp()
        {
            _state = new ResetViewButtonState();
        }

        [Test]
        public void Layout_SitsInTheTopRightOfTheSafeArea_WithinItsMargin()
        {
            // A 1080 x 2400 screen with a notch: 100 px cut from the top, and 20 from the right.
            var safe = new Rect(0f, 0f, 1060f, 2300f);

            ResetViewButtonLayout layout = ResetViewButtonLayout.TopRight(safe, 1080f, 0.07f, 0.02f, 0.13f);

            Assert.That(layout.Hit.xMax, Is.EqualTo(1060f - (0.02f * 1080f)).Within(0.01f));
            Assert.That(layout.Hit.yMax, Is.EqualTo(2300f - (0.02f * 1080f)).Within(0.01f));
            Assert.That(layout.Hit.width, Is.EqualTo(0.13f * 1080f).Within(0.01f));
            Assert.That(layout.Size, Is.EqualTo(0.07f * 1080f).Within(0.01f));
            Assert.That(layout.Hit.Contains(layout.Center), Is.True);
        }

        [Test]
        public void Layout_TouchAreaIsNeverSmallerThanTheDrawing()
        {
            ResetViewButtonLayout layout = ResetViewButtonLayout.TopRight(new Rect(0f, 0f, 1000f, 2000f), 1000f, 0.1f, 0.02f, 0.05f);

            Assert.That(layout.Hit.width, Is.GreaterThanOrEqualTo(layout.Size));
        }

        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        public void Visible_OnlyWhileTheBallRestsAndNothingIsAimed(bool atRest, bool aiming, bool expected)
        {
            Assert.That(ResetViewButtonState.IsVisible(atRest, aiming), Is.EqualTo(expected));
        }

        [Test]
        public void ClaimsATouch_OnlyWhileVisible_AndOnTheTouchArea()
        {
            Assert.That(ResetViewButtonState.ClaimsTouch(true, Hit, On), Is.True);
            Assert.That(ResetViewButtonState.ClaimsTouch(true, Hit, Off), Is.False);
            Assert.That(ResetViewButtonState.ClaimsTouch(false, Hit, On), Is.False);
        }

        [Test]
        public void Press_FiresWhenTheTouchLiftsOnTheButton()
        {
            Assert.That(_state.Update(true, Hit, true, On, 1), Is.False);
            Assert.That(_state.IsPressed, Is.True);
            Assert.That(_state.Update(true, Hit, true, On, 1), Is.False);

            Assert.That(_state.Update(true, Hit, false, On, 0), Is.True);
            Assert.That(_state.IsPressed, Is.False);
            Assert.That(_state.Update(true, Hit, false, On, 0), Is.False);
        }

        [Test]
        public void Press_SlidingOffTheButton_Cancels()
        {
            _state.Update(true, Hit, true, On, 1);
            _state.Update(true, Hit, true, Off, 1);

            Assert.That(_state.IsPressed, Is.False);
            Assert.That(_state.Update(true, Hit, false, Off, 0), Is.False);
        }

        [Test]
        public void Press_ADragFromElsewhere_EndingOnTheButton_DoesNothing()
        {
            _state.Update(true, Hit, true, Off, 1);
            _state.Update(true, Hit, true, On, 1);

            Assert.That(_state.IsPressed, Is.False);
            Assert.That(_state.Update(true, Hit, false, On, 0), Is.False);
        }

        [Test]
        public void Press_HidingMidPress_Cancels()
        {
            _state.Update(true, Hit, true, On, 1);
            _state.Update(false, Hit, true, On, 1);

            Assert.That(_state.IsPressed, Is.False);
            Assert.That(_state.Update(true, Hit, false, On, 0), Is.False);
        }

        [Test]
        public void Press_ASecondFinger_Cancels_ForAPinch()
        {
            _state.Update(true, Hit, true, On, 1);
            _state.Update(true, Hit, true, On, 2);

            Assert.That(_state.IsPressed, Is.False);
            Assert.That(_state.Update(true, Hit, false, On, 0), Is.False);
        }
    }
}
