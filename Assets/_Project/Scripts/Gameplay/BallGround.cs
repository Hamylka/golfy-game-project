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

        public BallGround(CourseShapeSettings shape)
        {
            _fields = new CourseFields(shape);
            _heights = new CourseHeightField(shape);
            _waterLevel = shape.WaterLevel;
            Tee = shape.Tee;
            Hole = shape.Hole;
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
    }
}
