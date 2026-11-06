using System;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Smooth 2D gradient noise for course shapes, written in plain C# so the same inputs give the same
    /// course in the Editor and on the phone. <c>Mathf.PerlinNoise</c> is native engine code whose exact
    /// output isn't specified, so it can't promise that. Lattice corners pick one of eight directions from
    /// an integer hash, and a quintic fade joins the cells with no crease in height or slope.
    /// </summary>
    public static class CourseNoise
    {
        // Unit gradients: the four axes and the four diagonals.
        private const float Diagonal = 0.70710678f;

        private static readonly float[] GradientX = { 1f, -1f, 0f, 0f, Diagonal, -Diagonal, Diagonal, -Diagonal };
        private static readonly float[] GradientY = { 0f, 0f, 1f, -1f, Diagonal, Diagonal, -Diagonal, -Diagonal };

        // With unit gradients, 2D gradient noise stays within plus or minus the square root of a half.
        private const float SignedToUnit = 0.70710678f;

        /// <summary>
        /// Noise in [0, 1], exactly 0.5 on every whole-number point and smooth everywhere. Neighbouring
        /// values change about as fast as <c>Mathf.PerlinNoise</c>, so the feature size of a given input
        /// scale is unchanged. A different seed gives an unrelated field.
        /// </summary>
        public static float Value01(float x, float y, int seed = 0)
        {
            return 0.5f + (Signed(x, y, seed) * SignedToUnit);
        }

        /// <summary>The same noise centred on zero, within plus or minus the square root of a half.</summary>
        public static float Signed(float x, float y, int seed = 0)
        {
            int x0 = FloorToInt(x);
            int y0 = FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            float a = Corner(x0, y0, fx, fy, seed);
            float b = Corner(x0 + 1, y0, fx - 1f, fy, seed);
            float c = Corner(x0, y0 + 1, fx, fy - 1f, seed);
            float d = Corner(x0 + 1, y0 + 1, fx - 1f, fy - 1f, seed);

            float u = Fade(fx);
            float v = Fade(fy);
            float bottom = a + ((b - a) * u);
            float top = c + ((d - c) * u);
            return bottom + ((top - bottom) * v);
        }

        /// <summary>
        /// Mixes a lattice point and the seed into 32 well-spread bits. Integer arithmetic only, which wraps
        /// the same way on every platform.
        /// </summary>
        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                // The added constant keeps the origin from hashing to zero whatever the seed.
                uint h = ((uint)x * 0x8DA6B343u) ^ ((uint)y * 0xD8163841u) ^ ((uint)seed * 0xCB1AB31Fu);
                h += 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        private static float Corner(int ix, int iy, float dx, float dy, int seed)
        {
            uint index = Hash(ix, iy, seed) & 7u;
            return (GradientX[index] * dx) + (GradientY[index] * dy);
        }

        /// <summary>6t^5 - 15t^4 + 10t^3: flat in slope and curvature at both ends of a cell.</summary>
        private static float Fade(float t)
        {
            return t * t * t * ((t * ((t * 6f) - 15f)) + 10f);
        }

        private static int FloorToInt(float value)
        {
            return (int)Math.Floor(value);
        }
    }
}
