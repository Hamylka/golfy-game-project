using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// The hole and its flag. The hole is a dark disc on the ground rather than a cut in the terrain: a dropped
    /// ball sinks below the ground, where the terrain hides it. This object sits at the origin of the course
    /// container, so the disc's vertices are in the course's local space.
    /// </summary>
    public sealed class HoleView : MonoBehaviour
    {
        [SerializeField] private PlayPieceSettings settings;
        [SerializeField] private PaletteSettings palette;
        [Tooltip("The vertex color material with alpha, as for the ball's shadow.")]
        [SerializeField] private Material holeMaterial;
        [Tooltip("The terrain's vertex color material, so the flag is lit and shadowed like everything else.")]
        [SerializeField] private Material flagMaterial;

        private Mesh _holeMesh;
        private Mesh _flagMesh;

        public void Build(BallGround ground)
        {
            Vector2 hole = ground.Hole;

            var disc = new GroundDisc(settings.HoleRadius, settings.DiscRings, settings.DiscSegments, ground.DrawnHeight, ground.IsOnIsland);
            disc.Place(hole.x, hole.y, settings.HoleLift, TerrainBuilder.ToVertexColor(palette.Hole));
            _holeMesh = new Mesh { name = "Hole", vertices = disc.Positions, colors32 = disc.Colors, triangles = disc.Triangles };
            Add("Hole", _holeMesh, holeMaterial).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _flagMesh = PlayPieceBuilder.BuildFlag(settings, palette).ToMesh("Flag");
            MeshRenderer flag = Add("Flag", _flagMesh, flagMaterial);
            flag.transform.localPosition = new Vector3(hole.x, ground.DrawnHeight(hole.x, hole.y), hole.y);
        }

        private MeshRenderer Add(string childName, Mesh mesh, Material material)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void OnDestroy()
        {
            Destroy(_holeMesh);
            Destroy(_flagMesh);
        }
    }
}
