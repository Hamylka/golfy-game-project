using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Sandbox stand-in for the shot input: a touch starting near the ball is kept away from the camera,
    /// and logs a line. Main uses the real gesture, SlingshotShotInput. This stays for Sandbox_Camera.
    /// </summary>
    [RequireComponent(typeof(IslandCamera))]
    public sealed class SandboxBallTouchClaim : MonoBehaviour
    {
        [SerializeField] private Transform ball;

        [Tooltip("How close to the ball a touch must start, as a share of the screen width. The ball itself is tiny on screen, so this is bigger than the ball.")]
        [SerializeField, Range(0.01f, 0.3f)] private float touchRadius = 0.08f;

        private Camera _camera;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            GetComponent<IslandCamera>().TouchClaim = IsOnBall;
        }

        private bool IsOnBall(Vector2 viewport)
        {
            Vector3 ballOnScreen = _camera.WorldToScreenPoint(ball.position);
            var touch = new Vector2((viewport.x + 1f) * 0.5f * Screen.width, (viewport.y + 1f) * 0.5f * Screen.height);
            bool claimed = Vector2.Distance(touch, ballOnScreen) <= touchRadius * Screen.width;

            if (claimed)
            {
                Debug.Log("[Sandbox] Touch started on the ball: the camera ignores it.");
            }

            return claimed;
        }
    }
}
