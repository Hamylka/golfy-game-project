using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Frames the island and applies the player's camera gestures. One finger is left alone for the shot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class IslandCamera : MonoBehaviour
    {
        [SerializeField] private CameraRigSettings settings;

        [Tooltip("The box to frame, usually the island. Its bottom is treated as the ground for gestures unless set below.")]
        [SerializeField] private Bounds island = new Bounds(Vector3.zero, new Vector3(34f, 4f, 10f));

        [SerializeField] private float groundHeight;

        private Camera _camera;
        private CameraNavigator _navigator;
        private Vector2Int _screenSize;
        private Vector2 _previousA;
        private Vector2 _previousB;
        private bool _gestureActive;
        private Vector2 _previousMouse;

        /// <summary>Turned off while aiming (from M2), so gestures can't disturb a shot.</summary>
        public bool GesturesEnabled { get; set; } = true;

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

            ReadTouches();
            ReadMouse();
            transform.position = _navigator.Position;
        }

        private void Rebuild()
        {
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            var view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, _screenSize.x / (float)_screenSize.y);
            Vector3 home = CameraFraming.Fit(CameraFraming.Corners(island), view, settings.ScreenFill);
            var limits = new Rect(island.min.x, island.min.z, island.size.x, island.size.z);

            _navigator = new CameraNavigator(home, view, limits, groundHeight, settings.ClosestZoom);
            _gestureActive = false;
            transform.position = home;
        }

        private void ReadTouches()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
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

            // Exactly two fingers. A third finger or a lifted one ends the gesture instead of making the view jump.
            if (pressed != 2 || !GesturesEnabled)
            {
                _gestureActive = false;
                return;
            }

            Vector2 a = ToViewport(first.position.ReadValue());
            Vector2 b = ToViewport(second.position.ReadValue());

            if (_gestureActive)
            {
                _navigator.ApplyGesture(_previousA, _previousB, a, b);
            }

            _previousA = a;
            _previousB = b;
            _gestureActive = true;
        }

        private void ReadMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !GesturesEnabled)
            {
                return;
            }

            Vector2 position = ToViewport(mouse.position.ReadValue());
            float scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                _navigator.ZoomAround(position, scroll > 0f ? 1f - settings.WheelZoomStep : 1f + settings.WheelZoomStep);
            }

            if (mouse.rightButton.isPressed)
            {
                _navigator.Pan(_previousMouse, position);
            }

            _previousMouse = position;
        }

        private Vector2 ToViewport(Vector2 screenPosition)
        {
            return new Vector2(
                ((screenPosition.x / _screenSize.x) * 2f) - 1f,
                ((screenPosition.y / _screenSize.y) * 2f) - 1f);
        }
    }
}
