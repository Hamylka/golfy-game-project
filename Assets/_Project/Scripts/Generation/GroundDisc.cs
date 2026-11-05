using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// A flat round mesh draped over the ground, for the ball's shadow and the hole. Rings of vertices each
    /// take the height of what's drawn below them, so the disc bends with the facets. Where the ground stops,
    /// vertices turn fully transparent, which trims the disc at the island's edge. A depth texture and a URP
    /// decal would do this more exactly, but the depth texture is off, because it costs too much on the phone.
    ///
    /// Moving the disc allocates nothing, because the shadow does it every frame.
    ///
    /// With an inner radius it's a ring instead: the same draping, with its rings between the two radii and
    /// nothing inside. A ring needs at least two rings of vertices. Its center vertex stays, unused by any triangle.
    /// </summary>
    public sealed class GroundDisc
    {
        private readonly Func<float, float, float> _height;
        private readonly Func<float, float, bool> _hasGround;
        private readonly Vector2[] _offsets;

        public GroundDisc(float radius, int rings, int segments, Func<float, float, float> height, Func<float, float, bool> hasGround, float innerRadius = 0f)
        {
            _height = height;
            _hasGround = hasGround;

            bool hollow = innerRadius > 0f;
            int count = 1 + (rings * segments);
            _offsets = new Vector2[count];
            Positions = new Vector3[count];
            Colors = new Color32[count];

            for (int ring = 0; ring < rings; ring++)
            {
                float distance = hollow ? Mathf.Lerp(innerRadius, radius, ring / (rings - 1f)) : radius * (ring + 1) / rings;
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    _offsets[Index(ring, segment, segments)] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                }
            }

            Triangles = BuildTriangles(rings, segments, hollow);
        }

        public Vector3[] Positions { get; }

        public Color32[] Colors { get; }

        public int[] Triangles { get; }

        /// <summary>Drapes the disc around this point on the ground plane, in the course's local space.</summary>
        public void Place(float x, float z, float lift, Color32 color)
        {
            Color32 hidden = color;
            hidden.a = 0;

            for (int i = 0; i < _offsets.Length; i++)
            {
                float vertexX = x + _offsets[i].x;
                float vertexZ = z + _offsets[i].y;
                Positions[i] = new Vector3(vertexX, _height(vertexX, vertexZ) + lift, vertexZ);
                Colors[i] = _hasGround(vertexX, vertexZ) ? color : hidden;
            }
        }

        private static int Index(int ring, int segment, int segments)
        {
            return 1 + (ring * segments) + (segment % segments);
        }

        private static int[] BuildTriangles(int rings, int segments, bool hollow)
        {
            // A fan around the center, then two triangles per segment for each ring beyond the first.
            // A ring has no fan.
            var triangles = new int[(((hollow ? 0 : segments) + ((rings - 1) * segments * 2)) * 3)];
            int next = 0;

            // Angles rise from +X toward +Z, so this order faces up in Unity's left-handed space.
            for (int segment = 0; !hollow && segment < segments; segment++)
            {
                triangles[next++] = 0;
                triangles[next++] = Index(0, segment + 1, segments);
                triangles[next++] = Index(0, segment, segments);
            }

            for (int ring = 1; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int innerNear = Index(ring - 1, segment, segments);
                    int innerFar = Index(ring - 1, segment + 1, segments);
                    int outerNear = Index(ring, segment, segments);
                    int outerFar = Index(ring, segment + 1, segments);

                    triangles[next++] = innerNear;
                    triangles[next++] = innerFar;
                    triangles[next++] = outerFar;
                    triangles[next++] = innerNear;
                    triangles[next++] = outerFar;
                    triangles[next++] = outerNear;
                }
            }

            return triangles;
        }
    }
}
