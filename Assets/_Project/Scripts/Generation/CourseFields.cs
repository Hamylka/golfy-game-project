using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Describes each surface as a field over the ground: negative inside, positive outside, zero exactly
    /// on the border. Height is ignored, so a border is the same line whatever the terrain does. A surface
    /// with several pieces (two bunkers, say) is the nearest piece's field, so the union of them all.
    /// The island is the union of every island's outline, so a point in a gap between two islands is off the island.
    /// </summary>
    public sealed class CourseFields
    {
        // What a surface with no pieces returns: far outside, but finite, so differences stay numbers.
        private const float Nowhere = 1e6f;

        private readonly CourseLayout _layout;

        public CourseFields(CourseLayout layout)
        {
            _layout = layout;
        }

        public float Island(Vector3 p)
        {
            float field = Nowhere;
            foreach (IslandOutline island in _layout.Islands)
            {
                field = Mathf.Min(field, Island(p, island));
            }

            return field;
        }

        public float Pond(Vector3 p) => Nearest(p, _layout.Ponds);

        public float Bunker(Vector3 p) => Nearest(p, _layout.Bunkers);

        public float Green(Vector3 p)
        {
            float field = Nowhere;
            foreach (GreenLayout green in _layout.Greens)
            {
                field = Mathf.Min(field, Ellipse(p, green.Outline));
            }

            return field;
        }

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
            FairwayLayout fairway = _layout.Fairway;
            float wobble = (CourseNoise.Value01((p.x * 0.2f) + 70f, 1.3f) - 0.5f) * fairway.Wobble;
            float across = Mathf.Abs(p.z) - (fairway.HalfWidth + wobble);
            float along = Mathf.Max(fairway.Start - p.x, p.x - fairway.End);
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
            return ((noise + Swells(x, z)) * FlatMask(x, z, _layout.Tee) * FlatMask(x, z, _layout.Hole)) + GreenTilts(x, z);
        }

        private float Noise(float x, float z)
        {
            GroundRelief ground = _layout.Relief;
            float amount = Mathf.Clamp01(Mathf.Abs(z) / ground.ReliefDistance);
            float relief = Mathf.Lerp(ground.AxisRelief, ground.EdgeRelief, amount);
            float noise = CourseNoise.Value01((x * ground.NoiseScale) + 40f, (z * ground.NoiseScale) + 9f);
            return (noise - 0.5f) * relief * 2f;
        }

        private float Swells(float x, float z)
        {
            float height = 0f;
            foreach (GroundSwell swell in _layout.Swells)
            {
                float dx = (x - swell.Center.x) / swell.Radius;
                float dz = (z - swell.Center.y) / swell.Radius;

                // A bell curve has no crease at its edge, so a swell can't show where it starts.
                height += swell.Height * Mathf.Exp(-((dx * dx) + (dz * dz)));
            }

            return height;
        }

        private float GreenTilts(float x, float z)
        {
            float height = 0f;
            foreach (GreenLayout green in _layout.Greens)
            {
                height += GreenTilt(x, z, green);
            }

            return height;
        }

        private float GreenTilt(float x, float z, GreenLayout layout)
        {
            EllipseShape green = layout.Outline;
            float dx = x - green.Center.x;
            float dz = z - green.Center.y;
            float across = Mathf.Sqrt(((dx / green.Radius.x) * (dx / green.Radius.x)) + ((dz / green.Radius.y) * (dz / green.Radius.y)));

            // The tilt holds across the green, then fades out around it, so it can't lift the far edge of the island.
            float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 2.2f, across));

            // Measured from the hole. Scaling a plane by a mask would make the ground around the flat area
            // steeper than the plane, as it catches up. Fading the slope instead never exceeds the tilt.
            float holeX = x - _layout.Hole.x;
            float holeZ = z - _layout.Hole.y;
            float distance = Mathf.Sqrt((holeX * holeX) + (holeZ * holeZ));
            if (distance < _layout.FlatRadius)
            {
                return 0f;
            }

            float scale = FadedDistance(distance) / distance;
            return ((holeX * layout.Tilt.x) + (holeZ * layout.Tilt.y)) * scale * weight;
        }

        /// <summary>
        /// How far the slope has acted at this distance from the hole: the sum of a slope that is zero
        /// across the flat area and rises smoothly to full strength over the blend.
        /// </summary>
        private float FadedDistance(float distance)
        {
            float blend = _layout.FlatBlend;
            float past = distance - _layout.FlatRadius;
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
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_layout.FlatRadius, _layout.FlatRadius + _layout.FlatBlend, distance));
        }

        /// <summary>How far the earth band reaches below the island edge, at a point on the edge.</summary>
        public float SkirtBottom(Vector3 p)
        {
            EdgeLayout edge = _layout.Edge;
            float noise = CourseNoise.Value01((p.x * edge.SkirtNoiseScale) + 5f, (p.z * edge.SkirtNoiseScale) + 5f);
            return -Mathf.Lerp(edge.SkirtDepth.x, edge.SkirtDepth.y, noise);
        }

        /// <summary>Which mowing stripe a point is on. Even and odd stripes alternate along the play axis.</summary>
        public int Stripe(float z)
        {
            return Mathf.FloorToInt(z / _layout.Fairway.StripeWidth);
        }

        private static float Island(Vector3 p, IslandOutline island)
        {
            float local = (p.x - island.CenterX) / island.HalfLength;
            float along = 1f - (local * local);
            if (along < 0f)
            {
                // Past the island's tip. The field must stay strictly positive here, also on the axis: a
                // zero would let the terrain cut treat a grid corner out in the gap as lying on the border,
                // and a thin spike of ground would reach into the gap.
                return Mathf.Abs(p.z) + ((Mathf.Abs(local) - 1f) * island.HalfLength);
            }

            float wobble = (CourseNoise.Value01((p.x * island.WidthNoiseScale) + 11f, 3.1f) - 0.5f) * island.WidthWobble;
            return Mathf.Abs(p.z) - ((island.HalfWidth + wobble) * Mathf.Sqrt(along));
        }

        private static float Nearest(Vector3 p, IReadOnlyList<EllipseShape> shapes)
        {
            float field = Nowhere;
            foreach (EllipseShape shape in shapes)
            {
                field = Mathf.Min(field, Ellipse(p, shape));
            }

            return field;
        }

        private static float Ellipse(Vector3 p, EllipseShape shape)
        {
            float dx = (p.x - shape.Center.x) / shape.Radius.x;
            float dz = (p.z - shape.Center.y) / shape.Radius.y;
            float noise = CourseNoise.Value01((p.x * 0.8f) + shape.Center.x + 30f, (p.z * 0.8f) + shape.Center.y + 30f) - 0.5f;
            return Mathf.Sqrt((dx * dx) + (dz * dz)) - 1f + (noise * shape.Wobble * 2f);
        }
    }
}
