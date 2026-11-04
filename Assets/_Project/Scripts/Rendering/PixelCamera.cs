using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Renders the world into the low-res texture that <see cref="PixelPresenter"/> shows on screen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCamera : MonoBehaviour
    {
        [SerializeField] private PixelCameraSettings settings;

        private Camera _camera;
        private RenderTexture _target;
        private RTHandle _targetHandle;
        private Vector2Int _screenSize;
        private Vector3 _requestedPosition;

        public PixelFrame Frame { get; private set; }

        public int PixelsPerUnit => settings.PixelsPerUnit;

        /// <summary>Texture offset for the upscale this frame, including the sub-pixel shift.</summary>
        public Vector2 PresentUvOffset { get; private set; }

        /// <summary>
        /// Where the camera should appear to be. The transform itself only ever holds the snapped
        /// position, so anything moving the camera must go through here.
        /// </summary>
        public Vector3 Position
        {
            get => _requestedPosition;
            set => _requestedPosition = value;
        }

        /// <summary>The Blitter draws textures through an RTHandle.</summary>
        public RTHandle TargetHandle => _targetHandle;

        public bool IsReady => _target != null;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = IsoCameraConvention.Rotation;
            _requestedPosition = transform.position;
            Rebuild();
            ApplySnap();
        }

        private void OnDisable()
        {
            ReleaseTarget();
        }

        private void Update()
        {
            // The Game view can be resized at any time, and a phone can report a new size on resume.
            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height)
            {
                Rebuild();
            }
        }

        private void LateUpdate()
        {
            // Late, so a camera moved in Update is snapped once, right before rendering.
            ApplySnap();
        }

        private void ApplySnap()
        {
            PixelSnap snap = PixelGrid.Snap(_requestedPosition, transform.rotation, settings.PixelsPerUnit);
            transform.position = snap.Position;
            PresentUvOffset = PixelGrid.PresentOffset(Frame, snap.ResidualTexels, settings.MarginTexels);
        }

        private void Rebuild()
        {
            ReleaseTarget();

            _screenSize = new Vector2Int(Screen.width, Screen.height);
            Frame = PixelGrid.Solve(_screenSize, settings.TargetWidth, settings.PixelsPerUnit, settings.MarginTexels);

            _target = new RenderTexture(
                Frame.RenderSize.x,
                Frame.RenderSize.y,
                GraphicsFormat.R8G8B8A8_SRGB,
                SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil))
            {
                name = "PixelCamera Target",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                antiAliasing = 1,
            };
            _target.Create();
            _targetHandle = RTHandles.Alloc(_target);

            _camera.orthographic = true;
            _camera.orthographicSize = Frame.OrthographicSize;
            _camera.targetTexture = _target;
        }

        private void ReleaseTarget()
        {
            if (_target == null)
            {
                return;
            }

            _camera.targetTexture = null;
            _targetHandle.Release();
            _targetHandle = null;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
