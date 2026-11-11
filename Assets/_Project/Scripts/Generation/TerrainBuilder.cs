using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Plain vertex data for one mesh. Vertices are never shared, so every triangle is flat shaded.</summary>
    public sealed class TerrainMeshData
    {
        // Twice the area, squared: 1e-12 is a triangle of about half a square millimetre. A border grazing a
        // grid corner leaves real slivers a few millimetres across, and dropping those opens a crack against
        // the neighbouring triangles, so only truly flat ones go.
        private const float MinTwiceAreaSquared = 1e-12f;

        public List<Vector3> Positions { get; } = new List<Vector3>();
        public List<Color32> Colors { get; } = new List<Color32>();

        public int TriangleCount => Positions.Count / 3;

        /// <summary>
        /// Per-vertex sway weight, written to UV1.x: 0 for ground, trunks, rocks, walls and the flag pole, up to 1
        /// for the tip of foliage and the free end of the pennant. Null until a vertex gets a non-zero weight (or
        /// a water glint), so the land mesh carries no extra channel.
        ///
        /// UV1 semantics, read by AmbientMotion.hlsl (keep the two comments in step):
        /// x &gt; 0 is a swaying vertex and y is its phase (0..1, one value per tree);
        /// x == 0 and y &gt; 0 is a water facet and y is its glint hash (0..1, one value per facet);
        /// (0, 0) is static.
        /// </summary>
        public List<float> Sway { get; private set; }

        /// <summary>UV1.y, see <see cref="Sway"/>: the phase of a swaying vertex, the glint hash of a water vertex, else 0.</summary>
        public List<float> Phase { get; private set; }

        public void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color32 color)
        {
            Add(a, b, c, facing, color, 0f, 0f, 0f);
        }

        /// <summary>Like the plain overload, with a sway weight per corner (given in a, b, c order).</summary>
        public void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color32 color, float swayA, float swayB, float swayC)
        {
            Add(a, b, c, facing, color, swayA, swayB, swayC, 0f);
        }

        /// <summary>
        /// Like <see cref="Add(Vector3,Vector3,Vector3,Vector3,Color32,float,float,float)"/>, with the tree's phase,
        /// kept only on corners that sway: a corner with weight 0 stays (0, 0), never mistaken for a water facet.
        /// </summary>
        public void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color32 color, float swayA, float swayB, float swayC, float phase)
        {
            AddCore(a, b, c, facing, color, swayA, swayB, swayC,
                swayA > 0f ? phase : 0f, swayB > 0f ? phase : 0f, swayC > 0f ? phase : 0f);
        }

        /// <summary>A water facet with its glint hash (0..1, never 0) on all three corners.</summary>
        public void AddGlint(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color32 color, float glint)
        {
            AddCore(a, b, c, facing, color, 0f, 0f, 0f, glint, glint, glint);
        }

        private void AddCore(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color32 color, float swayA, float swayB, float swayC, float phaseA, float phaseB, float phaseC)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);

            // A cut passing right next to a corner can leave a sliver with no area, and no usable normal.
            if (normal.sqrMagnitude < MinTwiceAreaSquared)
            {
                return;
            }

            // On a front face in Unity, this cross product points toward the viewer.
            bool flip = Vector3.Dot(normal, facing) < 0f;
            int before = Positions.Count;
            Positions.Add(a);
            Positions.Add(flip ? c : b);
            Positions.Add(flip ? b : c);
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);

            if (Sway == null && (swayA != 0f || swayB != 0f || swayC != 0f || phaseA != 0f || phaseB != 0f || phaseC != 0f))
            {
                Sway = new List<float>(new float[before]);
                Phase = new List<float>(new float[before]);
            }

            if (Sway != null)
            {
                Sway.Add(swayA);
                Sway.Add(flip ? swayC : swayB);
                Sway.Add(flip ? swayB : swayC);
                Phase.Add(phaseA);
                Phase.Add(flip ? phaseC : phaseB);
                Phase.Add(flip ? phaseB : phaseC);
            }
        }

        /// <summary>A new mesh from these triangles, with flat normals. Whoever calls this destroys it.</summary>
        public Mesh ToMesh(string meshName)
        {
            var indices = new int[Positions.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            var mesh = new Mesh { name = meshName };

            // A par 3 stays well under this, but a long course could pass the 16-bit vertex limit.
            mesh.indexFormat = Positions.Count > ushort.MaxValue
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Positions);
            mesh.SetColors(Colors);
            if (Sway != null)
            {
                var uvs = new List<Vector2>(Sway.Count);
                for (int i = 0; i < Sway.Count; i++)
                {
                    uvs.Add(new Vector2(Sway[i], Phase[i]));
                }

                mesh.SetUVs(1, uvs);
            }

            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>What a course's terrain turns into.</summary>
    public sealed class TerrainResult
    {
        public TerrainMeshData Land { get; } = new TerrainMeshData();

        /// <summary>Separate from the land; its facets carry a glint hash in UV1.y (see <see cref="TerrainMeshData.Sway"/>).</summary>
        public TerrainMeshData Water { get; } = new TerrainMeshData();

        /// <summary>Points along the island edge, top and bottom. Enough to frame the island tightly.</summary>
        public List<Vector3> Outline { get; } = new List<Vector3>();

        /// <summary>
        /// Each piece of every island's edge, in no particular order, tagged with the surface just inside it.
        /// Grass tufts grow along the land pieces; a pond that reaches the edge gives water pieces, at the water level.
        /// </summary>
        public List<EdgeSegment> Edge { get; } = new List<EdgeSegment>();
    }

    /// <summary>A straight piece of an island's edge, with the surface just inside it.</summary>
    public readonly struct EdgeSegment
    {
        public EdgeSegment(Vector3 a, Vector3 b, Vector3 outward, EdgeSurface inner = EdgeSurface.Land)
        {
            A = a;
            B = b;
            Outward = outward;
            Inner = inner;
        }

        public Vector3 A { get; }
        public Vector3 B { get; }
        public Vector3 Outward { get; }

        /// <summary>What lies on the inner side: land, or water where a pond reaches the edge.</summary>
        public EdgeSurface Inner { get; }
    }

    /// <summary>
    /// Builds the terrain. A grid of triangles is cut along each surface's exact border, one field at a
    /// time, so borders are clean lines rather than sawteeth following the grid.
    /// </summary>
    public sealed class TerrainBuilder
    {
        // Halves the error each step: 12 steps across a 1 m cell lands within a quarter of a millimetre.
        private const int CrossingSteps = 12;

        private const int InBunker = 1;
        private const int InGreen = 2;
        private const int InFairway = 4;

        private readonly CourseLayout _layout;
        private readonly CourseFields _fields;
        private readonly TerrainResult _result = new TerrainResult();
        private readonly Color32 _rough;
        private readonly Color32 _fairwayLight;
        private readonly Color32 _fairwayDark;
        private readonly Color32 _green;
        private readonly Color32 _sand;
        private readonly Color32 _water;
        private readonly Color32 _earth;

        private TerrainBuilder(CourseLayout layout, PaletteSettings palette)
        {
            _layout = layout;
            _fields = new CourseFields(layout);

            _rough = ToVertexColor(palette.Rough);
            _fairwayLight = ToVertexColor(palette.FairwayLight);
            _fairwayDark = ToVertexColor(palette.FairwayDark);
            _green = ToVertexColor(palette.Green);
            _sand = ToVertexColor(palette.Sand);
            _water = ToVertexColor(palette.Water);
            _earth = ToVertexColor(palette.Earth);
        }

        public static TerrainResult Build(CourseLayout layout, PaletteSettings palette)
        {
            var builder = new TerrainBuilder(layout, palette);
            builder.BuildGrid();
            return builder._result;
        }

        /// <summary>
        /// Vertex colors reach the shader untouched, but the project renders in linear space, while
        /// palette colors are sRGB. Without this conversion everything would come out too bright.
        /// </summary>
        public static Color32 ToVertexColor(Color color)
        {
            return color.linear;
        }

        private void BuildGrid()
        {
            float cell = _layout.Relief.CellSize;
            int halfX = Mathf.CeilToInt((_layout.HalfExtent.x + cell) / cell);
            int halfZ = Mathf.CeilToInt((_layout.HalfExtent.y + cell) / cell);

            for (int ix = -halfX; ix < halfX; ix++)
            {
                for (int iz = -halfZ; iz < halfZ; iz++)
                {
                    Vector3 a = Corner(ix, iz);
                    Vector3 b = Corner(ix, iz + 1);
                    Vector3 c = Corner(ix + 1, iz + 1);
                    Vector3 d = Corner(ix + 1, iz);

                    // Alternating diagonals, so the ground reads as facets rather than rows.
                    if (CourseHeightField.UsesRisingDiagonal(ix, iz))
                    {
                        AddGround(a, b, c);
                        AddGround(a, c, d);
                    }
                    else
                    {
                        AddGround(a, b, d);
                        AddGround(b, c, d);
                    }
                }
            }
        }

        private Vector3 Corner(int ix, int iz)
        {
            float x = ix * _layout.Relief.CellSize;
            float z = iz * _layout.Relief.CellSize;
            return new Vector3(x, _fields.Height(x, z), z);
        }

        private void AddGround(Vector3 a, Vector3 b, Vector3 c)
        {
            // Every triangle is cut by every border before anything is decided about it. If a piece
            // stopped being cut once it was "green", say, its neighbour's later cuts would leave a
            // vertex on their shared edge that only one side has, and that shows as a pinhole.
            List<Triangle> pieces = SplitAlongStripes(new Triangle(a, b, c));
            pieces = SplitAll(pieces, _fields.Bunker, InBunker);
            pieces = SplitAll(pieces, _fields.Green, InGreen);
            pieces = SplitAll(pieces, _fields.Fairway, InFairway);

            // The pond and island edge come last, because each also needs a wall along its cut, and
            // that wall has to match the final vertices.
            var wet = new List<Triangle>();
            var dry = new List<Triangle>();
            var bank = new List<Segment>();
            foreach (Triangle piece in pieces)
            {
                Split(piece, _fields.Pond, wet, dry, bank);
            }

            foreach (Triangle piece in dry)
            {
                var inside = new List<Triangle>();
                var edge = new List<Segment>();
                Split(piece, _fields.Island, inside, null, edge);

                foreach (Segment segment in edge)
                {
                    AddSkirt(segment, segment);
                    _result.Edge.Add(new EdgeSegment(segment.A, segment.B, Across(segment, _fields.Island, true)));
                }

                foreach (Triangle land in inside)
                {
                    _result.Land.Add(land.A, land.B, land.C, Vector3.up, ColorOf(land));
                }
            }

            foreach (Triangle piece in wet)
            {
                var inside = new List<Triangle>();
                var edge = new List<Segment>();
                Split(piece, _fields.Island, inside, null, edge);

                foreach (Segment segment in edge)
                {
                    var top = new Segment(Lower(segment.A), Lower(segment.B));
                    AddSkirt(segment, top);
                    _result.Edge.Add(new EdgeSegment(top.A, top.B, Across(segment, _fields.Island, true), EdgeSurface.Water));
                }

                foreach (Triangle water in inside)
                {
                    // One glint hash per facet, from its centre on the ground plane (read by the shader, never the simulation).
                    Vector3 centre = water.Centroid;
                    _result.Water.AddGlint(Lower(water.A), Lower(water.B), Lower(water.C), Vector3.up, _water, AmbientHash.Unit(centre.x, centre.z));
                }
            }

            foreach (Segment segment in bank)
            {
                AddBank(segment);
            }
        }

        private List<Triangle> SplitAlongStripes(Triangle triangle)
        {
            var pieces = new List<Triangle> { triangle };

            // Stripes are lines at whole multiples of the stripe width across the play axis: lines of constant Z on a
            // hand-made course, lines parallel to each leg on a generated one (rounding off around the outside of a bend).
            float a = _fields.Across(triangle.A), b = _fields.Across(triangle.B), c = _fields.Across(triangle.C);
            float low = Mathf.Min(a, b, c);
            float high = Mathf.Max(a, b, c);

            for (int stripe = _fields.Stripe(low); stripe <= _fields.Stripe(high); stripe++)
            {
                float line = stripe * _layout.Fairway.StripeWidth;

                // Only lines passing through the triangle. One along a grid row needs no cut at all.
                if (line > low && line < high)
                {
                    pieces = _layout.AlongCentreline
                        ? SplitAll(pieces, p => _fields.Across(p) - line)
                        : SplitAll(pieces, p => p.z - line);
                }
            }

            return pieces;
        }

        private static List<Triangle> SplitAll(List<Triangle> pieces, Func<Vector3, float> field, int insideTag = 0)
        {
            var result = new List<Triangle>();
            foreach (Triangle piece in pieces)
            {
                Split(piece, field, result, result, null, insideTag);
            }

            return result;
        }

        /// <summary>
        /// A piece's colour comes from the side of each cut it was split onto, never from a field read at its centroid: the cut
        /// is a straight chord across the cell, and a small hazard's true outline curves away from its chord by more than a
        /// piece is wide, so the centroid could disagree with the drawn border (a wedge of sand outside it, sand left out inside).
        /// </summary>
        private Color32 ColorOf(Triangle piece)
        {
            if ((piece.Tags & InBunker) != 0)
            {
                return _sand;
            }

            if ((piece.Tags & InGreen) != 0)
            {
                return _green;
            }

            if ((piece.Tags & InFairway) != 0)
            {
                // The stripe lines are exact straight cuts, so a piece lies wholly on one stripe and its centroid names it.
                return Mathf.Abs(_fields.Stripe(_fields.Across(piece.Centroid))) % 2 == 0 ? _fairwayLight : _fairwayDark;
            }

            return _rough;
        }

        private void AddSkirt(Segment borderOnGround, Segment top)
        {
            Vector3 outward = Across(borderOnGround, _fields.Island, true);
            Vector3 lowA = new Vector3(top.A.x, _fields.SkirtBottom(top.A), top.A.z);
            Vector3 lowB = new Vector3(top.B.x, _fields.SkirtBottom(top.B), top.B.z);

            AddWall(top, lowA, lowB, outward);

            _result.Outline.Add(top.A);
            _result.Outline.Add(lowA);
        }

        private void AddBank(Segment segment)
        {
            // Where the pond reaches past the island edge, only the part on the island gets a bank.
            Vector3[] ends = { segment.A, segment.B };
            bool aInside = _fields.Island(ends[0]) < 0f;
            bool bInside = _fields.Island(ends[1]) < 0f;
            if (!aInside && !bInside)
            {
                return;
            }

            if (aInside != bInside)
            {
                Vector3 cut = Cross(ends[0], ends[1], _fields.Island);
                segment = aInside ? new Segment(ends[0], cut) : new Segment(cut, ends[1]);
            }

            AddWall(segment, Lower(segment.A), Lower(segment.B), Across(segment, _fields.Pond, false));
        }

        private void AddWall(Segment top, Vector3 lowA, Vector3 lowB, Vector3 facing)
        {
            _result.Land.Add(top.A, top.B, lowB, facing, _earth);
            _result.Land.Add(top.A, lowB, lowA, facing, _earth);
        }

        private Vector3 Lower(Vector3 point)
        {
            // The water of the pond this point belongs to, so bank walls, water and water edges all agree per pond.
            return new Vector3(point.x, _fields.WaterLevelAt(point), point.z);
        }

        private static Vector3 Across(Segment segment, Func<Vector3, float> field, bool towardPositive)
        {
            Vector3 middle = (segment.A + segment.B) * 0.5f;
            Vector3 across = Vector3.Cross((segment.B - segment.A).normalized, Vector3.up);
            bool positive = field(middle + (across * 0.02f)) > field(middle);
            return positive == towardPositive ? across : -across;
        }

        private static void Split(Triangle triangle, Func<Vector3, float> field, List<Triangle> inside, List<Triangle> outside, List<Segment> border, int insideTag = 0)
        {
            Vector3[] corners = { triangle.A, triangle.B, triangle.C };
            var inPolygon = new List<Vector3>(4);
            var outPolygon = new List<Vector3>(4);
            var crossings = new List<Vector3>(2);

            for (int i = 0; i < 3; i++)
            {
                Vector3 p = corners[i];
                Vector3 q = corners[(i + 1) % 3];
                float fp = field(p);
                float fq = field(q);
                (fp < 0f ? inPolygon : outPolygon).Add(p);

                if ((fp < 0f) != (fq < 0f))
                {
                    Vector3 crossing = Cross(p, q, field);
                    inPolygon.Add(crossing);
                    outPolygon.Add(crossing);
                    crossings.Add(crossing);
                }
            }

            Fan(inPolygon, inside, triangle.Tags | insideTag);
            Fan(outPolygon, outside, triangle.Tags);

            if (border != null && crossings.Count == 2)
            {
                border.Add(new Segment(crossings[0], crossings[1]));
            }
        }

        private static Vector3 Cross(Vector3 p, Vector3 q, Func<Vector3, float> field)
        {
            // Two triangles share each edge but walk it in opposite directions. Always computing from
            // the same end gives both the exact same point, so no crack can open between them.
            bool ordered = p.x < q.x || (p.x == q.x && p.z < q.z);
            Vector3 from = ordered ? p : q;
            Vector3 to = ordered ? q : p;

            // A border running exactly through a corner must cut at that corner. Bisection would land
            // a hair beside it, opening a pinhole against the neighbouring triangle.
            if (field(from) == 0f)
            {
                return from;
            }

            if (field(to) == 0f)
            {
                return to;
            }

            // Bisection, not a straight-line estimate: the outline curves sharply at the island's
            // tips, where a straight line across a cell misses the real border by a third of a metre.
            bool fromInside = field(from) < 0f;
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < CrossingSteps; i++)
            {
                float middle = (low + high) * 0.5f;
                if ((field(Vector3.Lerp(from, to, middle)) < 0f) == fromInside)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return Vector3.Lerp(from, to, (low + high) * 0.5f);
        }

        private static void Fan(List<Vector3> polygon, List<Triangle> output, int tags)
        {
            for (int i = 1; output != null && i + 1 < polygon.Count; i++)
            {
                output.Add(new Triangle(polygon[0], polygon[i], polygon[i + 1], tags));
            }
        }

        private readonly struct Triangle
        {
            public Triangle(Vector3 a, Vector3 b, Vector3 c, int tags = 0)
            {
                A = a;
                B = b;
                C = c;
                Tags = tags;
            }

            /// <summary>Which surface cuts this piece fell inside of (<see cref="InBunker"/> and the others), kept through every later cut.</summary>
            public int Tags { get; }

            public Vector3 A { get; }
            public Vector3 B { get; }
            public Vector3 C { get; }
            public Vector3 Centroid => (A + B + C) / 3f;
        }

        private readonly struct Segment
        {
            public Segment(Vector3 a, Vector3 b)
            {
                A = a;
                B = b;
            }

            public Vector3 A { get; }
            public Vector3 B { get; }
        }
    }
}
