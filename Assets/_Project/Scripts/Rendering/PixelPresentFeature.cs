using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Upscales the low-res image to the screen with nearest-neighbour sampling.
    /// Only acts on a camera carrying <see cref="PixelPresenter"/>, so the Scene view is untouched.
    /// </summary>
    public sealed class PixelPresentFeature : ScriptableRendererFeature
    {
        private PresentPass _pass;

        public override void Create()
        {
            _pass = new PresentPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera.cameraType != CameraType.Game || !camera.TryGetComponent(out PixelPresenter presenter))
            {
                return;
            }

            PixelCamera source = presenter.Source;
            if (source == null || !source.IsReady)
            {
                return;
            }

            _pass.Setup(source);
            renderer.EnqueuePass(_pass);
        }

        private sealed class PresentPass : ScriptableRenderPass
        {
            private PixelCamera _source;

            public void Setup(PixelCamera source)
            {
                _source = source;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();

                Vector2 scale = _source.Frame.UvScale;
                Vector2 offset = _source.Frame.UvOffset;

                // Render textures and the back buffer use opposite vertical conventions on
                // Direct3D, Metal and Vulkan, but the same one on OpenGL ES.
                if (SystemInfo.graphicsUVStartsAtTop && resources.isActiveTargetBackBuffer)
                {
                    offset.y += scale.y;
                    scale.y = -scale.y;
                }

                // The texture is not imported into the render graph: import rejects a texture that
                // has both color and depth, and a camera target needs both. It is safe untracked,
                // because the pixel camera has finished rendering before this camera starts.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixel Present", out PassData data))
                {
                    data.Source = _source.TargetHandle;
                    data.ScaleBias = new Vector4(scale.x, scale.y, offset.x, offset.y);

                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, passData.Source, passData.ScaleBias, 0f, false);
                    });
                }
            }

            private sealed class PassData
            {
                public RTHandle Source;
                public Vector4 ScaleBias;
            }
        }
    }
}
