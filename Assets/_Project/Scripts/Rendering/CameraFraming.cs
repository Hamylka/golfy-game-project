using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The view of a perspective camera: a rotation (the fixed pitch and a yaw, never a roll) and a lens. Pure maths,
    /// so it can be tested without a scene.
    /// </summary>
    public readonly struct CameraView
    {
        public CameraView(Quaternion rotation, float verticalFieldOfView, float aspect)
            : this(rotation, Mathf.Tan(verticalFieldOfView * 0.5f * Mathf.Deg2Rad), aspect, true)
        {
        }

        /// <summary>The convention's pitch at this yaw, in degrees.</summary>
        public CameraView(float yaw, float verticalFieldOfView, float aspect)
            : this(IsoCameraConvention.RotationAt(yaw), verticalFieldOfView, aspect)
        {
        }

        private CameraView(Quaternion rotation, float tanHalfFov, float aspect, bool _)
        {
            Rotation = rotation;
            Aspect = aspect;
            TanHalfFov = tanHalfFov;
        }

        public Quaternion Rotation { get; }
        public float Aspect { get; }
        public float TanHalfFov { get; }

        /// <summary>The yaw, in degrees from 0 to 360.</summary>
        public float Yaw => Rotation.eulerAngles.y;

        /// <summary>The same view turned to another yaw, keeping its pitch and lens.</summary>
        public CameraView WithYaw(float yaw)
        {
            return new CameraView(Quaternion.Euler(Rotation.eulerAngles.x, yaw, 0f), TanHalfFov, Aspect, true);
        }

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
        /// The fully zoomed out camera for a course: the whole course if it's no longer than
        /// <paramref name="framedLength"/> along its centreline, otherwise its first stretch that long, laid along the
        /// first leg and as wide as the course measured across it. The view should face the first leg's heading. The
        /// camera's height then sets the one zoomed-out scale, kept at every heading the camera later takes.
        /// </summary>
        /// <param name="centreline">The course's centreline, tee to hole, in world X and Z. Null, or a single leg, for straight along +X.</param>
        public static Vector3 FitCourse(IReadOnlyList<Vector3> points, IReadOnlyList<Vector2> centreline, in CameraView view, float screenFill, float framedLength)
        {
            GroundPath path = AxisPath(points, centreline);

            // The course unbent: how far along the centreline each point lies. The width is measured beside the first
            // leg only, which the first stretch frames: a point by a bend, measured from the corner, would read wider.
            float firstBend = path.LegCount >= 2 ? path.LegStart(1) : float.MaxValue;
            float minAlong = float.MaxValue, maxAlong = float.MinValue, minAcross = float.MaxValue, maxAcross = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 point in points)
            {
                float along = path.Project(new Vector2(point.x, point.z), out float across);
                minAlong = Mathf.Min(minAlong, along);
                maxAlong = Mathf.Max(maxAlong, along);
                if (along < firstBend)
                {
                    minAcross = Mathf.Min(minAcross, across);
                    maxAcross = Mathf.Max(maxAcross, across);
                }

                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (maxAlong - minAlong <= framedLength)
            {
                return Fit(points, view, screenFill);
            }

            // The first stretch, laid along the first leg. On a straight course this is the box along +X it always was.
            Vector2 start = path.Points[0];
            Vector2 direction = path.LegDirection(0);
            Vector2 normal = GroundPath.Normal(direction);
            var stretch = new List<Vector3>(8);
            foreach (float along in new[] { minAlong, minAlong + framedLength })
            {
                foreach (float across in new[] { minAcross, maxAcross })
                {
                    Vector2 ground = start + (direction * along) + (normal * across);
                    stretch.Add(new Vector3(ground.x, minY, ground.y));
                    stretch.Add(new Vector3(ground.x, maxY, ground.y));
                }
            }

            return Fit(stretch, view, screenFill);
        }

        /// <summary>The fully zoomed out camera for a course framed along +X.</summary>
        public static Vector3 FitCourse(IReadOnlyList<Vector3> points, in CameraView view, float screenFill, float framedLength)
        {
            return FitCourse(points, null, view, screenFill, framedLength);
        }

        /// <summary>The centreline the framing follows: straight along +X unless it really bends.</summary>
        private static GroundPath AxisPath(IReadOnlyList<Vector3> points, IReadOnlyList<Vector2> centreline)
        {
            GroundPath path = centreline != null && centreline.Count > 0 ? new GroundPath(centreline) : null;
            if (path != null && path.LegCount >= 2)
            {
                return path;
            }

            Vector2 start = path != null ? path.Points[0] : new Vector2(points[0].x, points[0].z);
            return new GroundPath(new[] { start, start + Vector2.right });
        }

        /// <summary>The rectangle of a horizontal plane that a camera at this position sees.</summary>
        public static Rect GroundFootprint(Vector3 position, in CameraView view, float height)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 point = view.OnPlane(position, new Vector2(x, y), height);
                    minX = Mathf.Min(minX, point.x);
                    maxX = Mathf.Max(maxX, point.x);
                    minZ = Mathf.Min(minZ, point.z);
                    maxZ = Mathf.Max(maxZ, point.z);
                }
            }

            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

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
