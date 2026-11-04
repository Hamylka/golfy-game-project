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

            Outline = result.Outline;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void AddMesh(string meshName, TerrainMeshData data, Material material)
        {
            if (data.TriangleCount == 0)
            {
                return;
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
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
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
