using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Maps a course's tee-to-hole heading to the sun's yaw. The sun keeps the same place on screen as on a course
    /// heading along +X (top-left), because it turns with the camera's starting yaw. Pure maths, for tests.
    /// </summary>
    public static class SunHeading
    {
        /// <summary>
        /// The course's overall heading, tee to hole. A straight course (one leg) counts as +X whatever small offset to
        /// the side its hole has, as the camera's heading reads it, so the sun does not shift for it.
        /// </summary>
        public static Vector2 CourseHeading(System.Collections.Generic.IReadOnlyList<Vector2> centreline, Vector2 tee, Vector2 hole)
        {
            bool bent = centreline != null && centreline.Count > 2;
            return bent ? hole - tee : Vector2.right;
        }

        /// <summary>
        /// The sun's yaw, in degrees, for a course heading this way.
        /// </summary>
        /// <param name="plusXYaw">The sun's yaw as authored in the scene, which is right for a course heading along +X.</param>
        /// <param name="heading">The ground heading from tee to hole (x is world X, y is world Z).</param>
        public static float YawFor(float plusXYaw, Vector2 heading)
        {
            if (heading.sqrMagnitude < 1e-8f)
            {
                return plusXYaw;
            }

            return plusXYaw + (IsoCameraConvention.YawFor(heading) - IsoCameraConvention.StraightYaw);
        }
    }
}
