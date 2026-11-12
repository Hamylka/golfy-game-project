using System;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Three 64-bit hashes of a course, for the split fairway bit-identity guard (F3.1): its fields sampled on a grid, its terrain
    /// mesh, and its <see cref="CourseLayout"/> data (plan and props included). FNV-1a over the exact bits of every float, so any
    /// change in the last bit shows. Three separate hashes say which part drifted. Editor x64 Mono figures: a regression guard on
    /// this runtime, not a proof that two platforms agree.
    /// </summary>
    public static class CourseFingerprint
    {
        /// <summary>The grid step of the field samples, off the 1 m terrain cell so samples fall inside cells, not on their corners.</summary>
        public const float GridStep = 0.37f;

        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Fields(CourseLayout layout)
        {
            var fields = new CourseFields(layout);
            var hash = new Hasher();
            Vector2 extent = layout.HalfExtent + Vector2.one;
            int countX = Mathf.CeilToInt(extent.x * 2f / GridStep);
            int countZ = Mathf.CeilToInt(extent.y * 2f / GridStep);
            for (int ix = 0; ix <= countX; ix++)
            {
                float x = -extent.x + (ix * GridStep);
                for (int iz = 0; iz <= countZ; iz++)
                {
                    float z = -extent.y + (iz * GridStep);
                    var p = new Vector3(x, 0f, z);
                    hash.Add(fields.Island(p));
                    hash.Add(fields.Fairway(p));
                    hash.Add(fields.Green(p));
                    hash.Add(fields.Bunker(p));
                    hash.Add(fields.Pond(p));
                    hash.Add((int)fields.Surface(p));
                    hash.Add(fields.Across(p));
                    hash.Add(fields.Height(x, z));
                    hash.Add(fields.FeltHeight(x, z));
                }
            }

            return hash.Value;
        }

        public static ulong Mesh(CourseLayout layout, PaletteSettings palette)
        {
            TerrainResult result = TerrainBuilder.Build(layout, palette);
            var hash = new Hasher();
            Add(hash, result.Land);
            Add(hash, result.Water);
            hash.Add(result.Outline.Count);
            foreach (Vector3 point in result.Outline)
            {
                hash.Add(point);
            }

            hash.Add(result.Edge.Count);
            foreach (EdgeSegment segment in result.Edge)
            {
                hash.Add(segment.A);
                hash.Add(segment.B);
                hash.Add(segment.Outward);
                hash.Add((int)segment.Inner);
            }

            return hash.Value;
        }

        /// <summary>
        /// The layout's data as it stood before F3.1 (the fairway's strip values, not its pieces, which didn't exist then: their
        /// effect shows in <see cref="Fields"/> and <see cref="Mesh"/>), and the checked result's variant, candidates and drops.
        /// </summary>
        public static ulong Layout(CourseLayout layout, int variant, int candidates, int dropped)
        {
            var hash = new Hasher();
            hash.Add(variant);
            hash.Add(candidates);
            hash.Add(dropped);

            hash.Add(layout.Islands.Count);
            foreach (IslandOutline island in layout.Islands)
            {
                hash.Add(island.CenterX); hash.Add(island.CenterZ); hash.Add(island.Heading); hash.Add(island.HalfLength);
                hash.Add(island.HalfWidth); hash.Add(island.WidthWobble); hash.Add(island.WidthNoiseScale); hash.Add(island.NoiseOffset);
            }

            hash.Add(layout.Centreline.Points.Count);
            foreach (Vector2 point in layout.Centreline.Points)
            {
                hash.Add(point);
            }

            FairwayLayout fairway = layout.Fairway;
            hash.Add(fairway.Start); hash.Add(fairway.End); hash.Add(fairway.HalfWidth); hash.Add(fairway.Wobble);
            hash.Add(fairway.StripeWidth); hash.Add(fairway.NoiseOffset); hash.Add(fairway.StartCap); hash.Add(fairway.EndCap);

            hash.Add(layout.Greens.Count);
            foreach (GreenLayout green in layout.Greens)
            {
                Add(hash, green.Outline);
                hash.Add(green.Tilt);
            }

            hash.Add(layout.Bunkers.Count);
            foreach (EllipseShape bunker in layout.Bunkers)
            {
                Add(hash, bunker);
            }

            hash.Add(layout.Ponds.Count);
            foreach (PondLayout pond in layout.Ponds)
            {
                Add(hash, pond.Outline);
                hash.Add(pond.WaterLevel);
            }

            hash.Add(layout.Swells.Count);
            foreach (GroundSwell swell in layout.Swells)
            {
                hash.Add(swell.Center); hash.Add(swell.Radius); hash.Add(swell.Height);
            }

            hash.Add(layout.WaterLevel);
            GroundRelief relief = layout.Relief;
            hash.Add(relief.CellSize); hash.Add(relief.AxisRelief); hash.Add(relief.EdgeRelief);
            hash.Add(relief.ReliefDistance); hash.Add(relief.NoiseScale); hash.Add(relief.NoiseOffset);
            hash.Add(layout.Tee); hash.Add(layout.Hole); hash.Add(layout.FlatRadius); hash.Add(layout.FlatBlend);
            hash.Add(layout.Edge.SkirtDepth); hash.Add(layout.Edge.SkirtNoiseScale);
            hash.Add(layout.AlongCentreline ? 1 : 0);
            hash.Add(layout.HalfExtent);

            ElevationProfile elevation = layout.Elevation;
            hash.Add(elevation.KnotCount);
            hash.Add(elevation.LegBlend);
            for (int i = 0; i < elevation.KnotCount; i++)
            {
                ElevationKnot knot = elevation.Knot(i);
                hash.Add(knot.Along); hash.Add(knot.Height); hash.Add(knot.Camber);
            }

            Add(hash, layout.Plan);

            hash.Add(layout.Props.Count);
            foreach (CourseProp prop in layout.Props)
            {
                hash.Add((int)prop.Kind); hash.Add(prop.Root); hash.Add(prop.End); hash.Add(prop.Scale);
                hash.Add(prop.Seed); hash.Add(prop.Cluster); hash.Add(prop.YawDegrees);
                hash.Add(prop.Shapes.Count);
                foreach (PropShape shape in prop.Shapes)
                {
                    hash.Add((int)shape.Kind); hash.Add((int)shape.Material); hash.Add(shape.Center); hash.Add(shape.Extents); hash.Add(shape.Yaw);
                }
            }

            return hash.Value;
        }

        private static void Add(Hasher hash, CoursePlan plan)
        {
            if (plan == null)
            {
                hash.Add(-1);
                return;
            }

            hash.Add(plan.Hazards.Count);
            foreach (HazardInfo hazard in plan.Hazards)
            {
                hash.Add((int)hazard.Role); hash.Add(hazard.IsWater ? 1 : 0); hash.Add(hazard.Index);
            }

            hash.Add(plan.SafeRoute.Count);
            foreach (Vector2 point in plan.SafeRoute)
            {
                hash.Add(point);
            }

            hash.Add(plan.Lines.Count);
            foreach (ShotLine line in plan.Lines)
            {
                hash.Add(line.From); hash.Add(line.To); hash.Add((int)line.Kind);
            }

            hash.Add(plan.MissingRoles.Count);
            foreach (HazardRole role in plan.MissingRoles)
            {
                hash.Add((int)role);
            }

            hash.Add(plan.HazardTarget); hash.Add(plan.GapCountsAsHazard ? 1 : 0); hash.Add(plan.SafeTarget); hash.Add(plan.GuardDirection);
            hash.Add(plan.ShotCorridorHalfWidth); hash.Add(plan.Dogleg ? 1 : 0); hash.Add(plan.IslandGreen ? 1 : 0); hash.Add(plan.DirectOrigin);
        }

        private static void Add(Hasher hash, EllipseShape shape)
        {
            hash.Add(shape.Center); hash.Add(shape.Radius); hash.Add(shape.Wobble); hash.Add(shape.Heading);
        }

        private static void Add(Hasher hash, TerrainMeshData mesh)
        {
            hash.Add(mesh.Positions.Count);
            foreach (Vector3 position in mesh.Positions)
            {
                hash.Add(position);
            }

            foreach (Color32 colour in mesh.Colors)
            {
                hash.Add(colour.r | (colour.g << 8) | (colour.b << 16) | (colour.a << 24));
            }

            hash.Add(mesh.Sway == null ? -1 : mesh.Sway.Count);
            if (mesh.Sway != null)
            {
                foreach (float sway in mesh.Sway)
                {
                    hash.Add(sway);
                }

                foreach (float phase in mesh.Phase)
                {
                    hash.Add(phase);
                }
            }
        }

        /// <summary>One prop's hash, every field the layout hash reads for it (F3.4's props reference, one value per prop).</summary>
        public static ulong Prop(CourseProp prop)
        {
            var hash = new Hasher();
            hash.Add((int)prop.Kind); hash.Add(prop.Root); hash.Add(prop.End); hash.Add(prop.Scale);
            hash.Add(prop.Seed); hash.Add(prop.Cluster); hash.Add(prop.YawDegrees);
            hash.Add(prop.Shapes.Count);
            foreach (PropShape shape in prop.Shapes)
            {
                hash.Add((int)shape.Kind); hash.Add((int)shape.Material); hash.Add(shape.Center); hash.Add(shape.Extents); hash.Add(shape.Yaw);
            }

            return hash.Value;
        }

        private sealed class Hasher
        {
            public ulong Value { get; private set; } = Offset;

            public void Add(int value)
            {
                unchecked
                {
                    uint bits = (uint)value;
                    for (int i = 0; i < 4; i++)
                    {
                        Value = (Value ^ ((bits >> (i * 8)) & 0xFFu)) * Prime;
                    }
                }
            }

            public void Add(float value) => Add(BitConverter.SingleToInt32Bits(value));

            public void Add(Vector2 value)
            {
                Add(value.x);
                Add(value.y);
            }

            public void Add(Vector3 value)
            {
                Add(value.x);
                Add(value.y);
                Add(value.z);
            }
        }
    }
}
