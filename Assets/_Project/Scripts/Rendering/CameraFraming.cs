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

    /// <summary>How a course is framed when fully zoomed out.</summary>
    public readonly struct CourseFrame
    {
        private static readonly Vector2[] NoCorridor = { Vector2.zero };

        /// <summary>A frame whose view travels this far along +X, or not at all.</summary>
        public CourseFrame(Vector3 home, float corridorLength)
            : this(home, corridorLength > 0f ? new[] { Vector2.zero, new Vector2(corridorLength, 0f) } : NoCorridor)
        {
        }

        public CourseFrame(Vector3 home, IReadOnlyList<Vector2> corridor)
        {
            Home = home;
            Corridor = corridor ?? NoCorridor;
            float length = 0f;
            for (int i = 1; i < Corridor.Count; i++)
            {
                length += Vector2.Distance(Corridor[i - 1], Corridor[i]);
            }

            CorridorLength = length;
        }

        /// <summary>The first view, at the course's start.</summary>
        public Vector3 Home { get; }

        /// <summary>
        /// Where the fully zoomed out camera can travel, as a polyline of moves from <see cref="Home"/> along the
        /// ground (x is world X, y is world Z). It starts at zero, and is that single point for a course that fits
        /// one view.
        /// </summary>
        public IReadOnlyList<Vector2> Corridor { get; }

        /// <summary>How far the view can travel along <see cref="Corridor"/>. Zero for a course that fits in one view.</summary>
        public float CorridorLength { get; }
    }

    public static class CameraFraming
    {
        private const int Iterations = 60;

        // A bent course's framing is measured on its outline: each pass centres the legs, finds the corridor's end and
        // steps back if a leg is still too wide. This is how many times it may step back; one or two usually settle it.
        private const int BentPasses = 4;

        // A leg may reach this much past the screen fill before the camera steps back, so a measuring pass can stop.
        private const float WidthSlack = 1.01f;

        // The outline points that count as a view's middle row, in viewport units above and below its middle.
        private const float MiddleRowHalfHeight = 0.1f;

        // How far apart, in metres, the places tried for the end of a bent corridor are.
        private const float EndSearchStep = 0.25f;

        // How far apart, in metres, the views checked for a leg's width are.
        private const float WidthSampleStep = 2f;

        // Views this share of a framed stretch from a bend aren't checked for width: they see the corner.
        private const float CornerShare = 0.25f;

        /// <summary>
        /// Frames a course along the straight +X axis: the whole course if it's no longer than
        /// <paramref name="framedLength"/>, otherwise in stretches that long, at the same scale.
        /// </summary>
        public static CourseFrame FitCourse(IReadOnlyList<Vector3> points, in CameraView view, float screenFill, float framedLength)
        {
            return FitCourse(points, null, view, screenFill, framedLength);
        }

        /// <summary>
        /// Frames a course along its centreline. A course no longer than <paramref name="framedLength"/> along its
        /// centreline is framed whole. A longer one is framed on its first stretch, that long and as wide as the
        /// course measured across the centreline, and the fully zoomed out view then travels along the centreline at
        /// that same scale. A centreline with a single leg counts as straight along +X: a straight course runs along
        /// the play axis, whatever small offset to the side its hole has.
        /// </summary>
        /// <param name="centreline">The course's centreline, tee to hole, in world X and Z. Null for straight along +X.</param>
        /// <param name="groundHeight">The height of the ground the view's centre travels on.</param>
        /// <param name="maxStepBack">
        /// On a bent course, how much further back than the first stretch's framing the camera may go so that a leg
        /// running diagonally on screen keeps its whole width in view. 1 keeps the same scale as a straight course.
        /// </param>
        public static CourseFrame FitCourse(IReadOnlyList<Vector3> points, IReadOnlyList<Vector2> centreline, in CameraView view, float screenFill, float framedLength, float groundHeight = 0f, float maxStepBack = float.PositiveInfinity)
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

            float length = maxAlong - minAlong;
            if (length <= framedLength)
            {
                return new CourseFrame(Fit(points, view, screenFill), 0f);
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

            Vector3 home = Fit(stretch, view, screenFill);

            // The lowest the first stretch reaches on screen: its start, nearest the camera.
            float bottom = float.MaxValue;
            float framedHeight = home.y - groundHeight;
            foreach (Vector3 corner in stretch)
            {
                bottom = Mathf.Min(bottom, view.ToViewport(home, corner).y);
            }

            float first, side, last;
            var offsets = new float[path.LegCount];
            for (int pass = 0; ; pass++)
            {
                // The view's centre keeps the place beside the centreline it has on the first stretch.
                Vector3 center = view.OnPlane(home, Vector2.zero, groundHeight);
                first = path.Project(new Vector2(center.x, center.z), out side);
                float straightEnd = first + (length - framedLength);
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsets[i] = side;
                }

                // A straight course travels until its last stretch ends where the course does, as it always has.
                last = straightEnd;
                if (path.LegCount < 2)
                {
                    break;
                }

                // A bent one is fitted by measuring its real outline, which a pass or two settles.
                CentreLaterLegs(points, path, home, first, offsets, view, groundHeight);
                last = EndOfBentCorridor(points, path, home, first, straightEnd, maxAlong, offsets, framedLength, view, screenFill);
                float widest = WidestLeg(points, path, home, first, last, offsets, framedLength, view);

                // No further back than the cap allows: past it, a diagonal leg's sides are left off the overview.
                float height = home.y - groundHeight;
                float stepBack = Mathf.Min(widest / screenFill, maxStepBack * framedHeight / height);
                if (widest <= screenFill * WidthSlack || stepBack <= 1f + 1e-4f || pass == BentPasses)
                {
                    break;
                }

                // Further back, around the ground at the bottom of the first view, so the course's start stays low on
                // screen as on a straight course. Points shrink toward there on screen; the next pass centres the
                // legs and finds the corridor's end again at the new height.
                Vector3 anchor = view.OnPlane(home, new Vector2(0f, bottom), groundHeight);
                home = anchor + ((home - anchor) * stepBack);
            }

            Vector2[] views = path.OffsetSlice(first, last, offsets);
            var corridor = new Vector2[views.Length];
            for (int i = 0; i < views.Length; i++)
            {
                corridor[i] = views[i] - views[0];
            }

            return new CourseFrame(home, corridor);
        }

        /// <summary>The camera of the fully zoomed out view this far along a bent corridor.</summary>
        private static Vector3 CameraAlong(GroundPath path, Vector3 home, float first, float along, IReadOnlyList<float> offsets)
        {
            Vector2 move = path.OffsetAt(along, offsets) - path.OffsetAt(first, offsets);
            return home + new Vector3(move.x, 0f, move.y);
        }

        /// <summary>
        /// The island's leftmost and rightmost points on screen, among those close to the middle row of a view.
        /// Returns false if no point is there.
        /// </summary>
        private static bool MiddleRow(IReadOnlyList<Vector3> points, Vector3 camera, in CameraView view, out float left, out float right)
        {
            left = float.MaxValue;
            right = float.MinValue;
            foreach (Vector3 point in points)
            {
                Vector2 viewport = view.ToViewport(camera, point);
                if (Mathf.Abs(viewport.y) <= MiddleRowHalfHeight)
                {
                    left = Mathf.Min(left, viewport.x);
                    right = Mathf.Max(right, viewport.x);
                }
            }

            return left <= right;
        }

        /// <summary>The middle of a leg, kept inside the corridor.</summary>
        private static float LegMiddle(GroundPath path, int leg, float first, float last)
        {
            return Mathf.Clamp((path.LegStart(leg) + path.LegStart(leg + 1)) * 0.5f, first, Mathf.Max(first, last));
        }

        /// <summary>
        /// Moves every leg after the first sideways so that, seen from the middle of that leg, the island sits centred
        /// across the screen. The first leg keeps the place its framed stretch gives it. A leg that runs diagonally on
        /// screen needs this: the first leg's offset, measured square to a different direction, would push it aside.
        /// </summary>
        private static void CentreLaterLegs(IReadOnlyList<Vector3> points, GroundPath path, Vector3 home, float first, float[] offsets, in CameraView view, float groundHeight)
        {
            for (int leg = 1; leg < path.LegCount; leg++)
            {
                Vector3 camera = CameraAlong(path, home, first, LegMiddle(path, leg, first, float.MaxValue), offsets);
                if (!MiddleRow(points, camera, view, out float left, out float right))
                {
                    continue;
                }

                // A sideways step of the camera moves everything on screen by the same amount, at the middle row's depth.
                Vector3 screenRight = view.OnPlane(camera, Vector2.right, groundHeight) - view.OnPlane(camera, Vector2.zero, groundHeight);
                Vector2 shift = new Vector2(screenRight.x, screenRight.z) * ((left + right) * 0.5f);
                offsets[leg] += Vector2.Dot(shift, GroundPath.Normal(path.LegDirection(leg)));
            }
        }

        /// <summary>
        /// The widest the island's middle row reaches on screen along the corridor, from the middle of the screen, 1 at
        /// its edge. Views near a bend are left out: there a screen row crosses both legs and the corner between them,
        /// which is no leg's width.
        /// </summary>
        private static float WidestLeg(IReadOnlyList<Vector3> points, GroundPath path, Vector3 home, float first, float last, IReadOnlyList<float> offsets, float framedLength, in CameraView view)
        {
            float cornerReach = framedLength * CornerShare;
            float widest = 0f;
            for (float along = first; along <= last + (WidthSampleStep * 0.5f); along += WidthSampleStep)
            {
                float at = Mathf.Min(along, last);
                bool nearBend = false;
                for (int vertex = 1; vertex < path.LegCount; vertex++)
                {
                    nearBend |= Mathf.Abs(at - path.LegStart(vertex)) < cornerReach;
                }

                if (nearBend)
                {
                    continue;
                }

                Vector3 camera = CameraAlong(path, home, first, at, offsets);
                if (MiddleRow(points, camera, view, out float left, out float right))
                {
                    widest = Mathf.Max(widest, Mathf.Max(-left, right));
                }
            }

            return widest;
        }

        /// <summary>
        /// How far along the centreline a bent course's corridor must run for its last view to show the course's end.
        /// A view sees less of a leg that runs diagonally on screen than of one running up it, since the leg soon
        /// leaves by a side, so the straight course's rule (travel the length beyond one stretch) stops short there.
        /// This walks on from that rule's end until every point of the course's last quarter stretch is in view,
        /// or, if none does, takes the place where they come closest.
        /// </summary>
        private static float EndOfBentCorridor(IReadOnlyList<Vector3> points, GroundPath path, Vector3 home, float first, float straightEnd, float maxAlong, IReadOnlyList<float> offsets, float framedLength, in CameraView view, float screenFill)
        {
            float capStart = maxAlong - (framedLength * 0.25f);
            var cap = new List<Vector3>();
            foreach (Vector3 point in points)
            {
                if (path.Project(new Vector2(point.x, point.z), out _) >= capStart)
                {
                    cap.Add(point);
                }
            }

            float best = straightEnd;
            float bestOverflow = float.MaxValue;
            for (float along = straightEnd; along <= maxAlong; along += EndSearchStep)
            {
                Vector3 camera = CameraAlong(path, home, first, along, offsets);
                float overflow = float.MinValue;
                foreach (Vector3 point in cap)
                {
                    Vector2 viewport = view.ToViewport(camera, point);
                    overflow = Mathf.Max(overflow, Mathf.Max(Mathf.Abs(viewport.x), Mathf.Abs(viewport.y)) - screenFill);
                }

                if (overflow <= 0f)
                {
                    return along;
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    best = along;
                }
            }

            return best;
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
