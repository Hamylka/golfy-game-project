using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;

namespace IsoGolf.Tests
{
    /// <summary>
    /// <c>IsoGolf.Generation</c> cannot read <c>ShotSettings</c>, so <see cref="LevelGenSettings"/> keeps a copy of the numbers
    /// the hazard step needs. A shot tuning that forgets the copy fails here.
    /// </summary>
    public class LevelGenShotModelTests
    {
        [Test]
        public void TheCopiedShotNumbers_MatchTheRealOnes()
        {
            var shot = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            LevelGenSettings gen = CourseGeneratorTests.Settings;

            Assert.That(gen.MinCarry, Is.EqualTo(shot.CarryFor(0f)));
            Assert.That(gen.FullCarry, Is.EqualTo(shot.CarryFor(1f)));
            Assert.That(gen.CarryAt(0.7f), Is.EqualTo(shot.CarryFor(0.7f)).Within(1e-4f));
            Assert.That(gen.FullReach, Is.EqualTo(ShotReach.FullPowerDistance(shot)).Within(1f));

            // Strategic obstacles S1: a safe leg's planned arc (PlannedArc).
            Assert.That(gen.LaunchAngle, Is.EqualTo(shot.LaunchAngle));
            Assert.That(gen.BallRadius, Is.EqualTo(shot.BallRadius));
            Assert.That(shot.ResponseOf(CourseSurface.Fairway).CarryScale, Is.EqualTo(1f), "PlannedArc plays safe legs from fairway at its carry");
        }
    }
}
