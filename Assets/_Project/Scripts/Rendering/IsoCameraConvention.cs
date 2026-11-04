using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The fixed camera orientation every system relies on. See "World and camera" in CLAUDE.md.
    /// </summary>
    public static class IsoCameraConvention
    {
        // A 30 degree tilt makes a ground square project as a 2:1 diamond (sin 30 = 0.5),
        // which is what gives the pixel art its clean 2:1 stair-step lines.
        public static readonly Vector3 EulerAngles = new Vector3(30f, 45f, 0f);

        public static Quaternion Rotation => Quaternion.Euler(EulerAngles);
    }
}
