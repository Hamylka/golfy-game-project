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

            if (!ReadTouches())
            {
                ReadMouse();
            }

            transform.position = _navigator.Position;
        }

        private void Rebuild()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            var view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, _screenSize.x / (float)_screenSize.y);
            IReadOnlyList<Vector3> points = _framePoints ?? CameraFraming.Corners(island);
            Vector3 home = CameraFraming.Fit(points, view, settings.ScreenFill);
            Rect limits = GroundLimits(points);

            _navigator = new CameraNavigator(home, view, limits, groundHeight, settings.ClosestZoom);
            _router = new CameraGestureRouter(_navigator);
            transform.position = home;
        }

        /// <summary>Frames these points instead of the box, for a tighter fit around a real island shape.</summary>
        public void SetIsland(IReadOnlyList<Vector3> points)
        {
            _framePoints = points;
            Rebuild();
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
                _router.Update(0, default, default, GesturesEnabled, TouchClaim);
                return false;
            }

            Vector2 a = ToViewport(first.position.ReadValue());
            Vector2 b = second != null ? ToViewport(second.position.ReadValue()) : default;
            _router.Update(pressed, a, b, GesturesEnabled, TouchClaim);
            return true;
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 position = ToViewport(mouse.position.ReadValue());
            _router.Update(mouse.leftButton.isPressed ? 1 : 0, position, default, GesturesEnabled, TouchClaim);

            float scroll = mouse.scroll.ReadValue().y;
            if (GesturesEnabled && !Mathf.Approximately(scroll, 0f))
            {
                _navigator.ZoomAround(position, scroll > 0f ? 1f - settings.WheelZoomStep : 1f + settings.WheelZoomStep);
            }
        }

        private Vector2 ToViewport(Vector2 screenPosition)
        {
            return new Vector2(
                ((screenPosition.x / _screenSize.x) * 2f) - 1f,
                ((screenPosition.y / _screenSize.y) * 2f) - 1f);
        }
    }
}
