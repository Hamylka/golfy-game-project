using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Turns a course into meshes in the scene. The course is handed in (generated and checked by the level bootstrap) or, without one, read from a hand-made shape.</summary>
    public sealed class TerrainView : MonoBehaviour
    {
        [SerializeField] private CourseShapeSettings shape;

        [SerializeField] private PaletteSettings palette;
        [SerializeField] private Material landMaterial;
        [SerializeField] private Material waterMaterial;
        [Tooltip("Optional. Trees share the land material, so they add one draw call in all.")]
        [SerializeField] private TreeSettings trees;
        [Tooltip("Proportions of the trees above. Needed when trees is set.")]
        [SerializeField] private PropKitSettings propKit;

        [Tooltip("Optional. The grass border along the island edge. Shares the land material too.")]
        [SerializeField] private TuftSettings tufts;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<PropShape> _propShapes = new List<PropShape>();

        /// <summary>
        /// The collision shapes of the props this view built, in the course's local space (the hand-placed
        /// trees for now; M4 PR 4.3 reads them from the course's own prop list).
        /// </summary>
        public IReadOnlyList<PropShape> PropShapes => _propShapes;

        /// <summary>The course this view built at each <see cref="Build"/>.</summary>
        public CourseLayout Layout { get; private set; }

        /// <summary>Points along the island edge, for framing it.</summary>
        public IReadOnlyList<Vector3> Outline { get; private set; }

        /// <summary>Every piece of every island's edge, for where the camera may look.</summary>
        public IReadOnlyList<EdgeSegment> Edge { get; private set; }

        /// <summary>Builds the hand-made shape, the fixed test course of the sandbox scenes.</summary>
        public void Build()
        {
            Build(shape.ToLayout());
        }

        /// <summary>Builds this course, a generated one say.</summary>
        public void Build(CourseLayout layout)
        {
            Clear();
            Layout = layout;
            TerrainResult result = TerrainBuilder.Build(Layout, palette);
            AddMesh("Land", result.Land, landMaterial);
            AddMesh("Water", result.Water, waterMaterial);
            if (trees != null && propKit != null)
            {
                AddMesh("Trees", PropBuilder.BuildTrees(trees, propKit, palette, Layout, _propShapes), landMaterial);
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
            Edge = result.Edge;
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

            Mesh mesh = data.ToMesh(meshName);
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
            _propShapes.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
