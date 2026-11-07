using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Frames the island and applies the player's camera gestures: drag to move around, pinch to zoom, twist to turn. Turns the view
    /// to the automatic heading when a ball comes to rest and on the reset view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IslandCamera : MonoBehaviour
    {
        // How far apart, in metres, the island spots are where the headings the camera can take are read.
        private const float HeadingSampleStep = 1f;

        // How finely, in degrees, those headings are swept for the ground the views can show.
        private const float YawSampleStep = 5f;

        [SerializeField] private CameraRigSettings settings;

        [Tooltip("The box to frame, usually the island.")]
        [SerializeField] private Bounds island = new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f));

        [SerializeField] private float groundHeight;

        private Camera _camera;
        private CameraNavigator _navigator;
        private CameraGestureRouter _router;
        private Vector2Int _screenSize;
        private IReadOnlyList<Vector3> _framePoints;
        private Vector3 _home;
        private IReadOnlyList<Vector2> _centreline;
        private IReadOnlyList<GroundSegment> _edges;
        private IslandArea _area;
        private CameraHeading _rule;
        private ApproachFraming _approach;
        private CameraView _view;
        private CameraFollow _follow;
        private bool _fingerDown;
        private Vector3? _placedOn;
        private readonly List<Func<Vector2, bool>> _touchClaims = new List<Func<Vector2, bool>>();
        private Func<Vector2, bool> _claimsTouch;

        /// <summary>Turned off while aiming (from M2), so gestures can't disturb a shot.</summary>
        public bool GesturesEnabled { get; set; } = true;

        /// <summary>
        /// Adds a claim, asked when a first finger lands, in viewport coordinates from -1 to 1. If any claim returns
        /// true, the camera stays out of that touch. The ball claims its touches so dragging on it starts a shot,
        /// and the reset view button claims its own. Claims add up, so none can overwrite another.
        /// </summary>
        public void AddTouchClaim(Func<Vector2, bool> claim)
        {
            if (claim != null && !_touchClaims.Contains(claim))
            {
                _touchClaims.Add(claim);
            }
        }

        public void RemoveTouchClaim(Func<Vector2, bool> claim)
        {
            _touchClaims.Remove(claim);
        }

        public CameraNavigator Navigator => _navigator;

        /// <summary>The follow, which owns the heading and the player's twist.</summary>
        public CameraFollow Follower => _follow;

        private void OnEnable()
        {
            // One delegate for good, since the router is asked every time a finger lands.
            _claimsTouch ??= ClaimsTouch;
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = IsoCameraConvention.VerticalFieldOfView;
            Rebuild();
        }

        private void Update()
        {
            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height)
            {
                Rebuild();
            }

            // The router must run exactly once a frame, because it also advances the glide.
            _fingerDown = false;
            if (!ReadTouches() && !ReadMouse())
            {
                _router.Update(0, default, default, GesturesEnabled, _claimsTouch, Time.unscaledDeltaTime);
            }

            ApplyPose();
        }

        private void ApplyPose()
        {
            transform.SetPositionAndRotation(_navigator.Position, _navigator.View.Rotation);
        }

        private void Rebuild()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            IReadOnlyList<Vector3> points = _framePoints ?? CameraFraming.Corners(island);

            // Framed facing the first leg, which sets the one zoomed-out height for every heading. A straight course
            // faces +X, at the convention's exact rotation.
            Vector2 firstLeg = new CameraHeading(_centreline, Vector2.zero, 0f, 0f).FirstLeg;
            float yaw = IsoCameraConvention.YawFor(firstLeg);
            Quaternion rotation = Mathf.Abs(Mathf.DeltaAngle(yaw, IsoCameraConvention.StraightYaw)) < 1e-3f
                ? IsoCameraConvention.Rotation
                : IsoCameraConvention.RotationAt(yaw);
            _view = new CameraView(rotation, IsoCameraConvention.VerticalFieldOfView, _screenSize.x / (float)_screenSize.y);
            _home = CameraFraming.FitCourse(points, _centreline, _view, settings.ScreenFill, settings.FramedLength);

            _area = _edges != null && _edges.Count > 0
                ? new IslandArea(_edges, _centreline, settings.IslandMargin)
                : IslandArea.FromRect(GroundLimits(points), settings.IslandMargin);
            _navigator = new CameraNavigator(_home, _view, _area, groundHeight, settings.ClosestZoom);
            var momentum = new PanMomentum(settings.GlideTimeConstant, settings.GlideVelocityWindow, settings.GlideMinReleaseSpeed, settings.GlideStopSpeed);
            var ease = new HeadingEase(settings.HeadingSmoothTime, settings.HeadingMinTurn, settings.MaxTwist);
            _follow = new CameraFollow(_navigator, settings.FollowZone, settings.FollowBackstop, settings.RestBallViewport, settings.FollowSmoothTime, settings.ReturnSmoothTime, settings.DefaultZoom, settings.ResetSeconds, ease)
            {
                Rule = _rule,
                Approach = _approach,
            };

            // The twist has no glide after release: it is bounded, and a view still turning after the fingers lift
            // would be hard to stop on the angle wanted.
            _router = new CameraGestureRouter(_navigator, momentum, new TwistFilter(settings.TwistDeadZone, settings.TwistMinSpread), _follow.TwistBy);

            if (_placedOn.HasValue)
            {
                StartViewOn(_placedOn.Value);
            }

            ApplyPose();
        }

        /// <summary>
        /// Frames these points instead of the box, for a tighter fit around a real island shape, along the course's
        /// centreline (world X and Z, tee to hole). Without a centreline, or with a straight one, along +X. The edges,
        /// every piece of every island's outline, set where the view's centre may go; without them, the points'
        /// bounding rectangle does.
        /// </summary>
        public void SetIsland(IReadOnlyList<Vector3> points, IReadOnlyList<Vector2> centreline = null, IReadOnlyList<GroundSegment> edges = null)
        {
            _framePoints = points;
            _centreline = centreline;
            _edges = edges;
            Rebuild();
        }

        /// <summary>
        /// Gives the camera the hole and how far a full-power shot goes on flat fairway, so a ball at rest within that
        /// reach turns the view to face the hole, and comes closer near it. Without it, the view follows the leg the
        /// ball is on, at the player's zoom.
        /// </summary>
        public void SetHole(Vector2 hole, float fullPowerReach)
        {
            _rule = new CameraHeading(_centreline, hole, fullPowerReach, settings.HoleHeadingMinDistance, settings.HoleHeadingMaxTurn);
            _approach = new ApproachFraming(hole, fullPowerReach, settings.ApproachHoleViewportY, settings.DefaultZoom, settings.ApproachClosestZoom);
            _follow.Rule = _rule;
            _follow.Approach = _approach;
        }

        /// <summary>Starts the level's view on this spot, for the ball on its tee, at the default zoom (closer if the tee is near the hole).</summary>
        public void PlaceOn(Vector3 ball)
        {
            _placedOn = ball;
            StartViewOn(ball);
            ApplyPose();
        }

        private void StartViewOn(Vector3 ball)
        {
            _follow.Rule = _rule;
            _follow.Approach = _approach;
            _navigator.ZoomTo(settings.DefaultZoom);
            _follow.PlaceOn(ball);
        }

        /// <summary>
        /// Moves the camera for the shot in play. Call once a frame, after every Update, with the ball's position
        /// free of the course's floating.
        /// </summary>
        public void Follow(Vector3 ball, FollowedBallState state)
        {
            _follow.Update(ball, state, _fingerDown, Time.unscaledDeltaTime);
            if (_follow.IsEasing)
            {
                _router.StopGlide();
            }

            ApplyPose();
        }

        /// <summary>
        /// Eases the view back to the default one: the default zoom and the automatic heading, with this ball at its
        /// rest spot, and clears any twist. Meant for a ball at rest. A finger on the screen, or a shot, hands the
        /// camera back to the player.
        /// </summary>
        public void ResetView(Vector3 ball)
        {
            _router.StopGlide();
            _follow.BeginReset(ball);
        }

        private bool ClaimsTouch(Vector2 viewport)
        {
            for (int i = 0; i < _touchClaims.Count; i++)
            {
                if (_touchClaims[i](viewport))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The rectangle a horizontal plane shows across every fully zoomed out view the player can reach: the view's
        /// centre anywhere in the island's area, at every heading the camera can take there, twist included.
        /// </summary>
        public Rect VisibleGround(float height)
        {
            return VisibleGround(height, _home, _view, groundHeight, _area, ReachableYaws(_rule, _area, _view.Yaw, settings.MaxTwist));
        }

        /// <summary>The same, worked out from its parts, for tests.</summary>
        public static Rect VisibleGround(float height, Vector3 home, in CameraView view, float groundHeight, IslandArea area, IEnumerable<float> yaws)
        {
            // A view's footprint only depends on its heading, moved with its centre, so the views' union is the area's
            // bounds widened by the footprints of a view centred on the origin, at every heading.
            Vector3 centre = view.OnPlane(home, Vector2.zero, groundHeight);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (float yaw in yaws)
            {
                Vector3 camera = CameraNavigator.Turned(home, view.Yaw, yaw, centre) - centre;
                Rect footprint = CameraFraming.GroundFootprint(camera, view.WithYaw(yaw), height);
                minX = Mathf.Min(minX, footprint.xMin);
                maxX = Mathf.Max(maxX, footprint.xMax);
                minZ = Mathf.Min(minZ, footprint.yMin);
                maxZ = Mathf.Max(maxZ, footprint.yMax);
            }

            Rect bounds = area.Bounds;
            return Rect.MinMaxRect(bounds.xMin + minX, bounds.yMin + minZ, bounds.xMax + maxX, bounds.yMax + maxZ);
        }

        /// <summary>
        /// Every yaw the camera can take on this course, in steps of <see cref="YawSampleStep"/>: the automatic heading
        /// for a ball at rest anywhere on the islands, the starting one, and up to <paramref name="maxTwist"/> either
        /// side of each. With no rule, the starting yaw and its twists.
        /// </summary>
        public static List<float> ReachableYaws(CameraHeading rule, IslandArea area, float startYaw, float maxTwist)
        {
            var headings = new HashSet<int> { Bin(startYaw) };
            if (rule != null)
            {
                Rect bounds = area.IslandBounds;
                for (float x = bounds.xMin; x <= bounds.xMax; x += HeadingSampleStep)
                {
                    for (float z = bounds.yMin; z <= bounds.yMax; z += HeadingSampleStep)
                    {
                        var spot = new Vector2(x, z);
                        if (area.OnIsland(spot))
                        {
                            headings.Add(Bin(rule.YawFor(spot)));
                        }
                    }
                }
            }

            var yaws = new HashSet<int>();
            int twistBins = Mathf.CeilToInt(maxTwist / YawSampleStep);
            foreach (int heading in headings)
            {
                for (int twist = -twistBins; twist <= twistBins; twist++)
                {
                    yaws.Add(((heading + twist) % BinCount + BinCount) % BinCount);
                }
            }

            var result = new List<float>(yaws.Count);
            foreach (int bin in yaws)
            {
                result.Add(bin * YawSampleStep);
            }

            result.Sort();
            return result;
        }

        private static int BinCount => Mathf.RoundToInt(360f / YawSampleStep);

        private static int Bin(float yaw)
        {
            return Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / YawSampleStep) % BinCount;
        }

        private static Rect GroundLimits(IReadOnlyList<Vector3> points)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 point in points)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minZ = Mathf.Min(minZ, point.z);
                maxZ = Mathf.Max(maxZ, point.z);
            }

            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>Returns true if the screen is being touched, so the mouse is ignored this frame.</summary>
        private bool ReadTouches()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }

            TouchControl first = null;
            TouchControl second = null;
            int pressed = 0;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                pressed++;
                if (first == null)
                {
                    first = touch;
                }
                else if (second == null)
                {
                    second = touch;
                }
            }

            if (pressed == 0)
            {
                return false;
            }

            _fingerDown = true;
            Vector2 a = ToViewport(first.position.ReadValue());
            Vector2 b = second != null ? ToViewport(second.position.ReadValue()) : default;
            _router.Update(pressed, a, b, GesturesEnabled, _claimsTouch, Time.unscaledDeltaTime);
            return true;
        }

        /// <summary>Returns false if there is no mouse.</summary>
        private bool ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            _fingerDown = mouse.leftButton.isPressed;
            Vector2 position = ToViewport(mouse.position.ReadValue());
            _router.Update(mouse.leftButton.isPressed ? 1 : 0, position, default, GesturesEnabled, _claimsTouch, Time.unscaledDeltaTime);

            float scroll = mouse.scroll.ReadValue().y;
            if (GesturesEnabled && !Mathf.Approximately(scroll, 0f))
            {
                _navigator.ZoomAround(position, scroll > 0f ? 1f - settings.WheelZoomStep : 1f + settings.WheelZoomStep);
            }

            return true;
        }

        private Vector2 ToViewport(Vector2 screenPosition)
        {
            return new Vector2(
                ((screenPosition.x / _screenSize.x) * 2f) - 1f,
                ((screenPosition.y / _screenSize.y) * 2f) - 1f);
        }
    }
}
