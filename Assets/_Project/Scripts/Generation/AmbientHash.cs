using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Deterministic hashes for the ambient motion (M4 PR 4.5): a phase per tree, a glint value per water facet.
    /// Integer arithmetic on whole centimetres in course-local space, so the same course gives the same values on
    /// every device, and never <c>UnityEngine.Random</c>. Visual only: nothing in the simulation reads these.
    /// </summary>
    public static class AmbientHash
    {
        /// <summary>A value in (0, 1] from a point on the ground plane (x and z, to the centimetre).</summary>
        public static float Unit(float x, float z)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(x * 100f) * 73856093u;
                h ^= (uint)Mathf.RoundToInt(z * 100f) * 19349663u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                h *= 0x297a2d39u;
                h ^= h >> 15;

                // 24 bits, shifted by one so the value is never 0: 0 means "no glint" in the mesh channel.
                return ((h >> 8) + 1u) / 16777216f;
            }
        }
    }
}
