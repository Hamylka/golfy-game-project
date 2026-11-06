using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// How far a full-power shot goes, carry and roll, on flat fairway with no hole in the way. The camera faces the
    /// hole from within this reach. Slopes and rough are left out on purpose: the heading should be predictable, so it
    /// is read from one number per level, not from the lie of each shot.
    /// </summary>
    public static class ShotReach
    {
        // A full shot settles in a few seconds; this is a guard against a setting that never stops the ball.
        private const int MaxSteps = 60 * 60;

        public static float FullPowerDistance(ShotSettings settings)
        {
            var simulation = new BallSimulation(new FlatFairway(), settings, Vector2.zero);
            simulation.Shoot(Vector2.right, 1f);
            for (int i = 0; i < MaxSteps && simulation.State != BallState.Resting; i++)
            {
                simulation.Step();
            }

            return new Vector2(simulation.Position.x, simulation.Position.z).magnitude;
        }

        private sealed class FlatFairway : IBallGround
        {
            public Vector2 Hole => new Vector2(1e6f, 1e6f);

            public float Height(float x, float z) => 0f;

            public float LandingHeight(float x, float z) => 0f;

            public Vector2 Slope(float x, float z) => Vector2.zero;

            public Vector3 Normal(float x, float z) => Vector3.up;

            public CourseSurface Surface(float x, float z) => CourseSurface.Fairway;
        }
    }
}
