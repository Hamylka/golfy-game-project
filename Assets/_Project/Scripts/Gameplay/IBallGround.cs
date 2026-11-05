using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// The ground as the ball's simulation asks about it, in the course's local space. It's an interface so
    /// tests can roll a ball on an exact plane, where the expected result can be worked out by hand.
    /// </summary>
    public interface IBallGround
    {
        /// <summary>Where the hole is, on the ground plane (X, Z).</summary>
        Vector2 Hole { get; }

        /// <summary>The height a ball sits at while rolling or resting.</summary>
        float Height(float x, float z);

        /// <summary>The height that ends a flight: the ground, or the water's surface over a pond.</summary>
        float LandingHeight(float x, float z);

        /// <summary>The rise per metre along X and along Z that the ball feels. It points uphill.</summary>
        Vector2 Slope(float x, float z);

        /// <summary>The upward direction of the ground the ball feels, for bounces.</summary>
        Vector3 Normal(float x, float z);

        CourseSurface Surface(float x, float z);
    }
}
