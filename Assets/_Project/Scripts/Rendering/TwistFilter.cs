using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Decides how much of the angle between two fingers counts as a twist. A pinch never keeps its two fingers on one
    /// line, so a dead zone lets a plain pinch go by without wobbling the heading: nothing turns until the fingers have
    /// rotated past it, and from there on the view follows them one to one. Fingers too close together give a noisy
    /// angle and are ignored.
    /// </summary>
    public sealed class TwistFilter
    {
        private readonly float _deadZone;
        private readonly float _minSpread;
        private float _accumulated;
        private bool _engaged;

        /// <param name="deadZone">How far the fingers must rotate before the view follows, in degrees.</param>
        /// <param name="minSpread">The least distance between the fingers, in viewport units, for the angle to count.</param>
        public TwistFilter(float deadZone, float minSpread)
        {
            _deadZone = Mathf.Max(0f, deadZone);
            _minSpread = Mathf.Max(0f, minSpread);
        }

        /// <summary>Forgets what the fingers have done so far. Call when a two-finger gesture starts.</summary>
        public void Reset()
        {
            _accumulated = 0f;
            _engaged = false;
        }

        /// <summary>
        /// Takes one frame's rotation of the fingers and returns how many degrees the view should turn.
        /// </summary>
        /// <param name="degrees">The rotation since the last frame, in degrees.</param>
        /// <param name="spread">The distance between the fingers on screen, in viewport units.</param>
        public float Step(float degrees, float spread)
        {
            if (spread < _minSpread)
            {
                return 0f;
            }

            if (_engaged)
            {
                return degrees;
            }

            // Only the part past the dead zone counts, so the view doesn't jump by the zone's width as it engages.
            _accumulated += degrees;
            if (Mathf.Abs(_accumulated) <= _deadZone)
            {
                return 0f;
            }

            _engaged = true;
            return _accumulated - (Mathf.Sign(_accumulated) * _deadZone);
        }
    }
}
