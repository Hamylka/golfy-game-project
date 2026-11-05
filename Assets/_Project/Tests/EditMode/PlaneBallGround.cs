using System;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>A perfect plane through the origin, so a test's expected result can be worked out by hand.</summary>
    public sealed class PlaneBallGround : IBallGround
    {
        public const float WaterLevel = -0.5f;

        private readonly Vector2 _tilt;
        private readonly Func<float, float, CourseSurface> _surface;

        public PlaneBallGround(Vector2 tilt, Func<float, float, CourseSurface> surface)
        {
            _tilt = tilt;
            _surface = surface;
        }

        /// <summary>Far away by default, for tests that aren't about the hole.</summary>
        public Vector2 Hole { get; set; } = new Vector2(100000f, 100000f);

        public static PlaneBallGround All(CourseSurface surface, Vector2 tilt = default)
        {
            return new PlaneBallGround(tilt, (x, z) => surface);
        }

        public float Height(float x, float z) => (x * _tilt.x) + (z * _tilt.y);

        public float LandingHeight(float x, float z)
        {
            return Surface(x, z) == CourseSurface.Water ? WaterLevel : Height(x, z);
        }

        public Vector2 Slope(float x, float z) => _tilt;

        public Vector3 Normal(float x, float z) => new Vector3(-_tilt.x, 1f, -_tilt.y).normalized;

        public CourseSurface Surface(float x, float z) => _surface(x, z);
    }
}
