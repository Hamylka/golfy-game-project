using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Draws a course's birds (M4 PR 4.6): every bird in one mesh, rewritten on the CPU each frame, so the whole flock costs one draw call
    /// (and its shadow pass) however many perch. It lives under the Course container at the origin, so the birds are in course-local space and
    /// float with the island. Visual only: it reads the ball's position through <see cref="Advance"/>, which the level bootstrap calls, and
    /// nothing reads the birds. The mesh is on the land material, so the birds are lit and faceted like the props. Lives in Generation, next to
    /// <see cref="TerrainView"/> and <see cref="CloudView"/>, because Rendering cannot see the palette or the kit.
    /// </summary>
    public sealed class BirdsView : MonoBehaviour
    {
        [SerializeField] private BirdSettings settings;
        [SerializeField] private PaletteSettings palette;
        [SerializeField] private Material material;

        private readonly List<BirdFlight> _birds = new List<BirdFlight>();
        private BirdMeshKit _kit;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Vector3[] _vertices;

        public IReadOnlyList<BirdFlight> Birds => _birds;

        /// <summary>Perches the flock of this course and builds its mesh.</summary>
        public void Build(CourseLayout layout, int seed, int par, int variant)
        {
            Clear();
            foreach (BirdPerch perch in BirdPerches.Place(layout, seed, par, variant, settings))
            {
                _birds.Add(new BirdFlight(perch, settings));
            }

            if (_birds.Count == 0)
            {
                return;
            }

            _kit = BirdMeshKit.Build(palette);
            int per = _kit.VertexCount;
            _vertices = new Vector3[per * _birds.Count];
            var colors = new Color32[_vertices.Length];
            var indices = new int[_vertices.Length];
            for (int b = 0; b < _birds.Count; b++)
            {
                for (int i = 0; i < per; i++)
                {
                    colors[(b * per) + i] = _kit.Colors[i];
                }
            }

            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            _mesh = new Mesh { name = "Birds" };
            _mesh.MarkDynamic();
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            Pose(0f);
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(indices, 0);

            // Birds fly out over the whole island and beyond: never culled by a bound that has not followed them.
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(400f, 200f, 400f));

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            Refresh();
        }

        /// <summary>
        /// Moves every bird on by <paramref name="dt"/> and redraws. <paramref name="ball"/> is the ball's course-local position and
        /// <paramref name="moving"/> whether it is flying or rolling.
        /// </summary>
        public void Advance(float dt, Vector3 ball, bool moving)
        {
            if (_mesh == null)
            {
                return;
            }

            foreach (BirdFlight bird in _birds)
            {
                bird.Update(dt, ball, moving);
            }

            Refresh();
        }

        private void Refresh()
        {
            bool any = Pose(settings.BirdScale);
            _mesh.SetVertices(_vertices);
            _mesh.RecalculateNormals();

            // Perched birds count as visible; the renderer only goes off when every bird is away and gone.
            _renderer.enabled = any;
        }

        private bool Pose(float scale)
        {
            bool any = false;
            int per = _kit.VertexCount;
            for (int b = 0; b < _birds.Count; b++)
            {
                BirdPose pose = _birds[b].Pose;
                any |= pose.Visible;
                _kit.Apply(pose, scale > 0f ? scale : 1f, _vertices, b * per);
            }

            return any;
        }

        private void Clear()
        {
            _birds.Clear();
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }

            foreach (var filter in GetComponents<MeshFilter>())
            {
                Destroy(filter);
            }

            foreach (var renderer in GetComponents<MeshRenderer>())
            {
                Destroy(renderer);
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
