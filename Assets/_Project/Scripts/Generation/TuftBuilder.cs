using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Builds the grass fringe that hides the island's clean edge. Each tuft is a small tent of four
    /// facets: two lie on the grass and rise to a crease on the edge, and two fold over it and hang down
    /// the earth band like thatch. Tufts overlap, so the fringe reads as one piece of ground folding
    /// over the edge rather than as separate objects sitting on it.
    /// </summary>
    public sealed class TuftBuilder
    {
        // Candidates this close together along the edge. Well under a tuft's width, so none can be missed.
        private const float ScanStep = 0.04f;

        // Below this, an edge piece is too short to give a direction.
        private const float MinSegmentLength = 0.0001f;

        // How far the crease and tip lean along the edge, as a fraction of the half width, so tufts
        // don't look machined. Lean plus tip width has to stay under 1, or a facet would fold over its neighbour.
        private const float Lean = 0.3f;

        private readonly TuftSettings _settings;
        private readonly TerrainMeshData _mesh = new TerrainMeshData();
        private readonly Color32 _light;
        private readonly Color32 _mid;
        private readonly Color32 _dark;

        private TuftBuilder(TuftSettings settings, PaletteSettings palette)
        {
            _settings = settings;
            _light = TerrainBuilder.ToVertexColor(palette.TuftLight);
            _mid = TerrainBuilder.ToVertexColor(palette.TuftMid);
            _dark = TerrainBuilder.ToVertexColor(palette.TuftDark);
        }

        /// <summary>Builds a tuft on each spot along the edge. The same settings and edge always give the same tufts.</summary>
        public static TerrainMeshData Build(TuftSettings settings, PaletteSettings palette, IReadOnlyList<EdgeSegment> edge)
        {
            var builder = new TuftBuilder(settings, palette);
            var random = new System.Random(settings.Seed);

            foreach (TuftSpot spot in Place(edge, settings.MinSpacing, random))
            {
                builder.AddTuft(spot, random);
            }

            return builder._mesh;
        }

        /// <summary>
        /// Picks spots along the edge in random order, keeping each one that's far enough from those kept
        /// so far. Every rejected spot lies within <paramref name="minSpacing"/> of a kept one, so tufts
        /// can never leave a long bare stretch, and the spacing never settles into a regular rhythm.
        /// Working from separate pieces means an edge made of several islands needs nothing extra.
        /// </summary>
        public static List<TuftSpot> Place(IReadOnlyList<EdgeSegment> edge, float minSpacing, System.Random random)
        {
            List<TuftSpot> candidates = Candidates(edge);
            Shuffle(candidates, random);

            var kept = new List<TuftSpot>();
            float limit = minSpacing * minSpacing;
            foreach (TuftSpot candidate in candidates)
            {
                if (!IsNearAny(candidate.Position, kept, limit))
                {
                    kept.Add(candidate);
                }
            }

            return kept;
        }

        private static List<TuftSpot> Candidates(IReadOnlyList<EdgeSegment> edge)
        {
            var candidates = new List<TuftSpot>();
            foreach (EdgeSegment segment in edge)
            {
                // No fringe where water meets the edge (ART_DIRECTION.md §6).
                if (segment.Inner == EdgeSurface.Water)
                {
                    continue;
                }

                float length = Vector3.Distance(segment.A, segment.B);
                Vector3 outward = new Vector3(segment.Outward.x, 0f, segment.Outward.z);
                if (length < MinSegmentLength || outward.sqrMagnitude < MinSegmentLength)
                {
                    continue;
                }

                outward.Normalize();
                int count = Mathf.CeilToInt(length / ScanStep);
                for (int i = 0; i < count; i++)
                {
                    candidates.Add(new TuftSpot(Vector3.Lerp(segment.A, segment.B, (i + 0.5f) / count), outward));
                }
            }

            return candidates;
        }

        private static void Shuffle(List<TuftSpot> items, System.Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (items[i], items[other]) = (items[other], items[i]);
            }
        }

        private static bool IsNearAny(Vector3 point, List<TuftSpot> spots, float limitSquared)
        {
            foreach (TuftSpot spot in spots)
            {
                float dx = spot.Position.x - point.x;
                float dz = spot.Position.z - point.z;
                if ((dx * dx) + (dz * dz) < limitSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddTuft(TuftSpot spot, System.Random random)
        {
            Vector3 along = Vector3.Cross(Vector3.up, spot.Outward);
            float halfWidth = Between(random, _settings.HalfWidth);

            Vector3 left = spot.Position - (along * halfWidth);
            Vector3 right = spot.Position + (along * halfWidth);
            Vector3 back = spot.Position - (spot.Outward * Between(random, _settings.Inset));
            Vector3 crease = spot.Position
                + (Vector3.up * Between(random, _settings.Rise))
                + (along * (Jitter(random, Lean) * halfWidth));

            // The tip sits further out than the crease, so the hanging facets tilt up like a roof and
            // catch the light. Tilting them down, under the crease, is what made earlier tufts read as
            // dark shards.
            Vector3 tip = spot.Position
                + (Vector3.down * Between(random, _settings.Drop))
                + (spot.Outward * Between(random, _settings.Flare))
                + (along * (Jitter(random, Lean) * halfWidth));

            Color32 color = PickColor(random);
            _mesh.Add(back, left, crease, Vector3.up, color);
            _mesh.Add(back, crease, right, Vector3.up, color);

            if (_settings.TipWidth <= 0f)
            {
                _mesh.Add(left, crease, tip, spot.Outward, color);
                _mesh.Add(crease, right, tip, spot.Outward, color);
                return;
            }

            // A blunt tip has two low points instead of one, which takes the spike out of the outline.
            Vector3 tipOffset = along * (halfWidth * _settings.TipWidth);
            Vector3 tipLeft = tip - tipOffset;
            Vector3 tipRight = tip + tipOffset;
            _mesh.Add(left, crease, tipLeft, spot.Outward, color);
            _mesh.Add(crease, tipRight, tipLeft, spot.Outward, color);
            _mesh.Add(crease, right, tipRight, spot.Outward, color);
        }

        private Color32 PickColor(System.Random random)
        {
            double roll = random.NextDouble();
            if (roll < _settings.LightChance)
            {
                return _light;
            }

            return roll < _settings.LightChance + _settings.DarkChance ? _dark : _mid;
        }

        private static float Between(System.Random random, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        private static float Jitter(System.Random random, float amount)
        {
            return ((float)random.NextDouble() - 0.5f) * 2f * amount;
        }
    }

    /// <summary>A point on the island edge, and which way is out from there.</summary>
    public readonly struct TuftSpot
    {
        public TuftSpot(Vector3 position, Vector3 outward)
        {
            Position = position;
            Outward = outward;
        }

        public Vector3 Position { get; }
        public Vector3 Outward { get; }
    }
}
