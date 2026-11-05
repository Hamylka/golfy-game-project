using System;
using IsoGolf.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// The drag on the ball, pulled back like a slingshot. It feeds <see cref="ShotGesture"/> from the pointer,
    /// and keeps the camera out of a touch that belongs to the shot.
    /// </summary>
    [RequireComponent(typeof(BallView))]
    public sealed class SlingshotShotInput : MonoBehaviour, IShotInput
    {
        [SerializeField] private ShotSettings shot;
        [SerializeField] private IslandCamera islandCamera;

        private BallView _ball;
        private Camera _camera;
        private ShotGesture _gesture;
        private Vector3 _worldDirection;

        public bool IsAiming => _gesture != null && _gesture.IsAiming;

        public Vector2 Direction { get; private set; }

        public float Power => _gesture != null && _ball.Simulation != null ? shot.PowerForPull(_gesture.Pull, _ball.Simulation.IsPutt) : 0f;

        public event Action<Vector2, float> ShotReleased;

        private bool BallCanBeGrabbed => _ball.Simulation != null && _ball.Simulation.State == BallState.Resting;

        private Vector2 BallOnScreen => _camera.WorldToScreenPoint(transform.position);

        private void Awake()
        {
            _ball = GetComponent<BallView>();
            _camera = islandCamera.GetComponent<Camera>();
            _gesture = new ShotGesture(shot.TouchRadius, shot.FullPowerPull, shot.HoldSeconds);
        }

        private void OnEnable()
        {
            islandCamera.TouchClaim = IsTouchOnBall;
        }

        private void OnDisable()
        {
            islandCamera.TouchClaim = null;
            islandCamera.GesturesEnabled = true;
            _gesture.Cancel();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _gesture.Cancel();
            }
        }

        private void Update()
        {
            ShotPointer pointer = ReadPointer();
            bool released = _gesture.Update(pointer, BallOnScreen, Screen.width, BallCanBeGrabbed, Time.unscaledDeltaTime);

            // Blocks the mouse wheel as well, and the router stays out of the touch until every finger lifts.
            islandCamera.GesturesEnabled = !_gesture.IsActive;

            if (released)
            {
                ShotReleased?.Invoke(Direction, Power);
            }
            else if (_gesture.IsAiming)
            {
                UpdateDirection(pointer.Position);
            }
        }

        private void UpdateDirection(Vector2 finger)
        {
            // A released shot uses the last direction read, the one the player saw, not the position it lifted from.
            if (ShotGesture.TryAim(_camera.ScreenPointToRay(finger), transform.position, out Vector3 world))
            {
                _worldDirection = world;
            }

            Vector3 local = transform.parent.InverseTransformDirection(_worldDirection);
            Direction = new Vector2(local.x, local.z).normalized;
        }

        /// <summary>Asked by the camera when a first finger lands, in its viewport coordinates from -1 to 1.</summary>
        private bool IsTouchOnBall(Vector2 viewport)
        {
            var finger = new Vector2((viewport.x + 1f) * 0.5f * Screen.width, (viewport.y + 1f) * 0.5f * Screen.height);
            return BallCanBeGrabbed && _gesture.IsOnBall(finger, BallOnScreen, Screen.width);
        }

        private static ShotPointer ReadPointer()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                int fingers = CountFingers(touchscreen);
                TouchControl primary = touchscreen.primaryTouch;
                if (fingers > 0)
                {
                    // The primary touch is the first finger down. It stays the first even when a second one lands.
                    return new ShotPointer
                    {
                        Pressed = primary.press.isPressed,
                        Position = primary.position.ReadValue(),
                        Fingers = fingers,
                    };
                }

                if (primary.phase.ReadValue() == TouchPhase.Canceled)
                {
                    return new ShotPointer { Canceled = true };
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return default;
            }

            bool pressed = mouse.leftButton.isPressed;
            return new ShotPointer { Pressed = pressed, Position = mouse.position.ReadValue(), Fingers = pressed ? 1 : 0 };
        }

        private static int CountFingers(Touchscreen touchscreen)
        {
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
    }
}
