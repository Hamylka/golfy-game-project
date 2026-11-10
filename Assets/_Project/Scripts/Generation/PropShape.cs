using UnityEngine;

namespace IsoGolf.Generation
{
    public enum PropShapeKind
    {
        /// <summary>Vertical cylinder: trunks, a cypress's foliage, the flag pole.</summary>
        Cylinder,

        /// <summary>Axis-aligned ellipsoid (a sphere when its radii are equal): crowns, bushes, rocks.</summary>
        Ellipsoid,

        /// <summary>Box turned about the vertical axis: walls, and later benches, sheds, the bridge.</summary>
        Box,
    }

    /// <summary>What a prop is made of. M4 PR 4.2 maps each to a restitution and an along-keep in <c>ShotSettings</c>.</summary>
    public enum PropMaterial
    {
        Wood,
        Stone,

        /// <summary>Crowns and bushes: soft, the ball loses most of its speed and drops.</summary>
        Foliage,
    }

    /// <summary>
    /// A collision shape as plain data, in the course's local space, for the ball simulation to read
    /// (M4 PR 4.2). The kit only produces them; nothing here collides.
    /// </summary>
    public readonly struct PropShape
    {
        private PropShape(PropShapeKind kind, PropMaterial material, Vector3 center, Vector3 extents, float yaw)
        {
            Kind = kind;
            Material = material;
            Center = center;
            Extents = extents;
            Yaw = yaw;
        }

        public PropShapeKind Kind { get; }
        public PropMaterial Material { get; }

        /// <summary>The middle of the shape (for a cylinder, the middle of its height).</summary>
        public Vector3 Center { get; }

        /// <summary>
        /// Cylinder: (radius, half height, radius). Ellipsoid: the three radii. Box: half sizes along its own
        /// axes, x along the yaw direction.
        /// </summary>
        public Vector3 Extents { get; }

        /// <summary>Box only: rotation about +Y in radians, x axis at (cos, 0, sin) like the kit's <c>Around</c>.</summary>
        public float Yaw { get; }

        public static PropShape Cylinder(Vector3 baseCenter, float radius, float height, PropMaterial material)
        {
            return new PropShape(PropShapeKind.Cylinder, material, baseCenter + (Vector3.up * (height * 0.5f)),
                new Vector3(radius, height * 0.5f, radius), 0f);
        }

        public static PropShape Ellipsoid(Vector3 center, Vector3 radii, PropMaterial material)
        {
            return new PropShape(PropShapeKind.Ellipsoid, material, center, radii, 0f);
        }

        public static PropShape Box(Vector3 center, Vector3 halfSize, float yaw, PropMaterial material)
        {
            return new PropShape(PropShapeKind.Box, material, center, halfSize, yaw);
        }

        /// <summary>
        /// How far a point is from the middle, as a fraction of the shape: at most 1 inside, 1 on the
        /// surface, more outside. (Cylinder and box use the largest of their per-axis fractions.)
        /// </summary>
        public float Level(Vector3 point)
        {
            Vector3 d = point - Center;
            switch (Kind)
            {
                case PropShapeKind.Cylinder:
                    return Mathf.Max(
                        new Vector2(d.x, d.z).magnitude / Extents.x,
                        Mathf.Abs(d.y) / Extents.y);
                case PropShapeKind.Ellipsoid:
                    return new Vector3(d.x / Extents.x, d.y / Extents.y, d.z / Extents.z).magnitude;
                default:
                    float cos = Mathf.Cos(Yaw);
                    float sin = Mathf.Sin(Yaw);
                    float along = (d.x * cos) + (d.z * sin);
                    float across = (-d.x * sin) + (d.z * cos);
                    return Mathf.Max(
                        Mathf.Abs(along) / Extents.x,
                        Mathf.Max(Mathf.Abs(d.y) / Extents.y, Mathf.Abs(across) / Extents.z));
            }
        }
    }
}
