using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class IsoCameraConventionTests
    {
        // Projects a world direction onto the screen axes at the screen centre: x = right, y = up.
        private static Vector2 ToScreen(Vector3 worldDirection)
        {
            Vector3 view = Quaternion.Inverse(IsoCameraConvention.Rotation) * worldDirection;
            return new Vector2(view.x, view.y);
        }

        [Test]
        public void WorldPositiveX_RunsUpTheScreenWithASlightLeanToTheRight()
        {
            Vector2 screen = ToScreen(Vector3.right);

            Assert.That(screen.x, Is.GreaterThan(0f));
            Assert.That(screen.y, Is.GreaterThan(screen.x * 2f));
        }

        [Test]
        public void WorldPositiveZ_GoesTowardTheLeftOfTheScreen()
        {
            Vector2 screen = ToScreen(Vector3.forward);

            Assert.That(screen.x, Is.LessThan(0f));
            Assert.That(Mathf.Abs(screen.x), Is.GreaterThan(Mathf.Abs(screen.y)));
        }

        [Test]
        public void Camera_LooksDownAtTheIsland()
        {
            Vector3 forward = IsoCameraConvention.Rotation * Vector3.forward;

            Assert.That(forward.y, Is.LessThan(0f));
        }
    }
}
