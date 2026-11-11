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
        private readonly Color32 _woodLight;
        private readonly Color32 _woodDark;
        private readonly Color32 _tileLight;
        private readonly Color32 _tileDark;
        private readonly Color32 _slateLight;
        private readonly Color32 _slateMid;
        private readonly Color32 _slateDark;
        private readonly Color32 _metal;
        private readonly Color32 _metalDark;
        private readonly Color32 _red;
        private readonly Color32 _blue;
        private readonly Color32 _offWhite;

        public PropBuilder(PropKitSettings settings, PaletteSettings palette, TerrainMeshData mesh = null)
        {
            _settings = settings;
            _kit = new PropMeshKit(mesh ?? new TerrainMeshData());
            _foliageLight = palette != null ? TerrainBuilder.ToVertexColor(palette.FoliageLight) : default;
            _foliageMid = palette != null ? TerrainBuilder.ToVertexColor(palette.FoliageMid) : default;
            _foliageDark = palette != null ? TerrainBuilder.ToVertexColor(palette.FoliageDark) : default;
            _oliveLight = palette != null ? TerrainBuilder.ToVertexColor(palette.OliveLight) : default;
            _trunk = palette != null ? TerrainBuilder.ToVertexColor(palette.Trunk) : default;
            _stoneLight = palette != null ? TerrainBuilder.ToVertexColor(palette.StoneLight) : default;
            _stoneMid = palette != null ? TerrainBuilder.ToVertexColor(palette.StoneMid) : default;
            _stoneDark = palette != null ? TerrainBuilder.ToVertexColor(palette.StoneDark) : default;
            _woodLight = palette != null ? TerrainBuilder.ToVertexColor(palette.WoodLight) : default;
            _woodDark = palette != null ? TerrainBuilder.ToVertexColor(palette.WoodDark) : default;
            _tileLight = palette != null ? TerrainBuilder.ToVertexColor(palette.TileLight) : default;
            _tileDark = palette != null ? TerrainBuilder.ToVertexColor(palette.TileDark) : default;
            _slateLight = palette != null ? TerrainBuilder.ToVertexColor(palette.SlateLight) : default;
            _slateMid = palette != null ? TerrainBuilder.ToVertexColor(palette.SlateMid) : default;
            _slateDark = palette != null ? TerrainBuilder.ToVertexColor(palette.SlateDark) : default;
            _metal = palette != null ? TerrainBuilder.ToVertexColor(palette.Metal) : default;
            _metalDark = palette != null ? TerrainBuilder.ToVertexColor(palette.MetalDark) : default;
            _red = palette != null ? TerrainBuilder.ToVertexColor(palette.TraceRed) : default;
            _blue = palette != null ? TerrainBuilder.ToVertexColor(palette.TraceBlue) : default;
            _offWhite = palette != null ? TerrainBuilder.ToVertexColor(palette.TraceOffWhite) : default;
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

        /// <summary>
        /// Builds a generated course's props (<see cref="CourseLayout.Props"/>), each from its own seed, so the mesh is the one
        /// the placement fitted the props' shapes to. Their shapes go into <paramref name="shapes"/>, when given, in the order of
        /// <see cref="CourseLayout.PropShapes"/>.
        /// </summary>
        public static TerrainMeshData BuildCourseProps(System.Collections.Generic.IReadOnlyList<CourseProp> props, PropKitSettings kit, PaletteSettings palette,
            System.Collections.Generic.List<PropShape> shapes = null)
        {
            var builder = new PropBuilder(kit, palette);
            foreach (CourseProp prop in props)
            {
                PropShape[] built = builder.Add(prop.Kind, prop.Root, prop.End, prop.Scale, prop.Seed, prop.YawDegrees);
                shapes?.AddRange(built);
            }

            return builder.Mesh;
        }

        /// <summary>
        /// One prop of any kind from its seed: <paramref name="end"/> is a wall's or a bridge's other end, <paramref name="scale"/> a
        /// rock's width, <paramref name="yawDegrees"/> the way a built prop or trace faces.
        /// </summary>
        public PropShape[] Add(PropKind kind, Vector3 root, Vector3 end, float scale, int seed, float yawDegrees = 0f)
        {
            var random = new System.Random(seed);
            float yaw = yawDegrees * Mathf.Deg2Rad;
            switch (kind)
            {
                case PropKind.Shed: return AddShed(root, yaw, scale, random);
                case PropKind.Bench: return AddBench(root, yaw, scale, random);
                case PropKind.Bridge: return AddBridge(root, end, scale, random);
                case PropKind.Rake: return AddRake(root, yaw, random);
                case PropKind.Bucket: return AddBucket(root, scale, random);
                case PropKind.Bicycle: return AddBicycle(root, yaw, random);
                case PropKind.Laundry: return AddLaundry(root, yaw, random);
                case PropKind.Chapel: return AddChapel(root, yaw, scale, random);
                case PropKind.Lighthouse: return AddLighthouse(root, yaw, scale, random);
                case PropKind.Olive: return AddOlive(root, scale, random);
                case PropKind.Cypress: return AddCypress(root, scale, random);
                case PropKind.Bush: return AddBush(root, scale, random);
                case PropKind.Rock: return AddRock(root, scale, random);
                case PropKind.RoundRock: return AddRoundRock(root, scale, random);
                default: return AddWall(root, end, random);
            }
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
            _kit.SetSway(root.y + trunkHeight, radius * CrownSwaySpan, AmbientHash.Unit(root.x, root.z));

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
            _kit.SetSway(foot.y, height, AmbientHash.Unit(root.x, root.z));

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
            _kit.SetSway(root.y, radius * BushSwaySpan, AmbientHash.Unit(root.x, root.z));

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

        // ---- Built props, traces and landmarks (M4 PR 4.4) ----

        // Walls and boxes start slightly below the ground line, like trunks: the ground mesh only follows the height field at its corners.
        private const float BuiltSink = 0.1f;
        private const float BoxJitter = 0.02f;

        private static Vector3 Local(Vector3 root, float yaw, float x, float y, float z)
        {
            Vector3 right = PropMeshKit.Around(yaw);
            Vector3 forward = new Vector3(-right.z, 0f, right.x);
            return root + (right * x) + (Vector3.up * y) + (forward * z);
        }

        /// <summary>A box standing on the ground at local (x, z), <paramref name="height"/> tall, sunk a little into it.</summary>
        private void AddStanding(Vector3 root, float yaw, float x, float z, Vector3 halfWidths, float height, Color32 color, System.Random random)
        {
            _kit.AddBox(Local(root, yaw, x, (height - BuiltSink) * 0.5f, z), new Vector3(halfWidths.x, (height + BuiltSink) * 0.5f, halfWidths.z), yaw, BoxJitter, random, color, true);
        }

        /// <summary>
        /// Tile-roofed shed: whitewashed stone walls, a wooden door on the long side that faces the prop's +z, a two-tone tile roof.
        /// One box collider holds walls and roof.
        /// </summary>
        public PropShape[] AddShed(Vector3 root, float yaw, float scale, System.Random random)
        {
            float length = _settings.ShedLength * scale;
            float width = _settings.ShedWidth * scale;
            float wall = _settings.ShedWallHeight * scale;
            float overhang = _settings.ShedOverhang * scale;
            int first = _kit.Mesh.Positions.Count;

            AddStanding(root, yaw, 0f, 0f, new Vector3(length * 0.5f, 0f, width * 0.5f), wall, _stoneLight, random);
            _kit.AddGableRoof(Local(root, yaw, 0f, wall, 0f), length + (2f * overhang), width + (2f * overhang), _settings.ShedRoofRise * scale, yaw, _tileLight, _tileDark);
            AddStanding(root, yaw, 0f, (width * 0.5f) + 0.01f, new Vector3(0.28f * scale, 0f, 0.02f), 0.95f * scale, _trunk, null);

            return new[] { FitBox(first, yaw, PropMaterial.Stone) };
        }

        /// <summary>Wooden bench: seat, back and two legs, its seat facing the prop's +z side.</summary>
        public PropShape[] AddBench(Vector3 root, float yaw, float scale, System.Random random)
        {
            float length = _settings.BenchLength * scale;
            int first = _kit.Mesh.Positions.Count;

            _kit.AddBox(Local(root, yaw, 0f, 0.45f * scale, 0f), new Vector3(length * 0.5f, 0.035f * scale, 0.22f * scale), yaw, 0.008f, random, _woodLight, false);
            _kit.AddBox(Local(root, yaw, 0f, 0.72f * scale, -0.2f * scale), new Vector3(length * 0.5f, 0.15f * scale, 0.025f * scale), yaw, 0.008f, random, _woodLight, false);
            for (int side = -1; side <= 1; side += 2)
            {
                AddStanding(root, yaw, side * ((length * 0.5f) - (0.12f * scale)), 0f, new Vector3(0.04f * scale, 0f, 0.2f * scale), 0.42f * scale, _trunk, random);
            }

            return new[] { FitBox(first, yaw, PropMaterial.Wood) };
        }

        /// <summary>
        /// Small stone bridge between two bank points: a row of stone blocks from well below the lower bank up to a deck that rises
        /// to a hump in the middle, with a parapet on each side. One box collider holds the whole span (arch to deck), so the ball
        /// never rolls on the deck and never crosses the pond on it.
        /// </summary>
        public PropShape[] AddBridge(Vector3 start, Vector3 end, float scale, System.Random random)
        {
            var flat = new Vector3(end.x - start.x, 0f, end.z - start.z);
            float total = flat.magnitude;
            if (total < 0.01f)
            {
                return new PropShape[0];
            }

            Vector3 along = flat / total;
            Vector3 side = new Vector3(-along.z, 0f, along.x);
            float yaw = Mathf.Atan2(along.z, along.x);
            float width = _settings.BridgeWidth * scale;
            int blocks = Mathf.Max(3, Mathf.RoundToInt(total / _settings.BridgeBlockLength));
            float bottom = Mathf.Min(start.y, end.y) - _settings.BridgeDepth;
            int first = _kit.Mesh.Positions.Count;

            for (int i = 0; i < blocks; i++)
            {
                float t = (i + 0.5f) / blocks;
                float top = Mathf.Lerp(start.y, end.y, t) + _settings.BridgeDeck + (_settings.BridgeRise * Mathf.Sin(Mathf.PI * t));
                Vector3 middle = start + (along * (total * t));
                var center = new Vector3(middle.x, (top + bottom) * 0.5f, middle.z);
                float half = total / blocks * 0.5f;
                _kit.AddBox(center, new Vector3(half, (top - bottom) * 0.5f, width * 0.5f), yaw, BoxJitter, random, PickStone(random), true);

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 parapet = center + (side * (s * ((width * 0.5f) - 0.09f))) + (Vector3.up * (((top - bottom) * 0.5f) + 0.13f));
                    _kit.AddBox(parapet, new Vector3(half, 0.13f, 0.09f), yaw, BoxJitter, random, PickStone(random), true);
                }
            }

            return new[] { FitBox(first, yaw, PropMaterial.Stone) };
        }

        private Color32 PickStone(System.Random random)
        {
            float pick = PropMeshKit.Next(random);
            return pick < 0.35f ? _stoneLight : (pick < 0.8f ? _stoneMid : _stoneDark);
        }

        /// <summary>A rake lying flat in a bunker, handle along <paramref name="yaw"/>. It is flat on the ground, so it has no collider (director Q11 A).</summary>
        public PropShape[] AddRake(Vector3 root, float yaw, System.Random random)
        {
            const float Lift = 0.05f;
            _kit.AddBox(Local(root, yaw, 0f, Lift, 0f), new Vector3(0.65f, 0.02f, 0.022f), yaw, 0.004f, random, _woodLight, false);
            _kit.AddBox(Local(root, yaw, 0.65f, Lift, 0f), new Vector3(0.045f, 0.022f, 0.27f), yaw, 0.004f, random, _metal, false);
            return new PropShape[0];
        }

        /// <summary>A forgotten bucket of balls: a small red pail with three white balls in it.</summary>
        public PropShape[] AddBucket(Vector3 root, float scale, System.Random random)
        {
            float radius = 0.16f * scale;
            float height = 0.28f * scale;
            // A pail is a closed vessel: a tapered body, and a dark lid ring at the rim so no inside face is ever seen through the open top.
            float start = PropMeshKit.RandomAngle(random);
            _kit.AddFrustum(root, radius * 0.82f, radius, height, 8, start, _red, BuiltSink);
            Vector3 rim = root + (Vector3.up * height);
            for (int i = 0; i < 8; i++)
            {
                float a0 = start + (i * Mathf.PI * 2f / 8);
                float a1 = start + ((i + 1) * Mathf.PI * 2f / 8);
                _kit.AddTriangle(rim + (PropMeshKit.Around(a0) * radius), rim + (PropMeshKit.Around(a1) * radius), rim, Vector3.up, _metalDark);
            }


            for (int i = 0; i < 3; i++)
            {
                float angle = PropMeshKit.RandomAngle(random);
                Vector3 ball = root + (PropMeshKit.Around(angle) * (i == 0 ? 0f : radius * 0.5f)) + (Vector3.up * (height + (0.03f * scale)));
                _kit.AddBlob(ball, 0.06f * scale, 0.95f, 0.05f, 0, _offWhite, _offWhite, 0f, random, null);
            }

            return new[] { PropShape.Cylinder(root, radius * 1.05f, height + (0.12f * scale), PropMaterial.Wood) };
        }

        /// <summary>A bicycle standing on its wheels, its length along <paramref name="yaw"/>: two wheels, a blue frame, a seat and handlebars.</summary>
        public PropShape[] AddBicycle(Vector3 root, float yaw, System.Random random)
        {
            const float WheelRadius = 0.32f;
            const float Base = 0.5f;
            Vector3 along = PropMeshKit.Around(yaw);
            int first = _kit.Mesh.Positions.Count;

            _kit.AddUprightRing(Local(root, yaw, -Base, WheelRadius, 0f), along, WheelRadius, WheelRadius * 0.82f, 8, _metal);
            _kit.AddUprightRing(Local(root, yaw, Base, WheelRadius, 0f), along, WheelRadius, WheelRadius * 0.82f, 8, _metal);
            _kit.AddBox(Local(root, yaw, 0f, 0.62f, 0f), new Vector3(0.52f, 0.016f, 0.016f), yaw, 0f, null, _blue, false);
            _kit.AddBox(Local(root, yaw, -0.32f, 0.5f, 0f), new Vector3(0.016f, 0.2f, 0.016f), yaw, 0f, null, _blue, false);
            _kit.AddBox(Local(root, yaw, -0.32f, 0.74f, 0f), new Vector3(0.07f, 0.02f, 0.03f), yaw, 0f, null, _metalDark, false);
            _kit.AddBox(Local(root, yaw, Base, 0.58f, 0f), new Vector3(0.016f, 0.28f, 0.016f), yaw, 0f, null, _blue, false);
            _kit.AddBox(Local(root, yaw, Base, 0.88f, 0f), new Vector3(0.03f, 0.02f, 0.2f), yaw, 0f, null, _metal, false);

            return new[] { FitBox(first, yaw, PropMaterial.Wood) };
        }

        /// <summary>Laundry on a line between two wooden posts, three cloths hanging from it. Only the posts collide.</summary>
        public PropShape[] AddLaundry(Vector3 root, float yaw, System.Random random)
        {
            const float Half = 1.1f;
            const float PostRadius = 0.03f;
            const float PostHeight = 1.7f;
            Vector3 along = PropMeshKit.Around(yaw);
            var shapes = new PropShape[2];

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 post = root + (along * (side * Half));
                _kit.AddPrism(post, PostRadius, PostHeight, 6, 0f, _woodLight, BuiltSink);
                shapes[side < 0 ? 0 : 1] = PropShape.Cylinder(post, PostRadius * 1.05f, PostHeight, PropMaterial.Wood);
            }

            _kit.AddBox(Local(root, yaw, 0f, 1.6f, 0f), new Vector3(Half, 0.008f, 0.008f), yaw, 0f, null, _metalDark, false);
            Color32[] cloths = { _offWhite, _blue, _offWhite };
            for (int i = 0; i < 3; i++)
            {
                float length = 0.4f + (PropMeshKit.Next(random) * 0.1f);
                _kit.AddBox(Local(root, yaw, (i - 1) * 0.65f, 1.6f - 0.3f, 0f), new Vector3(length * 0.5f, 0.3f, 0.012f), yaw, 0f, null, cloths[i], false);
            }

            return shapes;
        }

        /// <summary>Small chapel: whitewashed nave under a slate gable roof, a door in the +x gable end and a square bell tower at the other end.</summary>
        public PropShape[] AddChapel(Vector3 root, float yaw, float scale, System.Random random)
        {
            float length = _settings.ChapelLength * scale;
            float width = _settings.ChapelWidth * scale;
            float wall = _settings.ChapelWallHeight * scale;
            float tower = _settings.ChapelTowerHeight * scale;
            float towerHalf = 0.4f * scale;
            int first = _kit.Mesh.Positions.Count;

            AddStanding(root, yaw, 0f, 0f, new Vector3(length * 0.5f, 0f, width * 0.5f), wall, _stoneLight, random);
            _kit.AddGableRoof(Local(root, yaw, 0f, wall, 0f), length + 0.2f, width + 0.2f, 0.7f * scale, yaw, _slateLight, _slateMid);
            float towerX = (-length * 0.5f) + towerHalf;
            AddStanding(root, yaw, towerX, 0f, new Vector3(towerHalf, 0f, towerHalf), tower, _stoneMid, random);
            _kit.AddCone(Local(root, yaw, towerX, tower, 0f), towerHalf * 1.414f * 1.15f, 0.75f * scale, 4, yaw + (Mathf.PI * 0.25f), _slateMid, true);
            AddStanding(root, yaw, (length * 0.5f) + 0.01f, 0f, new Vector3(0.02f, 0f, 0.26f * scale), 0.95f * scale, _trunk, null);

            return new[] { FitBox(first, yaw, PropMaterial.Stone) };
        }

        /// <summary>
        /// Lighthouse: an eight-sided stone tower that narrows toward the top with a darker band, a slate gallery, lantern and cap.
        /// Two stacked cylinders hold it.
        /// </summary>
        public PropShape[] AddLighthouse(Vector3 root, float yaw, float scale, System.Random random)
        {
            float radius = _settings.LighthouseRadius * scale;
            float height = _settings.LighthouseHeight * scale;
            const int Sides = 8;
            int first = _kit.Mesh.Positions.Count;

            _kit.AddFrustum(root, radius, radius * 0.78f, height * 0.55f, Sides, yaw, _stoneLight, BuiltSink);
            _kit.AddFrustum(root + (Vector3.up * (height * 0.55f)), radius * 0.78f, radius * 0.74f, height * 0.15f, Sides, yaw, _stoneDark, 0f);
            _kit.AddFrustum(root + (Vector3.up * (height * 0.7f)), radius * 0.74f, radius * 0.62f, height * 0.3f, Sides, yaw, _stoneLight, 0f);
            Vector3 top = root + (Vector3.up * height);
            // The gallery is a closed slab (sides, a top that slopes a little, an underside), so it never shows its inside; the lantern
            // and the cap take the lighter slate shades, since the darkest reads near black once lit.
            float slab = 0.14f * scale;
            float galleryRadius = radius * 0.82f;
            _kit.AddPrism(top, galleryRadius, slab, Sides, yaw, _slateMid, 0f);
            _kit.AddCone(top + (Vector3.up * slab), galleryRadius, 0.04f * scale, Sides, yaw, _slateLight, false);
            for (int i = 0; i < Sides; i++)
            {
                float a0 = yaw + (i * Mathf.PI * 2f / Sides);
                float a1 = yaw + ((i + 1) * Mathf.PI * 2f / Sides);
                _kit.AddTriangle(top, top + (PropMeshKit.Around(a0) * galleryRadius), top + (PropMeshKit.Around(a1) * galleryRadius), Vector3.down, _slateDark);
            }

            _kit.AddPrism(top + (Vector3.up * slab), radius * 0.42f, 0.5f * scale, Sides, yaw, _slateLight, 0f);
            _kit.AddCone(top + (Vector3.up * (slab + (0.5f * scale))), radius * 0.6f, 0.5f * scale, Sides, yaw, _slateMid, true);

            return FitCylinders(first, root);
        }

        /// <summary>
        /// Two stacked vertical cylinders, split at half the height, each as wide as the vertices in its half reach from the root:
        /// they enclose the mesh by construction and follow a tower's taper.
        /// </summary>
        private PropShape[] FitCylinders(int firstVertex, Vector3 root)
        {
            var points = _kit.Mesh.Positions;
            float top = root.y;
            for (int i = firstVertex; i < points.Count; i++)
            {
                top = Mathf.Max(top, points[i].y);
            }

            float split = root.y + ((top - root.y) * 0.5f);
            float lower = 0f;
            float upper = 0f;
            for (int i = firstVertex; i < points.Count; i++)
            {
                float reach = new Vector2(points[i].x - root.x, points[i].z - root.z).magnitude;
                if (points[i].y <= split)
                {
                    lower = Mathf.Max(lower, reach);
                }
                else
                {
                    upper = Mathf.Max(upper, reach);
                }
            }

            // The lower cylinder holds a face that starts in it and ends in the upper half.
            lower = Mathf.Max(lower, upper);
            return new[]
            {
                PropShape.Cylinder(root, lower, split - root.y, PropMaterial.Stone),
                PropShape.Cylinder(new Vector3(root.x, split, root.z), Mathf.Max(upper, 0.01f), top - split, PropMaterial.Stone),
            };
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
