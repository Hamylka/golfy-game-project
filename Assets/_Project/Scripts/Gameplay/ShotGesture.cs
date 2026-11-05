using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>The pointer as the gesture needs it, read once a frame.</summary>
    public struct ShotPointer
    {
        /// <summary>Whether the finger the gesture follows is down. A second finger doesn't count.</summary>
        public bool Pressed;

        /// <summary>The system took the touch away, for example for a navigation swipe. That isn't a release.</summary>
        public bool Canceled;

        public Vector2 Position;

        /// <summary>All fingers on the screen, so a pinch landing on the ball doesn't start a shot.</summary>
        public int Fingers;
    }

    /// <summary>
    /// The slingshot gesture as plain logic, in screen pixels. A press on the ball grabs it, a pull beyond the
    /// touch radius aims, and letting go shoots. Letting go inside the radius cancels.
    ///
    /// Power comes from the pull on screen, not on the ground, so a pull feels the same in every direction and
    /// at every zoom. The radius is both the grab area and the cancel area on purpose: a smaller cancel area
    /// would turn a tap on the edge of the grab area into a weak shot.
    /// </summary>
    public sealed class ShotGesture
    {
        private readonly float _touchRadius;
        private readonly float _fullPowerPull;
        private readonly float _holdSeconds;

        private bool _wasPressed;
        private bool _armed;
        private float _held;

        /// <param name="touchRadius">As a share of the screen's width.</param>
        /// <param name="fullPowerPull">As a share of the screen's width.</param>
        /// <param name="holdSeconds">How long the ball must be held before a pull counts. 0 means no wait.</param>
        public ShotGesture(float touchRadius, float fullPowerPull, float holdSeconds)
        {
            _touchRadius = touchRadius;
            _fullPowerPull = fullPowerPull;
            _holdSeconds = holdSeconds;
        }

        /// <summary>Whether a touch belongs to the shot, from the grab until it ends or is canceled.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Whether letting go now would play a shot.</summary>
        public bool IsAiming { get; private set; }

        /// <summary>
        /// How far the pull has gone, from 0 at the touch radius to 1 at full pull. <see cref="ShotSettings"/> turns
        /// this into power. Only meaningful while <see cref="IsAiming"/>.
        /// </summary>
        public float Pull { get; private set; }

        public bool IsOnBall(Vector2 finger, Vector2 ball, float screenWidth)
        {
            return Vector2.Distance(finger, ball) <= _touchRadius * screenWidth;
        }

        /// <summary>
        /// Call once a frame. <paramref name="ball"/> is the ball's center on screen. Returns true on the frame
        /// a shot is released, with <see cref="Pull"/> as it last read while the finger was down.
        /// </summary>
        public bool Update(ShotPointer pointer, Vector2 ball, float screenWidth, bool ballCanBeGrabbed, float deltaTime)
        {
            bool landed = pointer.Pressed && !_wasPressed;
            _wasPressed = pointer.Pressed;

            if (!IsActive)
            {
                if (landed && pointer.Fingers == 1 && ballCanBeGrabbed && IsOnBall(pointer.Position, ball, screenWidth))
                {
                    IsActive = true;
                    _armed = _holdSeconds <= 0f;
                    _held = 0f;
                }

                return false;
            }

            if (!pointer.Pressed)
            {
                bool shot = IsAiming && !pointer.Canceled;
                Reset();
                return shot;
            }

            Hold(pointer.Position, ball, screenWidth, deltaTime);
            return false;
        }

        /// <summary>Drops the shot, for example when the app loses focus. The finger must lift before the next one.</summary>
        public void Cancel()
        {
            Reset();
        }

        /// <summary>
        /// The direction a shot goes, as a horizontal world vector. It's the finger's point on a level plane at the
        /// ball, seen from the ball and reversed. A straight line on the ground stays straight on screen, so the
        /// shot line on screen runs from the finger through the ball. Returns false if the finger's ray misses the plane.
        /// </summary>
        public static bool TryAim(Ray finger, Vector3 ball, out Vector3 direction)
        {
            direction = default;
            var plane = new Plane(Vector3.up, ball);
            if (!plane.Raycast(finger, out float distance))
            {
                return false;
            }

            Vector3 away = ball - finger.GetPoint(distance);
            away.y = 0f;
            if (away == Vector3.zero)
            {
                return false;
            }

            direction = away.normalized;
            return true;
        }

        private void Hold(Vector2 finger, Vector2 ball, float screenWidth, float deltaTime)
        {
            float pull = Vector2.Distance(finger, ball) / screenWidth;

            if (!_armed)
            {
                // With a hold time set, pulling away too soon drops the shot rather than aiming it.
                if (pull > _touchRadius)
                {
                    Reset();
                    return;
                }

                _held += deltaTime;
                _armed = _held >= _holdSeconds;
                return;
            }

            IsAiming = pull > _touchRadius;
            Pull = IsAiming ? Mathf.InverseLerp(_touchRadius, _fullPowerPull, pull) : 0f;
        }

        private void Reset()
        {
            // Pull stays as it last read, for the frame a shot is released.
            IsActive = false;
            IsAiming = false;
        }
    }
}
