using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Applies the target frame rate. It has to be set explicitly: with vSync off and no target, Android
    /// runs the game at 30 fps however cheap the frame is, as the M1.5 capture on the phone showed.
    /// </summary>
    public sealed class FrameRateCap : MonoBehaviour
    {
        [SerializeField] private PerformanceSettings settings;

        private void Awake()
        {
            Application.targetFrameRate = settings.TargetFrameRate;
        }
    }
}
