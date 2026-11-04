using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    public class CourseFloatTests
    {
        private const float Amplitude = 0.15f;
        private const float Period = 7f;
        private const float Tolerance = 0.0001f;

        [Test]
        public void StartsAtRest()
        {
            Assert.That(CourseFloat.Offset(0f, Amplitude, Period), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void PeaksAtItsAmplitudeInBothDirections()
        {
            Assert.That(CourseFloat.Offset(Period * 0.25f, Amplitude, Period), Is.EqualTo(Amplitude).Within(Tolerance));
            Assert.That(CourseFloat.Offset(Period * 0.75f, Amplitude, Period), Is.EqualTo(-Amplitude).Within(Tolerance));
        }

        [Test]
        public void RepeatsEveryPeriod()
        {
            float first = CourseFloat.Offset(1.3f, Amplitude, Period);
            float later = CourseFloat.Offset(1.3f + (Period * 3f), Amplitude, Period);

            Assert.That(later, Is.EqualTo(first).Within(Tolerance));
        }

        [Test]
        public void NeverLeavesItsRange()
        {
            for (float time = 0f; time < Period * 2f; time += 0.05f)
            {
                Assert.That(Mathf.Abs(CourseFloat.Offset(time, Amplitude, Period)), Is.LessThanOrEqualTo(Amplitude + Tolerance));
            }
        }

        [Test]
        public void NoAmplitudeMeansNoMovement()
        {
            Assert.That(CourseFloat.Offset(2f, 0f, Period), Is.EqualTo(0f));
        }
    }
}
