using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>
    /// How the gauge's fill follows the shot's power. It only eases what's drawn: the shot always uses the real power.
    /// </summary>
    public static class GaugeMotion
    {
        /// <summary>
        /// Closes a share of the gap that depends only on the time passed, so the fill moves the same at any frame
        /// rate and can't overshoot. <paramref name="smoothTime"/> is the seconds to close about 63% of the gap; 0 follows at once.
        /// </summary>
        public static float Toward(float current, float target, float smoothTime, float deltaTime)
        {
            if (smoothTime <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / smoothTime));
        }
    }
}
