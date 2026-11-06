using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Zoom, pan and heading for the camera. Everything happens on the ground plane: the ground under the fingers
    /// stays under the fingers, which is what makes a gesture feel attached to the world. Wherever the camera goes,
    /// the ground at the view's centre stays within the island's area.
    /// </summary>
    public sealed class CameraNavigator
    {
        private readonly float _homeHeight;
        private readonly IslandArea _area;
        private readonly float _groundHeight;
        private readonly float _closestHeight;

        /// <param name="homePosition">The fully zoomed out camera position, which sets the zoomed-out height on every heading.</param>
        /// <param name="view">The camera's starting rotation and its lens.</param>
        /// <param name="area">Where the view's centre may go, at every zoom and heading.</param>
        /// <param name="groundHeight">World height of the ground plane.</param>
        /// <param name="closestFraction">Closest zoom, as a fraction of the home distance to the ground.</param>
        public CameraNavigator(Vector3 homePosition, in CameraView view, IslandArea area, float groundHeight, float closestFraction)
        {
            _homeHeight = homePosition.y;
            View = view;
            _area = area;
            _groundHeight = groundHeight;
            _closestHeight = groundHeight + ((homePosition.y - groundHeight) * closestFraction);
            Position = Clamp(homePosition);
        }

        /// <summary>For a box-shaped island: the view's centre stays within this rectangle of world X and Z.</summary>
        public CameraNavigator(Vector3 homePosition, in CameraView view, Rect groundLimits, float groundHeight, float closestFraction)
            : this(homePosition, view, IslandArea.FromRect(groundLimits), groundHeight, closestFraction)
        {
        }

        public Vector3 Position { get; private set; }

        /// <summary>The camera's current rotation and lens.</summary>
        public CameraView View { get; private set; }

        /// <summary>The camera's current yaw, in degrees.</summary>
        public float Yaw => View.Yaw;

        public IslandArea Area => _area;

        /// <summary>0 when fully zoomed out, 1 when fully zoomed in. The same on every heading.</summary>
        public float Zoom => Mathf.InverseLerp(_homeHeight, _closestHeight, Position.y);

        /// <summary>The ground at the middle of the view.</summary>
        public Vector3 ViewCentre => View.OnPlane(Position, Vector2.zero, _groundHeight);

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
            Vector3 grabbed = View.OnPlane(Position, fromViewport, _groundHeight);
            Vector3 under = View.OnPlane(Position, toViewport, _groundHeight);
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
            Vector3 focus = View.OnPlane(Position, viewport, _groundHeight);
            float height = Position.y - _groundHeight;
            float limited = Mathf.Clamp(height * scale, _closestHeight - _groundHeight, _homeHeight - _groundHeight);
            Position = Clamp(focus + ((Position - focus) * (limited / height)));
        }

        /// <summary>Zooms straight to a level, around the middle of the view. 0 is fully zoomed out, 1 the closest.</summary>
        public void ZoomTo(float zoom)
        {
            ZoomAround(Vector2.zero, HeightAt(zoom) / (Position.y - _groundHeight));
        }

        /// <summary>
        /// Turns the camera to a yaw around a vertical axis through <paramref name="pivot"/>, which keeps that point
        /// where it was on screen. The pitch, the zoom and the lens never change. Limits apply afterwards.
        /// </summary>
        public void TurnTo(float yaw, Vector3 pivot)
        {
            if (!SameYaw(yaw))
            {
                Position = Turned(Position, Yaw, yaw, pivot);
                View = View.WithYaw(yaw);
            }

            Position = Clamp(Position);
        }

        /// <summary>Puts the camera at a position and yaw at once, limits applied.</summary>
        public void SetPose(Vector3 position, float yaw)
        {
            if (!SameYaw(yaw))
            {
                View = View.WithYaw(yaw);
            }

            Position = Clamp(position);
        }

        // A turn this small is no turn: the rotation is left exactly as it was, so a course along +X keeps the very
        // rotation of the convention.
        private bool SameYaw(float yaw)
        {
            return Mathf.Abs(Mathf.DeltaAngle(Yaw, yaw)) < 1e-3f;
        }

        /// <summary>A camera position turned from one yaw to another around a vertical axis through the pivot.</summary>
        public static Vector3 Turned(Vector3 position, float fromYaw, float toYaw, Vector3 pivot)
        {
            Quaternion turn = Quaternion.Euler(0f, Mathf.DeltaAngle(fromYaw, toYaw), 0f);
            Vector3 offset = position - pivot;
            return pivot + (turn * offset);
        }

        /// <summary>
        /// Where the camera would be at this zoom with a point on screen at a given viewport spot, limits applied.
        /// Nothing moves: the camera is left where it was.
        /// </summary>
        public Vector3 PositionWith(float zoom, Vector3 point, Vector2 viewport)
        {
            return PositionWith(zoom, point, viewport, Yaw);
        }

        /// <summary>The same, at another yaw. Nothing moves or turns.</summary>
        public Vector3 PositionWith(float zoom, Vector3 point, Vector2 viewport, float yaw)
        {
            Vector3 before = Position;
            CameraView view = View;
            if (!SameYaw(yaw))
            {
                View = View.WithYaw(yaw);
            }

            ZoomTo(zoom);
            Move(point - View.OnPlane(Position, viewport, point.y));
            Vector3 target = Position;
            Position = before;
            View = view;
            return target;
        }

        private float HeightAt(float zoom)
        {
            return Mathf.Lerp(_homeHeight, _closestHeight, zoom) - _groundHeight;
        }

        private float GroundDistance(Vector2 a, Vector2 b)
        {
            return (View.OnPlane(Position, a, _groundHeight) - View.OnPlane(Position, b, _groundHeight)).magnitude;
        }

        private Vector3 Clamp(Vector3 position)
        {
            // Only the view's centre is limited, which is the same rule at every zoom and heading: moving the camera
            // sideways moves that centre by the same amount, so the correction is exact.
            if (_area == null)
            {
                return position;
            }

            Vector3 center = View.OnPlane(position, Vector2.zero, _groundHeight);
            var wanted = new Vector2(center.x, center.z);
            Vector2 allowed = _area.Nearest(wanted);
            return allowed == wanted ? position : position + new Vector3(allowed.x - wanted.x, 0f, allowed.y - wanted.y);
        }
    }
}
