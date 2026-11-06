using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>A course's real ground, joining its smooth height field and its surface outlines.</summary>
    public sealed class BallGround : IBallGround
    {
        private readonly CourseFields _fields;
        private readonly CourseHeightField _heights;
        private readonly float _waterLevel;

        public BallGround(CourseLayout layout)
        {
            _fields = new CourseFields(layout);
            _heights = new CourseHeightField(layout);
            _waterLevel = layout.WaterLevel;
            Tee = layout.Tee;
            Hole = layout.Hole;
        }

        public Vector2 Tee { get; }

        public Vector2 Hole { get; }

        public float Height(float x, float z) => _heights.Height(x, z);

        public float LandingHeight(float x, float z)
        {
            // Water comes from the pond's outline. The height field runs on under the pond as if it were
            // dry, so its height there says nothing about whether a point is wet.
            return Surface(x, z) == CourseSurface.Water ? _waterLevel : _heights.Height(x, z);
        }

        public Vector2 Slope(float x, float z) => _heights.Slope(x, z);

        public Vector3 Normal(float x, float z) => _heights.Normal(x, z);

        public CourseSurface Surface(float x, float z) => _fields.Surface(new Vector3(x, 0f, z));

        /// <summary>
        /// The height of what's drawn here: the water's surface over a pond, otherwise the visible facet. It's
        /// for what must lie on the drawing, like the ball's shadow, and not for the ball, which uses <see cref="Height"/>.
        /// </summary>
        public float DrawnHeight(float x, float z)
        {
            return _fields.Pond(new Vector3(x, 0f, z)) < 0f ? _waterLevel : _heights.FacetHeight(x, z);
        }

        public bool IsOnIsland(float x, float z) => _fields.Island(new Vector3(x, 0f, z)) < 0f;
    }
}
