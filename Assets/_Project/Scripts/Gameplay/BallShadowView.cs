using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// The ball's shadow: always straight under the ball, on whatever is drawn there. It's what makes the arc
    /// readable, so it stays a separate blob, and the ball itself casts no shadow, which would make two. This
    /// object sits at the origin of the course container, so the mesh's vertices are in the course's local space.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BallShadowView : MonoBehaviour
    {
        [SerializeField] private PlayPieceSettings settings;
        [SerializeField] private PaletteSettings palette;

        private GroundDisc _disc;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Color32 _color;

        public void Build(BallGround ground)
        {
            _disc = new GroundDisc(settings.ShadowRadius, settings.DiscRings, settings.DiscSegments, ground.DrawnHeight, ground.IsOnIsland);
            _color = TerrainBuilder.ToVertexColor(palette.BallShadow);
            _renderer = GetComponent<MeshRenderer>();

            _mesh = new Mesh { name = "Ball Shadow" };
            _mesh.MarkDynamic();
            _mesh.vertices = _disc.Positions;
            _mesh.colors32 = _disc.Colors;
            _mesh.triangles = _disc.Triangles;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        /// <summary>Puts the shadow under this point, or hides it.</summary>
        public void Show(bool visible, Vector3 ball)
        {
            _renderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            _disc.Place(ball.x, ball.z, settings.ShadowLift, _color);
            _mesh.SetVertices(_disc.Positions);
            _mesh.SetColors(_disc.Colors);
            _mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }
    }
}
