using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Frames the island and applies the player's camera gestures: drag to move around, pinch to zoom.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IslandCamera : MonoBehaviour
    {
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
        private IReadOnlyList<Vector2> _corridor;
        private IReadOnlyList<Vector2> _centreline;
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

        private void OnEnable()
        {
            // One delegate for good, since the router is asked every time a finger lands.
            _claimsTouch ??= ClaimsTouch;
            _camera = GetComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = IsoCameraConvention.VerticalFieldOfView;
            transform.rotation = IsoCameraConvention.Rotation;
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

            transform.position = _navigator.Position;
        }

        private void Rebuild()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, _screenSize.x / (float)_screenSize.y);
            IReadOnlyList<Vector3> points = _framePoints ?? CameraFraming.Corners(island);
            CourseFrame frame = CameraFraming.FitCourse(points, _centreline, _view, settings.ScreenFill, settings.FramedLength, groundHeight, settings.DoglegMaxStepBack);
            _home = frame.Home;
            _corridor = frame.Corridor;
            Rect limits = GroundLimits(points);

            // From the default zoom on, so the follow can rest the ball at its spot along the whole course.
            float fullLimitsZoom = settings.DefaultZoom > 0f ? settings.DefaultZoom : 1f;
            _navigator = new CameraNavigator(_home, _view, limits, groundHeight, settings.ClosestZoom, _corridor, fullLimitsZoom);
            var momentum = new PanMomentum(settings.GlideTimeConstant, settings.GlideVelocityWindow, settings.GlideMinReleaseSpeed, settings.GlideStopSpeed);
            _router = new CameraGestureRouter(_navigator, momentum);
            _follow = new CameraFollow(_navigator, _view, settings.FollowZone, settings.FollowBackstop, settings.RestBallViewport, settings.FollowSmoothTime, settings.ReturnSmoothTime, settings.DefaultZoom, settings.ResetSeconds, settings.RestLookAhead, settings.RestMaxShift);
            if (_placedOn.HasValue)
            {
                StartViewOn(_placedOn.Value);
            }

            transform.position = _navigator.Position;
        }

        /// <summary>
        /// Frames these points instead of the box, for a tighter fit around a real island shape, along the course's
        /// centreline (world X and Z, tee to hole). Without a centreline, or with a straight one, along +X.
        /// </summary>
        public void SetIsland(IReadOnlyList<Vector3> points, IReadOnlyList<Vector2> centreline = null)
        {
            _framePoints = points;
            _centreline = centreline;
            Rebuild();
        }

        /// <summary>Starts the level's view on this spot, for the ball on its tee, at the default zoom.</summary>
        public void PlaceOn(Vector3 ball)
        {
            _placedOn = ball;
            StartViewOn(ball);
            transform.position = _navigator.Position;
        }

        private void StartViewOn(Vector3 ball)
        {
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

            transform.position = _navigator.Position;
        }

        /// <summary>
        /// Eases the view back to the default one: the default zoom, with this ball at its rest spot on screen. Meant
        /// for a ball at rest. A finger on the screen, or a shot, hands the camera back to the player.
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

        /// <summary>The rectangle a horizontal plane shows across the framed views, the widest the player can see.</summary>
        public Rect VisibleGround(float height)
        {
            // Every framed view is the first one moved along the corridor, so together they cover its footprint
            // widened by the corridor's extent.
            Rect first = CameraFraming.GroundFootprint(_home, _view, height);
            float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
            foreach (Vector2 move in _corridor)
            {
                minX = Mathf.Min(minX, move.x);
                maxX = Mathf.Max(maxX, move.x);
                minZ = Mathf.Min(minZ, move.y);
                maxZ = Mathf.Max(maxZ, move.y);
            }

            return Rect.MinMaxRect(first.xMin + minX, first.yMin + minZ, first.xMax + maxX, first.yMax + maxZ);
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
