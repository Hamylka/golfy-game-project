using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Sandbox_Props: every prop kind of the kit in a row, a few seeds each, on a flat patch of ground, under
    /// the game's own material, sun and camera angle. Kinds run across the screen (world +Z toward the left),
    /// seeds run up it (+X). A review tool for the director: nothing in <c>Main</c> reads it.
    /// </summary>
    public sealed class PropShowroom : MonoBehaviour
    {
        private const int GroundSides = 28;
        private const float GroundRadius = 15f;
        private const float KindSpacing = 3.2f;
        private const float SeedSpacing = 3.6f;
        private const float WallLength = 2.6f;

        [SerializeField] private PaletteSettings palette;
        [SerializeField] private PropKitSettings propKit;
        [SerializeField] private Material landMaterial;
        [SerializeField, Range(1, 5)] private int seedsPerKind = 3;
        [SerializeField] private int seed = 100;

        private Mesh _mesh;

        private void Start()
        {
            Build();
        }

        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>Builds the ground and the props as one mesh on the land material, like a course.</summary>
        [ContextMenu("Rebuild")]
        public void Build()
        {
            Clear();
            var data = new TerrainMeshData();
            AddGround(data);

            var builder = new PropBuilder(propKit, palette, data);
            string[] kinds = { "olive", "cypress", "bush", "rock", "round rock", "wall" };
            float zStart = (kinds.Length - 1) * KindSpacing * 0.5f;

            for (int k = 0; k < kinds.Length; k++)
            {
                for (int s = 0; s < seedsPerKind; s++)
                {
                    float x = (s - ((seedsPerKind - 1) * 0.5f)) * SeedSpacing;
                    var root = new Vector3(x, 0f, zStart - (k * KindSpacing));

                    // One generator per prop: same seed, same prop, whatever else is in the row.
                    var random = new System.Random(unchecked(seed + (k * 131) + (s * 7919)));
                    float size = 0.8f + (s * 0.25f);
                    switch (k)
                    {
                        case 0: builder.AddOlive(root, size, random); break;
                        case 1: builder.AddCypress(root, size, random); break;
                        case 2: builder.AddBush(root, size, random); break;
                        case 3: builder.AddRock(root, 0.4f + (s * 0.4f), random); break;
                        case 4: builder.AddRoundRock(root, 0.4f + (s * 0.4f), random); break;
                        default:
                            // A wall lies across the screen, so its side faces the camera.
                            builder.AddWall(root + (Vector3.back * (-WallLength * 0.5f)), root + (Vector3.back * (WallLength * 0.5f)), random);
                            break;
                    }
                }
            }

            _mesh = data.ToMesh("Props showroom");
            var child = new GameObject("Showroom");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = _mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = landMaterial;
        }

        private void AddGround(TerrainMeshData data)
        {
            Color32 color = TerrainBuilder.ToVertexColor(palette.Rough);
            var middle = Vector3.down * 0.0f;
            for (int i = 0; i < GroundSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / GroundSides;
                float a1 = (i + 1) * Mathf.PI * 2f / GroundSides;
                data.Add(middle, PropMeshKit.Around(a0) * GroundRadius, PropMeshKit.Around(a1) * GroundRadius, Vector3.up, color);
            }
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }

            if (_mesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_mesh);
                }
                else
                {
                    DestroyImmediate(_mesh);
                }

                _mesh = null;
            }
        }
    }
}
