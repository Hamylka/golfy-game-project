using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Turns cloud settings into a mesh in the scene and drifts it slowly.</summary>
    public sealed class CloudView : MonoBehaviour
    {
        [SerializeField] private CloudSettings settings;
        [SerializeField] private PaletteSettings palette;
        [SerializeField] private Material material;
        [Tooltip("Facet tones are baked from this light's direction when the clouds are built.")]
        [SerializeField] private Light sun;

        private Mesh _mesh;

        /// <summary>Builds the layer. <paramref name="visibleAtHeight"/> gives the ground rectangle the framed view sees at a height.</summary>
        public void Build(Func<float, Rect> visibleAtHeight)
        {
            // Lower planes reach further up the screen, higher planes further down it, so take both.
            Rect high = visibleAtHeight(Mathf.Max(settings.Highest, settings.NearHighest));
            Rect low = visibleAtHeight(Mathf.Min(settings.Lowest, settings.NearLowest));
            Rect visible = Rect.MinMaxRect(
                Mathf.Min(high.xMin, low.xMin),
                Mathf.Min(high.yMin, low.yMin),
                Mathf.Max(high.xMax, low.xMax),
                Mathf.Max(high.yMax, low.yMax));

            TerrainMeshData data = CloudBuilder.Build(settings, palette, visible, -sun.transform.forward);
            var indices = new int[data.Positions.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            _mesh = new Mesh { name = "Clouds" };
            _mesh.indexFormat = data.Positions.Count > ushort.MaxValue
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.SetVertices(data.Positions);
            _mesh.SetColors(data.Colors);
            _mesh.SetTriangles(indices, 0);
            _mesh.RecalculateBounds();

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            // The material is unlit, and nothing below the clouds could receive their shadow.
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void Update()
        {
            float offset = CloudBuilder.DriftOffset(Time.time, settings.DriftSpeed, settings.DriftRange);
            transform.position = new Vector3(0f, 0f, offset);
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }
    }
}
