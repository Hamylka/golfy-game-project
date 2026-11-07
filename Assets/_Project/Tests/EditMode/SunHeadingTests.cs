using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>The sun's yaw for a course heading: unchanged on +X, turning with the heading.</summary>
    public class SunHeadingTests
    {
        private const float AuthoredYaw = 215f;

        private static Vector2 Rotated(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        [Test]
        public void PlusX_KeepsTheAuthoredYaw()
        {
            Assert.AreEqual(AuthoredYaw, SunHeading.YawFor(AuthoredYaw, Vector2.right), 1e-3f);
        }

        [Test]
        public void Heading_ScalesWithLength()
        {
            Assert.AreEqual(AuthoredYaw, SunHeading.YawFor(AuthoredYaw, new Vector2(40f, 0f)), 1e-3f);
        }

        [TestCase(35f)]
        [TestCase(-35f)]
        [TestCase(80f)]
        [TestCase(-80f)]
        public void Sun_TurnsWithTheCourse(float degrees)
        {
            // A heading toward +Z lowers the camera's yaw, and the sun follows it by the same amount.
            Assert.AreEqual(AuthoredYaw - degrees, SunHeading.YawFor(AuthoredYaw, Rotated(degrees)), 1e-2f);
        }

        [TestCase(0f)]
        [TestCase(35f)]
        [TestCase(-80f)]
        public void Sun_KeepsItsPlaceRelativeToTheCamera(float degrees)
        {
            Vector2 heading = Rotated(degrees);
            float sunYaw = SunHeading.YawFor(AuthoredYaw, heading);
            float cameraYaw = IsoCameraConvention.YawFor(heading);
            Assert.AreEqual(AuthoredYaw - IsoCameraConvention.StraightYaw, sunYaw - cameraYaw, 1e-2f);
        }

        [Test]
        public void CourseHeading_StraightCourse_IsPlusXWhateverTheHoleOffset()
        {
            var line = new[] { new Vector2(-14f, 0f), new Vector2(14.2f, 0.4f) };
            Assert.AreEqual(Vector2.right, SunHeading.CourseHeading(line, line[0], line[1]));
        }

        [Test]
        public void CourseHeading_BentCourse_IsTeeToHole()
        {
            var line = new[] { new Vector2(0f, 0f), new Vector2(20f, 0f), new Vector2(40f, 10f) };
            Assert.AreEqual(new Vector2(40f, 10f), SunHeading.CourseHeading(line, line[0], line[2]));
        }

        [Test]
        public void ZeroHeading_KeepsTheAuthoredYaw()
        {
            Assert.AreEqual(AuthoredYaw, SunHeading.YawFor(AuthoredYaw, Vector2.zero), 1e-3f);
        }
    }
}
