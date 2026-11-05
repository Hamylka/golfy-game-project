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
        private CameraView _view;

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
            _home = CameraFraming.Fit(points, _view, settings.ScreenFill);
            Rect limits = GroundLimits(points);

            _navigator = new CameraNavigator(_home, _view, limits, groundHeight, settings.ClosestZoom);
            var momentum = new PanMomentum(settings.GlideTimeConstant, settings.GlideVelocityWindow, settings.GlideMinReleaseSpeed, settings.GlideStopSpeed);
            _router = new CameraGestureRouter(_navigator, momentum);
            transform.position = _home;
        }

        /// <summary>Frames these points instead of the box, for a tighter fit around a real island shape.</summary>
        public void SetIsland(IReadOnlyList<Vector3> points)
        {
            _framePoints = points;
            Rebuild();
        }

        /// <summary>The rectangle a horizontal plane shows in the framed view, the widest the player can see.</summary>
        public Rect VisibleGround(float height)
        {
            return CameraFraming.GroundFootprint(_home, _view, height);
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
