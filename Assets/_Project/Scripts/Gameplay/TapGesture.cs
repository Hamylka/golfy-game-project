using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>TEMPORARY, see <see cref="TestShotTap"/>. Tells a quick tap from a camera drag or a pinch.</summary>
    public sealed class TapGesture
    {
        private readonly float _maxSeconds;
        private readonly float _maxTravel;

        private bool _pressed;
        private bool _stillATap;
        private float _elapsed;
        private Vector2 _start;

        public TapGesture(float maxSeconds, float maxTravel)
        {
            _maxSeconds = maxSeconds;
            _maxTravel = maxTravel;
        }

        /// <summary>Call once a frame. Returns true on the frame a tap ends.</summary>
        public bool Update(bool pressed, Vector2 position, int fingers, float deltaTime)
        {
            if (!pressed)
            {
                bool tapped = _pressed && _stillATap;
                _pressed = false;
                return tapped;
            }

            if (!_pressed)
            {
                _pressed = true;
                _stillATap = true;
                _elapsed = 0f;
                _start = position;
            }

            _elapsed += deltaTime;
            if (fingers > 1 || _elapsed > _maxSeconds || Vector2.Distance(position, _start) > _maxTravel)
            {
                _stillATap = false;
            }

            return false;
        }
    }
}
