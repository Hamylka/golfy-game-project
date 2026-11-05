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

        /// <summary>
        /// The surface at a point, in the same order the terrain mesh draws them. Height plays no part: the
        /// ground height under a pond isn't its water level. These are the true outlines, and the mesh cuts
        /// them as straight chords across each cell, so what's drawn can differ a little: up to 27 cm at the
        /// pond's wobble on the par 3.
        /// </summary>
        public CourseSurface Surface(Vector3 p)
        {
            if (Island(p) >= 0f)
            {
                return CourseSurface.OffIsland;
            }

            if (Pond(p) < 0f)
            {
                return CourseSurface.Water;
            }

            if (Bunker(p) < 0f)
            {
                return CourseSurface.Bunker;
            }

            if (Green(p) < 0f)
            {
                return CourseSurface.Green;
            }

            return Fairway(p) < 0f ? CourseSurface.Fairway : CourseSurface.Rough;
        }

        public float Fairway(Vector3 p)
        {
            float wobble = (Mathf.PerlinNoise((p.x * 0.2f) + 70f, 1.3f) - 0.5f) * _shape.FairwayWobble;
            float across = Mathf.Abs(p.z) - (_shape.FairwayHalfWidth + wobble);
            float along = Mathf.Max(_shape.FairwayStart - p.x, p.x - _shape.FairwayEnd);
            return Mathf.Max(across, along);
        }

        /// <summary>
        /// The visible ground height: small facets that are calm near the play axis and more broken toward
        /// the edges, on top of broad swells and the green's tilt. Everything fades out around the tee and
        /// the hole, so the ball can rest there.
        /// </summary>
        public float Height(float x, float z)
        {
            return Ground(x, z, Noise(x, z));
        }

        /// <summary>
        /// The ground whose slope the ball feels. A facet's tone comes from its slope, so facets that read
        /// well would make a ball wander if it felt them: 20 to 30% locally. On the fairway and green it
        /// feels only the broad slopes. In the rough it feels the facets too, which suits rough ground.
        /// </summary>
        public float FeltHeight(float x, float z)
        {
            var point = new Vector3(x, 0f, z);

            // Calm from the border inward. The blend sits outside the border, because the smooth curve
            // in a cell reads vertices a metre beyond that cell.
            float fairway = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0f, Fairway(point)));
            float green = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0f, Green(point)));
            return Ground(x, z, Noise(x, z) * (1f - Mathf.Max(fairway, green)));
        }

        private float Ground(float x, float z, float noise)
        {
            return ((noise + Swells(x, z)) * FlatMask(x, z, _shape.Tee) * FlatMask(x, z, _shape.Hole)) + GreenTilt(x, z);
        }

        private float Noise(float x, float z)
        {
            float amount = Mathf.Clamp01(Mathf.Abs(z) / _shape.ReliefDistance);
            float relief = Mathf.Lerp(_shape.AxisRelief, _shape.EdgeRelief, amount);
            float noise = Mathf.PerlinNoise((x * _shape.ReliefNoiseScale) + 40f, (z * _shape.ReliefNoiseScale) + 9f);
            return (noise - 0.5f) * relief * 2f;
        }

        private float Swells(float x, float z)
        {
            float height = 0f;
            foreach (GroundSwell swell in _shape.Swells)
            {
                float dx = (x - swell.Center.x) / swell.Radius;
                float dz = (z - swell.Center.y) / swell.Radius;

                // A bell curve has no crease at its edge, so a swell can't show where it starts.
                height += swell.Height * Mathf.Exp(-((dx * dx) + (dz * dz)));
            }

            return height;
        }

        private float GreenTilt(float x, float z)
        {
            EllipseShape green = _shape.Green;
            float dx = x - green.Center.x;
            float dz = z - green.Center.y;
            float across = Mathf.Sqrt(((dx / green.Radius.x) * (dx / green.Radius.x)) + ((dz / green.Radius.y) * (dz / green.Radius.y)));

            // The tilt holds across the green, then fades out around it, so it can't lift the far edge of the island.
            float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 2.2f, across));

            // Measured from the hole. Scaling a plane by a mask would make the ground around the flat area
            // steeper than the plane, as it catches up. Fading the slope instead never exceeds the tilt.
            float holeX = x - _shape.Hole.x;
            float holeZ = z - _shape.Hole.y;
            float distance = Mathf.Sqrt((holeX * holeX) + (holeZ * holeZ));
            if (distance < _shape.FlatRadius)
            {
                return 0f;
            }

            float scale = FadedDistance(distance) / distance;
            return ((holeX * _shape.GreenTilt.x) + (holeZ * _shape.GreenTilt.y)) * scale * weight;
        }

        /// <summary>
        /// How far the slope has acted at this distance from the hole: the sum of a slope that is zero
        /// across the flat area and rises smoothly to full strength over the blend.
        /// </summary>
        private float FadedDistance(float distance)
        {
            float blend = _shape.FlatBlend;
            float past = distance - _shape.FlatRadius;
            if (past >= blend)
            {
                return past - (blend * 0.5f);
            }

            // The integral of the smoothstep curve 3t^2 - 2t^3.
            float t = past / blend;
            return blend * t * t * t * (1f - (0.5f * t));
        }

        private float FlatMask(float x, float z, Vector2 center)
        {
            float distance = Vector2.Distance(new Vector2(x, z), center);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_shape.FlatRadius, _shape.FlatRadius + _shape.FlatBlend, distance));
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
