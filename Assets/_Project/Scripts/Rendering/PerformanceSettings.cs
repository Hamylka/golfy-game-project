using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>Performance targets for the player.</summary>
    [CreateAssetMenu(fileName = "PerformanceSettings", menuName = "Iso Golf/Performance Settings")]
    public sealed class PerformanceSettings : ScriptableObject
    {
        [Tooltip("Frames per second to aim for. Android caps the game at 30 unless this is set.")]
        [Min(1)] [SerializeField] private int targetFrameRate = 60;

        public int TargetFrameRate => targetFrameRate;
    }
}
