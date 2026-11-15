using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Draws the waterfall's transparent material once, invisibly, at course load. On Vulkan the first draw of a material builds its
    /// pipeline state, which cost one 82-99 ms frame at the first shot of a course with a waterfall (Docs/Measurements/m4). The
    /// pipeline state depends on the render pass it is drawn in, so the warm-up is a real draw in the main camera's own pass: a
    /// triangle just past the near plane whose vertex alpha is 0, so nothing shows. It is visual only and never read by the simulation.
    /// </summary>
    public sealed class WaterfallWarmup : MonoBehaviour
    {
        private const int FramesToKeep = 2;

        private Mesh _mesh;
        private int _endFrame;

        /// <summary>Starts a warm-up with this material in front of the main camera, if there is one.</summary>
        public static void Run(Material material)
        {
            Camera camera = Camera.main;
            if (material == null || camera == null)
            {
                return;
            }

            var go = new GameObject("WaterfallWarmup");
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, camera.nearClipPlane * 2f);

            var mesh = new Mesh { name = "WaterfallWarmup" };
            mesh.vertices = new[] { new Vector3(-0.01f, -0.01f, 0f), new Vector3(0f, 0.01f, 0f), new Vector3(0.01f, -0.01f, 0f) };
            mesh.colors = new[] { new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 1, 2 };
            // Parented to the camera it is always in view; wide bounds keep it from ever being culled.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var warmup = go.AddComponent<WaterfallWarmup>();
            warmup._mesh = mesh;
            warmup._endFrame = Time.frameCount + FramesToKeep;
        }

        private void Update()
        {
            if (Time.frameCount >= _endFrame)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }
    }

#if ISOGOLF_MEASURE
    /// <summary>Phone checks only: logs the frame at which the waterfall renderer is first seen by a camera, to set against the first-shot hitch.</summary>
    public sealed class WaterfallVisibilityLog : MonoBehaviour
    {
        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (_renderer != null && _renderer.isVisible)
            {
                Debug.Log($"MEASURE_WATERFALL_VISIBLE frame={Time.frameCount} t={Time.realtimeSinceStartup:F3}");
                enabled = false;
            }
        }
    }
#endif
}
