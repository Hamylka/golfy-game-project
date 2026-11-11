using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Mesh helpers shared by every procedural prop: prism, cone, box with slight jitter, gable roof,
    /// jittered blob and block wall. Each writes flat-shaded triangles with one palette vertex colour per
    /// triangle into a <see cref="TerrainMeshData"/>, like the terrain and the tufts, so a whole island of
    /// props is one mesh on the land material. Every random choice comes from the
    /// <see cref="System.Random"/> the caller passes, so the same seed makes the same prop on every device.
    /// </summary>
    public sealed class PropMeshKit
    {
        private float _swayBase;
        private float _swaySpan;

        public PropMeshKit(TerrainMeshData mesh)
        {
            Mesh = mesh;
        }

        public TerrainMeshData Mesh { get; }

        /// <summary>
        /// From now on, vertices get a sway weight (UV1.x) that rises from 0 at <paramref name="baseY"/> to 1
        /// <paramref name="span"/> metres above it. Foliage turns it on; trunks, rocks and walls leave it off.
        /// </summary>
        public void SetSway(float baseY, float span)
        {
            _swayBase = baseY;
            _swaySpan = span;
        }

        public void ClearSway()
        {
            _swaySpan = 0f;
        }

        /// <summary>Unit vector on the ground plane at this angle, x then z.</summary>
        public static Vector3 Around(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        public static float Next(System.Random random) => (float)random.NextDouble();

        public static float RandomAngle(System.Random random) => Next(random) * Mathf.PI * 2f;

        public static bool SameColor(Color32 x, Color32 y) => x.r == y.r && x.g == y.g && x.b == y.b;

        private float SwayAt(Vector3 point)
        {
            return _swaySpan > 0f ? Mathf.Clamp01((point.y - _swayBase) / _swaySpan) : 0f;
        }

        /// <summary>One flat triangle turned to face along <paramref name="facing"/>, with the current sway weights.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color32 color)
        {
            if (_swaySpan > 0f)
            {
                Mesh.Add(a, b, c, facing, color, SwayAt(a), SwayAt(b), SwayAt(c));
            }
            else
            {
                Mesh.Add(a, b, c, facing, color);
            }
        }

        /// <summary>
        /// An upright prism with no caps. Its top is <paramref name="height"/> above <paramref name="root"/>;
        /// its foot is <paramref name="sink"/> lower, because the ground mesh only follows the height field
        /// at its corners.
        /// </summary>
        public void AddPrism(Vector3 root, float radius, float height, int sides, float startAngle, Color32 color, float sink)
        {
            Vector3 top = root + (Vector3.up * height);
            root += Vector3.down * sink;

            for (int i = 0; i < sides; i++)
            {
                float a0 = startAngle + (i * Mathf.PI * 2f / sides);
                float a1 = startAngle + ((i + 1) * Mathf.PI * 2f / sides);
                Vector3 outward = Around((a0 + a1) * 0.5f);
                Vector3 low0 = root + (Around(a0) * radius);
                Vector3 low1 = root + (Around(a1) * radius);
                Vector3 high0 = top + (Around(a0) * radius);
                Vector3 high1 = top + (Around(a1) * radius);
                AddTriangle(low0, high0, high1, outward, color);
                AddTriangle(low0, high1, low1, outward, color);
            }
        }

        /// <summary>A cone with its tip straight above the base's middle, optionally closed underneath.</summary>
        public void AddCone(Vector3 baseCenter, float radius, float height, int sides, float startAngle, Color32 color, bool closeBase)
        {
            Vector3 tip = baseCenter + (Vector3.up * height);

            for (int i = 0; i < sides; i++)
            {
                float a0 = startAngle + (i * Mathf.PI * 2f / sides);
                float a1 = startAngle + ((i + 1) * Mathf.PI * 2f / sides);
                Vector3 foot0 = baseCenter + (Around(a0) * radius);
                Vector3 foot1 = baseCenter + (Around(a1) * radius);
                AddTriangle(foot0, tip, foot1, Around((a0 + a1) * 0.5f), color);

                if (closeBase)
                {
                    AddTriangle(foot0, foot1, baseCenter, Vector3.down, color);
                }
            }
        }

        /// <summary>
        /// An upright tapered prism with no caps: <paramref name="bottomRadius"/> at the root (<paramref name="sink"/> lower),
        /// <paramref name="topRadius"/> at <paramref name="height"/> above it. Lighthouse tower and gallery.
        /// </summary>
        public void AddFrustum(Vector3 root, float bottomRadius, float topRadius, float height, int sides, float startAngle, Color32 color, float sink)
        {
            Vector3 top = root + (Vector3.up * height);
            root += Vector3.down * sink;

            for (int i = 0; i < sides; i++)
            {
                float a0 = startAngle + (i * Mathf.PI * 2f / sides);
                float a1 = startAngle + ((i + 1) * Mathf.PI * 2f / sides);
                Vector3 outward = Around((a0 + a1) * 0.5f);
                Vector3 low0 = root + (Around(a0) * bottomRadius);
                Vector3 low1 = root + (Around(a1) * bottomRadius);
                Vector3 high0 = top + (Around(a0) * topRadius);
                Vector3 high1 = top + (Around(a1) * topRadius);
                AddTriangle(low0, high0, high1, outward, color);
                AddTriangle(low0, high1, low1, outward, color);
            }
        }

        /// <summary>
        /// A flat disc standing on its edge: the wheel of a bicycle. It lies in the plane spanned by <paramref name="along"/>
        /// (horizontal, unit) and up, with <paramref name="sides"/> corners, both faces drawn.
        /// </summary>
        public void AddUprightDisc(Vector3 center, Vector3 along, float radius, int sides, Color32 color)
        {
            Vector3 face = new Vector3(-along.z, 0f, along.x);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = center + (along * (Mathf.Cos(a0) * radius)) + (Vector3.up * (Mathf.Sin(a0) * radius));
                Vector3 p1 = center + (along * (Mathf.Cos(a1) * radius)) + (Vector3.up * (Mathf.Sin(a1) * radius));
                AddTriangle(center, p0, p1, face, color);
                AddTriangle(center, p1, p0, -face, color);
            }
        }

        /// <summary>
        /// A flat ring standing on its edge (a tyre): like <see cref="AddUprightDisc"/> with a hole of <paramref name="innerRadius"/>,
        /// both faces drawn.
        /// </summary>
        public void AddUprightRing(Vector3 center, Vector3 along, float outerRadius, float innerRadius, int sides, Color32 color)
        {
            Vector3 face = new Vector3(-along.z, 0f, along.x);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = (along * Mathf.Cos(a0)) + (Vector3.up * Mathf.Sin(a0));
                Vector3 d1 = (along * Mathf.Cos(a1)) + (Vector3.up * Mathf.Sin(a1));
                Vector3 o0 = center + (d0 * outerRadius);
                Vector3 o1 = center + (d1 * outerRadius);
                Vector3 i0 = center + (d0 * innerRadius);
                Vector3 i1 = center + (d1 * innerRadius);
                AddTriangle(o0, o1, i1, face, color);
                AddTriangle(o0, i1, i0, face, color);
                AddTriangle(o0, i1, o1, -face, color);
                AddTriangle(o0, i0, i1, -face, color);
            }
        }

        /// <summary>
        /// A box turned by <paramref name="yaw"/> about +Y (its x axis along <see cref="Around"/>), with every
        /// corner moved by up to <paramref name="jitter"/> metres on each axis, so no two read as machined. With
        /// no <paramref name="random"/> the corners stay put. The underside is left out when it sits on the ground.
        /// </summary>
        public void AddBox(Vector3 center, Vector3 halfSize, float yaw, float jitter, System.Random random, Color32 color, bool skipBottom)
        {
            Vector3 right = Around(yaw);
            Vector3 forward = new Vector3(-right.z, 0f, right.x);
            var corners = new Vector3[8];

            // Corner index bits: 1 = +x, 2 = +y, 4 = +z.
            for (int i = 0; i < 8; i++)
            {
                float sx = (i & 1) != 0 ? 1f : -1f;
                float sy = (i & 2) != 0 ? 1f : -1f;
                float sz = (i & 4) != 0 ? 1f : -1f;
                Vector3 corner = center + (right * (sx * halfSize.x)) + (Vector3.up * (sy * halfSize.y)) + (forward * (sz * halfSize.z));

                if (random != null && jitter > 0f)
                {
                    corner += new Vector3(Signed(random), Signed(random), Signed(random)) * jitter;
                }

                corners[i] = corner;
            }

            AddQuad(corners[1], corners[5], corners[7], corners[3], center, color); // +x
            AddQuad(corners[0], corners[2], corners[6], corners[4], center, color); // -x
            AddQuad(corners[2], corners[3], corners[7], corners[6], center, color); // +y
            AddQuad(corners[0], corners[1], corners[3], corners[2], center, color); // -z
            AddQuad(corners[4], corners[5], corners[7], corners[6], center, color); // +z

            if (!skipBottom)
            {
                AddQuad(corners[0], corners[1], corners[5], corners[4], center, color); // -y
            }
        }

        /// <summary>
        /// A roof with two slopes and two gable ends, its ridge along the local x axis. <paramref name="length"/>
        /// and <paramref name="width"/> are the eaves' rectangle, so the caller adds any overhang. The second
        /// slope takes <paramref name="shade"/>, so a tiled roof reads as two planes. Used by the built props of M4 PR 4.4.
        /// </summary>
        public void AddGableRoof(Vector3 eaveCenter, float length, float width, float ridgeHeight, float yaw, Color32 color, Color32 shade)
        {
            Vector3 right = Around(yaw);
            Vector3 forward = new Vector3(-right.z, 0f, right.x);
            float hl = length * 0.5f;
            float hw = width * 0.5f;
            Vector3 up = Vector3.up * ridgeHeight;
            Vector3 e00 = eaveCenter - (right * hl) - (forward * hw);
            Vector3 e10 = eaveCenter + (right * hl) - (forward * hw);
            Vector3 e11 = eaveCenter + (right * hl) + (forward * hw);
            Vector3 e01 = eaveCenter - (right * hl) + (forward * hw);
            Vector3 r0 = eaveCenter - (right * hl) + up;
            Vector3 r1 = eaveCenter + (right * hl) + up;
            Vector3 middle = eaveCenter + (up * 0.5f);

            AddQuad(e00, e10, r1, r0, middle, color);
            AddQuad(e11, e01, r0, r1, middle, shade);
            AddTriangle(e00, e01, r0, -right, color);
            AddTriangle(e10, r1, e11, right, color);
        }

        /// <summary>
        /// A jittered ball: an icosahedron split <paramref name="subdivisions"/> times (0 gives 20 facets, 1 gives
        /// 80), each corner moved in or out by up to <paramref name="jitter"/> of the radius, and squashed on y.
        /// Facets take <paramref name="main"/>, or <paramref name="alt"/> with <paramref name="altChance"/>;
        /// facets facing steeply up take <paramref name="top"/> when one is given. One random number is spent per
        /// corner then one per facet whatever the shades, so changing a shade never moves the shape.
        /// </summary>
        public void AddBlob(Vector3 center, float radius, float squash, float jitter, int subdivisions, Color32 main, Color32 alt, float altChance, System.Random random, Color32? top)
        {
            var corners = new List<Vector3>(Icosahedron.Corners.Length);
            foreach (Vector3 corner in Icosahedron.Corners)
            {
                corners.Add(corner.normalized);
            }

            var faces = new List<int>(Icosahedron.Faces);

            for (int pass = 0; pass < subdivisions; pass++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(faces.Count * 4);

                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i];
                    int b = faces[i + 1];
                    int c = faces[i + 2];
                    int ab = Midpoint(a, b, corners, midpoints);
                    int bc = Midpoint(b, c, corners, midpoints);
                    int ca = Midpoint(c, a, corners, midpoints);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                faces = next;
            }

            // Corners move once each and are shared by their faces, so the blob stays closed.
            for (int i = 0; i < corners.Count; i++)
            {
                float length = radius * (1f + (Signed(random) * jitter));
                Vector3 corner = corners[i] * length;
                corner.y *= squash;
                corners[i] = center + corner;
            }

            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = corners[faces[i]];
                Vector3 b = corners[faces[i + 1]];
                Vector3 c = corners[faces[i + 2]];
                Vector3 outward = ((a + b + c) / 3f) - center;
                Color32 color = Next(random) < altChance ? alt : main;

                if (top.HasValue && outward.y > outward.magnitude * BlobTopFacing)
                {
                    color = top.Value;
                }

                AddTriangle(a, b, c, outward, color);
            }
        }

        // A facet counts as facing up when its direction is within about 53 degrees of vertical.
        private const float BlobTopFacing = 0.6f;

        /// <summary>
        /// A straight wall of rectangular blocks from <paramref name="start"/> to <paramref name="end"/> (ground
        /// points: the wall's height above them is <c>courses * blockHeight</c>), laid in courses with each row's
        /// joints offset half a block from the one below. A block's tone is picked on its own, so the wall reads as
        /// stones, not one slab. The bottom course reaches <paramref name="sink"/> below the ground line, and the
        /// ground line slopes straight between the two points: split a wall on uneven ground into short pieces.
        /// </summary>
        public void AddBlockWall(Vector3 start, Vector3 end, int courses, float blockHeight, float blockLength, float lengthVariation, float thickness, float jitter, float sink, Color32 light, Color32 mid, Color32 dark, System.Random random)
        {
            Vector3 flat = new Vector3(end.x - start.x, 0f, end.z - start.z);
            float total = flat.magnitude;
            if (total < 0.01f)
            {
                return;
            }

            Vector3 along = flat / total;
            float yaw = Mathf.Atan2(along.z, along.x);

            for (int course = 0; course < courses; course++)
            {
                // Every other course starts with half a block, so joints never line up.
                float t = 0f;
                float firstLength = (course % 2 == 1) ? blockLength * 0.5f : 0f;

                while (t < total - 0.001f)
                {
                    float length = firstLength > 0f ? firstLength : blockLength * (1f + (Signed(random) * lengthVariation));
                    firstLength = 0f;

                    // A leftover too short to be a stone joins the last block.
                    if (total - (t + length) < blockLength * 0.4f)
                    {
                        length = total - t;
                    }

                    length = Mathf.Min(length, total - t);
                    float mid01 = (t + (length * 0.5f)) / total;
                    float groundY = Mathf.Lerp(start.y, end.y, mid01);
                    float bottom = groundY + (course * blockHeight) - (course == 0 ? sink : 0f);
                    float top = groundY + ((course + 1) * blockHeight);
                    Vector3 center = new Vector3(
                        start.x + (along.x * (t + (length * 0.5f))),
                        (bottom + top) * 0.5f,
                        start.z + (along.z * (t + (length * 0.5f))));
                    var half = new Vector3(length * 0.5f, (top - bottom) * 0.5f, thickness * 0.5f);

                    float pick = Next(random);
                    Color32 color = pick < 0.35f ? light : (pick < 0.8f ? mid : dark);
                    AddBox(center, half, yaw, jitter, random, color, course == 0);
                    t += length;
                }
            }
        }

        private static float Signed(System.Random random) => (Next(random) * 2f) - 1f;

        // Corners go round the face, in either order: Add turns the triangle to face away from the prop's middle.
        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 propCenter, Color32 color)
        {
            Vector3 facing = ((a + b + c + d) * 0.25f) - propCenter;
            AddTriangle(a, b, c, facing, color);
            AddTriangle(a, c, d, facing, color);
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
    }
}
