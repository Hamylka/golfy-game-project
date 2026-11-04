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
        private CameraView _view;

        /// <param name="homePosition">The fully zoomed out camera position.</param>
        /// <param name="view">The camera's fixed rotation and lens.</param>
        /// <param name="groundLimits">Where the view centre may go when fully zoomed in, in world X and Z.</param>
        /// <param name="groundHeight">World height of the ground plane.</param>
        /// <param name="closestFraction">Closest zoom, as a fraction of the home distance to the ground.</param>
        public CameraNavigator(Vector3 homePosition, in CameraView view, Rect groundLimits, float groundHeight, float closestFraction)
        {
            _homePosition = homePosition;
            _view = view;
            _groundLimits = groundLimits;
            _groundHeight = groundHeight;
            _closestHeight = groundHeight + ((homePosition.y - groundHeight) * closestFraction);
            _homeCenter = view.OnPlane(homePosition, Vector2.zero, groundHeight);
            Position = homePosition;
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

        private float GroundDistance(Vector2 a, Vector2 b)
        {
            return (_view.OnPlane(Position, a, _groundHeight) - _view.OnPlane(Position, b, _groundHeight)).magnitude;
        }

        private Vector3 Clamp(Vector3 position)
        {
            // The allowed area grows from a single point when zoomed out to the full limits when
            // zoomed in. So zooming out always lands back on the framed island, with no off-centre
            // view that would need correcting afterwards.
            float zoom = Mathf.InverseLerp(_homePosition.y, _closestHeight, position.y);
            float minX = Mathf.Lerp(_homeCenter.x, _groundLimits.xMin, zoom);
            float maxX = Mathf.Lerp(_homeCenter.x, _groundLimits.xMax, zoom);
            float minZ = Mathf.Lerp(_homeCenter.z, _groundLimits.yMin, zoom);
            float maxZ = Mathf.Lerp(_homeCenter.z, _groundLimits.yMax, zoom);

            Vector3 center = _view.OnPlane(position, Vector2.zero, _groundHeight);
            var limited = new Vector3(Mathf.Clamp(center.x, minX, maxX), center.y, Mathf.Clamp(center.z, minZ, maxZ));
            return position + (limited - center);
        }
    }
}
