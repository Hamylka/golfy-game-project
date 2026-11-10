using System.Collections.Generic;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>Where a straight move of the ball's center first touches an obstacle.</summary>
    public readonly struct ChordHit
    {
        public ChordHit(float fraction, Vector3 normal, int index, PropMaterial material, bool isPole, bool fallThrough)
        {
            FallThrough = fallThrough;
            Fraction = fraction;
            Normal = normal;
            Index = index;
            Material = material;
            IsPole = isPole;
        }

        /// <summary>How far along the move, from 0 at its start to 1 at its end.</summary>
        public float Fraction { get; }

        /// <summary>The outward direction of the obstacle at the contact, unit length.</summary>
        public Vector3 Normal { get; }

        /// <summary>Which obstacle, so the simulation can leave it out of the next arc.</summary>
        public int Index { get; }

        public PropMaterial Material { get; }
        public bool IsPole { get; }

        /// <summary>Soft and off the ground, like a tree's crown: the ball goes on into it and falls out underneath.</summary>
        public bool FallThrough { get; }
    }

    /// <summary>
    /// Everything standing on a course that the ball can strike, in the course's local space: the props'
    /// shapes from the kit and, optionally, the flag pole, which counts in flight only.
    ///
    /// Each shape is grown by the ball's radius, so the ball becomes a point: its center. A move of that
    /// point is then tested against each grown shape exactly, by solving where the segment enters it, with
    /// no stepping inside the move, so even a fast ball can't pass through the thin pole between two steps.
    /// Only entries count: a segment that starts inside a shape ignores it, so a ball never sticks to a
    /// surface it already touches. (Growing an ellipsoid or a box by a radius doesn't give exactly an
    /// ellipsoid or a box: corners are sharp instead of rounded, a few millimetres at most. The shot and its
    /// preview use this same rule, so it never makes them disagree.)
    ///
    /// A uniform grid of 2 m cells over the shapes finds the few near a move. The order shapes are tested in
    /// never matters: the earliest entry wins, ties going to the lowest index. Not thread safe: it keeps a
    /// stamp per shape to test each once per query (the game has no threads, and WebGL none either).
    /// </summary>
    public sealed class BallObstacles
    {
        public const int None = -1;

        private const float CellSize = 2f;

        // Below this horizontal length, a normal is treated as pointing straight up or down.
        private const float FlatNormal = 0.05f;

        private readonly Body[] _bodies;
        private readonly List<int>[] _cells;
        private readonly int[] _stamps;
        private readonly int _cellsX;
        private readonly int _cellsZ;
        private readonly float _minX;
        private readonly float _minZ;
        private int _stamp;

        /// <param name="props">The props' shapes, as the kit made them.</param>
        /// <param name="ballRadius">The ball's radius in the simulation, which grows every shape.</param>
        /// <param name="pole">The flag pole, a cylinder, or null for none.</param>
        /// <param name="ground">The ground the props stand on, to tell a raised crown from a bush; null treats all foliage as bushes.</param>
        public BallObstacles(IReadOnlyList<PropShape> props, float ballRadius, PropShape? pole = null, IBallGround ground = null)
        {
            int count = props.Count + (pole.HasValue ? 1 : 0);
            _bodies = new Body[count];
            for (int i = 0; i < props.Count; i++)
            {
                _bodies[i] = new Body(props[i], ballRadius, false, ground);
            }

            if (pole.HasValue)
            {
                _bodies[count - 1] = new Body(pole.Value, ballRadius, true, null);
            }

            _stamps = new int[count];
            if (count == 0)
            {
                _cells = new List<int>[0];
                return;
            }

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (Body body in _bodies)
            {
                minX = Mathf.Min(minX, body.Min.x);
                minZ = Mathf.Min(minZ, body.Min.z);
                maxX = Mathf.Max(maxX, body.Max.x);
                maxZ = Mathf.Max(maxZ, body.Max.z);
            }

            _minX = minX;
            _minZ = minZ;
            _cellsX = Mathf.FloorToInt((maxX - minX) / CellSize) + 1;
            _cellsZ = Mathf.FloorToInt((maxZ - minZ) / CellSize) + 1;
            _cells = new List<int>[_cellsX * _cellsZ];

            for (int i = 0; i < count; i++)
            {
                CellRange(_bodies[i].Min, _bodies[i].Max, out int x0, out int z0, out int x1, out int z1);
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int cell = (z * _cellsX) + x;
                        (_cells[cell] ??= new List<int>()).Add(i);
                    }
                }
            }
        }

        /// <summary>A course's props and its flag pole, standing at the hole on the ball's ground.</summary>
        public static BallObstacles ForCourse(IReadOnlyList<PropShape> props, IBallGround ground, ShotSettings settings)
        {
            Vector2 hole = ground.Hole;
            var foot = new Vector3(hole.x, ground.Height(hole.x, hole.y), hole.y);
            PropShape pole = PropShape.Cylinder(foot, settings.PoleRadius, settings.PoleHeight, PropMaterial.Wood);
            return new BallObstacles(props, settings.BallRadius, pole, ground);
        }

        public int Count => _bodies.Length;

        /// <summary>
        /// The first obstacle the ball's center enters on its way from <paramref name="from"/> to
        /// <paramref name="to"/>. A rolling ball ignores the pole, and its normal lies flat on the ground,
        /// since the ball stays on it.
        /// </summary>
        public bool FirstHit(Vector3 from, Vector3 to, bool rolling, int ignore, out ChordHit hit)
        {
            hit = default;
            if (_bodies.Length == 0)
            {
                return false;
            }

            Vector3 min = Vector3.Min(from, to);
            Vector3 max = Vector3.Max(from, to);
            if (max.x < _minX || max.z < _minZ)
            {
                return false;
            }

            CellRange(min, max, out int x0, out int z0, out int x1, out int z1);
            if (x0 > x1 || z0 > z1)
            {
                return false;
            }

            _stamp++;
            Vector3 move = to - from;
            float best = float.MaxValue;
            int bestIndex = None;
            Vector3 bestNormal = Vector3.zero;

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    List<int> cell = _cells[(z * _cellsX) + x];
                    if (cell == null)
                    {
                        continue;
                    }

                    foreach (int i in cell)
                    {
                        if (_stamps[i] == _stamp || i == ignore)
                        {
                            continue;
                        }

                        _stamps[i] = _stamp;
                        Body body = _bodies[i];
                        if (rolling && body.IsPole)
                        {
                            continue;
                        }

                        if (!body.Entry(from, move, out float fraction, out Vector3 normal))
                        {
                            continue;
                        }

                        if (fraction < best || (fraction == best && i < bestIndex))
                        {
                            best = fraction;
                            bestIndex = i;
                            bestNormal = normal;
                        }
                    }
                }
            }

            if (bestIndex == None)
            {
                return false;
            }

            if (rolling)
            {
                bestNormal = FlatNormalAt(_bodies[bestIndex], from + (move * best), bestNormal, move);
            }

            hit = new ChordHit(best, bestNormal, bestIndex, _bodies[bestIndex].Material, _bodies[bestIndex].IsPole, _bodies[bestIndex].FallThrough);
            return true;
        }

        /// <summary>
        /// The contact normal laid flat. Striking the top of a low rock gives a normal pointing almost straight
        /// up; then the way from the shape's middle to the ball pushes it off, and failing that, straight back.
        /// </summary>
        private static Vector3 FlatNormalAt(Body body, Vector3 point, Vector3 normal, Vector3 move)
        {
            var flat = new Vector3(normal.x, 0f, normal.z);
            if (flat.magnitude >= FlatNormal)
            {
                return flat.normalized;
            }

            flat = new Vector3(point.x - body.Center.x, 0f, point.z - body.Center.z);
            if (flat.magnitude >= FlatNormal * 0.1f)
            {
                return flat.normalized;
            }

            return -new Vector3(move.x, 0f, move.z).normalized;
        }

        private void CellRange(Vector3 min, Vector3 max, out int x0, out int z0, out int x1, out int z1)
        {
            x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - _minX) / CellSize));
            z0 = Mathf.Max(0, Mathf.FloorToInt((min.z - _minZ) / CellSize));
            x1 = Mathf.Min(_cellsX - 1, Mathf.FloorToInt((max.x - _minX) / CellSize));
            z1 = Mathf.Min(_cellsZ - 1, Mathf.FloorToInt((max.z - _minZ) / CellSize));
        }

        /// <summary>One shape, grown by the ball's radius.</summary>
        private readonly struct Body
        {
            private readonly PropShapeKind _kind;
            private readonly Vector3 _extents;
            private readonly float _cos;
            private readonly float _sin;

            public Body(PropShape shape, float ballRadius, bool isPole, IBallGround ground)
            {
                // Room for the ball between the ground and the shape's bottom: a crown, not a bush sitting on the grass.
                FallThrough = shape.Material == PropMaterial.Foliage && ground != null &&
                    shape.Center.y - shape.Extents.y > ground.Height(shape.Center.x, shape.Center.z) + (2f * ballRadius);
                _kind = shape.Kind;
                Center = shape.Center;
                _extents = shape.Extents + (Vector3.one * ballRadius);
                _cos = Mathf.Cos(shape.Yaw);
                _sin = Mathf.Sin(shape.Yaw);
                Material = shape.Material;
                IsPole = isPole;

                Vector3 reach = _extents;
                if (_kind == PropShapeKind.Box)
                {
                    float ac = Mathf.Abs(_cos);
                    float asn = Mathf.Abs(_sin);
                    reach = new Vector3((ac * _extents.x) + (asn * _extents.z), _extents.y, (asn * _extents.x) + (ac * _extents.z));
                }

                Min = Center - reach;
                Max = Center + reach;
            }

            public Vector3 Center { get; }
            public Vector3 Min { get; }
            public Vector3 Max { get; }
            public PropMaterial Material { get; }
            public bool IsPole { get; }
            public bool FallThrough { get; }

            /// <summary>Where the segment from + s * move, s in [0, 1], enters the shape from outside.</summary>
            public bool Entry(Vector3 from, Vector3 move, out float fraction, out Vector3 normal)
            {
                Vector3 q = from - Center;
                switch (_kind)
                {
                    case PropShapeKind.Cylinder:
                        return CylinderEntry(q, move, out fraction, out normal);
                    case PropShapeKind.Ellipsoid:
                        return EllipsoidEntry(q, move, out fraction, out normal);
                    default:
                        return BoxEntry(q, move, out fraction, out normal);
                }
            }

            private bool CylinderEntry(Vector3 q, Vector3 d, out float fraction, out Vector3 normal)
            {
                fraction = 0f;
                normal = Vector3.zero;
                float radius = _extents.x;
                float half = _extents.y;

                float c = (q.x * q.x) + (q.z * q.z) - (radius * radius);
                if (c <= 0f && Mathf.Abs(q.y) <= half)
                {
                    return false;
                }

                if (!Slab(q.y, d.y, half, out float y0, out float y1))
                {
                    return false;
                }

                float r0, r1;
                float a = (d.x * d.x) + (d.z * d.z);
                if (a == 0f)
                {
                    if (c > 0f)
                    {
                        return false;
                    }

                    r0 = float.NegativeInfinity;
                    r1 = float.PositiveInfinity;
                }
                else
                {
                    float b = (q.x * d.x) + (q.z * d.z);
                    float disc = (b * b) - (a * c);
                    if (disc < 0f)
                    {
                        return false;
                    }

                    float root = Mathf.Sqrt(disc);
                    r0 = (-b - root) / a;
                    r1 = (-b + root) / a;
                }

                float enter = Mathf.Max(y0, r0);
                float leave = Mathf.Min(y1, r1);
                if (enter > leave || enter < 0f || enter > 1f)
                {
                    return false;
                }

                fraction = enter;
                if (y0 >= r0)
                {
                    normal = d.y < 0f ? Vector3.up : Vector3.down;
                }
                else
                {
                    Vector3 p = q + (d * enter);
                    normal = new Vector3(p.x, 0f, p.z).normalized;
                }

                return true;
            }

            private bool EllipsoidEntry(Vector3 q, Vector3 d, out float fraction, out Vector3 normal)
            {
                fraction = 0f;
                normal = Vector3.zero;
                var qs = new Vector3(q.x / _extents.x, q.y / _extents.y, q.z / _extents.z);
                var ds = new Vector3(d.x / _extents.x, d.y / _extents.y, d.z / _extents.z);

                float c = Vector3.Dot(qs, qs) - 1f;
                if (c <= 0f)
                {
                    return false;
                }

                float a = Vector3.Dot(ds, ds);
                if (a == 0f)
                {
                    return false;
                }

                float b = Vector3.Dot(qs, ds);
                float disc = (b * b) - (a * c);
                if (disc < 0f)
                {
                    return false;
                }

                float enter = (-b - Mathf.Sqrt(disc)) / a;
                if (enter < 0f || enter > 1f)
                {
                    return false;
                }

                fraction = enter;
                Vector3 p = q + (d * enter);
                normal = new Vector3(
                    p.x / (_extents.x * _extents.x),
                    p.y / (_extents.y * _extents.y),
                    p.z / (_extents.z * _extents.z)).normalized;
                return true;
            }

            private bool BoxEntry(Vector3 q, Vector3 d, out float fraction, out Vector3 normal)
            {
                fraction = 0f;
                normal = Vector3.zero;

                // Into the box's own axes: x along its yaw, z across it.
                var ql = new Vector3((q.x * _cos) + (q.z * _sin), q.y, (-q.x * _sin) + (q.z * _cos));
                var dl = new Vector3((d.x * _cos) + (d.z * _sin), d.y, (-d.x * _sin) + (d.z * _cos));
                if (Mathf.Abs(ql.x) <= _extents.x && Mathf.Abs(ql.y) <= _extents.y && Mathf.Abs(ql.z) <= _extents.z)
                {
                    return false;
                }

                if (!Slab(ql.x, dl.x, _extents.x, out float x0, out float x1) ||
                    !Slab(ql.y, dl.y, _extents.y, out float y0, out float y1) ||
                    !Slab(ql.z, dl.z, _extents.z, out float z0, out float z1))
                {
                    return false;
                }

                float enter = Mathf.Max(x0, Mathf.Max(y0, z0));
                float leave = Mathf.Min(x1, Mathf.Min(y1, z1));
                if (enter > leave || enter < 0f || enter > 1f)
                {
                    return false;
                }

                fraction = enter;
                Vector3 local;
                if (enter == x0)
                {
                    local = new Vector3(dl.x < 0f ? 1f : -1f, 0f, 0f);
                }
                else if (enter == y0)
                {
                    local = new Vector3(0f, dl.y < 0f ? 1f : -1f, 0f);
                }
                else
                {
                    local = new Vector3(0f, 0f, dl.z < 0f ? 1f : -1f);
                }

                // Back to the course's axes.
                normal = new Vector3((local.x * _cos) - (local.z * _sin), local.y, (local.x * _sin) + (local.z * _cos));
                return true;
            }

            /// <summary>The span of s where start + s * move lies within ±half on one axis.</summary>
            private static bool Slab(float start, float move, float half, out float enter, out float leave)
            {
                if (move == 0f)
                {
                    enter = float.NegativeInfinity;
                    leave = float.PositiveInfinity;
                    return Mathf.Abs(start) <= half;
                }

                float a = (-half - start) / move;
                float b = (half - start) / move;
                enter = Mathf.Min(a, b);
                leave = Mathf.Max(a, b);
                return true;
            }
        }
    }
}
