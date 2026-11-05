using IsoGolf.Rendering;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>Tells the camera where the ball is and what it's doing, since the camera knows nothing of the ball.</summary>
    [RequireComponent(typeof(BallView))]
    public sealed class BallCameraFollow : MonoBehaviour
    {
        [SerializeField] private IslandCamera islandCamera;

        private BallView _ball;

        private void Awake()
        {
            _ball = GetComponent<BallView>();
        }

        // After every Update, so the camera sees the ball where it's drawn this frame.
        private void LateUpdate()
        {
            if (_ball.Simulation == null)
            {
                return;
            }

            // The local position leaves out the course's floating, so the camera doesn't bob with it. This relies on
            // the container resting at the origin, which holds until something moves a course as a whole.
            islandCamera.Follow(transform.localPosition, ToFollowState(_ball.Simulation.State));
        }

        private static FollowedBallState ToFollowState(BallState state)
        {
            switch (state)
            {
                case BallState.Resting:
                    return FollowedBallState.AtRest;
                case BallState.Flying:
                case BallState.Rolling:
                    return FollowedBallState.Moving;
                default:
                    return FollowedBallState.Holding;
            }
        }
    }
}
