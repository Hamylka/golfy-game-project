using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>One bird's perch and the seeds of its fixed flight, in the course's local space.</summary>
    public readonly struct BirdPerch
    {
        public BirdPerch(Vector3 position, float yawDegrees, float pathDegrees, float takeOffDelay, float returnDelay, int phase)
        {
            Position = position;
            YawDegrees = yawDegrees;
            PathDegrees = pathDegrees;
            TakeOffDelay = takeOffDelay;
            ReturnDelay = returnDelay;
            Phase = phase;
        }

        /// <summary>Where its feet stand.</summary>
        public Vector3 Position { get; }

        /// <summary>Which way it faces while perched, degrees from +X toward +Z, like the kit's props.</summary>
        public float YawDegrees { get; }

        /// <summary>Heading of its take-off path, degrees from +X toward +Z. Fixed per bird; only its sign flips when the ball is on that side.</summary>
        public float PathDegrees { get; }

        /// <summary>Seconds between the trigger and the take-off.</summary>
        public float TakeOffDelay { get; }

        /// <summary>Seconds it stays away at least before it may come back.</summary>
        public float ReturnDelay { get; }

        /// <summary>0..255, offsets the wing beat so a flock never flaps in step.</summary>
        public int Phase { get; }
    }

    /// <summary>
    /// Where a course's birds perch (M4 PR 4.6). A view-time layer, kept out of <see cref="CourseLayout"/> on purpose: nothing in
    /// generation, the checks, the simulation or the aim preview reads the birds, so every prop and fingerprint stays as it was.
    /// Deterministic: the same (seed, par, variant) on the same course gives the same flock.
    ///
    /// How it works: the candidates are the tops of the course's own props (olive crowns, bushes, sheds, benches; never the cream stone of rocks and walls, where a white dove vanishes),
    /// each tested for clear ground around it (rough or fairway only: no bunker, water, green or void within the clearance, away from
    /// the tee and the +X end). One candidate is drawn for the first bird; the others come from the candidates within the flock's
    /// spread, in a fixed order, and share a perch (offset a few centimetres) when too few remain. A course with no suitable prop
    /// falls back to rough ground on a grid. Own stream, salt <see cref="BirdsStream"/> (6 surfaces, 7 props, 8 fairway shapes are
    /// taken), so no other stream moves. Every discrete choice is an integer draw.
    /// </summary>
    public static class BirdPerches
    {
        /// <summary>Salt of the birds stream.</summary>
        public const int BirdsStream = 9;

        public static List<BirdPerch> Place(CourseLayout layout, int seed, int par, int variant, BirdSettings settings)
        {
            var result = new List<BirdPerch>();
            if (layout == null || settings == null)
            {
                return result;
            }

            var fields = new CourseFields(layout);
            int course = unchecked((int)CourseNoise.Hash(unchecked((int)CourseNoise.Hash(seed, par, variant)), BirdsStream, 0));
            var random = new SeededRandom(course);

            List<Vector3> spots = PropTops(layout, fields, settings);
            if (spots.Count == 0)
            {
                spots = GroundSpots(layout, fields, settings);
            }

            if (spots.Count == 0)
            {
                return result;
            }

            int count = random.Int(settings.Count.x, settings.Count.y);
            Vector3 first = spots[random.Int(0, spots.Count - 1)];

            // The flock: the other spots near the first, nearest first, in whole centimetres so ties break the same on every device.
            var near = new List<Vector3>();
            foreach (Vector3 spot in spots)
            {
                if (spot != first && Geo.Cm(Flat(spot - first).magnitude) <= Geo.Cm(settings.FlockSpread))
                {
                    near.Add(spot);
                }
            }

            near.Sort((a, b) =>
            {
                int byDistance = Geo.Cm(Flat(a - first).magnitude).CompareTo(Geo.Cm(Flat(b - first).magnitude));
                if (byDistance != 0)
                {
                    return byDistance;
                }

                int byX = Geo.Cm(a.x).CompareTo(Geo.Cm(b.x));
                return byX != 0 ? byX : Geo.Cm(a.z).CompareTo(Geo.Cm(b.z));
            });

            var chosen = new List<Vector3> { first };
            for (int i = 0; i < near.Count && chosen.Count < count; i++)
            {
                // An even draw over what is left, so the flock is not always the nearest few.
                int pick = random.Int(i, near.Count - 1);
                (near[i], near[pick]) = (near[pick], near[i]);
                chosen.Add(near[i]);
            }

            // Too few spots: birds share one, a few centimetres apart.
            int shared = 0;
            while (chosen.Count < count)
            {
                Vector3 spot = chosen[shared % chosen.Count];
                float angle = random.Int(0, 359) * Mathf.Deg2Rad;
                chosen.Add(spot + (new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.12f));
                shared++;
            }

            foreach (Vector3 spot in chosen)
            {
                result.Add(new BirdPerch(
                    spot,
                    random.Int(0, 359),
                    random.Int(0, 359),
                    random.Hundredths(settings.TakeOffDelay),
                    random.Hundredths(settings.ReturnDelay),
                    random.Int(0, 255)));
            }

            return result;
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>The top of every prop a bird can stand on whose ground is clear.</summary>
        private static List<Vector3> PropTops(CourseLayout layout, CourseFields fields, BirdSettings settings)
        {
            var spots = new List<Vector3>();
            foreach (CourseProp prop in layout.Props)
            {
                if (!CanPerch(prop.Kind))
                {
                    continue;
                }

                // The highest shape's top, straight above its middle.
                bool found = false;
                Vector3 best = default;
                foreach (PropShape shape in prop.Shapes)
                {
                    var top = new Vector3(shape.Center.x, shape.Center.y + shape.Extents.y, shape.Center.z);
                    if (!found || Geo.Cm(top.y) > Geo.Cm(best.y))
                    {
                        best = top;
                        found = true;
                    }
                }

                if (!found)
                {
                    continue;
                }

                bool soft = prop.Kind == PropKind.Olive || prop.Kind == PropKind.Bush;
                if (soft)
                {
                    best.y -= settings.PerchSink;
                }

                if (ClearGround(layout, fields, new Vector2(prop.Root.x, prop.Root.z), settings, 24))
                {
                    spots.Add(best);
                }
            }

            return spots;
        }

        private static bool CanPerch(PropKind kind)
        {
            switch (kind)
            {
                case PropKind.Olive:
                case PropKind.Bush:
                case PropKind.Shed:
                case PropKind.Bench:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Open rough ground on a grid, for a course with no prop to perch on.</summary>
        private static List<Vector3> GroundSpots(CourseLayout layout, CourseFields fields, BirdSettings settings)
        {
            var spots = new List<Vector3>();
            float step = Mathf.Max(0.25f, settings.GroundGrid);
            float half = layout.Centreline.Length;
            Vector2 start = layout.Centreline.Start;
            for (float x = start.x - half; x <= start.x + half; x += step)
            {
                for (float z = start.y - half; z <= start.y + half; z += step)
                {
                    var p = new Vector2(Mathf.Round(x * 100f) / 100f, Mathf.Round(z * 100f) / 100f);
                    if (fields.Surface(new Vector3(p.x, 0f, p.y)) == CourseSurface.Rough && ClearGround(layout, fields, p, settings, 8))
                    {
                        spots.Add(new Vector3(p.x, fields.Height(p.x, p.y), p.y));
                    }
                }
            }

            return spots;
        }

        /// <summary>
        /// True when the ground at <paramref name="p"/> and on a ring around it is rough or fairway, and the spot is off the tee and
        /// not past the hole's far end: never in a hazard, on the green (its margin included), on the void or where M5's next island joins.
        /// </summary>
        internal static bool ClearGround(CourseLayout layout, CourseFields fields, Vector2 p, BirdSettings settings, int samples)
        {
            if (Geo.Cm((p - layout.Tee).magnitude) < Geo.Cm(settings.TeeClearance))
            {
                return false;
            }

            if (Geo.Cm(p.x) > Geo.Cm(layout.Hole.x))
            {
                return false;
            }

            if (!Open(fields, new Vector3(p.x, 0f, p.y)))
            {
                return false;
            }

            // Four rings out to the clearance, so a small bunker or pond between two rings is not slipped past.
            for (int ring = 1; ring <= 4; ring++)
            {
                float radius = settings.PerchClearance * ring / 4f;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    var q = new Vector3(p.x + (Mathf.Cos(angle) * radius), 0f, p.y + (Mathf.Sin(angle) * radius));
                    if (!Open(fields, q))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Open(CourseFields fields, Vector3 p)
        {
            CourseSurface surface = fields.Surface(p);
            return surface == CourseSurface.Rough || surface == CourseSurface.Fairway;
        }
    }
}
