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
        private float _corridorLength;
        private CameraView _view;
        private CameraFollow _follow;
        private bool _fingerDown;
        private Vector3? _placedOn;

        /// <summary>Turned off while aiming (from M2), so gestures can't disturb a shot.</summary>
        public bool GesturesEnabled { get; set; } = true;

        /// <summary>
        /// Asked when a first finger lands, in viewport coordinates from -1 to 1. Returning true keeps
        /// the camera out of that touch. The ball uses this, so dragging on it starts a shot instead.
        /// </summary>
        public Func<Vector2, bool> TouchClaim { get; set; }

        public CameraNavigator Navigator => _navigator;

        private void OnEnable()
        {
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
                _router.Update(0, default, default, GesturesEnabled, TouchClaim, Time.unscaledDeltaTime);
            }

            transform.position = _navigator.Position;
        }

        private void Rebuild()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            _view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, _screenSize.x / (float)_screenSize.y);
            IReadOnlyList<Vector3> points = _framePoints ?? CameraFraming.Corners(island);
            CourseFrame frame = CameraFraming.FitCourse(points, _view, settings.ScreenFill, settings.FramedLength);
            _home = frame.Home;
            _corridorLength = frame.CorridorLength;
            Rect limits = GroundLimits(points);

            // From the default zoom on, so the follow can rest the ball at its spot along the whole course.
            float fullLimitsZoom = settings.DefaultZoom > 0f ? settings.DefaultZoom : 1f;
            _navigator = new CameraNavigator(_home, _view, limits, groundHeight, settings.ClosestZoom, _corridorLength, fullLimitsZoom);
            var momentum = new PanMomentum(settings.GlideTimeConstant, settings.GlideVelocityWindow, settings.GlideMinReleaseSpeed, settings.GlideStopSpeed);
            _router = new CameraGestureRouter(_navigator, momentum);
            _follow = new CameraFollow(_navigator, _view, settings.FollowZone, settings.FollowBackstop, settings.RestBallViewport, settings.FollowSmoothTime, settings.ReturnSmoothTime);
            if (_placedOn.HasValue)
            {
                StartViewOn(_placedOn.Value);
            }

            transform.position = _navigator.Position;
        }

        /// <summary>Frames these points instead of the box, for a tighter fit around a real island shape.</summary>
        public void SetIsland(IReadOnlyList<Vector3> points)
        {
            _framePoints = points;
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
            if (_follow.IsReturning)
            {
                _router.StopGlide();
            }

            transform.position = _navigator.Position;
        }

        /// <summary>The rectangle a horizontal plane shows across the framed views, the widest the player can see.</summary>
        public Rect VisibleGround(float height)
        {
            Rect first = CameraFraming.GroundFootprint(_home, _view, height);
            first.xMax += _corridorLength;
            return first;
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
            _router.Update(pressed, a, b, GesturesEnabled, TouchClaim, Time.unscaledDeltaTime);
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
            _router.Update(mouse.leftButton.isPressed ? 1 : 0, position, default, GesturesEnabled, TouchClaim, Time.unscaledDeltaTime);

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
