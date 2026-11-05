using IsoGolf.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The plain pieces behind the ball's view.</summary>
    public class BallViewPartsTests
    {
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
    }
}
