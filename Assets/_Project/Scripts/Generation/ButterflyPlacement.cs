using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>One butterfly: the loop it flies round a bush, and which of the two tones it wears.</summary>
    public readonly struct ButterflySpec
    {
        public ButterflySpec(Vector3 centre, float radius, float speed, float phase, float rise, bool yellow)
        {
            Centre = centre;
            Radius = radius;
            Speed = speed;
            Phase = phase;
            Rise = rise;
            Yellow = yellow;
        }

        public Vector3 Centre { get; }
        public float Radius { get; }
        public float Speed { get; }
        public float Phase { get; }
        public float Rise { get; }
        public bool Yellow { get; }

        /// <summary>Where the butterfly is at this time: a Lissajous loop (two unrelated rates, so it never repeats too evenly) round its centre.</summary>
        public Vector3 PositionAt(float time)
        {
            float t = (time * Speed) + Phase;
            return Centre + new Vector3(Mathf.Cos(t) * Radius, Mathf.Sin(t * 1.7f) * Rise, Mathf.Sin(t * 1.3f) * Radius);
        }

        /// <summary>The horizontal direction of travel at this time.</summary>
        public Vector3 HeadingAt(float time)
        {
            Vector3 now = PositionAt(time);
            Vector3 next = PositionAt(time + 0.05f);
            Vector3 heading = new Vector3(next.x - now.x, 0f, next.z - now.z);
            return heading.sqrMagnitude < 1e-8f ? Vector3.right : heading.normalized;
        }
    }

    /// <summary>
    /// Picks a course's butterflies (M4 PR 4.7). View-time only, never part of <see cref="CourseLayout"/> or the checks, so no prop,
    /// fingerprint or shot changes. Own random stream, salt 10 (taken: 6 surfaces, 7 props, 8 fairway shapes, 9 birds). Every choice is an
    /// integer draw, so the same seed gives the same butterflies on every device. A course with no bush has none.
    /// </summary>
    public static class ButterflyPlacement
    {
        public const int StreamSalt = 10;

        public static List<ButterflySpec> Place(CourseLayout layout, int seed, int par, int variant, ButterflySettings settings)
        {
            var bushes = new List<Vector3>();
            foreach (CourseProp prop in layout.Props)
            {
                if (prop.Kind == PropKind.Bush)
                {
                    bushes.Add(prop.Root);
                }
            }

            var result = new List<ButterflySpec>();
            if (bushes.Count == 0)
            {
                return result;
            }

            var random = new SeededRandom(unchecked((int)CourseNoise.Hash(unchecked((int)CourseNoise.Hash(seed, par, variant)), StreamSalt, 0)));
            int wanted = random.Int(settings.Count.x, settings.Count.y);
            int count = Mathf.Min(bushes.Count, wanted);
            for (int i = 0; i < count; i++)
            {
                int pick = random.Int(0, bushes.Count - 1);
                Vector3 centre = bushes[pick] + (Vector3.up * settings.LoopHeight);
                bushes.RemoveAt(pick);
                result.Add(new ButterflySpec(
                    centre,
                    Lerp(settings.LoopRadius, random.Int(0, 1000)),
                    Lerp(settings.LoopSpeed, random.Int(0, 1000)),
                    random.Int(0, 627) * 0.01f,
                    Lerp(settings.Rise, random.Int(0, 1000)),
                    random.Coin()));
            }

            return result;
        }

        private static float Lerp(Vector2 range, int thousandths)
        {
            return range.x + ((range.y - range.x) * (thousandths / 1000f));
        }
    }
}
