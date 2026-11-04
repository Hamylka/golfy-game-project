using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class IsoCameraConventionTests
    {
        private const float Tolerance = 0.0001f;

        // Projects a world direction onto the orthographic screen plane: x = right, y = up.
        private static Vector2 ToScreen(Vector3 worldDirection)
        {
            Vector3 view = Quaternion.Inverse(IsoCameraConvention.Rotation) * worldDirection;
            return new Vector2(view.x, view.y);
        }

        [Test]
        public void WorldPositiveX_GoesToScreenTopRight()
        {
            Vector2 screen = ToScreen(Vector3.right);

            Assert.That(screen.x, Is.GreaterThan(0f));
            Assert.That(screen.y, Is.GreaterThan(0f));
        }

        [Test]
        public void WorldPositiveZ_GoesToScreenTopLeft()
        {
            Vector2 screen = ToScreen(Vector3.forward);

            Assert.That(screen.x, Is.LessThan(0f));
            Assert.That(screen.y, Is.GreaterThan(0f));
        }

        [Test]
        public void GroundDiagonals_ProjectAtTwoToOneRatio()
        {
            // These two ground diagonals are the screen-horizontal and screen-vertical axes of the diamond.
            float width = ToScreen(new Vector3(1f, 0f, -1f)).magnitude;
            float height = ToScreen(new Vector3(1f, 0f, 1f)).magnitude;

            Assert.That(width / height, Is.EqualTo(2f).Within(Tolerance));
        }
    }
}
