using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The camera's glide after a drag is released: it measures how fast the view was really moving just
    /// before release, then slows down exponentially, like vertical scrolling on a phone.
    /// </summary>
    public sealed class PanMomentum
    {
        // Enough for the velocity window at any frame rate a phone reaches. If frames are shorter still,
        // the oldest samples drop out and the window is simply shorter.
        private const int SampleCapacity = 32;

        private readonly float _timeConstant;
        private readonly float _velocityWindow;
        private readonly float _minReleaseSpeed;
        private readonly float _stopSpeed;
        private readonly Vector3[] _moved = new Vector3[SampleCapacity];
        private readonly float[] _durations = new float[SampleCapacity];
        private int _sampleCount;
        private int _nextSample;
        private Vector3 _velocity;

        /// <param name="timeConstant">Seconds for the speed to fall to about 37%. Longer glides further.</param>
        /// <param name="velocityWindow">How many seconds before release count toward the release speed.</param>
        /// <param name="minReleaseSpeed">Slower releases don't glide at all, in metres per second.</param>
        /// <param name="stopSpeed">The glide ends below this speed, in metres per second.</param>
        public PanMomentum(float timeConstant, float velocityWindow, float minReleaseSpeed, float stopSpeed)
        {
            _timeConstant = timeConstant;
            _velocityWindow = velocityWindow;
            _minReleaseSpeed = minReleaseSpeed;
            _stopSpeed = stopSpeed;
        }

        public bool IsGliding => _velocity != Vector3.zero;

        public Vector3 Velocity => _velocity;

        /// <summary>Forgets the drag so far, for when a new one starts.</summary>
        public void ClearSamples()
        {
            _sampleCount = 0;
            _nextSample = 0;
        }

        /// <summary>
        /// Records how far the camera really moved this frame. Call it every frame of a drag, including
        /// frames with no movement: a finger that pauses before lifting must not glide.
        /// </summary>
        public void AddSample(Vector3 moved, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _moved[_nextSample] = moved;
            _durations[_nextSample] = deltaTime;
            _nextSample = (_nextSample + 1) % SampleCapacity;
            _sampleCount = Mathf.Min(_sampleCount + 1, SampleCapacity);
        }

        /// <summary>The finger lifted: starts gliding if the recent drag was fast enough.</summary>
        public void Release()
        {
            Vector3 velocity = RecentVelocity();
            _velocity = velocity.magnitude >= _minReleaseSpeed ? velocity : Vector3.zero;
            ClearSamples();
        }

        /// <summary>Ends the glide at once, as a finger landing on a scrolling list does.</summary>
        public void Stop()
        {
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Advances the glide and returns how far to move. This integrates the decay exactly rather than
        /// stepping it, so the glide covers the same distance at any frame rate.
        /// </summary>
        public Vector3 Step(float deltaTime)
        {
            if (!IsGliding || deltaTime <= 0f)
            {
                return Vector3.zero;
            }

            float decay = Mathf.Exp(-deltaTime / _timeConstant);
            Vector3 displacement = _velocity * (_timeConstant * (1f - decay));
            _velocity *= decay;
            if (_velocity.magnitude < _stopSpeed)
            {
                _velocity = Vector3.zero;
            }

            return displacement;
        }

        /// <summary>Stops the glide along any axis where a limit cut the last move short, with no bounce.</summary>
        public void ApplyLimits(Vector3 requested, Vector3 actual)
        {
            const float Slack = 0.0001f;
            if (Mathf.Abs(actual.x) < Mathf.Abs(requested.x) - Slack)
            {
                _velocity.x = 0f;
            }

            if (Mathf.Abs(actual.z) < Mathf.Abs(requested.z) - Slack)
            {
                _velocity.z = 0f;
            }

            if (_velocity.magnitude < _stopSpeed)
            {
                _velocity = Vector3.zero;
            }
        }

        private Vector3 RecentVelocity()
        {
            Vector3 moved = Vector3.zero;
            float duration = 0f;
            for (int i = 1; i <= _sampleCount && duration < _velocityWindow; i++)
            {
                int index = (_nextSample - i + SampleCapacity) % SampleCapacity;
                moved += _moved[index];
                duration += _durations[index];
            }

            return duration > 0f ? moved / duration : Vector3.zero;
        }
    }
}
