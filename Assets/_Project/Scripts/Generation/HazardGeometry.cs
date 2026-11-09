using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Small plane helpers for the hazard step. Headings are degrees from +X toward +Z, like everywhere else.</summary>
    internal static class Geo
    {
        /// <summary>
        /// A length in whole centimetres. Every accept/reject test of the hazard step compares these instead of raw floats, so a
        /// result that differs in its last bits between Mono and IL2CPP can only flip a decision that lands exactly on a half
        /// centimetre. (Same class of residual risk as the outline generator's float geometry.)
        /// </summary>
        public static int Cm(float metres) => Mathf.RoundToInt(metres * 100f);

        public static Vector2 Dir(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static float HeadingOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        /// <summary>The unit vector to the left of v: +Z for +X.</summary>
        public static Vector2 Left(Vector2 v) => new Vector2(-v.y, v.x);

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new Vector2((v.x * cos) - (v.y * sin), (v.x * sin) + (v.y * cos));
        }

        public static Vector3 At(Vector2 p) => new Vector3(p.x, 0f, p.y);

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length = ab.sqrMagnitude;
            float t = length <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / length);
            return Vector2.Distance(p, a + (ab * t));
        }

        /// <summary>The direction the course heads at a distance along it, from a short central difference.</summary>
        public static Vector2 PathDirection(CoursePath path, float along)
        {
            Vector2 v = path.PointAt(along + 0.05f) - path.PointAt(along - 0.05f);
            return v.sqrMagnitude <= 0f ? Vector2.right : v.normalized;
        }
    }

    /// <summary>
    /// A hazard with its true outline measured: 24 points on the border of <see cref="CourseFields.Ellipse"/>'s field (so
    /// the wobble is in them), found by bisection along each ray. Every metric rule of the hazard step reads distances to this
    /// polygon instead of the normalised field, which is not metres. Also carries what the placement decided: the role, and
    /// for a pond its water level.
    /// </summary>
    internal sealed class HazardOutline
    {
        public const int RingPoints = 24;

        private HazardOutline(EllipseShape shape, bool isWater, HazardRole role)
        {
            Shape = shape;
            IsWater = isWater;
            Role = role;
            Centre = shape.Center;
            Ring = new Vector2[RingPoints];

            float far = Mathf.Max(shape.Radius.x, shape.Radius.y) * (1f + shape.Wobble + 0.05f) + 0.25f;
            for (int i = 0; i < RingPoints; i++)
            {
                Vector2 direction = Geo.Dir(shape.Heading + (i * 360f / RingPoints));
                Vector2 local = Geo.Dir(i * 360f / RingPoints);

                // The ray's length to the border, in metres: the field is negative at the centre and positive past `far`.
                // The ellipse's own radius that way is where to look; the wobble moves it by a fraction.
                float nominal = 1f / Mathf.Sqrt(((local.x * local.x) / (shape.Radius.x * shape.Radius.x)) + ((local.y * local.y) / (shape.Radius.y * shape.Radius.y)));
                float lo = 0f, hi = Mathf.Min(far, nominal * (1f + shape.Wobble + 0.05f) + 0.25f);
                for (int step = 0; step < 18; step++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (CourseFields.Ellipse(Geo.At(Centre + (direction * mid)), shape) < 0f)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid;
                    }
                }

                Ring[i] = Centre + (direction * ((lo + hi) * 0.5f));
                BoundRadius = Mathf.Max(BoundRadius, (Ring[i] - Centre).magnitude);
            }
        }

        public static HazardOutline Of(EllipseShape shape, bool isWater, HazardRole role) => new HazardOutline(shape, isWater, role);

        public EllipseShape Shape { get; }
        public bool IsWater { get; }
        public HazardRole Role { get; }
        public Vector2 Centre { get; }
        public Vector2[] Ring { get; }

        /// <summary>The farthest ring point from the centre.</summary>
        public float BoundRadius { get; }

        /// <summary>Pond only: the height of its water, set by the placement from the rim.</summary>
        public float WaterLevel { get; set; }

        /// <summary>Inside the true outline, the field's own sign.</summary>
        public bool Contains(Vector2 p) => CourseFields.Ellipse(Geo.At(p), Shape) < 0f;

        /// <summary>
        /// Metres from <paramref name="p"/> to the border, negative inside. Returns <paramref name="cutoff"/> as soon as the point is
        /// known to be at least that far, so far points cost one subtraction.
        /// </summary>
        public float SignedDistance(Vector2 p, float cutoff = 100f)
        {
            float rough = Vector2.Distance(p, Centre) - BoundRadius;
            if (rough >= cutoff)
            {
                return cutoff;
            }

            float best = float.MaxValue;
            for (int i = 0; i < RingPoints; i++)
            {
                best = Mathf.Min(best, Geo.DistanceToSegment(p, Ring[i], Ring[(i + 1) % RingPoints]));
            }

            return Contains(p) ? -best : best;
        }

        /// <summary>Free ground between two outlines in metres, negative if one holds the other's centre.</summary>
        public float Gap(HazardOutline other)
        {
            if (Contains(other.Centre) || other.Contains(Centre))
            {
                return -1f;
            }

            if (Vector2.Distance(Centre, other.Centre) - BoundRadius - other.BoundRadius >= 100f)
            {
                return 100f;
            }

            float best = float.MaxValue;
            for (int i = 0; i < RingPoints; i++)
            {
                best = Mathf.Min(best, other.SignedDistance(Ring[i]));
                best = Mathf.Min(best, SignedDistance(other.Ring[i]));
            }

            return best;
        }
    }
}
