using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The bird's faceted mesh as a template (M4 PR 4.6): a small body, head, beak and tail that never move, and two wings in two parts
    /// (an inner and an outer one, the outer beating further) that the CPU rotates about the body axis each frame, so the wings read as
    /// flapping. Local space: +X forward, +Y up, 0.22 m long at scale 1. Palette vertex colours only: off-white and stone for the body and
    /// wings, tile for the beak, nothing black. Triangles are not shared (flat shading); wings and tail are double-sided, a top and a bottom
    /// triangle each, so they are seen from above and below whatever the culling.
    /// </summary>
    public sealed class BirdMeshKit
    {
        private readonly List<Vector3> _local = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<float> _weights = new List<float>();
        private readonly List<bool> _wing = new List<bool>();

        private BirdMeshKit()
        {
        }

        public int VertexCount => _local.Count;

        public IReadOnlyList<Vector3> Local => _local;

        public IReadOnlyList<Color32> Colors => _colors;

        public IReadOnlyList<bool> IsWing => _wing;

        /// <summary>Per vertex, how much of the flap angle a wing vertex takes: 0 at the shoulder, 0.5 at the joint, 1 at the tip.</summary>
        public IReadOnlyList<float> Weights => _weights;

        public static BirdMeshKit Build(PaletteSettings palette)
        {
            var kit = new BirdMeshKit();
            // Doves (director, PR 67; darker, PR 4.7): an off-white, slightly warm head and wing tops, a stone back, wing undersides and
            // tail, lighter stone wing tips, so the facets and the flap still read and a dove does not vanish against white clouds.
            // Existing palette entries only. The stone row moved one step darker in PR 4.7, so the stone entries here are the tones
            // the "darker" option showed on the phone (the old mid and dark).
            Color32 back = Tone(palette.StoneMid);
            Color32 belly = Tone(palette.TraceOffWhite);
            Color32 head = Tone(palette.TraceOffWhite);
            Color32 beak = Tone(palette.TileDark);
            Color32 wingTop = Tone(palette.TraceOffWhite);
            Color32 wingTipTone = Tone(palette.StoneLight);
            Color32 wingUnder = Tone(palette.StoneMid);
            Color32 tail = Tone(palette.StoneMid);

            // Body: a flattened octahedron.
            var nose = new Vector3(0.10f, 0f, 0f);
            var rear = new Vector3(-0.09f, 0.01f, 0f);
            var up = new Vector3(0f, 0.045f, 0f);
            var down = new Vector3(0f, -0.04f, 0f);
            var left = new Vector3(0f, 0f, 0.04f);
            var right = new Vector3(0f, 0f, -0.04f);
            kit.Solid(nose, up, left, back, Vector3.zero);
            kit.Solid(nose, right, up, back, Vector3.zero);
            kit.Solid(nose, left, down, belly, Vector3.zero);
            kit.Solid(nose, down, right, belly, Vector3.zero);
            kit.Solid(rear, left, up, back, Vector3.zero);
            kit.Solid(rear, up, right, back, Vector3.zero);
            kit.Solid(rear, down, left, belly, Vector3.zero);
            kit.Solid(rear, right, down, belly, Vector3.zero);

            // Head: a small octahedron on the nose, and a beak, a three-sided point.
            var centre = new Vector3(0.105f, 0.03f, 0f);
            var hFront = centre + (Vector3.right * 0.03f);
            var hBack = centre - (Vector3.right * 0.03f);
            var hUp = centre + (Vector3.up * 0.03f);
            var hDown = centre - (Vector3.up * 0.026f);
            var hLeft = centre + (Vector3.forward * 0.028f);
            var hRight = centre - (Vector3.forward * 0.028f);
            kit.Solid(hFront, hUp, hLeft, head, centre);
            kit.Solid(hFront, hRight, hUp, head, centre);
            kit.Solid(hFront, hLeft, hDown, head, centre);
            kit.Solid(hFront, hDown, hRight, head, centre);
            kit.Solid(hBack, hLeft, hUp, head, centre);
            kit.Solid(hBack, hUp, hRight, head, centre);
            kit.Solid(hBack, hDown, hLeft, head, centre);
            kit.Solid(hBack, hRight, hDown, head, centre);
            var tip = new Vector3(0.17f, 0.028f, 0f);
            var b0 = new Vector3(0.128f, 0.02f, 0f);
            var b1 = new Vector3(0.128f, 0.036f, 0.012f);
            var b2 = new Vector3(0.128f, 0.036f, -0.012f);
            var beakCentre = (tip + b0 + b1 + b2) * 0.25f;
            kit.Solid(tip, b1, b0, beak, beakCentre);
            kit.Solid(tip, b0, b2, beak, beakCentre);
            kit.Solid(tip, b2, b1, beak, beakCentre);

            // Tail: a flat wedge, two faces.
            var t0 = new Vector3(-0.08f, 0.012f, 0.015f);
            var t1 = new Vector3(-0.08f, 0.012f, -0.015f);
            var t2 = new Vector3(-0.17f, 0.012f, 0.035f);
            var t3 = new Vector3(-0.17f, 0.012f, -0.035f);
            kit.Flat(t0, t2, t3, tail, tail, false, 0f, 0f, 0f);
            kit.Flat(t0, t3, t1, tail, tail, false, 0f, 0f, 0f);

            // Wings, left then mirrored: shoulder front and back, then a joint (inner, weight 0.5) and a tip (weight 1). They sit on
            // the body's top, so folded they lie along the back.
            for (int side = 1; side >= -1; side -= 2)
            {
                var sFront = new Vector3(0.04f, 0.04f, 0.02f * side);
                var sBack = new Vector3(-0.06f, 0.035f, 0.02f * side);
                var jointFront = new Vector3(0.02f, 0.04f, 0.15f * side);
                var jointBack = new Vector3(-0.07f, 0.04f, 0.15f * side);
                var wingTip = new Vector3(-0.03f, 0.04f, 0.30f * side);
                kit.Flat(sFront, jointFront, jointBack, wingTop, wingUnder, true, 0f, 0.5f, 0.5f);
                kit.Flat(sFront, jointBack, sBack, wingTop, wingUnder, true, 0f, 0.5f, 0f);
                kit.Flat(jointFront, wingTip, jointBack, wingTipTone, wingUnder, true, 0.5f, 1f, 0.5f);
            }

            return kit;
        }

        /// <summary>
        /// Turns the template into <paramref name="output"/> at <paramref name="offset"/>: wings folded or spread and rotated about the body
        /// axis, then the whole bird scaled, turned to its yaw and pitch, and moved to its position.
        /// </summary>
        public void Apply(in BirdPose pose, float scale, Vector3[] output, int offset)
        {
            float sin = Mathf.Sin(pose.Flap);
            float cos = Mathf.Cos(pose.Flap);
            float span = Mathf.Lerp(0.3f, 1f, pose.Spread);
            Quaternion turn = Quaternion.Euler(0f, -pose.YawDegrees, 0f) * Quaternion.Euler(0f, 0f, pose.PitchDegrees);
            float size = scale * pose.Scale;

            for (int i = 0; i < _local.Count; i++)
            {
                Vector3 v = _local[i];
                if (_wing[i])
                {
                    float side = v.z < 0f ? -1f : 1f;
                    float d = Mathf.Abs(v.z) * span;
                    float w = _weights[i];

                    // Rotate (height, distance from the body) by the flap angle, the more the further from the shoulder.
                    float angle = pose.Flap * w;
                    float s = angle == pose.Flap ? sin : Mathf.Sin(angle);
                    float c = angle == pose.Flap ? cos : Mathf.Cos(angle);
                    float y = (v.y * c) + (d * s);
                    float d2 = (d * c) - (v.y * s);
                    v = new Vector3(v.x, y, side * d2);
                }

                output[offset + i] = pose.Position + (turn * (v * size));
            }
        }

        private static Color32 Tone(Color color) => TerrainBuilder.ToVertexColor(color);

        // A triangle with its normal pointing away from `inside`.
        private void Solid(Vector3 a, Vector3 b, Vector3 c, Color32 color, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;
            if (Vector3.Dot(normal, centroid - inside) < 0f)
            {
                (b, c) = (c, b);
            }

            Push(a, b, c, color, false, 0f, 0f, 0f);
        }

        // A thin double-sided triangle: the face looking up, then the one looking down.
        private void Flat(Vector3 a, Vector3 b, Vector3 c, Color32 top, Color32 under, bool wing, float wa, float wb, float wc)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f)
            {
                (b, c) = (c, b);
                (wb, wc) = (wc, wb);
            }

            Push(a, b, c, top, wing, wa, wb, wc);
            Push(a, c, b, under, wing, wa, wc, wb);
        }

        private void Push(Vector3 a, Vector3 b, Vector3 c, Color32 color, bool wing, float wa, float wb, float wc)
        {
            _local.Add(a);
            _local.Add(b);
            _local.Add(c);
            for (int i = 0; i < 3; i++)
            {
                _colors.Add(color);
                _wing.Add(wing);
            }

            _weights.Add(wa);
            _weights.Add(wb);
            _weights.Add(wc);
        }
    }
}
