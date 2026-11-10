using System.Collections.Generic;
using IsoGolf.Gameplay;
using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>Where the aim preview's dots go. It only places them, along a path the simulation has already worked out.</summary>
    public static class DottedPath
    {
        /// <summary>
        /// Dots along a flight, with equal ground distance between them, lifted to where the ball's center will
        /// pass. The speed along the ground is constant in flight, so equal distances are also equal times. The
        /// last dot keeps half a spacing clear of the landing, where the marker sits.
        /// </summary>
        public static void AlongArc(BallisticArc arc, ArcLanding landing, float spacing, float lift, List<Vector3> dots)
        {
            AlongArc(arc, landing.Time, spacing, lift, dots);
        }

        /// <summary>The same, up to any end of the flight: a landing, or a hit on an obstacle, whose mark sits there.</summary>
        public static void AlongArc(BallisticArc arc, float endTime, float spacing, float lift, List<Vector3> dots)
        {
            dots.Clear();

            Vector3 velocity = arc.Velocity;
            float groundSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            if (groundSpeed <= 0f)
            {
                return;
            }

            float interval = spacing / groundSpeed;
            float last = endTime - (interval * 0.5f);
            for (int i = 1; i * interval <= last; i++)
            {
                dots.Add(arc.PositionAt(i * interval) + (Vector3.up * lift));
            }
        }

        /// <summary>
        /// Dots along the first share of a path, by distance on the ground, the first one a spacing from its start.
        /// A short path whose share is under one spacing still gets that first dot, if the path reaches it: the
        /// lightest putts would otherwise show no direction at all. It's a little past the share, but still on the
        /// true path, so it can't lie.
        /// </summary>
        public static void AlongPath(List<Vector3> path, float share, float spacing, List<Vector3> dots)
        {
            dots.Clear();

            float total = GroundLength(path);
            float shown = Mathf.Min(Mathf.Max(total * share, spacing), total);

            float next = spacing;
            float travelled = 0f;
            for (int i = 1; i < path.Count && next <= shown; i++)
            {
                float length = GroundDistance(path[i - 1], path[i]);
                while (next <= travelled + length && next <= shown)
                {
                    dots.Add(Vector3.Lerp(path[i - 1], path[i], (next - travelled) / length));
                    next += spacing;
                }

                travelled += length;
            }
        }

        private static float GroundLength(List<Vector3> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                total += GroundDistance(path[i - 1], path[i]);
            }

            return total;
        }

        private static float GroundDistance(Vector3 from, Vector3 to)
        {
            return new Vector2(to.x - from.x, to.z - from.z).magnitude;
        }
    }
}
