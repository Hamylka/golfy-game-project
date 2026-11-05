using IsoGolf.Gameplay;
using IsoGolf.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Pins the hole-in-one balance on the hand-made par 3 with the shipped settings. The ceilings are set by
    /// the tuning PR from a real sweep; if one fails after a settings change, read the report in the log first.
    /// </summary>
    public class ShotSweepTests
    {
        private ShotSettings _settings;
        private CourseShapeSettings _shape;
        private BallGround _ground;

        [SetUp]
        public void SetUp()
        {
            _settings = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            _shape = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>("Assets/_Project/Settings/Par3CourseShape.asset");
            _ground = new BallGround(_shape);
        }

        [Test]
        public void Sweep_IsRepeatable()
        {
            ShotSweepResult a = ShotSweep.Run(_settings, _ground, _ground.Tee, _ground.Hole, 3f, 0.5f, 0.9f, 1f, 0.05f);
            ShotSweepResult b = ShotSweep.Run(_settings, _ground, _ground.Tee, _ground.Hole, 3f, 0.5f, 0.9f, 1f, 0.05f);

            Assert.That(b.Holed, Is.EqualTo(a.Holed));
            Assert.That(b.HoledPowers, Is.EqualTo(a.HoledPowers));
            Assert.That(a.Shots, Is.EqualTo(13 * 3));
        }

        [Test]
        public void HoleInOne_OnThePar3_IsRare()
        {
            // The baseline sweep (+-6 deg at 0.1, power 0.50 to 1.00 at 0.05) logged here for the PR description.
            ShotSweepResult grid = ShotSweep.Run(_settings, _ground, _ground.Tee, _ground.Hole);
            // Powers between the 0.05 steps too.
            ShotSweepResult fine = ShotSweep.Run(_settings, _ground, _ground.Tee, _ground.Hole, 6f, 0.1f, 0.5f, 1f, 0.01f);

            Debug.Log($"Sweep grid: {grid.Holed}/{grid.Shots} holed, widest window {grid.WidestWindowDegrees:0.0} deg, powers {grid.HoledPowers}\n" +
                      $"Sweep fine: {fine.Holed}/{fine.Shots} holed ({fine.HoledShare:P2}), widest window {fine.WidestWindowDegrees:0.0} deg, powers {fine.HoledPowers}");

            Assert.That(fine.HoledShare, Is.LessThan(MaxHoledShare), "Holes in one should stay rare");

            // The pull gives exactly power 1 past 30% of the screen, so full power must not hole: it would make an ace a matter of aim alone.
            ShotSweepResult full = ShotSweep.Run(_settings, _ground, _ground.Tee, _ground.Hole, 6f, 0.1f, 1f, 1f, 1f);
            Assert.That(full.Holed, Is.EqualTo(0), "Full power must overshoot the hole");
        }

        // Measured 1.0% with the tuned values (61 of 6171); the baseline was 0.68%. The ceiling guards against a drift, not against that figure.
        private const float MaxHoledShare = 0.015f;
    }
}
