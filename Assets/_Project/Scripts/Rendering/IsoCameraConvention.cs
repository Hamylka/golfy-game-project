using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The camera setup every system relies on. See "World and camera" in CLAUDE.md.
    /// </summary>
    public static class IsoCameraConvention
    {
        // Yaw 80 rather than 90 gives the slight lean: world +X, the play axis, runs up the
        // screen and a little to the right.
        public static readonly Vector3 EulerAngles = new Vector3(40f, 80f, 0f);

        public const float VerticalFieldOfView = 45f;

        /// <summary>The pitch, which never changes. The camera never rolls either.</summary>
        public static float Pitch => EulerAngles.x;

        /// <summary>The yaw for a course heading along +X.</summary>
        public static float StraightYaw => EulerAngles.y;

        /// <summary>The rotation for a course heading along +X.</summary>
        public static Quaternion Rotation => Quaternion.Euler(EulerAngles);

        /// <summary>The camera's rotation at this yaw, in degrees: the fixed pitch, no roll.</summary>
        public static Quaternion RotationAt(float yaw)
        {
            return Quaternion.Euler(Pitch, yaw, 0f);
        }

        /// <summary>
        /// The yaw that shows a ground heading (x is world X, y is world Z) the way +X is shown: up the screen with
        /// the same slight lean to the right. A heading turned toward +Z, which is left on screen, lowers the yaw.
        /// </summary>
        public static float YawFor(Vector2 heading)
        {
            return StraightYaw - Vector2.SignedAngle(Vector2.right, heading);
        }

        /// <summary>The ground heading a yaw shows up the screen, the inverse of <see cref="YawFor"/>.</summary>
        public static Vector2 HeadingOf(float yaw)
        {
            float radians = (StraightYaw - yaw) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
