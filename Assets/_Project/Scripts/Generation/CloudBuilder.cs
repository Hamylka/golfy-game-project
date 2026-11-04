using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Builds the faceted clouds below the island into one mesh, so the whole layer costs one draw call.
    /// Each facet's tone is baked from the sun: lit shading made clouds look like grey boulders, because
    /// its dark side is only as bright as the ambient light.
    /// </summary>
    public sealed class CloudBuilder
    {
        // Clouds have flat undersides and puffy tops.
        private const float BlobTopSquash = 0.75f;
        private const float BlobBottomSquash = 0.3f;
        private const float BlobCornerJitter = 0.07f;

        // Blobs line up loosely along one axis, so a cluster reads long and flat instead of as a ball.
        private const float BlobSpacing = 0.95f;
        private const float BlobScatter = 0.5f;
        private const float EndBlobSize = 0.55f;
        private const float MiddleBlobSize = 1.1f;
        private const float BlobSizeJitter = 0.15f;
        private const float BlobRise = 0.1f;

        private readonly CloudSettings _settings;
        private readonly TerrainMeshData _mesh = new TerrainMeshData();
        private readonly Color _light;
        private readonly Color _shade;
        private readonly Vector3 _toSun;

        private CloudBuilder(CloudSettings settings, PaletteSettings palette, Vector3 toSun)
        {
            _settings = settings;
            _light = palette.CloudLight;
            _shade = palette.CloudShade;
            _toSun = toSun.normalized;
        }

        /// <summary>
        /// Builds clouds over <paramref name="visible"/>, the ground rectangle the framed view can see,
        /// with room around it for the layer to drift.
        /// </summary>
        public static TerrainMeshData Build(CloudSettings settings, PaletteSettings palette, Rect visible, Vector3 toSun)
        {
            var builder = new CloudBuilder(settings, palette, toSun);

            // Drift only moves along z, so only z needs that much spare room.
            float padZ = settings.DriftRange + settings.Margin;
            Rect area = Rect.MinMaxRect(visible.xMin - settings.Margin, visible.yMin - padZ, visible.xMax + settings.Margin, visible.yMax + padZ);

            // One generator per layer, so tuning one leaves the other unchanged.
            builder.AddLayer(area, settings.CellSize, settings.Occupancy, settings.Lowest, settings.Highest, new System.Random(settings.Seed));

            Rect near = Rect.MinMaxRect(area.xMin, area.yMin, Mathf.Min(area.xMax, area.xMin + settings.NearBandLength), area.yMax);
            builder.AddLayer(near, settings.NearCellSize, settings.NearOccupancy, settings.NearLowest, settings.NearHighest, new System.Random(unchecked(settings.Seed + 1)));

            return builder._mesh;
        }

        /// <summary>
        /// The layer's offset along z at a given time. A sine rather than a constant speed, so it can
        /// run forever over a finite mesh and turns back too gently to notice.
        /// </summary>
        public static float DriftOffset(float time, float speed, float range)
        {
            return range <= 0f ? 0f : range * Mathf.Sin(time * speed / range);
        }

        private void AddLayer(Rect area, float cell, float occupancy, float lowest, float highest, System.Random random)
        {
            for (float x = area.xMin; x < area.xMax; x += cell)
            {
                for (float z = area.yMin; z < area.yMax; z += cell)
                {
                    double chance = random.NextDouble();
                    var center = new Vector3(x + (Next(random) * cell), Mathf.Lerp(lowest, highest, Next(random)), z + (Next(random) * cell));
                    if (chance < occupancy)
                    {
                        AddCluster(center, random);
                    }
                }
            }
        }

        private void AddCluster(Vector3 center, System.Random random)
        {
            int blobs = random.Next(_settings.MinBlobs, _settings.MaxBlobs + 1);
            float size = Mathf.Lerp(_settings.MinClusterSize, _settings.MaxClusterSize, Next(random));
            float yaw = Next(random) * Mathf.PI * 2f;
            var axis = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));

            for (int i = 0; i < blobs; i++)
            {
                float along = i - ((blobs - 1) * 0.5f);
                float middle = 1f - (Mathf.Abs(along) / blobs);
                float radius = size * Mathf.Lerp(EndBlobSize, MiddleBlobSize, middle) * (1f + Spread(random, BlobSizeJitter));
                Vector3 position = center + (axis * (along * size * BlobSpacing)) + new Vector3(
                    Spread(random, BlobScatter) * size,
                    radius * BlobRise,
                    Spread(random, BlobScatter) * size);
                AddBlob(position, radius, random);
            }
        }

        private void AddBlob(Vector3 center, float radius, System.Random random)
        {
            var corners = new Vector3[Icosahedron.Corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 unit = Icosahedron.Corners[i].normalized;
                float height = unit.y * (unit.y < 0f ? BlobBottomSquash : BlobTopSquash);
                float jitter = 1f + Spread(random, BlobCornerJitter);
                corners[i] = center + (new Vector3(unit.x, height, unit.z) * (radius * jitter));
            }

            for (int i = 0; i < Icosahedron.Faces.Length; i += 3)
            {
                Vector3 a = corners[Icosahedron.Faces[i]];
                Vector3 b = corners[Icosahedron.Faces[i + 1]];
                Vector3 c = corners[Icosahedron.Faces[i + 2]];
                Vector3 outward = ((a + b + c) / 3f) - center;
                _mesh.Add(a, b, c, outward, FacetColor(a, b, c, outward, random));
            }
        }

        private Color32 FacetColor(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, System.Random random)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                normal = -normal;
            }

            float facing = (Vector3.Dot(normal, _toSun) * 0.5f) + 0.5f;
            facing = Mathf.Clamp01(facing + Spread(random, _settings.ToneJitter));
            float tone = Mathf.InverseLerp(_settings.ShadeBelow, _settings.LightAbove, facing);
            return TerrainBuilder.ToVertexColor(Color.Lerp(_shade, _light, tone));
        }

        private static float Next(System.Random random)
        {
            return (float)random.NextDouble();
        }

        /// <summary>A random value between -amount and +amount.</summary>
        private static float Spread(System.Random random, float amount)
        {
            return (Next(random) - 0.5f) * 2f * amount;
        }
    }
}
