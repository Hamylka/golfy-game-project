using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Moves the course container up and down, purely for the look. Everything on the course lives under
    /// this object, including the ball from M2, whose simulation runs in this object's local space. The
    /// offset is never an input to gameplay, so a shot lands the same wherever the course is in its motion.
    /// The camera and clouds stay outside, which is what makes the motion visible.
    /// </summary>
    public sealed class CourseFloat : MonoBehaviour
    {
        [SerializeField] private CourseFloatSettings settings;

        private Vector3 _restPosition;

        /// <summary>The height offset at a given time. Starts at zero, and a sine has no sudden changes of speed.</summary>
        public static float Offset(float time, float amplitude, float period)
        {
            return amplitude * Mathf.Sin(time * Mathf.PI * 2f / period);
        }

        private void Awake()
        {
            _restPosition = transform.localPosition;
        }

        private void Update()
        {
            float offset = Offset(Time.time, settings.Amplitude, settings.Period);
            transform.localPosition = _restPosition + (Vector3.up * offset);
        }
    }
}
