using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Zoom and pan for the fixed-rotation camera. Everything happens on the ground plane: the ground
    /// under the fingers stays under the fingers, which is what makes a gesture feel attached to the world.
    /// </summary>
    public sealed class CameraNavigator
    {
        private readonly Vector3 _homePosition;
        private readonly Vector3 _homeCenter;
        private readonly Rect _groundLimits;
        private readonly float _groundHeight;
        private readonly float _closestHeight;
        private readonly float _fullLimitsZoom;
        private readonly Vector2[] _corridor;
        private readonly GroundPath _path;
        private CameraView _view;

        /// <param name="homePosition">The fully zoomed out camera position.</param>
        /// <param name="view">The camera's fixed rotation and lens.</param>
        /// <param name="groundLimits">Where the view centre may go when fully zoomed in, in world X and Z.</param>
        /// <param name="groundHeight">World height of the ground plane.</param>
        /// <param name="closestFraction">Closest zoom, as a fraction of the home distance to the ground.</param>
        /// <param name="corridorLength">How far along +X the fully zoomed out view can travel, for a long course.</param>
        /// <param name="fullLimitsZoom">The zoom from which the view can reach the full limits. Above 0.</param>
        public CameraNavigator(Vector3 homePosition, in CameraView view, Rect groundLimits, float groundHeight, float closestFraction, float corridorLength = 0f, float fullLimitsZoom = 1f)
            : this(homePosition, view, groundLimits, groundHeight, closestFraction, new CourseFrame(homePosition, corridorLength).Corridor, fullLimitsZoom)
        {
        }

        /// <param name="corridor">
        /// Where the fully zoomed out view can travel, as moves from the home position along the ground (x is world X,
        /// y is world Z), from <see cref="CourseFrame.Corridor"/>. A single zero point for a course that fits one view.
        /// </param>
        public CameraNavigator(Vector3 homePosition, in CameraView view, Rect groundLimits, float groundHeight, float closestFraction, IReadOnlyList<Vector2> corridor, float fullLimitsZoom = 1f)
        {
            _fullLimitsZoom = fullLimitsZoom;
            _homePosition = homePosition;
            _view = view;
            _groundLimits = groundLimits;
            _groundHeight = groundHeight;
            _closestHeight = groundHeight + ((homePosition.y - groundHeight) * closestFraction);
            _homeCenter = view.OnPlane(homePosition, Vector2.zero, groundHeight);
            Position = homePosition;

            // The corridor on the ground: where the fully zoomed out view's centre may be.
            int count = corridor != null && corridor.Count > 0 ? corridor.Count : 1;
            _corridor = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                _corridor[i] = new Vector2(_homeCenter.x, _homeCenter.z) + (corridor != null && corridor.Count > 0 ? corridor[i] : Vector2.zero);
            }

            _path = new GroundPath(_corridor);
        }

        /// <summary>
        /// The direction the course heads near a ground point, on the ground (x is world X, y is world Z): from where
        /// the point projects onto the corridor to <paramref name="ahead"/> metres further along it. +X on a straight
        /// course and on one that fits a single view.
        /// </summary>
        public Vector2 CourseHeading(Vector3 point, float ahead)
        {
            if (_path.LegCount == 0)
            {
                return Vector2.right;
            }

            float along = _path.Project(new Vector2(point.x, point.z), out _);
            Vector2 heading = _path.PointAt(along + ahead) - _path.PointAt(along);
            return heading.sqrMagnitude > 1e-8f ? heading.normalized : _path.DirectionAt(along);
        }

        public Vector3 Position { get; private set; }

        /// <summary>0 when fully zoomed out, 1 when fully zoomed in.</summary>
        public float Zoom => Mathf.InverseLerp(_homePosition.y, _closestHeight, Position.y);

        /// <summary>
        /// Applies one step of a two-finger gesture. Positions are viewport coordinates from -1 to 1.
        /// </summary>
        public void ApplyGesture(Vector2 previousA, Vector2 previousB, Vector2 currentA, Vector2 currentB)
        {
            Vector2 previousMiddle = (previousA + previousB) * 0.5f;
            Vector2 currentMiddle = (currentA + currentB) * 0.5f;

            Pan(previousMiddle, currentMiddle);

            // Compared on the ground, not on screen: a finger moving up the screen covers more ground
            // than one moving sideways, and screen distance would make the zoom speed depend on direction.
            float previousSpread = GroundDistance(previousA, previousB);
            float currentSpread = GroundDistance(currentA, currentB);
            if (previousSpread > Mathf.Epsilon && currentSpread > Mathf.Epsilon)
            {
                ZoomAround(currentMiddle, previousSpread / currentSpread);
            }
        }

        /// <summary>
        /// Moves the view so the ground under one viewport point ends up under another. Returns how far the
        /// camera really moved once limits are applied.
        /// </summary>
        public Vector3 Pan(Vector2 fromViewport, Vector2 toViewport)
        {
            Vector3 grabbed = _view.OnPlane(Position, fromViewport, _groundHeight);
            Vector3 under = _view.OnPlane(Position, toViewport, _groundHeight);
            return Move(grabbed - under);
        }

        /// <summary>Moves the camera along the ground, within limits. Returns how far it really moved.</summary>
        public Vector3 Move(Vector3 displacement)
        {
            Vector3 before = Position;
            Position = Clamp(Position + displacement);
            return Position - before;
        }

        /// <summary>The part of a move that the limits would allow, without moving.</summary>
        public Vector3 Reachable(Vector3 displacement)
        {
            return Clamp(Position + displacement) - Position;
        }

        /// <summary>
        /// Scales the camera's distance around the ground under a viewport point, which keeps that point
        /// in place on screen. A scale below 1 moves closer. The lens and rotation never change.
        /// </summary>
        public void ZoomAround(Vector2 viewport, float scale)
        {
            Vector3 focus = _view.OnPlane(Position, viewport, _groundHeight);
            float height = Position.y - _groundHeight;
            float limited = Mathf.Clamp(height * scale, _closestHeight - _groundHeight, _homePosition.y - _groundHeight);
            Position = Clamp(focus + ((Position - focus) * (limited / height)));
        }

        /// <summary>Zooms straight to a level, around the middle of the view. 0 is fully zoomed out, 1 the closest.</summary>
        public void ZoomTo(float zoom)
        {
            float target = Mathf.Lerp(_homePosition.y, _closestHeight, zoom) - _groundHeight;
            ZoomAround(Vector2.zero, target / (Position.y - _groundHeight));
        }

        /// <summary>
        /// Where the camera would be at this zoom with a point on screen at a given viewport spot, limits applied.
        /// Nothing moves: the camera is left where it was.
        /// </summary>
        public Vector3 PositionWith(float zoom, Vector3 point, Vector2 viewport)
        {
            Vector3 before = Position;
            ZoomTo(zoom);
            Move(point - _view.OnPlane(Position, viewport, point.y));
            Vector3 target = Position;
            Position = before;
            return target;
        }

        private float GroundDistance(Vector2 a, Vector2 b)
        {
            return (_view.OnPlane(Position, a, _groundHeight) - _view.OnPlane(Position, b, _groundHeight)).magnitude;
        }

        private Vector3 Clamp(Vector3 position)
        {
            // The allowed area grows from the framed views when zoomed out to the full limits from fullLimitsZoom on.
            // Zoomed out, that's the corridor: a single point on a course that fits one view, a polyline along the
            // course's centreline on a longer one. In between it's every blend lerp(c, l, reach) of a corridor point c
            // with a point l of the limits: the corridor shrunk by (1 - reach), widened by the limits scaled by reach.
            // That area only grows with the zoom and always holds the corridor, so zooming out lands back on a framed
            // view, with no off-centre view to correct afterwards. On a straight corridor it is exactly the old
            // rectangle, whose sides were lerps from the corridor's ends to the limits.
            float zoom = Mathf.InverseLerp(_homePosition.y, _closestHeight, position.y);
            float reach = Mathf.Clamp01(zoom / _fullLimitsZoom);
            var low = new Vector2(_groundLimits.xMin, _groundLimits.yMin) * reach;
            var high = new Vector2(_groundLimits.xMax, _groundLimits.yMax) * reach;

            Vector3 center = _view.OnPlane(position, Vector2.zero, _groundHeight);
            var wanted = new Vector2(center.x, center.z);
            Vector2 best = wanted;
            float bestDistance = float.MaxValue;
            int legs = Mathf.Max(1, _corridor.Length - 1);
            for (int i = 0; i < legs; i++)
            {
                Vector2 from = _corridor[i] * (1f - reach);
                Vector2 to = _corridor[Mathf.Min(i + 1, _corridor.Length - 1)] * (1f - reach);
                Vector2 allowed = NearestBeside(wanted, from, to, low, high);
                float distance = (allowed - wanted).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = allowed;
                }

                if (distance == 0f)
                {
                    return position;
                }
            }

            return position + new Vector3(best.x - center.x, 0f, best.y - center.z);
        }

        /// <summary>
        /// The point nearest to <paramref name="point"/> in the area a box [low, high] sweeps as its origin slides from
        /// <paramref name="from"/> to <paramref name="to"/>. The point itself if it's inside.
        /// </summary>
        private static Vector2 NearestBeside(Vector2 point, Vector2 from, Vector2 to, Vector2 low, Vector2 high)
        {
            // Inside if some slide puts the point in the box on both axes at once: each axis allows an interval of the
            // slide, and the point is inside where they overlap.
            Vector2 leg = to - from;
            Vector2 offset = point - from;
            float first = 0f, last = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                if (leg[axis] == 0f)
                {
                    if (offset[axis] < low[axis] || offset[axis] > high[axis])
                    {
                        first = 1f;
                        last = 0f;
                    }

                    continue;
                }

                float a = (offset[axis] - high[axis]) / leg[axis];
                float b = (offset[axis] - low[axis]) / leg[axis];
                first = Mathf.Max(first, Mathf.Min(a, b));
                last = Mathf.Min(last, Mathf.Max(a, b));
            }

            if (first <= last)
            {
                return point;
            }

            // Outside, the nearest point lies on the area's edge, made of the box at either end and the four lines its
            // corners trace. Each candidate is worked out exactly, with no search, so no precision is lost to a
            // distance that's large along one axis and tiny along the other.
            Vector2 best = from + Clamp(offset, low, high);
            float bestDistance = (point - best).sqrMagnitude;
            Consider(to + Clamp(point - to, low, high), point, ref best, ref bestDistance);
            if (leg.sqrMagnitude > 0f)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    var offsetOfCorner = new Vector2((corner & 1) == 0 ? low.x : high.x, (corner & 2) == 0 ? low.y : high.y);
                    Vector2 start = from + offsetOfCorner;
                    float t = Mathf.Clamp01(Vector2.Dot(point - start, leg) / leg.sqrMagnitude);
                    Consider(start + (leg * t), point, ref best, ref bestDistance);
                }
            }

            return best;
        }

        private static void Consider(Vector2 candidate, Vector2 point, ref Vector2 best, ref float bestDistance)
        {
            float distance = (point - candidate).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        private static Vector2 Clamp(Vector2 offset, Vector2 low, Vector2 high)
        {
            return new Vector2(Mathf.Clamp(offset.x, low.x, high.x), Mathf.Clamp(offset.y, low.y, high.y));
        }
    }
}
