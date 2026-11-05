using System;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using IsoGolf.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace IsoGolf.UI
{
    /// <summary>
    /// The small reset view button in the top right: press it to ease the camera back to the default view. It's a
    /// mesh drawn in front of the camera rather than UGUI, so it adds no Canvas or EventSystem, and the logic of
    /// where it sits and when it fires lives in plain C# (<see cref="ResetViewButtonLayout"/>,
    /// <see cref="ResetViewButtonState"/>).
    ///
    /// Put this on a child of the camera. A touch that starts on the button is claimed from the camera, so it
    /// never pans, and it can't start a shot because that needs a touch on the ball.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ResetViewButton : MonoBehaviour
    {
        [SerializeField] private IslandCamera islandCamera;
        [SerializeField] private BallView ball;
        [SerializeField] private ResetViewButtonSettings settings;
        [SerializeField] private PaletteSettings palette;

        // Above the aim preview, though the two never show at once.
        private const int SortingOrder = 2;

        private readonly AimMesh _geometry = new AimMesh();
        private readonly ResetViewButtonState _state = new ResetViewButtonState();

        private Camera _camera;
        private IShotInput _input;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Func<Vector2, bool> _claim;
        private ResetViewButtonLayout _layout;
        private Vector2Int _screenSize;
        private Rect _safeArea;
        private bool _visible;
        private bool _wasPressed;
        private Color32 _markColor;
        private Color32 _backingColor;

        private void Awake()
        {
            _camera = islandCamera.GetComponent<Camera>();
            _input = ball.GetComponent<IShotInput>();
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = SortingOrder;
            _renderer.enabled = false;

            _markColor = TerrainBuilder.ToVertexColor(palette.ButtonMark);
            _backingColor = TerrainBuilder.ToVertexColor(palette.ButtonBacking);

            _mesh = new Mesh { name = "Reset View Button" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _claim = ClaimsTouch;
        }

        private void OnEnable()
        {
            islandCamera.AddTouchClaim(_claim);
        }

        private void OnDisable()
        {
            islandCamera.RemoveTouchClaim(_claim);
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }

        // After the camera's own Update, which reads the same touch, so the claim and the press agree.
        private void LateUpdate()
        {
            BallSimulation simulation = ball.Simulation;
            _visible = simulation != null && ResetViewButtonState.IsVisible(simulation.State == BallState.Resting, _input.IsAiming);

            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height || _safeArea != Screen.safeArea)
            {
                _screenSize = new Vector2Int(Screen.width, Screen.height);
                _safeArea = Screen.safeArea;
                _layout = ResetViewButtonLayout.TopRight(_safeArea, Screen.width, settings.Size, settings.Margin, settings.HitSize);
            }

            ReadPointer(out bool pressed, out Vector2 pixel, out int fingers);
            if (_state.Update(_visible, _layout.Hit, pressed, pixel, fingers))
            {
                islandCamera.ResetView(ball.transform.localPosition);
            }

            _renderer.enabled = _visible;
            if (_visible)
            {
                Draw();
            }
        }

        private bool ClaimsTouch(Vector2 viewport)
        {
            var pixel = new Vector2((viewport.x + 1f) * 0.5f * Screen.width, (viewport.y + 1f) * 0.5f * Screen.height);
            return ResetViewButtonState.ClaimsTouch(_visible, _layout.Hit, pixel);
        }

        private void Draw()
        {
            // The camera never rotates and the button sits straight in front of it, so its axes are the camera's own.
            float metresPerPixel = AimMesh.MetresPerScreenWidth(_camera.fieldOfView, _camera.aspect, settings.Depth) / Screen.width;
            Vector3 center = new Vector3(
                (_layout.Center.x - (Screen.width * 0.5f)) * metresPerPixel,
                (_layout.Center.y - (Screen.height * 0.5f)) * metresPerPixel,
                settings.Depth);

            float size = _layout.Size * (_state.IsPressed ? settings.PressedScale : 1f) * metresPerPixel;
            float radius = size * 0.5f;

            _geometry.Clear();
            _geometry.AddDot(center, radius, settings.Segments, Vector3.right, Vector3.up, _backingColor);
            _geometry.AddRing(center, radius * (1f - (settings.RingThickness * 2f)), radius * 0.92f, settings.Segments, Vector3.right, Vector3.up, _markColor);
            _geometry.AddDot(center, size * settings.DotRadius, settings.Segments, Vector3.right, Vector3.up, _markColor);

            _mesh.Clear();
            _mesh.SetVertices(_geometry.Positions);
            _mesh.SetColors(_geometry.Colors);
            _mesh.SetTriangles(_geometry.Triangles, 0);
        }

        private static void ReadPointer(out bool pressed, out Vector2 pixel, out int fingers)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                fingers = 0;
                foreach (TouchControl touch in touchscreen.touches)
                {
                    if (touch.press.isPressed)
                    {
                        fingers++;
                    }
                }

                if (fingers > 0)
                {
                    TouchControl primary = touchscreen.primaryTouch;
                    pressed = primary.press.isPressed;
                    pixel = primary.position.ReadValue();
                    return;
                }
            }

            Mouse mouse = Mouse.current;
            pressed = mouse != null && mouse.leftButton.isPressed;
            pixel = mouse != null ? mouse.position.ReadValue() : default;
            fingers = pressed ? 1 : 0;
        }
    }
}
