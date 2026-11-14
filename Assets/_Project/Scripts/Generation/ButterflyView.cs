using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Draws a course's butterflies (M4 PR 4.7): one to three, each looping round a bush and flapping two flat wings. Every wing is in one
    /// mesh rewritten each frame, so the lot costs one draw call (no shadow pass: they cast none). Lives under the Course container at
    /// the origin, so they float with the island. View-time only: nothing in the simulation, the checks or the aim preview reads them,
    /// and <see cref="ButterflyPlacement"/> draws from its own stream, so no prop moves. The mesh is on the land material.
    /// </summary>
    public sealed class ButterflyView : MonoBehaviour
    {
        private const int VerticesPerButterfly = 12;

        [SerializeField] private ButterflySettings settings;
        [SerializeField] private PaletteSettings palette;
        [SerializeField] private Material material;

        private List<ButterflySpec> _butterflies = new List<ButterflySpec>();
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _vertices;

        public IReadOnlyList<ButterflySpec> Butterflies => _butterflies;

        /// <summary>Picks this course's butterflies and builds their mesh. A course with no bush has none.</summary>
        public void Build(CourseLayout layout, int seed, int par, int variant)
        {
            Clear();
            _butterflies = ButterflyPlacement.Place(layout, seed, par, variant, settings);
            if (_butterflies.Count == 0)
            {
                return;
            }

            Color32 white = TerrainBuilder.ToVertexColor(palette.TraceOffWhite);
            Color32 yellow = TerrainBuilder.ToVertexColor(palette.GaugeFill);
            _vertices = new Vector3[_butterflies.Count * VerticesPerButterfly];
            var colors = new Color32[_vertices.Length];
            var normals = new Vector3[_vertices.Length];
            var triangles = new int[_vertices.Length];
            for (int i = 0; i < _butterflies.Count; i++)
            {
                for (int v = 0; v < VerticesPerButterfly; v++)
                {
                    int index = (i * VerticesPerButterfly) + v;
                    colors[index] = _butterflies[i].Yellow ? yellow : white;
                    normals[index] = Vector3.up;
                    triangles[index] = index;
                }
            }

            _mesh = new Mesh { name = "Butterflies" };
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(colors);
            _mesh.SetNormals(normals);
            _mesh.SetTriangles(triangles, 0);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Pose(0f);
        }

        private void Update()
        {
            if (_mesh != null)
            {
                Pose(Time.time);
            }
        }

        private void Pose(float time)
        {
            float size = settings.WingSize;
            for (int i = 0; i < _butterflies.Count; i++)
            {
                ButterflySpec b = _butterflies[i];
                Vector3 position = b.PositionAt(time);
                Vector3 forward = b.HeadingAt(time);
                Vector3 side = Vector3.Cross(Vector3.up, forward);

                // The wings beat about the body line: both tips lift and drop together.
                float flap = Mathf.Sin((time * settings.FlapRate * Mathf.PI * 2f) + (b.Phase * 3f)) * 0.8f;
                Vector3 lift = Vector3.up * (flap * size * 0.7f);
                Vector3 front = position + (forward * (size * 0.45f));
                Vector3 back = position - (forward * (size * 0.35f));

                int o = i * VerticesPerButterfly;
                Wing(o, front, back, position + (side * size) + lift);
                Wing(o + 6, front, back, position - (side * size) + lift);
            }

            _mesh.SetVertices(_vertices);
            _mesh.RecalculateBounds();
        }

        // One wing, two-sided (one triangle each way): from the body's front and back points to the wing tip.
        private void Wing(int o, Vector3 front, Vector3 back, Vector3 tip)
        {
            _vertices[o] = front;
            _vertices[o + 1] = back;
            _vertices[o + 2] = tip;
            _vertices[o + 3] = front;
            _vertices[o + 4] = tip;
            _vertices[o + 5] = back;
        }

        private void Clear()
        {
            _butterflies = new List<ButterflySpec>();
            foreach (var filter in GetComponents<MeshFilter>())
            {
                Destroy(filter);
            }

            foreach (var renderer in GetComponents<MeshRenderer>())
            {
                Destroy(renderer);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
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
}
