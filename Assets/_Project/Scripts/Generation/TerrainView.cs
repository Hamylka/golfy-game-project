using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Turns a course shape into meshes in the scene.</summary>
    public sealed class TerrainView : MonoBehaviour
    {
        [SerializeField] private CourseShapeSettings shape;
        [SerializeField] private PaletteSettings palette;
        [SerializeField] private Material landMaterial;
        [SerializeField] private Material waterMaterial;
        [Tooltip("Optional. Trees share the land material, so they add one draw call in all.")]
        [SerializeField] private TreeSettings trees;

        [Tooltip("Optional. The grass border along the island edge. Shares the land material too.")]
        [SerializeField] private TuftSettings tufts;

        private readonly List<Mesh> _meshes = new List<Mesh>();

        /// <summary>Points along the island edge, for framing it.</summary>
        public IReadOnlyList<Vector3> Outline { get; private set; }

        public void Build()
        {
            Clear();
            TerrainResult result = TerrainBuilder.Build(shape, palette);
            AddMesh("Land", result.Land, landMaterial);
            AddMesh("Water", result.Water, waterMaterial);
            if (trees != null)
            {
                AddMesh("Trees", TreeBuilder.Build(trees, palette, shape), landMaterial);
            }

            if (tufts != null)
            {
                MeshRenderer renderer = AddMesh("Tufts", TuftBuilder.Build(tufts, palette, result.Edge), landMaterial);
                if (renderer != null && !tufts.CastShadows)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            Outline = result.Outline;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private MeshRenderer AddMesh(string meshName, TerrainMeshData data, Material material)
        {
            if (data.TriangleCount == 0)
            {
                return null;
            }

            var indices = new int[data.Positions.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            var mesh = new Mesh { name = meshName };

            // A par 3 stays well under this, but a long course could pass the 16-bit vertex limit.
            mesh.indexFormat = data.Positions.Count > ushort.MaxValue
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(data.Positions);
            mesh.SetColors(data.Colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var child = new GameObject(meshName);
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void Clear()
        {
            foreach (Mesh mesh in _meshes)
            {
                Destroy(mesh);
            }

            _meshes.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
