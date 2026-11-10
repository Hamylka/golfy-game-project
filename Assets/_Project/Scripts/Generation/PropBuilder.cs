using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The prop kinds of M4 PR 4.1 (olive, cypress, bush, rocks, dry-stone wall), built from
    /// <see cref="PropMeshKit"/> with the proportions of <see cref="PropKitSettings"/> and the colours of
    /// <see cref="PaletteSettings"/>. Each <c>Add*</c> takes the spot on the ground and a seeded
    /// <see cref="System.Random"/> (the same seed makes the same prop on every device; callers give each prop
    /// its own generator so removing one leaves the others unchanged), writes into one shared mesh, and returns
    /// the prop's collision shapes for the ball simulation of PR 4.2.
    /// </summary>
    public sealed class PropBuilder
    {
        // The ground mesh only follows the height field at its corners, so trunks and rocks start slightly below it.
        private const float TrunkSink = 0.2f;
        private const int TrunkSides = 6;

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

        // A crown spans from about its outer blobs' bottom to the top blob's cap: 2.4 blob radii.
        private const float CrownSwaySpan = 2.4f;

        private const float BushSquash = 0.8f;

        // A bush's top is about 1.4 radii up.
        private const float BushSwaySpan = 1.5f;
        private const float BushDarkFacetChance = 0.2f;
        private const float BushSatelliteReach = 0.8f;
        private const float BushSatelliteDarkChance = 0.15f;
        private const float RoundRockSquash = 0.75f;
        private const float RockDarkFacetChance = 0.3f;
        private const float RoundRockMidFacetChance = 0.25f;
        private const float WallSink = 0.1f;

        private readonly PropKitSettings _settings;
        private readonly PropMeshKit _kit;
        private readonly Color32 _foliageLight;
        private readonly Color32 _foliageMid;
        private readonly Color32 _foliageDark;
        private readonly Color32 _oliveLight;
        private readonly Color32 _trunk;
        private readonly Color32 _stoneLight;
        private readonly Color32 _stoneMid;
        private readonly Color32 _stoneDark;

        public PropBuilder(PropKitSettings settings, PaletteSettings palette, TerrainMeshData mesh = null)
        {
            _settings = settings;
            _kit = new PropMeshKit(mesh ?? new TerrainMeshData());
            _foliageLight = TerrainBuilder.ToVertexColor(palette.FoliageLight);
            _foliageMid = TerrainBuilder.ToVertexColor(palette.FoliageMid);
            _foliageDark = TerrainBuilder.ToVertexColor(palette.FoliageDark);
            _oliveLight = TerrainBuilder.ToVertexColor(palette.OliveLight);
            _trunk = TerrainBuilder.ToVertexColor(palette.Trunk);
            _stoneLight = TerrainBuilder.ToVertexColor(palette.StoneLight);
            _stoneMid = TerrainBuilder.ToVertexColor(palette.StoneMid);
            _stoneDark = TerrainBuilder.ToVertexColor(palette.StoneDark);
        }

        /// <summary>Everything built so far, as one mesh for the land material.</summary>
        public TerrainMeshData Mesh => _kit.Mesh;

        /// <summary>
        /// Builds the hand-placed trees of a sandbox course, standing on its ground. Each tree has its own
        /// generator, so moving or removing a tree leaves the others unchanged. Their collision shapes go
        /// into <paramref name="shapes"/>, when given, for the ball.
        /// </summary>
        public static TerrainMeshData BuildTrees(TreeSettings trees, PropKitSettings kit, PaletteSettings palette, CourseLayout layout,
            System.Collections.Generic.List<PropShape> shapes = null)
        {
            var builder = new PropBuilder(kit, palette);
            var fields = new CourseFields(layout);

            for (int i = 0; i < trees.Placements.Length; i++)
            {
                TreePlacement placement = trees.Placements[i];
                float height = fields.Height(placement.Position.x, placement.Position.y);
                var root = new Vector3(placement.Position.x, height, placement.Position.y);
                var random = new System.Random(unchecked(trees.Seed + (i * 7919)));

                PropShape[] tree = placement.Kind == TreeKind.Cypress
                    ? builder.AddCypress(root, placement.Scale, random)
                    : builder.AddOlive(root, placement.Scale, random);
                shapes?.AddRange(tree);
            }

            return builder.Mesh;
        }

        /// <summary>Olive tree: a short trunk and a cauliflower crown of jittered blobs, silvery light shade on top.</summary>
        public PropShape[] AddOlive(Vector3 root, float scale, System.Random random)
        {
            float trunkHeight = _settings.TrunkHeight * scale;
            float radius = _settings.BlobRadius * scale;
            float spread = _settings.CrownSpread * scale;
            float trunkTop = trunkHeight + radius;

            _kit.AddPrism(root, _settings.TrunkRadius * scale, trunkTop, TrunkSides, PropMeshKit.RandomAngle(random), _trunk, TrunkSink);

            int crownStart = _kit.Mesh.Positions.Count;
            Vector3 crown = root + (Vector3.up * trunkTop);
            int outerCount = Mathf.Max(0, _settings.BlobCount - 1);
            float startAngle = PropMeshKit.RandomAngle(random);
            _kit.SetSway(root.y + trunkHeight, radius * CrownSwaySpan);

            for (int i = 0; i < outerCount; i++)
            {
                float angle = startAngle + (i * Mathf.PI * 2f / outerCount);
                var offset = new Vector3(Mathf.Cos(angle) * spread, -OuterBlobDrop * radius, Mathf.Sin(angle) * spread);
                float size = radius * (OuterBlobMinSize + (PropMeshKit.Next(random) * OuterBlobSizeRange));
                bool mid = i % 2 == 0;
                _kit.AddBlob(crown + offset, size, BlobSquash, _settings.BlobJitter, 1,
                    mid ? _foliageMid : _oliveLight, mid ? _foliageDark : _foliageMid, _settings.DarkFacetChance, random, null);
            }

            _kit.AddBlob(crown + (Vector3.up * (TopBlobRise * radius)), radius, BlobSquash, _settings.BlobJitter, 1,
                _oliveLight, _foliageMid, _settings.DarkFacetChance, random, null);
            _kit.ClearSway();

            return new[]
            {
                PropShape.Cylinder(root, _settings.TrunkRadius * scale, trunkTop, PropMaterial.Wood),
                FitEllipsoid(crownStart, PropMaterial.Foliage),
            };
        }

        /// <summary>Cypress: a thin flame of 6 to 8 sides on a short trunk.</summary>
        public PropShape[] AddCypress(Vector3 root, float scale, System.Random random)
        {
            int sides = _settings.CypressSides;
            float trunkHeight = _settings.CypressTrunkHeight * scale;
            float height = _settings.CypressHeight * scale;
            float radius = _settings.CypressRadius * scale;
            float startAngle = PropMeshKit.RandomAngle(random);
            float trunkTop = trunkHeight + (height * CypressTrunkOverlap);

            _kit.AddPrism(root, radius * CypressTrunkWidth, trunkTop, TrunkSides, startAngle, _trunk, TrunkSink);

            Vector3 foot = root + (Vector3.up * trunkHeight);
            Vector3 belly = foot + (Vector3.up * (height * CypressBellyHeight));
            Vector3 tip = foot + (Vector3.up * height);
            _kit.SetSway(foot.y, height);

            for (int i = 0; i < sides; i++)
            {
                float a0 = startAngle + (i * Mathf.PI * 2f / sides);
                float a1 = startAngle + ((i + 1) * Mathf.PI * 2f / sides);
                Vector3 foot0 = foot + (PropMeshKit.Around(a0) * (radius * CypressFootWidth));
                Vector3 foot1 = foot + (PropMeshKit.Around(a1) * (radius * CypressFootWidth));
                Vector3 belly0 = belly + (PropMeshKit.Around(a0) * radius);
                Vector3 belly1 = belly + (PropMeshKit.Around(a1) * radius);
                Vector3 outward = PropMeshKit.Around((a0 + a1) * 0.5f);

                // Dark foliage, kept clear of the darkest shade on most facets, so no side of the tree goes near black.
                Color32 upper = PropMeshKit.Next(random) < CypressDarkFacetChance ? _foliageDark : _foliageMid;
                _kit.AddTriangle(foot0, belly0, belly1, outward, _foliageMid);
                _kit.AddTriangle(foot0, belly1, foot1, outward, _foliageMid);
                _kit.AddTriangle(belly0, tip, belly1, outward, upper);
                _kit.AddTriangle(foot0, foot1, foot, Vector3.down, _foliageDark);
            }

            _kit.ClearSway();

            return new[]
            {
                PropShape.Cylinder(root, radius * CypressTrunkWidth, trunkTop, PropMaterial.Wood),
                PropShape.Cylinder(foot, radius, height, PropMaterial.Foliage),
            };
        }

        /// <summary>Round bush: one main blob with two small ones at its sides, low to the ground.</summary>
        public PropShape[] AddBush(Vector3 root, float scale, System.Random random)
        {
            float radius = _settings.BushRadius * scale;
            float satellite = radius * _settings.BushSatelliteSize;
            float startAngle = PropMeshKit.RandomAngle(random);
            int start = _kit.Mesh.Positions.Count;
            _kit.SetSway(root.y, radius * BushSwaySpan);

            _kit.AddBlob(root + (Vector3.up * (radius * BushSquash * 0.7f)), radius, BushSquash, _settings.BushJitter, 1,
                _foliageLight, _foliageMid, BushDarkFacetChance, random, null);

            for (int i = 0; i < 2; i++)
            {
                float angle = startAngle + (i * 2.4f);
                Vector3 offset = PropMeshKit.Around(angle) * (radius * BushSatelliteReach);
                _kit.AddBlob(root + offset + (Vector3.up * (satellite * 0.5f)), satellite, BushSquash, _settings.BushJitter, 0,
                    _foliageMid, _foliageDark, BushSatelliteDarkChance, random, null);
            }

            _kit.ClearSway();
            return new[] { FitEllipsoid(start, PropMaterial.Foliage) };
        }

        /// <summary>
        /// Faceted rock, <paramref name="size"/> metres across at the widest (0.3 to 1.2 in the kit's use), with
        /// light tops and a few dark facets. 20 facets, so it reads as cut stone.
        /// </summary>
        public PropShape[] AddRock(Vector3 root, float size, System.Random random)
        {
            float radius = size * 0.5f / (1f + _settings.RockJitter);
            float squash = _settings.RockHeightRatio;
            int start = _kit.Mesh.Positions.Count;
            _kit.AddBlob(root + (Vector3.up * (radius * squash * 0.5f)), radius, squash, _settings.RockJitter, 0,
                _stoneMid, _stoneDark, RockDarkFacetChance, random, _stoneLight);
            return new[] { FitEllipsoid(start, PropMaterial.Stone) };
        }

        /// <summary>Round white rock for a pond's rim (ART_DIRECTION §9): 80 facets, barely jittered, mostly the lightest stone.</summary>
        public PropShape[] AddRoundRock(Vector3 root, float size, System.Random random)
        {
            float radius = size * 0.5f / (1f + _settings.RoundRockJitter);
            int start = _kit.Mesh.Positions.Count;
            _kit.AddBlob(root + (Vector3.up * (radius * RoundRockSquash * 0.5f)), radius, RoundRockSquash, _settings.RoundRockJitter, 1,
                _stoneLight, _stoneMid, RoundRockMidFacetChance, random, null);
            return new[] { FitEllipsoid(start, PropMaterial.Stone) };
        }

        /// <summary>
        /// Low dry-stone wall between two ground points, one box collider along its whole length. The wall
        /// follows a straight line between the points' heights, so pass short pieces on uneven ground.
        /// </summary>
        public PropShape[] AddWall(Vector3 start, Vector3 end, System.Random random)
        {
            int first = _kit.Mesh.Positions.Count;
            _kit.AddBlockWall(start, end, _settings.WallCourses, _settings.WallBlockHeight, _settings.WallBlockLength,
                _settings.WallBlockLengthVariation, _settings.WallThickness, _settings.WallJitter, WallSink,
                _stoneLight, _stoneMid, _stoneDark, random);

            if (_kit.Mesh.Positions.Count == first)
            {
                return new PropShape[0];
            }

            return new[] { FitBox(first, Mathf.Atan2(end.z - start.z, end.x - start.x), PropMaterial.Stone) };
        }

        /// <summary>
        /// The smallest ellipsoid with the mesh's bounding box's proportions that holds every vertex built
        /// since <paramref name="firstVertex"/>: it encloses the prop by construction, and stays near it.
        /// </summary>
        private PropShape FitEllipsoid(int firstVertex, PropMaterial material)
        {
            var points = _kit.Mesh.Positions;
            Bounds bounds = BoundsFrom(firstVertex);
            Vector3 ratio = Vector3.Max(bounds.extents, new Vector3(0.001f, 0.001f, 0.001f));
            float scale = 1f;

            for (int i = firstVertex; i < points.Count; i++)
            {
                Vector3 d = points[i] - bounds.center;
                scale = Mathf.Max(scale, new Vector3(d.x / ratio.x, d.y / ratio.y, d.z / ratio.z).magnitude);
            }

            return PropShape.Ellipsoid(bounds.center, ratio * scale, material);
        }

        /// <summary>The tightest box along the wall's own axes that holds every vertex built since <paramref name="firstVertex"/>.</summary>
        private PropShape FitBox(int firstVertex, float yaw, PropMaterial material)
        {
            var points = _kit.Mesh.Positions;
            Vector3 right = PropMeshKit.Around(yaw);
            Vector3 forward = new Vector3(-right.z, 0f, right.x);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = firstVertex; i < points.Count; i++)
            {
                var local = new Vector3(Vector3.Dot(points[i], right), points[i].y, Vector3.Dot(points[i], forward));
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            Vector3 middle = (min + max) * 0.5f;
            Vector3 center = (right * middle.x) + (Vector3.up * middle.y) + (forward * middle.z);
            return PropShape.Box(center, (max - min) * 0.5f, yaw, material);
        }

        private Bounds BoundsFrom(int firstVertex)
        {
            var points = _kit.Mesh.Positions;
            var bounds = new Bounds(points[firstVertex], Vector3.zero);
            for (int i = firstVertex + 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return bounds;
        }
    }
}
