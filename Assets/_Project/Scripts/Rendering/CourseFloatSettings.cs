using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>How the whole course gently rises and falls in the air.</summary>
    [CreateAssetMenu(fileName = "CourseFloatSettings", menuName = "Iso Golf/Course Float Settings")]
    public sealed class CourseFloatSettings : ScriptableObject
    {
        [Tooltip("How far the course moves up and down from its resting height, in metres.")]
        [Min(0f)] [SerializeField] private float amplitude = 0.15f;

        [Tooltip("Seconds for one full rise and fall.")]
        [Min(0.1f)] [SerializeField] private float period = 7f;

        public float Amplitude => amplitude;
        public float Period => period;
    }
}
