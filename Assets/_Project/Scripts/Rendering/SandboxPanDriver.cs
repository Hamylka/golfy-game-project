using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Sandbox tool for judging shimmer: slowly pans along the play axis, or follows a finger or mouse drag.
    /// A short tap pauses or resumes the automatic pan. Not meant for gameplay scenes.
    /// </summary>
    [RequireComponent(typeof(PixelCamera))]
    public sealed class SandboxPanDriver : MonoBehaviour
    {
        [Tooltip("How far the automatic pan travels each way along world X, in units.")]
        [SerializeField, Min(0f)] private float autoPanDistance = 4f;

        [Tooltip("Seconds for one full automatic pan back and forth.")]
        [SerializeField, Min(0.1f)] private float autoPanPeriod = 20f;

        [Tooltip("A press shorter than this, without moving, counts as a tap. In seconds.")]
        [SerializeField, Min(0f)] private float tapSeconds = 0.25f;

        [Tooltip("A press moving further than this counts as a drag. In screen pixels.")]
        [SerializeField, Min(0f)] private float tapMaxPixels = 16f;

        private PixelCamera _pixelCamera;
        private Vector3 _origin;
        private bool _autoPan = true;
        private float _autoPanTime;
        private bool _pressed;
        private bool _dragged;
        private float _pressTime;
        private Vector2 _pressPosition;
        private Vector2 _lastPointer;

        private void Start()
        {
            _pixelCamera = GetComponent<PixelCamera>();
            _origin = _pixelCamera.Position;
        }

        private void Update()
        {
            HandlePointer();

            if (_autoPan && !_pressed)
            {
                _autoPanTime += Time.deltaTime;

                // A sine sweeps through every speed, including the very slow ones where shimmer shows most.
                float offset = Mathf.Sin(_autoPanTime * Mathf.PI * 2f / autoPanPeriod) * autoPanDistance;
                _pixelCamera.Position = _origin + (Vector3.right * offset);
            }
        }

        private void HandlePointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            Vector2 position = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pressed = true;
                _dragged = false;
                _pressTime = Time.unscaledTime;
                _pressPosition = position;
                _lastPointer = position;
            }

            if (_pressed && pointer.press.isPressed)
            {
                if ((position - _pressPosition).magnitude > tapMaxPixels)
                {
                    _dragged = true;
                }

                if (_dragged)
                {
                    DragBy(position - _lastPointer);
                }

                _lastPointer = position;
            }

            if (_pressed && pointer.press.wasReleasedThisFrame)
            {
                _pressed = false;

                if (!_dragged && Time.unscaledTime - _pressTime <= tapSeconds)
                {
                    _autoPan = !_autoPan;
                }
                else if (_dragged)
                {
                    _autoPan = false;
                }
            }
        }

        private void DragBy(Vector2 screenDelta)
        {
            // One to one with the finger: the world stays under it.
            float unitsPerScreenPixel = 1f / (_pixelCamera.PixelsPerUnit * _pixelCamera.Frame.Scale);
            Vector3 move = (transform.right * screenDelta.x) + (transform.up * screenDelta.y);
            _pixelCamera.Position -= move * unitsPerScreenPixel;
        }
    }
}
