using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>One continuous stretch of island edge where a pond reaches it: points at the water level, in walking order.</summary>
    public sealed class WaterfallRun
    {
        public List<Vector3> Points { get; } = new List<Vector3>();

        /// <summary>The pond's depth colour where it meets this run (sRGB), from the edge pieces; clear when they carry none.</summary>
        public Color Tone { get; set; }

        /// <summary>The length along the points.</summary>
        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Points.Count; i++)
                {
                    length += Vector3.Distance(Points[i - 1], Points[i]);
                }

                return length;
            }
        }
    }

    /// <summary>
    /// Builds the waterfall under the water pieces of the island edge: one continuous curtain per stretch of edge. It leaves the edge
    /// at the water level, rolls over a rounded brink, falls in an arc and bulges out in its middle, with cheeks closing its sides back
    /// to the cliff, so it has volume. Broad columns of facets each take their own light. It breaks into white foam over the brink,
    /// turns pale blue and fades to transparent at its foot (vertex alpha, in the transparent waterfall material). A pure function of
    /// the edge, the settings and the palette (positions are hashed to the centimetre, no random numbers), so a seed always gives the
    /// same waterfall. The tone flows down in the water shader (UV1, see <see cref="TerrainMeshData.FallPhase"/>).
    ///
    /// The joint with the pond: the top row starts on the very points of the edge, at the pond's shore tone and lit as if it faced up
    /// like the pond (explicit lighting normals, <see cref="TerrainMeshData.Normals"/>), so the pond flows into the fall with no tone
    /// step; lower down each column's normal turns toward its own face.
    /// Visual only: the ball never reads it. It is its own mesh, not part of the terrain, so the terrain stays as the checks see it.
    /// </summary>
    public sealed class WaterfallBuilder
    {
        private const float MinSegmentLength = 0.01f;
        private const int ToneLevels = 5;
        private const int MaxRows = 15;

        // Rows on the rounded brink, at even angles over its quarter circle.
        private const int BrinkRows = 2;

        // Below the brink, rows are denser near the top, where the water bends: depth = brink + rest * (row / rows) ^ RowCurve.
        private const float RowCurve = 1.3f;

        // How far along the edge the outward direction is averaged, so a corner in the edge never folds the sheet.
        private const float DirectionSpan = 0.3f;

        private readonly WaterfallSettings _settings;
        private readonly PaletteSettings _palette;
        private readonly TerrainMeshData _mesh = new TerrainMeshData();

        // The tone at the top of the sheet being built: the pond's where it meets the run.
        private Color _top;

        private WaterfallBuilder(WaterfallSettings settings, PaletteSettings palette)
        {
            _settings = settings;
            _palette = palette;
        }

        /// <summary>The waterfall's mesh for these edge pieces: one sheet per run. Empty when no pond reaches an edge.</summary>
        public static TerrainMeshData Build(WaterfallSettings settings, PaletteSettings palette, IReadOnlyList<EdgeSegment> edge)
        {
            var builder = new WaterfallBuilder(settings, palette);
            foreach (WaterfallRun run in FindRuns(edge, settings.GapTolerance, settings.MinRunLength))
            {
                builder.AddSheet(run);
            }

            return builder._mesh;
        }

        /// <summary>
        /// Joins the water pieces of the edge into runs. Pieces are turned to walk with outward on the side of Cross(direction, up),
        /// and a piece joins a run when its start lies within <paramref name="gapTolerance"/> of the run's end: small, so a run never
        /// bridges over land. Comparisons are in whole centimetres and the order is fixed by position, so the result never depends on
        /// the order of <paramref name="edge"/>. Runs shorter than <paramref name="minLength"/> are dropped.
        /// </summary>
        public static List<WaterfallRun> FindRuns(IReadOnlyList<EdgeSegment> edge, float gapTolerance, float minLength)
        {
            var pieces = new List<Piece>();
            foreach (EdgeSegment segment in edge)
            {
                if (segment.Inner != EdgeSurface.Water || Vector3.Distance(segment.A, segment.B) < MinSegmentLength)
                {
                    continue;
                }

                Vector3 direction = segment.B - segment.A;
                bool keep = Vector3.Dot(Vector3.Cross(direction, Vector3.up), segment.Outward) >= 0f;
                pieces.Add(keep ? new Piece(segment.A, segment.B, segment.WaterTone) : new Piece(segment.B, segment.A, segment.WaterTone));
            }

            pieces.Sort(Piece.Compare);

            int tolerance = Mathf.RoundToInt(gapTolerance * 100f);
            var used = new bool[pieces.Count];
            var runs = new List<WaterfallRun>();

            for (int i = 0; i < pieces.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                used[i] = true;
                var chain = new LinkedList<Vector3>();
                chain.AddLast(pieces[i].A);
                chain.AddLast(pieces[i].B);
                Color tone = pieces[i].WeightedTone;

                int next;
                while ((next = Nearest(pieces, used, chain.Last.Value, tolerance, true)) >= 0)
                {
                    used[next] = true;
                    chain.AddLast(pieces[next].B);
                    tone += pieces[next].WeightedTone;
                }

                int previous;
                while ((previous = Nearest(pieces, used, chain.First.Value, tolerance, false)) >= 0)
                {
                    used[previous] = true;
                    chain.AddFirst(pieces[previous].A);
                    tone += pieces[previous].WeightedTone;
                }

                var run = new WaterfallRun();
                foreach (Vector3 point in chain)
                {
                    // A joint closer than a centimetre is the same point.
                    if (run.Points.Count == 0 || CentimetresBetween(run.Points[run.Points.Count - 1], point) > 0)
                    {
                        run.Points.Add(point);
                    }
                }

                // The pond's tone along the run, weighted by length: each piece's alpha (1) times its length sums the lengths. Clear
                // when the pieces carry no tone, as in hand-built tests.
                float walked = run.Length;
                run.Tone = tone.a > 0f ? tone * (1f / tone.a) : Color.clear;

                if (run.Points.Count >= 2 && Mathf.RoundToInt(walked * 100f) >= Mathf.RoundToInt(minLength * 100f))
                {
                    runs.Add(run);
                }
            }

            return runs;
        }

        /// <summary>The nearest unused piece whose start (or end) is within the tolerance of a point, in whole centimetres; -1 when none.</summary>
        private static int Nearest(List<Piece> pieces, bool[] used, Vector3 point, int toleranceCentimetres, bool byStart)
        {
            int best = -1;
            int bestDistance = int.MaxValue;
            for (int j = 0; j < pieces.Count; j++)
            {
                if (used[j])
                {
                    continue;
                }

                int distance = CentimetresBetween(point, byStart ? pieces[j].A : pieces[j].B);
                if (distance <= toleranceCentimetres && distance < bestDistance)
                {
                    best = j;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static int CentimetresBetween(Vector3 a, Vector3 b)
        {
            return Mathf.RoundToInt(Vector3.Distance(a, b) * 100f);
        }

        /// <summary>
        /// Depth below the water level of each row of vertices, from 0 at the lip to the fall depth: two rows on the brink's quarter
        /// circle, then rows denser near the top. At most <see cref="MaxRows"/> rows of facets.
        /// </summary>
        public static List<float> RowDepths(WaterfallSettings settings)
        {
            float brink = Mathf.Min(settings.BrinkRadius, settings.FallDepth * 0.5f);
            float rest = settings.FallDepth - brink;
            int restRows = Mathf.Clamp(Mathf.CeilToInt(rest / settings.RowHeight), 1, MaxRows - BrinkRows);

            var depths = new List<float> { 0f };
            for (int r = 1; r <= BrinkRows; r++)
            {
                float angle = (float)r / BrinkRows * Mathf.PI * 0.5f;
                depths.Add(brink * (1f - Mathf.Cos(angle)));
            }

            for (int r = 1; r <= restRows; r++)
            {
                depths.Add(brink + (rest * Mathf.Pow((float)r / restRows, RowCurve)));
            }

            return depths;
        }

        /// <summary>
        /// How far out from the edge the water is at a depth below the lip: a quarter circle over the brink (leaving the lip level, as
        /// the pond's surface does), then a square-root arc settling at the reach, then a slow lean.
        /// </summary>
        public static float Outward(WaterfallSettings settings, float depth)
        {
            float brink = Mathf.Min(settings.BrinkRadius, settings.FallDepth * 0.5f);
            if (depth <= brink)
            {
                float rise = brink - depth;
                return Mathf.Sqrt(Mathf.Max(0f, (brink * brink) - (rise * rise)));
            }

            float below = depth - brink;
            float arc = Mathf.Sqrt(Mathf.Min(below, settings.ArcLength) / settings.ArcLength);
            return brink + (settings.Reach * arc) + (settings.LeanPerMetre * Mathf.Max(0f, below - settings.ArcLength));
        }

        private void AddSheet(WaterfallRun run)
        {
            _top = run.Tone.a > 0f ? run.Tone : _palette.WaterShallow;
            float length = run.Length;
            int columns = Mathf.Max(2, Mathf.RoundToInt(length / _settings.ColumnWidth));
            List<float> depths = RowDepths(_settings);
            int rows = depths.Count - 1;
            float fall = depths[rows];

            // Each end pulls in a little as the water falls, by its own hashed share. A long edge pulls in at most the max inset per end
            // and stays a wide curtain. The sides are straight lines: no ragged ends, which read as spikes.
            Vector3 first = run.Points[0];
            Vector3 last = run.Points[run.Points.Count - 1];
            float inset = Mathf.Min(_settings.MaxEndInset, length * (1f - _settings.Taper) * 0.5f);
            float insetStart = inset * Mathf.Lerp(0.7f, 1.3f, AmbientHash.Unit(first.x + 71f, first.z - 13f));
            float insetEnd = inset * Mathf.Lerp(0.7f, 1.3f, AmbientHash.Unit(last.x - 29f, last.z + 53f));

            // Volume: the curtain bulges out in its middle, in a parabola across the run, growing below the brink so the lip stays on
            // the edge. A short run bulges less.
            float bulge = Mathf.Min(_settings.MaxBulge, _settings.BulgePerMetre * length);
            float swellDepth = (2f * _settings.BrinkRadius) + _settings.ArcLength;

            var grid = new Vector3[columns + 1, rows + 1];

            // The edge straight behind each end of the sheet, at that row's height: the cheeks close the curtain's sides back to the
            // cliff, so it reads as a thick fall of water rather than a plane.
            var cliffStart = new Vector3[rows + 1];
            var cliffEnd = new Vector3[rows + 1];
            var alphas = new byte[rows + 1];
            for (int r = 0; r <= rows; r++)
            {
                float ease = Mathf.Pow(depths[r] / fall, 1.3f);
                float lo = insetStart * ease;
                float hi = length - (insetEnd * ease);
                float swell = bulge * Mathf.SmoothStep(0f, 1f, depths[r] / swellDepth);

                for (int i = 0; i <= columns; i++)
                {
                    float u = (float)i / columns;
                    float along = Mathf.Lerp(lo, hi, u);
                    Vector3 direction = Direction(run, along);
                    Vector3 point = Sample(run, along, out _);
                    float across = (2f * u) - 1f;
                    grid[i, r] = Vertex(point, direction, depths[r], swell * (1f - (across * across)), i, r, columns, rows);
                }

                cliffStart[r] = Behind(run, lo, grid[0, r].y);
                cliffEnd[r] = Behind(run, hi, grid[columns, r].y);
                alphas[r] = AlphaAt(_settings, depths[r]);
            }

            // Each column is lit as one broad face: its outward direction across the swell, turned a little by its own hash. Taken
            // halfway down, where the bulge is full.
            int middleRow = Mathf.Max(1, rows / 2);
            var columnOutward = new Vector3[columns];
            var streak = new bool[columns];
            for (int i = 0; i < columns; i++)
            {
                Vector3 across = grid[i + 1, middleRow] - grid[i, middleRow];
                across.y = 0f;
                Vector3 outward = Vector3.Cross(across, Vector3.up).normalized;
                Vector3 centre = (grid[i, 0] + grid[i + 1, 0]) * 0.5f;
                float yaw = Signed(AmbientHash.Unit(centre.x + 151f, centre.z - 37f)) * _settings.ColumnYaw;
                columnOutward[i] = Quaternion.AngleAxis(yaw, Vector3.up) * outward;
                streak[i] = AmbientHash.Unit(centre.x - 97f, centre.z + 61f) < _settings.StreakShare;
            }

            for (int i = 0; i < columns; i++)
            {
                for (int r = 0; r < rows; r++)
                {
                    float middle = (depths[r] + depths[r + 1]) * 0.5f;
                    Vector3 light = LightNormal(columnOutward[i], r, middle);
                    var band = new Band(r, middle / fall, alphas[r], alphas[r + 1], streak[i]);
                    AddQuad(grid[i, r], grid[i + 1, r], grid[i + 1, r + 1], grid[i, r + 1], light, band, (i + r) % 2 == 0);
                }
            }

            // The cheeks: from each side of the sheet back to the cliff, on every row. Row 0 starts on the edge itself, so its first
            // facet has no area and is dropped.
            Vector3 startFacing = -Direction(run, 0f);
            Vector3 endFacing = Direction(run, length);
            for (int r = 0; r < rows; r++)
            {
                float middle = (depths[r] + depths[r + 1]) * 0.5f;
                var band = new Band(r, middle / fall, alphas[r], alphas[r + 1], streak[0]);
                AddCheek(cliffStart[r], grid[0, r], grid[0, r + 1], cliffStart[r + 1], startFacing, LightNormal(startFacing, r, middle), band);

                band = new Band(r, middle / fall, alphas[r], alphas[r + 1], streak[columns - 1]);
                AddCheek(grid[columns, r], cliffEnd[r], cliffEnd[r + 1], grid[columns, r + 1], endFacing, LightNormal(endFacing, r, middle), band);
            }
        }

        /// <summary>The edge point at a distance along the run, at a given height: the cliff straight behind the sheet.</summary>
        private static Vector3 Behind(WaterfallRun run, float distance, float height)
        {
            Vector3 point = Sample(run, distance, out _);
            point.y = height;
            return point;
        }

        /// <summary>
        /// The light a facet takes: straight up on the brink's first row, like the pond, so the joint shows no tone step; turning
        /// toward the column's face over the brink, then the same all the way down, so each column reads as one broad face catching
        /// the light in its own way (a light that kept turning down the fall would step darker from row to row).
        /// </summary>
        private Vector3 LightNormal(Vector3 face, int row, float depth)
        {
            if (row == 0)
            {
                return Vector3.up;
            }

            float tilt = _settings.NormalTilt * Mathf.Clamp01(depth / (2f * _settings.BrinkRadius));
            return Vector3.Lerp(Vector3.up, face, tilt).normalized;
        }

        /// <summary>
        /// The opacity at a depth below the lip: opaque down to the fade start, then easing to fully clear at the foot, so the curtain
        /// has no lower edge to read. Never rises with depth.
        /// </summary>
        public static byte AlphaAt(WaterfallSettings settings, float depth)
        {
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.FadeStart * settings.FallDepth, settings.FallDepth, depth));
            return (byte)Mathf.RoundToInt(255f * (1f - fade));
        }

        /// <summary>The run's direction around a distance along it, averaged over a short span so a corner turns the sheet smoothly.</summary>
        private static Vector3 Direction(WaterfallRun run, float distance)
        {
            Vector3 before = Sample(run, distance - DirectionSpan, out Vector3 directionBefore);
            Vector3 after = Sample(run, distance + DirectionSpan, out _);
            Vector3 direction = after - before;
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : directionBefore;
        }

        private Vector3 Vertex(Vector3 point, Vector3 direction, float depth, float swell, int column, int row, int columns, int rows)
        {
            Vector3 outward = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 position = point + (outward * (Outward(_settings, depth) + swell)) + (Vector3.down * depth);

            // The lip row lies exactly on the edge, where the pond's surface ends, so there is no gap and no earth line at the joint.
            // Below it, inner vertices are nudged by a hash of the position, so the water is faceted and never a grid; the nudge grows
            // over the brink, which stays smooth.
            if (row > 0)
            {
                float grow = Mathf.Clamp01(depth / (2f * _settings.BrinkRadius));
                float shift = _settings.Jitter * _settings.ColumnWidth * grow;
                float along = column > 0 && column < columns ? Signed(AmbientHash.Unit(point.x + (row * 3.7f), point.z + 11f)) * shift : 0f;
                float vertical = Signed(AmbientHash.Unit(point.x - 5f, point.z + (row * 2.3f))) * _settings.RowHeight * _settings.Jitter * grow;
                float outwardShift = Signed(AmbientHash.Unit(point.x + 21f, point.z - (row * 1.9f))) * shift * 0.5f;

                // The foot is calm: a slight unevenness only, as it has faded to clear by then.
                if (row == rows)
                {
                    vertical = Signed(AmbientHash.Unit(point.x + 41f, point.z + 7f)) * _settings.FootJitter;
                }

                position += (direction * along) + (outward * outwardShift) + (Vector3.up * vertical);
            }

            return position;
        }

        /// <summary>One row of one column: what its facets share (row, share of the way down, alpha at its top and foot, streak).</summary>
        private readonly struct Band
        {
            public Band(int row, float share, byte alphaTop, byte alphaFoot, bool streak)
            {
                Row = row;
                Share = share;
                AlphaTop = alphaTop;
                AlphaFoot = alphaFoot;
                Streak = streak;
            }

            public int Row { get; }
            public float Share { get; }
            public byte AlphaTop { get; }
            public byte AlphaFoot { get; }
            public bool Streak { get; }
        }

        // a, b on the band's top row (walking along the run), c, d on its foot (c under b, d under a).
        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 light, Band band, bool diagonalAcross)
        {
            Vector3 outward = Vector3.Cross(b - a, Vector3.up).normalized;
            byte top = band.AlphaTop;
            byte foot = band.AlphaFoot;
            if (diagonalAcross)
            {
                AddFacet(a, b, c, outward, light, band, top, top, foot);
                AddFacet(a, c, d, outward, light, band, top, foot, foot);
            }
            else
            {
                AddFacet(a, b, d, outward, light, band, top, top, foot);
                AddFacet(b, c, d, outward, light, band, top, foot, foot);
            }
        }

        // a, b on the band's top row, c under b, d under a; facing points away from the sheet along the run.
        private void AddCheek(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Vector3 light, Band band)
        {
            AddFacet(a, b, c, facing, light, band, band.AlphaTop, band.AlphaTop, band.AlphaFoot);
            AddFacet(a, c, d, facing, light, band, band.AlphaTop, band.AlphaFoot, band.AlphaFoot);
        }

        private void AddFacet(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Vector3 light, Band band, byte alphaA, byte alphaB, byte alphaC)
        {
            Vector3 centre = (a + b + c) / 3f;
            float hash = AmbientHash.Unit(centre.x + 313f, centre.z - 313f);
            Color32 color = TerrainBuilder.ToVertexColor(FacetColor(band.Row, hash, band.Share, band.Streak));

            // The shader makes the tone flow down by the facet's row; the colour here is the tone it flows around. The alpha fades
            // the curtain out toward its foot, smoothly across the facets.
            _mesh.AddFall(a, b, c, facing, color, band.Row, hash, light, alphaA, alphaB, alphaC);
        }

        /// <summary>
        /// A facet's tone. The brink's first row is the pond's own tone where it meets the edge, with the pond's fixed per-facet
        /// spread, so the pond flows into the fall with no step. Below it the water breaks into white foam, turning pale blue by the
        /// foam end; some columns keep a whiter streak down their upper half. A light spread keeps the curtain faceted.
        /// </summary>
        private Color FacetColor(int row, float hash, float share, bool streak)
        {
            int level = Mathf.Min(ToneLevels - 1, (int)(hash * ToneLevels));
            float spread = ((level / (float)(ToneLevels - 1)) - 0.5f) * 2f;
            if (row == 0)
            {
                float pondGain = 1f + (spread * _palette.WaterToneVariation);
                return new Color(Mathf.Clamp01(_top.r * pondGain), Mathf.Clamp01(_top.g * pondGain), Mathf.Clamp01(_top.b * pondGain), 1f);
            }

            float blue = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, _settings.FoamEnd, share));
            Color color = Color.Lerp(_settings.FoamTone, _settings.FadeTone, blue);
            if (streak)
            {
                // The streak whitens the upper half and is gone by the middle of the fall.
                color = Color.Lerp(color, _settings.FoamTone, 0.6f * (1f - Mathf.Clamp01(share * 2f)));
            }

            float gain = 1f + (spread * _palette.WaterToneVariation * 0.5f);
            return new Color(Mathf.Clamp01(color.r * gain), Mathf.Clamp01(color.g * gain), Mathf.Clamp01(color.b * gain), 1f);
        }

        /// <summary>For tests: the tone of a facet in this row at this share of the way down, in a column without a streak.</summary>
        public static Color ToneAt(WaterfallSettings settings, PaletteSettings palette, Color top, int row, float hash, float share)
        {
            return new WaterfallBuilder(settings, palette) { _top = top }.FacetColor(row, hash, share, false);
        }

        private static float Signed(float unit)
        {
            return (unit * 2f) - 1f;
        }

        /// <summary>The point at a distance along the run (clamped to its ends), and the direction of the piece it lies on.</summary>
        private static Vector3 Sample(WaterfallRun run, float distance, out Vector3 direction)
        {
            List<Vector3> points = run.Points;
            float walked = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float piece = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= walked + piece || i == points.Count - 1)
                {
                    direction = (points[i] - points[i - 1]).normalized;
                    float t = piece > 0f ? Mathf.Clamp01((distance - walked) / piece) : 0f;
                    return Vector3.Lerp(points[i - 1], points[i], t);
                }

                walked += piece;
            }

            direction = Vector3.right;
            return points[0];
        }

        private readonly struct Piece
        {
            public Piece(Vector3 a, Vector3 b, Color tone)
            {
                A = a;
                B = b;
                WeightedTone = tone * Vector3.Distance(a, b);
            }

            public Vector3 A { get; }
            public Vector3 B { get; }

            /// <summary>The pond's tone at this piece times its length, summed and divided along a run.</summary>
            public Color WeightedTone { get; }

            public static int Compare(Piece p, Piece q)
            {
                int c = Centimetres(p.A.x).CompareTo(Centimetres(q.A.x));
                if (c != 0) return c;
                c = Centimetres(p.A.z).CompareTo(Centimetres(q.A.z));
                if (c != 0) return c;
                c = Centimetres(p.B.x).CompareTo(Centimetres(q.B.x));
                return c != 0 ? c : Centimetres(p.B.z).CompareTo(Centimetres(q.B.z));
            }

            private static int Centimetres(float value)
            {
                return Mathf.RoundToInt(value * 100f);
            }
        }
    }
}
