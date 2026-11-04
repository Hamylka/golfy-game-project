using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Builds the grass border that hides the island's clean edge: small faceted prisms straddling the
    /// edge line, with one point above the grass and one hanging over the earth band.
    /// </summary>
    public sealed class TuftBuilder
    {
        // Candidates this close together along the edge. Well under a tuft's width, so none can be missed.
        private const float ScanStep = 0.04f;

        // Below this, an edge piece is too short to give a direction.
        private const float MinSegmentLength = 0.0001f;

        // Keeps a tuft's flat side from fighting with the ground for the same pixels.
        private const float RingLift = 0.03f;

        // Tufts are narrower across the edge than along it, so they hug the outline instead of
        // spreading onto the grass.
        private const float AcrossSquash = 0.65f;

        // Each corner moves by up to this fraction of the radius, so no two tufts share a shape.
        private const float CornerJitter = 0.25f;

        // How far a point leans sideways, as a fraction of the radius, so tufts don't look machined.
        private const float PointLean = 0.3f;

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
            float radius = Between(random, _settings.Radius);
            float rise = Between(random, _settings.Rise);
            float drop = Between(random, _settings.Drop);
            Vector3 middle = spot.Position + (spot.Outward * Between(random, _settings.Overhang)) + (Vector3.up * RingLift);
            Color32 color = PickColor(random);

            int sides = random.NextDouble() < _settings.FourSidedChance ? 4 : 3;
            var ring = new Vector3[sides];
            float startAngle = (float)(random.NextDouble() * Mathf.PI * 2f);
            for (int i = 0; i < sides; i++)
            {
                float angle = startAngle + (i * Mathf.PI * 2f / sides);
                float reach = radius * (1f + Jitter(random, CornerJitter));
                ring[i] = middle
                    + (along * (Mathf.Cos(angle) * reach))
                    + (spot.Outward * (Mathf.Sin(angle) * reach * AcrossSquash));
            }

            Vector3 lean = along * (Jitter(random, PointLean) * radius);
            Vector3 top = middle + (Vector3.up * rise) + lean;

            // The lower point leans outward, so it sits on the earth band's face rather than inside it.
            Vector3 bottom = middle + (Vector3.down * drop) + (spot.Outward * (radius * PointLean)) - lean;

            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % sides];
                AddFacet(a, b, top, middle, color);

                // A tuft with no drop rests on the edge, where facing down would never be seen.
                if (drop > 0f)
                {
                    AddFacet(a, b, bottom, middle, color);
                }
            }
        }

        private void AddFacet(Vector3 a, Vector3 b, Vector3 point, Vector3 middle, Color32 color)
        {
            Vector3 center = (a + b + point) / 3f;

            // With no rise the upper facets lie flat, where pointing away from the middle says nothing.
            Vector3 facing = point.y >= middle.y ? (center - middle) + (Vector3.up * 0.01f) : center - middle;
            _mesh.Add(a, b, point, facing, color);
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
