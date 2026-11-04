using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The ground as the ball meets it. The visible terrain is flat triangles, so its slope jumps at every
    /// crease, and a ball following it would bump. Catmull-Rom curves through the grid heights replace it,
    /// with no sudden change of slope anywhere. There are two, for two different jobs:
    /// <see cref="Height"/> runs through the visible vertices, so the ball is drawn sitting on the facets,
    /// and <see cref="Slope"/> comes from the felt ground, so facets that read well don't push the ball.
    /// All coordinates are in the course's local space.
    /// </summary>
    public sealed class CourseHeightField
    {
        private readonly float _cell;
        private readonly int _originX;
        private readonly int _originZ;
        private readonly float[,] _heights;
        private readonly float[,] _feltHeights;

        public CourseHeightField(CourseShapeSettings shape)
        {
            var fields = new CourseFields(shape);
            _cell = shape.CellSize;

            // Two extra rows past the terrain grid, because the curve in a cell reads one row beyond it.
            int halfX = Mathf.CeilToInt((shape.HalfLength + _cell) / _cell) + 2;
            int halfZ = Mathf.CeilToInt((shape.HalfWidth + (shape.WidthWobble * 0.5f) + _cell) / _cell) + 2;
            _originX = -halfX;
            _originZ = -halfZ;
            _heights = new float[(halfX * 2) + 1, (halfZ * 2) + 1];
            _feltHeights = new float[_heights.GetLength(0), _heights.GetLength(1)];

            for (int x = 0; x < _heights.GetLength(0); x++)
            {
                for (int z = 0; z < _heights.GetLength(1); z++)
                {
                    float worldX = (x + _originX) * _cell;
                    float worldZ = (z + _originZ) * _cell;
                    _heights[x, z] = fields.Height(worldX, worldZ);
                    _feltHeights[x, z] = fields.FeltHeight(worldX, worldZ);
                }
            }
        }

        /// <summary>
        /// Which way a grid cell is split. True when its diagonal runs from its lowest corner to its
        /// highest. The terrain mesh and <see cref="FacetHeight"/> must always agree on this.
        /// </summary>
        public static bool UsesRisingDiagonal(int ix, int iz)
        {
            return (ix + iz) % 2 == 0;
        }

        /// <summary>The height of the felt ground. Rolling uses its <see cref="Slope"/>; this is mostly for checking.</summary>
        public float FeltHeight(float x, float z)
        {
            Locate(x, z, out int ix, out int iz, out float tx, out float tz);
            Weights(tx, out float x0, out float x1, out float x2, out float x3);
            Weights(tz, out float z0, out float z1, out float z2, out float z3);
            return Blend(_feltHeights, ix, iz, x0, x1, x2, x3, z0, z1, z2, z3);
        }

        /// <summary>The smooth height the ball sits and lands on, close to the visible facets.</summary>
        public float Height(float x, float z)
        {
            Locate(x, z, out int ix, out int iz, out float tx, out float tz);
            Weights(tx, out float x0, out float x1, out float x2, out float x3);
            Weights(tz, out float z0, out float z1, out float z2, out float z3);
            return Blend(_heights, ix, iz, x0, x1, x2, x3, z0, z1, z2, z3);
        }

        /// <summary>The felt ground's rise per metre along X and along Z. It points uphill.</summary>
        public Vector2 Slope(float x, float z)
        {
            Locate(x, z, out int ix, out int iz, out float tx, out float tz);
            Weights(tx, out float x0, out float x1, out float x2, out float x3);
            Weights(tz, out float z0, out float z1, out float z2, out float z3);
            Slopes(tx, out float dx0, out float dx1, out float dx2, out float dx3);
            Slopes(tz, out float dz0, out float dz1, out float dz2, out float dz3);
            return new Vector2(
                Blend(_feltHeights, ix, iz, dx0, dx1, dx2, dx3, z0, z1, z2, z3) / _cell,
                Blend(_feltHeights, ix, iz, x0, x1, x2, x3, dz0, dz1, dz2, dz3) / _cell);
        }

        /// <summary>The felt ground's upward direction, for bounces.</summary>
        public Vector3 Normal(float x, float z)
        {
            Vector2 gradient = Slope(x, z);
            return new Vector3(-gradient.x, 1f, -gradient.y).normalized;
        }

        /// <summary>The height of the visible triangle at this point, for what must sit on the facets, like the ball's shadow.</summary>
        public float FacetHeight(float x, float z)
        {
            Locate(x, z, out int ix, out int iz, out float tx, out float tz);
            float a = _heights[ix, iz];
            float b = _heights[ix, iz + 1];
            float c = _heights[ix + 1, iz + 1];
            float d = _heights[ix + 1, iz];

            if (UsesRisingDiagonal(ix + _originX, iz + _originZ))
            {
                return tz > tx
                    ? a + ((c - b) * tx) + ((b - a) * tz)
                    : a + ((d - a) * tx) + ((c - d) * tz);
            }

            return tx + tz <= 1f
                ? a + ((d - a) * tx) + ((b - a) * tz)
                : c + ((b - c) * (1f - tx)) + ((d - c) * (1f - tz));
        }

        /// <summary>Finds the cell holding a point and how far across it the point is. Far outside the grid, it uses the nearest edge.</summary>
        private void Locate(float x, float z, out int ix, out int iz, out float tx, out float tz)
        {
            float gridX = (x / _cell) - _originX;
            float gridZ = (z / _cell) - _originZ;
            ix = Mathf.Clamp(Mathf.FloorToInt(gridX), 1, _heights.GetLength(0) - 3);
            iz = Mathf.Clamp(Mathf.FloorToInt(gridZ), 1, _heights.GetLength(1) - 3);
            tx = Mathf.Clamp01(gridX - ix);
            tz = Mathf.Clamp01(gridZ - iz);
        }

        private static float Blend(float[,] grid, int ix, int iz, float x0, float x1, float x2, float x3, float z0, float z1, float z2, float z3)
        {
            return (z0 * Row(grid, ix, iz - 1, x0, x1, x2, x3))
                + (z1 * Row(grid, ix, iz, x0, x1, x2, x3))
                + (z2 * Row(grid, ix, iz + 1, x0, x1, x2, x3))
                + (z3 * Row(grid, ix, iz + 2, x0, x1, x2, x3));
        }

        private static float Row(float[,] grid, int ix, int iz, float x0, float x1, float x2, float x3)
        {
            return (x0 * grid[ix - 1, iz])
                + (x1 * grid[ix, iz])
                + (x2 * grid[ix + 1, iz])
                + (x3 * grid[ix + 2, iz]);
        }

        /// <summary>How much each of four neighbouring heights counts at position t on the curve.</summary>
        private static void Weights(float t, out float w0, out float w1, out float w2, out float w3)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            w0 = 0.5f * (-t + (2f * t2) - t3);
            w1 = 0.5f * (2f - (5f * t2) + (3f * t3));
            w2 = 0.5f * (t + (4f * t2) - (3f * t3));
            w3 = 0.5f * (t3 - t2);
        }

        /// <summary>The rate of change of <see cref="Weights"/>, which gives the slope.</summary>
        private static void Slopes(float t, out float w0, out float w1, out float w2, out float w3)
        {
            float t2 = t * t;
            w0 = 0.5f * (-1f + (4f * t) - (3f * t2));
            w1 = 0.5f * ((-10f * t) + (9f * t2));
            w2 = 0.5f * (1f + (8f * t) - (9f * t2));
            w3 = 0.5f * ((3f * t2) - (2f * t));
        }
    }
}
