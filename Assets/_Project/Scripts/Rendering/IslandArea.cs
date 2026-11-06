using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>A straight piece of an island's edge on the ground (x is world X, y is world Z).</summary>
    public readonly struct GroundSegment
    {
        public GroundSegment(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }

        public Vector2 A { get; }
        public Vector2 B { get; }
    }

    /// <summary>
    /// Where the view's centre may go: over the course's islands, or within a margin of them, or within that margin of
    /// the course's centreline. The centreline crosses every gap between islands, so a gap wider than twice the margin
    /// still leaves a way across. The shape is the same at every zoom and heading, and the nearest allowed point is
    /// found exactly, so a pan sliding along a limit stays smooth.
    /// </summary>
    public sealed class IslandArea
    {
        private readonly GroundSegment[] _edges;
        private readonly Vector2[] _centreline;
        private readonly float _margin;

        /// <param name="edges">Every piece of every island's edge, in any order. Together they must close each island.</param>
        /// <param name="centreline">The course's centreline, tee to hole. May be null or empty.</param>
        /// <param name="margin">How far past the islands and the centreline the view's centre may go, in metres.</param>
        public IslandArea(IReadOnlyList<GroundSegment> edges, IReadOnlyList<Vector2> centreline, float margin)
        {
            _edges = new GroundSegment[edges?.Count ?? 0];
            for (int i = 0; i < _edges.Length; i++)
            {
                _edges[i] = edges[i];
            }

            _centreline = new Vector2[centreline?.Count ?? 0];
            for (int i = 0; i < _centreline.Length; i++)
            {
                _centreline[i] = centreline[i];
            }

            _margin = Mathf.Max(0f, margin);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (GroundSegment edge in _edges)
            {
                min = Vector2.Min(min, Vector2.Min(edge.A, edge.B));
                max = Vector2.Max(max, Vector2.Max(edge.A, edge.B));
            }

            foreach (Vector2 point in _centreline)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            IslandBounds = _edges.Length > 0 ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
            Bounds = _edges.Length > 0 || _centreline.Length > 0
                ? Rect.MinMaxRect(min.x - _margin, min.y - _margin, max.x + _margin, max.y + _margin)
                : default;
        }

        /// <summary>A rectangle's outline as an area, for a box island or a test.</summary>
        public static IslandArea FromRect(Rect rect, float margin = 0f)
        {
            var corners = new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
            var edges = new GroundSegment[4];
            for (int i = 0; i < 4; i++)
            {
                edges[i] = new GroundSegment(corners[i], corners[(i + 1) % 4]);
            }

            return new IslandArea(edges, null, margin);
        }

        public float Margin => _margin;

        /// <summary>The islands' bounding rectangle, without the margin.</summary>
        public Rect IslandBounds { get; }

        /// <summary>The allowed area's bounding rectangle, margin included.</summary>
        public Rect Bounds { get; }

        /// <summary>True if the point is on an island. Counts edge crossings, so the edges may come in any order.</summary>
        public bool OnIsland(Vector2 point)
        {
            if (!IslandBounds.Contains(point))
            {
                return false;
            }

            bool inside = false;
            foreach (GroundSegment edge in _edges)
            {
                Vector2 a = edge.A, b = edge.B;
                if ((a.y > point.y) != (b.y > point.y))
                {
                    float x = a.x + ((point.y - a.y) / (b.y - a.y) * (b.x - a.x));
                    if (point.x < x)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        public bool Contains(Vector2 point)
        {
            return Nearest(point) == point;
        }

        /// <summary>The allowed point nearest to this one: the point itself if it's allowed.</summary>
        public Vector2 Nearest(Vector2 point)
        {
            if (_edges.Length == 0 && _centreline.Length == 0)
            {
                return point;
            }

            if (OnIsland(point))
            {
                return point;
            }

            // Off the islands, the allowed area is everything within the margin of the islands or the centreline, so the
            // nearest allowed point lies toward the nearest point of either, at the margin's distance from it.
            Vector2 nearest = point;
            float best = float.MaxValue;
            foreach (GroundSegment edge in _edges)
            {
                Consider(NearestOnSegment(point, edge.A, edge.B), point, ref nearest, ref best);
            }

            if (_centreline.Length == 1)
            {
                Consider(_centreline[0], point, ref nearest, ref best);
            }

            for (int i = 1; i < _centreline.Length; i++)
            {
                Consider(NearestOnSegment(point, _centreline[i - 1], _centreline[i]), point, ref nearest, ref best);
            }

            float distance = Mathf.Sqrt(best);
            if (distance <= _margin)
            {
                return point;
            }

            return nearest + ((point - nearest) * (_margin / distance));
        }

        private static void Consider(Vector2 candidate, Vector2 point, ref Vector2 nearest, ref float best)
        {
            float distance = (candidate - point).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = candidate;
            }
        }

        private static Vector2 NearestOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 leg = b - a;
            float length = leg.sqrMagnitude;
            float t = length > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, leg) / length) : 0f;
            return a + (leg * t);
        }
    }
}
