using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The view of a perspective camera with a fixed rotation. Pure maths, so it can be tested without a scene.
    /// </summary>
    public readonly struct CameraView
    {
        public CameraView(Quaternion rotation, float verticalFieldOfView, float aspect)
        {
            Rotation = rotation;
            Aspect = aspect;
            TanHalfFov = Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        public Quaternion Rotation { get; }
        public float Aspect { get; }
        public float TanHalfFov { get; }

        /// <summary>World direction through a viewport point, where x and y run from -1 to 1.</summary>
        public Vector3 Direction(Vector2 viewport)
        {
            return Rotation * new Vector3(viewport.x * TanHalfFov * Aspect, viewport.y * TanHalfFov, 1f);
        }

        /// <summary>Viewport position of a world point, where x and y run from -1 to 1 on screen.</summary>
        public Vector2 ToViewport(Vector3 cameraPosition, Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(Rotation) * (point - cameraPosition);
            return new Vector2(local.x / (local.z * TanHalfFov * Aspect), local.y / (local.z * TanHalfFov));
        }

        /// <summary>Where the ray through a viewport point meets a horizontal plane.</summary>
        public Vector3 OnPlane(Vector3 cameraPosition, Vector2 viewport, float planeHeight)
        {
            Vector3 direction = Direction(viewport);
            float distance = (planeHeight - cameraPosition.y) / direction.y;
            return cameraPosition + (direction * distance);
        }
    }

    public static class CameraFraming
    {
        private const int Iterations = 60;

        /// <summary>
        /// Finds the camera position that shows every point, centred, filling the screen up to
        /// <paramref name="screenFill"/> (1 touches the screen edge) along the tighter axis.
        /// </summary>
        public static Vector3 Fit(IReadOnlyList<Vector3> points, in CameraView view, float screenFill)
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 point in points)
            {
                center += point / points.Count;
            }

            float radius = 0f;
            foreach (Vector3 point in points)
            {
                radius = Mathf.Max(radius, (point - center).magnitude);
            }

            // Perspective has no closed form here, because moving the camera changes every point's
            // projection differently. Each pass recentres, then moves closer or further, and it
            // settles quickly. Starts far enough back that every point is in front of the camera.
            Vector3 position = center - (view.Rotation * Vector3.forward * (radius * 4f / view.TanHalfFov));
            Quaternion inverse = Quaternion.Inverse(view.Rotation);

            for (int i = 0; i < Iterations; i++)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, depth = 0f;
                foreach (Vector3 point in points)
                {
                    Vector2 viewport = view.ToViewport(position, point);
                    minX = Mathf.Min(minX, viewport.x);
                    maxX = Mathf.Max(maxX, viewport.x);
                    minY = Mathf.Min(minY, viewport.y);
                    maxY = Mathf.Max(maxY, viewport.y);
                    depth += (inverse * (point - position)).z / points.Count;
                }

                var recentre = new Vector3(
                    (minX + maxX) * 0.5f * depth * view.TanHalfFov * view.Aspect,
                    (minY + maxY) * 0.5f * depth * view.TanHalfFov,
                    0f);
                float fill = Mathf.Max(maxX - minX, maxY - minY) * 0.5f / screenFill;
                var advance = new Vector3(0f, 0f, depth * (1f - fill) * 0.5f);

                position += view.Rotation * (recentre + advance);
            }

            return position;
        }

        /// <summary>The eight corners of a box, for framing with <see cref="Fit"/>.</summary>
        public static Vector3[] Corners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
            };
        }
    }
}
