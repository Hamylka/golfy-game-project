using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The fixed camera setup every system relies on. See "World and camera" in CLAUDE.md.
    /// </summary>
    public static class IsoCameraConvention
    {
        // Yaw 80 rather than 90 gives the slight lean: world +X, the play axis, runs up the
        // screen and a little to the right.
        public static readonly Vector3 EulerAngles = new Vector3(40f, 80f, 0f);

        public const float VerticalFieldOfView = 45f;

        public static Quaternion Rotation => Quaternion.Euler(EulerAngles);
    }
}
