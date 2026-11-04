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

        public PixelFrame Frame { get; private set; }

        /// <summary>The Blitter draws textures through an RTHandle.</summary>
        public RTHandle TargetHandle => _targetHandle;

        public bool IsReady => _target != null;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            transform.rotation = IsoCameraConvention.Rotation;
            Rebuild();
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
