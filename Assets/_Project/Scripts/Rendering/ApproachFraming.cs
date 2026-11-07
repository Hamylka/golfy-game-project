using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// The zoom for a ball at rest near the hole: just close enough to put the hole at a set height on screen, with the
    /// ball at its rest spot below it. There is no threshold: far from the hole that zoom is the default zoom, and it
    /// grows smoothly to the closest allowed as the ball nears the hole. Pure maths, for the camera and for tests.
    /// </summary>
    public sealed class ApproachFraming
    {
        // Enough for a zoom step far below what the eye sees: the range is halved each time.
        private const int Iterations = 24;

        private readonly Vector2 _hole;
        private readonly float _reach;
        private readonly float _holeViewportY;
        private readonly float _defaultZoom;
        private readonly float _closestZoom;

        /// <param name="hole">The hole on the ground (x is world X, y is world Z).</param>
        /// <param name="reach">
        /// How far a full-power shot goes, in metres. Further away the heading follows the leg, the hole isn't straight
        /// up the screen, and the default zoom applies.
        /// </param>
        /// <param name="holeViewportY">How high on screen the hole should sit, from -1 (bottom) to 1 (top).</param>
        /// <param name="defaultZoom">The furthest this framing goes: the default zoom.</param>
        /// <param name="closestZoom">The closest it goes, on the navigator's scale (1 is the closest the player can zoom).</param>
        public ApproachFraming(Vector2 hole, float reach, float holeViewportY, float defaultZoom, float closestZoom)
        {
            _hole = hole;
            _reach = reach;
            _holeViewportY = holeViewportY;
            _defaultZoom = defaultZoom;
            _closestZoom = Mathf.Max(defaultZoom, closestZoom);
        }

        public float DefaultZoom => _defaultZoom;

        /// <summary>
        /// The zoom for a ball resting here, seen at this yaw with the ball at the rest spot on screen. Between the
        /// default zoom and the closest zoom.
        /// </summary>
        public float ZoomFor(CameraNavigator navigator, Vector3 ball, Vector2 rest, float yaw)
        {
            if ((_hole - new Vector2(ball.x, ball.z)).magnitude > _reach)
            {
                return _defaultZoom;
            }

            // Read at the ball's height: the green near the hole is at about the same height as the ball on it.
            var hole = new Vector3(_hole.x, ball.y, _hole.y);

            // A closer camera, with the ball held at its spot, shows what lies ahead higher up the screen: the height
            // of the hole only rises with the zoom, so halving the range finds the zoom that puts it where it should be.
            // A hole at the ball, or behind it, never gets there and takes the closest zoom.
            if (HoleHeight(navigator, ball, hole, rest, yaw, _defaultZoom) >= _holeViewportY)
            {
                return _defaultZoom;
            }

            if (HoleHeight(navigator, ball, hole, rest, yaw, _closestZoom) <= _holeViewportY)
            {
                return _closestZoom;
            }

            float low = _defaultZoom, high = _closestZoom;
            for (int i = 0; i < Iterations; i++)
            {
                float middle = (low + high) * 0.5f;
                if (HoleHeight(navigator, ball, hole, rest, yaw, middle) < _holeViewportY)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            // The furthest zoom that gets the hole there: the hole sits at the height wanted, or just above it.
            return high;
        }

        private static float HoleHeight(CameraNavigator navigator, Vector3 ball, Vector3 hole, Vector2 rest, float yaw, float zoom)
        {
            Vector3 camera = navigator.FreePositionWith(zoom, ball, rest, yaw, out CameraView view);
            return view.ToViewport(camera, hole).y;
        }
    }
}
