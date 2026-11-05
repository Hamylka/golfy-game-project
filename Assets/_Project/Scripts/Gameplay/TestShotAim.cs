using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>TEMPORARY, see <see cref="TestShotTap"/>. The power that plays a shot over a given distance.</summary>
    public static class TestShotAim
    {
        /// <summary>
        /// From the green, the power whose putt rolls this far on level ground. From anywhere else, the power
        /// whose first landing is this far on level ground, after the lie's penalty. Too far means full power.
        /// </summary>
        public static float Power(ShotSettings settings, CourseSurface lie, float distance)
        {
            if (lie == CourseSurface.Green)
            {
                return Mathf.InverseLerp(settings.PuttDistanceFor(0f), settings.PuttDistanceFor(1f), distance);
            }

            float carry = distance / settings.ResponseOf(lie).CarryScale;
            return Mathf.InverseLerp(settings.CarryFor(0f), settings.CarryFor(1f), carry);
        }
    }
}
