using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Describes each surface as a field over the ground: negative inside, positive outside, zero exactly
    /// on the border. Height is ignored, so a border is the same line whatever the terrain does.
    /// </summary>
    public sealed class CourseFields
    {
        private readonly CourseShapeSettings _shape;

        public CourseFields(CourseShapeSettings shape)
        {
            _shape = shape;
        }

        public float Island(Vector3 p)
        {
            float along = Mathf.Clamp01(1f - ((p.x / _shape.HalfLength) * (p.x / _shape.HalfLength)));
            float wobble = (Mathf.PerlinNoise((p.x * _shape.WidthNoiseScale) + 11f, 3.1f) - 0.5f) * _shape.WidthWobble;
            return Mathf.Abs(p.z) - ((_shape.HalfWidth + wobble) * Mathf.Sqrt(along));
        }

        public float Pond(Vector3 p) => Ellipse(p, _shape.Pond);

        public float Bunker(Vector3 p) => Ellipse(p, _shape.Bunker);

        public float Green(Vector3 p) => Ellipse(p, _shape.Green);

        public float Fairway(Vector3 p)
        {
            float wobble = (Mathf.PerlinNoise((p.x * 0.2f) + 70f, 1.3f) - 0.5f) * _shape.FairwayWobble;
            float across = Mathf.Abs(p.z) - (_shape.FairwayHalfWidth + wobble);
            float along = Mathf.Max(_shape.FairwayStart - p.x, p.x - _shape.FairwayEnd);
            return Mathf.Max(across, along);
        }

        /// <summary>Ground height. Calm near the play axis and more broken toward the edges.</summary>
        public float Height(float x, float z)
        {
            float amount = Mathf.Clamp01(Mathf.Abs(z) / _shape.ReliefDistance);
            float relief = Mathf.Lerp(_shape.AxisRelief, _shape.EdgeRelief, amount);
            float noise = Mathf.PerlinNoise((x * _shape.ReliefNoiseScale) + 40f, (z * _shape.ReliefNoiseScale) + 9f);
            return (noise - 0.5f) * relief * 2f;
        }

        /// <summary>How far the earth band reaches below the island edge, at a point on the edge.</summary>
        public float SkirtBottom(Vector3 p)
        {
            float noise = Mathf.PerlinNoise((p.x * _shape.SkirtNoiseScale) + 5f, (p.z * _shape.SkirtNoiseScale) + 5f);
            return -Mathf.Lerp(_shape.SkirtDepth.x, _shape.SkirtDepth.y, noise);
        }

        /// <summary>Which mowing stripe a point is on. Even and odd stripes alternate along the play axis.</summary>
        public int Stripe(float z)
        {
            return Mathf.FloorToInt(z / _shape.StripeWidth);
        }

        private static float Ellipse(Vector3 p, EllipseShape shape)
        {
            float dx = (p.x - shape.Center.x) / shape.Radius.x;
            float dz = (p.z - shape.Center.y) / shape.Radius.y;
            float noise = Mathf.PerlinNoise((p.x * 0.8f) + shape.Center.x + 30f, (p.z * 0.8f) + shape.Center.y + 30f) - 0.5f;
            return Mathf.Sqrt((dx * dx) + (dz * dz)) - 1f + (noise * shape.Wobble * 2f);
        }
    }
}
