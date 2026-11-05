using IsoGolf.Generation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// TEMPORARY, until the shot input PR, which deletes this and <see cref="TapGesture"/> and <see cref="TestShotAim"/>.
    /// Tap or click the ground and the ball is played toward that spot, on the phone as in the Editor. After
    /// the ball is holed, a tap returns it to the tee. Drags and pinches still move the camera.
    /// </summary>
    [RequireComponent(typeof(BallView))]
    public sealed class TestShotTap : MonoBehaviour
    {
        // Named here rather than in a settings asset, because this class is short-lived.
        private const float TapMaxSeconds = 0.3f;
        private const float TapMaxTravelPixels = 20f;

        [SerializeField] private Camera viewCamera;

        private readonly TapGesture _tap = new TapGesture(TapMaxSeconds, TapMaxTravelPixels);
        private BallView _ball;

        private void Awake()
        {
            _ball = GetComponent<BallView>();
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || _ball.Simulation == null)
            {
                return;
            }

            Vector2 position = pointer.position.ReadValue();
            if (_tap.Update(pointer.press.isPressed, position, CountFingers(), Time.unscaledDeltaTime))
            {
                PlayToward(position);
            }
        }

        private static int CountFingers()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return 1;
            }

            int fingers = 0;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.press.isPressed)
                {
                    fingers++;
                }
            }

            return fingers;
        }

        private void PlayToward(Vector2 screenPosition)
        {
            BallSimulation ball = _ball.Simulation;
            if (ball.State == BallState.Holed)
            {
                _ball.ReturnToTee();
                return;
            }

            // The tap meets the course's base level, which is close enough for a test shot.
            Transform course = transform.parent;
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            var ground = new Plane(course.up, course.position);
            if (!ground.Raycast(ray, out float distance))
            {
                return;
            }

            Vector3 target = course.InverseTransformPoint(ray.GetPoint(distance));
            var toTarget = new Vector2(target.x - ball.Position.x, target.z - ball.Position.z);
            CourseSurface lie = _ball.Ground.Surface(ball.Position.x, ball.Position.z);
            ball.Shoot(toTarget, TestShotAim.Power(_ball.Shot, lie, toTarget.magnitude));
        }
    }
}
