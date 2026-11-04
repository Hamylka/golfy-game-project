using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Builds low poly trees as flat-shaded triangles. All trees go in one mesh with the terrain's
    /// vertex color material, so a whole forest costs one draw call.
    /// </summary>
    public sealed class TreeBuilder
    {
        private const int TrunkSides = 6;

        // The ground mesh only follows the height field at its corners, so trunks start slightly below it.
        private const float TrunkSink = 0.2f;

        // How far a cypress's widest ring sits up the foliage. Low, so the outline tapers like a flame.
        private const float CypressBellyHeight = 0.28f;
        private const float CypressFootWidth = 0.7f;
        private const float CypressTrunkWidth = 0.3f;
        private const float CypressDarkFacetChance = 0.25f;

        // The trunk runs a little way into the foliage, so no gap shows between them.
        private const float CypressTrunkOverlap = 0.1f;

        // Blobs are a little flatter than spheres, so the crown reads as clusters rather than balls.
        private const float BlobSquash = 0.85f;

        // Outer blobs hang slightly below the crown's middle, and the last one caps it.
        private const float OuterBlobDrop = 0.3f;
        private const float TopBlobRise = 0.55f;
        private const float OuterBlobMinSize = 0.8f;
        private const float OuterBlobSizeRange = 0.25f;

        private readonly TreeSettings _settings;
        private readonly TerrainMeshData _mesh = new TerrainMeshData();
        private readonly Color32 _light;
        private readonly Color32 _mid;
        private readonly Color32 _dark;
        private readonly Color32 _trunk;

        private TreeBuilder(TreeSettings settings, PaletteSettings palette)
        {
            _settings = settings;
            _light = TerrainBuilder.ToVertexColor(palette.FoliageLight);
            _mid = TerrainBuilder.ToVertexColor(palette.FoliageMid);
            _dark = TerrainBuilder.ToVertexColor(palette.FoliageDark);
            _trunk = TerrainBuilder.ToVertexColor(palette.Trunk);
        }

        /// <summary>Builds every placed tree, standing on the course's ground.</summary>
        public static TerrainMeshData Build(TreeSettings settings, PaletteSettings palette, CourseShapeSettings shape)
        {
            var builder = new TreeBuilder(settings, palette);
            var fields = new CourseFields(shape);

            for (int i = 0; i < settings.Placements.Length; i++)
            {
                TreePlacement placement = settings.Placements[i];
                float height = fields.Height(placement.Position.x, placement.Position.y);
                var root = new Vector3(placement.Position.x, height, placement.Position.y);

                // One generator per tree, so moving or removing a tree leaves the others unchanged.
                var random = new System.Random(unchecked(settings.Seed + (i * 7919)));
                builder.AddTree(placement.Kind, root, placement.Scale, random);
            }

            return builder._mesh;
        }

        private void AddTree(TreeKind kind, Vector3 root, float scale, System.Random random)
        {
            if (kind == TreeKind.Cypress)
            {
                AddCypress(root, scale, random);
            }
            else
            {
                AddCauliflower(root, scale, random);
            }
        }

        private void AddCauliflower(Vector3 root, float scale, System.Random random)
        {
            float trunkHeight = _settings.TrunkHeight * scale;
            float radius = _settings.BlobRadius * scale;
            float spread = _settings.CrownSpread * scale;

            AddPrism(root, _settings.TrunkRadius * scale, trunkHeight + radius, RandomAngle(random), _trunk);

            Vector3 crown = root + (Vector3.up * (trunkHeight + radius));
            int outerCount = Mathf.Max(0, _settings.BlobCount - 1);
            float startAngle = RandomAngle(random);

            for (int i = 0; i < outerCount; i++)
            {
                float angle = startAngle + (i * Mathf.PI * 2f / outerCount);
                var offset = new Vector3(Mathf.Cos(angle) * spread, -OuterBlobDrop * radius, Mathf.Sin(angle) * spread);
                float size = radius * (OuterBlobMinSize + (Next(random) * OuterBlobSizeRange));
                AddBlob(crown + offset, size, i % 2 == 0 ? _mid : _light, random);
            }

            AddBlob(crown + (Vector3.up * (TopBlobRise * radius)), radius, _light, random);
        }

        private void AddCypress(Vector3 root, float scale, System.Random random)
        {
            int sides = _settings.CypressSides;
            float trunkHeight = _settings.CypressTrunkHeight * scale;
            float height = _settings.CypressHeight * scale;
            float radius = _settings.CypressRadius * scale;
            float startAngle = RandomAngle(random);

            AddPrism(root, radius * CypressTrunkWidth, trunkHeight + (height * CypressTrunkOverlap), startAngle, _trunk);

            Vector3 foot = root + (Vector3.up * trunkHeight);
            Vector3 belly = foot + (Vector3.up * (height * CypressBellyHeight));
            Vector3 tip = foot + (Vector3.up * height);

            for (int i = 0; i < sides; i++)
            {
                float a0 = startAngle + (i * Mathf.PI * 2f / sides);
                float a1 = startAngle + ((i + 1) * Mathf.PI * 2f / sides);
                Vector3 foot0 = foot + (Around(a0) * (radius * CypressFootWidth));
                Vector3 foot1 = foot + (Around(a1) * (radius * CypressFootWidth));
                Vector3 belly0 = belly + (Around(a0) * radius);
                Vector3 belly1 = belly + (Around(a1) * radius);
                Vector3 outward = Around((a0 + a1) * 0.5f);

                // Dark foliage, kept clear of the darkest shade on most facets, so no side of the tree goes near black.
                Color32 upper = Next(random) < CypressDarkFacetChance ? _dark : _mid;
                _mesh.Add(foot0, belly0, belly1, outward, _mid);
                _mesh.Add(foot0, belly1, foot1, outward, _mid);
                _mesh.Add(belly0, tip, belly1, outward, upper);
                _mesh.Add(foot0, foot1, foot, Vector3.down, _dark);
            }
        }

        private void AddBlob(Vector3 center, float radius, Color32 shade, System.Random random)
        {
            var corners = new List<Vector3>(Icosahedron.Corners.Length);
            foreach (Vector3 corner in Icosahedron.Corners)
            {
                corners.Add(corner.normalized);
            }

            var midpoints = new Dictionary<long, int>();
            var faces = new List<int>(Icosahedron.Faces.Length * 4);

            for (int i = 0; i < Icosahedron.Faces.Length; i += 3)
            {
                int a = Icosahedron.Faces[i];
                int b = Icosahedron.Faces[i + 1];
                int c = Icosahedron.Faces[i + 2];
                int ab = Midpoint(a, b, corners, midpoints);
                int bc = Midpoint(b, c, corners, midpoints);
                int ca = Midpoint(c, a, corners, midpoints);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // Corners move once each and are shared by their faces, so the blob stays closed.
            for (int i = 0; i < corners.Count; i++)
            {
                float length = radius * (1f + (((Next(random) * 2f) - 1f) * _settings.BlobJitter));
                Vector3 corner = corners[i] * length;
                corner.y *= BlobSquash;
                corners[i] = center + corner;
            }

            Color32 darker = SameColor(shade, _light) ? _mid : _dark;

            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = corners[faces[i]];
                Vector3 b = corners[faces[i + 1]];
                Vector3 c = corners[faces[i + 2]];
                Vector3 outward = ((a + b + c) / 3f) - center;
                Color32 color = Next(random) < _settings.DarkFacetChance ? darker : shade;
                _mesh.Add(a, b, c, outward, color);
            }
        }

        private static int Midpoint(int a, int b, List<Vector3> corners, Dictionary<long, int> midpoints)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpoints.TryGetValue(key, out int existing))
            {
                return existing;
            }

            corners.Add((corners[a] + corners[b]).normalized);
            midpoints[key] = corners.Count - 1;
            return corners.Count - 1;
        }

        private void AddPrism(Vector3 root, float radius, float height, float startAngle, Color32 color)
        {
            Vector3 top = root + (Vector3.up * height);
            root += Vector3.down * TrunkSink;

            for (int i = 0; i < TrunkSides; i++)
            {
                float a0 = startAngle + (i * Mathf.PI * 2f / TrunkSides);
                float a1 = startAngle + ((i + 1) * Mathf.PI * 2f / TrunkSides);
                Vector3 outward = Around((a0 + a1) * 0.5f);
                Vector3 low0 = root + (Around(a0) * radius);
                Vector3 low1 = root + (Around(a1) * radius);
                Vector3 high0 = top + (Around(a0) * radius);
                Vector3 high1 = top + (Around(a1) * radius);
                _mesh.Add(low0, high0, high1, outward, color);
                _mesh.Add(low0, high1, low1, outward, color);
            }
        }

        private static Vector3 Around(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        private static float RandomAngle(System.Random random) => Next(random) * Mathf.PI * 2f;

        private static float Next(System.Random random) => (float)random.NextDouble();

        private static bool SameColor(Color32 x, Color32 y) => x.r == y.r && x.g == y.g && x.b == y.b;
    }
}
