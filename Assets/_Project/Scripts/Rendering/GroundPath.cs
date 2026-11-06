using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// A polyline on the ground, in world X and Z (a <see cref="Vector2"/>'s y is world Z). Its first leg runs on
    /// backward and its last leg forward as straight lines, so every point of the ground projects onto it. Distances
    /// along it are arc lengths from the first point: negative before it, past <see cref="Length"/> after the last.
    /// </summary>
    public sealed class GroundPath
    {
        private readonly Vector2[] _points;
        private readonly Vector2[] _directions;
        private readonly float[] _starts;

        public GroundPath(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                throw new ArgumentException("A path needs at least one point.", nameof(points));
            }

            // Repeated points would make a leg with no direction.
            var kept = new List<Vector2> { points[0] };
            for (int i = 1; i < points.Count; i++)
            {
                if ((points[i] - kept[kept.Count - 1]).sqrMagnitude > 1e-8f)
                {
                    kept.Add(points[i]);
                }
            }

            _points = kept.ToArray();
            int legs = Mathf.Max(0, _points.Length - 1);
            _directions = new Vector2[legs];
            _starts = new float[_points.Length];
            for (int i = 0; i < legs; i++)
            {
                Vector2 leg = _points[i + 1] - _points[i];
                float length = leg.magnitude;
                _directions[i] = leg / length;
                _starts[i + 1] = _starts[i] + length;
            }

            Length = _starts[_points.Length - 1];
        }

        public IReadOnlyList<Vector2> Points => _points;

        /// <summary>How many straight legs it has. Zero for a single point.</summary>
        public int LegCount => _directions.Length;

        public float Length { get; }

        /// <summary>The direction of a leg, a unit vector.</summary>
        public Vector2 LegDirection(int leg) => _directions[leg];

        /// <summary>Where a leg starts, as a distance along the path.</summary>
        public float LegStart(int leg) => _starts[leg];

        /// <summary>The point this far along the path, on the extended first or last leg past either end.</summary>
        public Vector2 PointAt(float distance)
        {
            if (LegCount == 0)
            {
                return _points[0];
            }

            int leg = LegAt(distance);
            return _points[leg] + (_directions[leg] * (distance - _starts[leg]));
        }

        /// <summary>The direction of the leg this far along the path.</summary>
        public Vector2 DirectionAt(float distance)
        {
            return LegCount == 0 ? Vector2.right : _directions[LegAt(distance)];
        }

        /// <summary>
        /// The distance along the path of the path point nearest to <paramref name="point"/>, and how far to the side
        /// of it the point lies: positive to the left of the direction of travel, toward +Z on a path along +X.
        /// </summary>
        public float Project(Vector2 point, out float across)
        {
            if (LegCount == 0)
            {
                across = 0f;
                return 0f;
            }

            float bestDistance = float.MaxValue;
            float bestAlong = 0f;
            across = 0f;
            for (int i = 0; i < LegCount; i++)
            {
                Vector2 direction = _directions[i];
                Vector2 normal = Normal(direction);
                Vector2 fromStart = point - _points[i];
                float along = Vector2.Dot(fromStart, direction);
                float legLength = _starts[i + 1] - _starts[i];

                // Only the ends of the path run on: an inner corner has no extension.
                float limited = Mathf.Clamp(along, i == 0 ? float.MinValue : 0f, i == LegCount - 1 ? float.MaxValue : legLength);
                Vector2 off = fromStart - (direction * limited);
                float distance = off.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAlong = _starts[i] + limited;
                    float side = Vector2.Dot(off, normal);
                    across = limited == along ? side : Mathf.Sign(side) * off.magnitude;
                }
            }

            return bestAlong;
        }

        /// <summary>
        /// The path moved sideways by <paramref name="across"/> (positive to the left), between two distances measured
        /// along this path. The legs stay parallel to this path's, joined with mitred corners.
        /// </summary>
        public Vector2[] OffsetSlice(float from, float to, float across)
        {
            return OffsetSlice(from, to, Uniform(across));
        }

        /// <summary>
        /// The path with each leg moved sideways by its own offset (positive to the left), between two distances
        /// measured along this path. Each leg stays parallel to this path's; neighbours meet where their lines cross.
        /// </summary>
        public Vector2[] OffsetSlice(float from, float to, IReadOnlyList<float> legOffsets)
        {
            var slice = new List<Vector2> { OffsetAt(from, legOffsets) };
            for (int i = 1; i < _points.Length - 1; i++)
            {
                if (_starts[i] > from && _starts[i] < to)
                {
                    slice.Add(OffsetVertex(i, legOffsets));
                }
            }

            if (to > from)
            {
                slice.Add(OffsetAt(to, legOffsets));
            }

            return slice.ToArray();
        }

        /// <summary>The point this far along the path, moved sideways by each leg's offset.</summary>
        public Vector2 OffsetAt(float distance, IReadOnlyList<float> legOffsets)
        {
            if (LegCount == 0)
            {
                return _points[0];
            }

            int leg = LegAt(distance);
            Vector2 start = OffsetVertex(leg, legOffsets);
            if (leg == 0 && distance < 0f)
            {
                return start + (_directions[0] * distance);
            }

            if (leg == LegCount - 1 && distance > Length)
            {
                return OffsetVertex(leg + 1, legOffsets) + (_directions[leg] * (distance - Length));
            }

            float fraction = (distance - _starts[leg]) / (_starts[leg + 1] - _starts[leg]);
            return start + ((OffsetVertex(leg + 1, legOffsets) - start) * fraction);
        }

        /// <summary>The left-hand normal of a direction on the ground.</summary>
        public static Vector2 Normal(Vector2 direction) => new Vector2(-direction.y, direction.x);

        private float[] Uniform(float across)
        {
            var offsets = new float[Mathf.Max(1, LegCount)];
            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = across;
            }

            return offsets;
        }

        private Vector2 OffsetVertex(int index, IReadOnlyList<float> offsets)
        {
            if (index == 0)
            {
                return _points[0] + (Normal(_directions[0]) * offsets[0]);
            }

            if (index == _points.Length - 1)
            {
                return _points[index] + (Normal(_directions[index - 1]) * offsets[index - 1]);
            }

            Vector2 before = Normal(_directions[index - 1]);
            Vector2 after = Normal(_directions[index]);
            float first = offsets[index - 1];
            float second = offsets[index];
            if (first == second)
            {
                // A mitre: the sum of both normals, lengthened so each leg stays exactly that far away.
                return _points[index] + ((before + after) * (first / (1f + Vector2.Dot(before, after))));
            }

            // Where the two moved legs' lines cross.
            Vector2 a = _points[index] + (before * first);
            Vector2 b = _points[index] + (after * second);
            Vector2 u = _directions[index - 1];
            Vector2 v = _directions[index];
            float cross = (u.x * v.y) - (u.y * v.x);
            if (Mathf.Abs(cross) < 1e-6f)
            {
                return (a + b) * 0.5f;
            }

            Vector2 gap = b - a;
            float t = ((gap.x * v.y) - (gap.y * v.x)) / cross;
            return a + (u * t);
        }

        private int LegAt(float distance)
        {
            for (int i = 0; i < LegCount - 1; i++)
            {
                if (distance < _starts[i + 1])
                {
                    return i;
                }
            }

            return LegCount - 1;
        }
    }
}
