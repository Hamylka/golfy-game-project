using System.Collections.Generic;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>
    /// The geometry of the aim preview, gathered into one mesh so it's one draw call. Dots and the gauge face the
    /// camera, which is cheap because the camera never rotates: the caller passes its axes. The lists keep
    /// their size between frames, so rebuilding allocates nothing once they've reached their largest.
    /// Triangles draw in the order they're added, so the gauge's fill goes on after its border.
    /// </summary>
    public sealed class AimMesh
    {
        public List<Vector3> Positions { get; } = new List<Vector3>();

        public List<Color32> Colors { get; } = new List<Color32>();

        public List<int> Triangles { get; } = new List<int>();

        /// <summary>How many metres one screen width covers at this depth from the camera.</summary>
        public static float MetresPerScreenWidth(float verticalFieldOfView, float aspect, float depth)
        {
            return 2f * Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad) * depth * aspect;
        }

        public void Clear()
        {
            Positions.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        /// <summary>A flat round dot facing the camera.</summary>
        public void AddDot(Vector3 center, float radius, int segments, Vector3 right, Vector3 up, Color32 color)
        {
            int first = Positions.Count;
            AddVertex(center, color);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                AddVertex(center + (((right * Mathf.Cos(angle)) + (up * Mathf.Sin(angle))) * radius), color);
            }

            // Angles run counterclockwise as the camera sees them, so this order faces it in Unity's left-handed space.
            for (int i = 0; i < segments; i++)
            {
                AddTriangle(first, first + 1 + ((i + 1) % segments), first + 1 + i);
            }
        }

        /// <summary>A flat ring facing the camera, between an inner and an outer radius.</summary>
        public void AddRing(Vector3 center, float innerRadius, float outerRadius, int segments, Vector3 right, Vector3 up, Color32 color)
        {
            int first = Positions.Count;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = (right * Mathf.Cos(angle)) + (up * Mathf.Sin(angle));
                AddVertex(center + (direction * innerRadius), color);
                AddVertex(center + (direction * outerRadius), color);
            }

            // Same winding as a dot: counterclockwise as the camera sees it.
            for (int i = 0; i < segments; i++)
            {
                int inner = first + (i * 2);
                int outer = inner + 1;
                int nextInner = first + (((i + 1) % segments) * 2);
                int nextOuter = nextInner + 1;
                AddTriangle(inner, nextOuter, outer);
                AddTriangle(inner, nextInner, nextOuter);
            }
        }

        /// <summary>
        /// A horizontal gauge facing the camera: the border and empty part in one color, with the fill growing
        /// from the left inside it. Width and height include the border.
        /// </summary>
        public void AddGauge(Vector3 center, float width, float height, float border, float fill, Vector3 right, Vector3 up, Color32 borderColor, Color32 fillColor)
        {
            AddQuad(center, -width * 0.5f, width * 0.5f, height * 0.5f, right, up, borderColor);

            float inside = width - (border * 2f);
            float left = -inside * 0.5f;
            AddQuad(center, left, left + (inside * Mathf.Clamp01(fill)), (height * 0.5f) - border, right, up, fillColor);
        }

        public void AddDisc(GroundDisc disc)
        {
            int first = Positions.Count;
            for (int i = 0; i < disc.Positions.Length; i++)
            {
                AddVertex(disc.Positions[i], disc.Colors[i]);
            }

            for (int i = 0; i < disc.Triangles.Length; i++)
            {
                Triangles.Add(first + disc.Triangles[i]);
            }
        }

        private void AddQuad(Vector3 center, float left, float right, float halfHeight, Vector3 rightAxis, Vector3 upAxis, Color32 color)
        {
            int first = Positions.Count;
            Vector3 down = upAxis * -halfHeight;
            Vector3 top = upAxis * halfHeight;
            AddVertex(center + (rightAxis * left) + down, color);
            AddVertex(center + (rightAxis * left) + top, color);
            AddVertex(center + (rightAxis * right) + top, color);
            AddVertex(center + (rightAxis * right) + down, color);
            AddTriangle(first, first + 1, first + 2);
            AddTriangle(first, first + 2, first + 3);
        }

        private void AddVertex(Vector3 position, Color32 color)
        {
            Positions.Add(position);
            Colors.Add(color);
        }

        private void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }
    }
}
