using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Builds a course from a seed and a par: centreline, then the stretches of land along it (cut by a gap or an island
    /// green), then one island per leg of each stretch, then the fairway along the centreline and the green at the hole.
    /// Plain C# with one seeded <see cref="SeededRandom"/> passed down every step in a fixed order, so the same (seed,
    /// par, variant) always gives the same course. Elevation (PR 5), hazards and the tee and hole strategy (PR 6) and
    /// the course checks (PR 7, which move a failing seed on to its next <c>variant</c>) plug in after these steps;
    /// for now the ground is flat as on the hand-made courses.
    /// </summary>
    public static class CourseGenerator
    {
        /// <summary>A stretch of the centreline, from <see cref="From"/> to <see cref="To"/> metres from the tee.</summary>
        private readonly struct Stretch
        {
            public Stretch(float from, float to)
            {
                From = from;
                To = to;
            }

            public float From { get; }
            public float To { get; }
        }

        public static CourseLayout Generate(int seed, int par, LevelGenSettings settings, int variant = 0)
        {
            ParRules rules = settings.Rules(par);

            // One integer from the three numbers, through the same hash the course noise uses, so nearby seeds are unrelated.
            var random = new SeededRandom(unchecked((int)CourseNoise.Hash(seed, par, variant)));

            // The order of the draws below is part of what a seed means: adding a draw in the middle changes every course
            // after it, which is fine until the first release.
            float length = random.Centimetres(rules.Length);
            List<float> bends = BendPlaces(random, rules, settings, length, out List<float> headings, out bool dogleg);
            List<Vector2> centreline = Centreline(bends, headings, length);
            List<Stretch> land = Land(random, par, rules, settings, bends, dogleg, length, out bool islandGreen);
            List<IslandOutline> islands = Islands(random, settings, land, bends, headings, centreline, islandGreen);

            // Centre the course on the origin: the terrain grid is laid out symmetrically around it.
            Vector2 shift = -Middle(islands);
            for (int i = 0; i < centreline.Count; i++)
            {
                centreline[i] += shift;
            }

            for (int i = 0; i < islands.Count; i++)
            {
                IslandOutline island = islands[i];
                islands[i] = new IslandOutline(island.HalfLength, island.HalfWidth, island.WidthWobble, island.WidthNoiseScale, island.CenterX + shift.x, island.CenterZ + shift.y, island.Heading, island.NoiseOffset);
            }

            var path = new CoursePath(centreline);
            Vector2 tee = path.Start;
            Vector2 hole = path.End;
            Vector2 greenCentre = path.PointAt(length - settings.GreenCentreShortOfHole);

            return new CourseLayout(
                islands,
                path,
                new FairwayLayout(-settings.FairwayBehindTee, length - settings.FairwayShortOfHole, settings.FairwayHalfWidth, settings.FairwayWobble, settings.StripeWidth, random.Int(0, 999)),
                new[] { new GreenLayout(new EllipseShape(greenCentre, settings.GreenRadius, settings.GreenWobble), Vector2.zero) },
                new EllipseShape[0],
                new EllipseShape[0],
                new GroundSwell[0],
                settings.WaterLevel,
                new GroundRelief(settings.CellSize, settings.AxisRelief, settings.EdgeRelief, settings.ReliefDistance, settings.ReliefNoiseScale, random.Int(0, 999)),
                tee,
                hole,
                settings.FlatRadius,
                settings.FlatBlend,
                new EdgeLayout(settings.SkirtDepth, settings.SkirtNoiseScale),
                alongCentreline: true);
        }

        /// <summary>
        /// Where the centreline turns, in metres from the tee, and the heading of each leg (one more than the bends), in
        /// degrees from +X, positive toward +Z (left on screen). A par 3 is straight along +X. A par 4 or 5 is a dogleg
        /// some of the time, turning hard once (a par 5 maybe twice); otherwise it gets soft bends. No leg heads further
        /// than <see cref="LevelGenSettings.MaxHeading"/> from +X, so the course always keeps heading toward +X.
        /// </summary>
        private static List<float> BendPlaces(SeededRandom random, ParRules rules, LevelGenSettings settings, float length, out List<float> headings, out bool dogleg)
        {
            var places = new List<float>();
            headings = new List<float> { 0f };

            dogleg = random.Chance(rules.DoglegChance);
            int count = dogleg ? (random.Chance(rules.SecondDoglegChance) ? 2 : 1) : rules.SoftBends;
            if (count == 0)
            {
                return places;
            }

            headings[0] = random.Degrees(new Vector2(-settings.FirstLegHeading, settings.FirstLegHeading));
            if (count == 1)
            {
                places.Add(random.Hundredths(settings.SingleBendPlace) * length);
            }
            else
            {
                places.Add(random.Hundredths(settings.FirstOfTwoBendsPlace) * length);
                places.Add(random.Hundredths(settings.SecondOfTwoBendsPlace) * length);
            }

            for (int i = 0; i < count; i++)
            {
                float turn = random.Degrees(dogleg ? settings.DoglegTurn : settings.SoftTurn);
                float previous = headings[i];
                float heading = random.Coin() ? previous + turn : previous - turn;

                // Turning the other way keeps the course heading toward +X; the clamp only matters for a hand-tuned range.
                if (Mathf.Abs(heading) > settings.MaxHeading)
                {
                    heading = (2f * previous) - heading;
                }

                headings.Add(Mathf.Clamp(heading, -settings.MaxHeading, settings.MaxHeading));
            }

            return places;
        }

        /// <summary>The centreline from the tee at the origin, leg by leg.</summary>
        private static List<Vector2> Centreline(List<float> bends, List<float> headings, float length)
        {
            var points = new List<Vector2> { Vector2.zero };
            float along = 0f;
            for (int leg = 0; leg < headings.Count; leg++)
            {
                float end = leg < bends.Count ? bends[leg] : length;
                points.Add(points[points.Count - 1] + (Direction(headings[leg]) * (end - along)));
                along = end;
            }

            return points;
        }

        /// <summary>
        /// The stretches of the centreline with land under them, from behind the tee to past the hole. A par 4 or 5 may
        /// lose one stretch to a gap of open sky; a par 3 may have its green on an island of its own.
        /// </summary>
        private static List<Stretch> Land(SeededRandom random, int par, ParRules rules, LevelGenSettings settings, List<float> bends, bool dogleg, float length, out bool islandGreen)
        {
            float from = -settings.BehindTee;
            float to = length + settings.PastHole;
            islandGreen = false;

            if (par == 3)
            {
                if (random.Chance(rules.IslandGreenChance))
                {
                    float greenStart = length - settings.IslandGreenBeforeHole;
                    float width = Mathf.Min(random.Centimetres(settings.IslandGreenGapWidth), greenStart - settings.IslandGreenFromTee);
                    if (width >= settings.IslandGreenGapWidth.x)
                    {
                        islandGreen = true;
                        return new List<Stretch> { new Stretch(from, greenStart - width), new Stretch(greenStart, to) };
                    }
                }

                return new List<Stretch> { new Stretch(from, to) };
            }

            if (random.Chance(rules.GapChance))
            {
                float width = random.Centimetres(settings.GapWidth);
                float clearance = dogleg ? settings.GapFromBend : settings.GapFromSoftBend;
                List<Vector2> places = GapStarts(settings.GapFromTee, length - settings.GapFromHole - width, width, bends, clearance);

                // One draw over every place the gap fits, so it lands anywhere they allow with even odds. A course with
                // nowhere to put it keeps its land whole.
                float room = 0f;
                foreach (Vector2 place in places)
                {
                    room += place.y - place.x;
                }

                if (places.Count > 0)
                {
                    float pick = random.Centimetres(new Vector2(0f, room));
                    foreach (Vector2 place in places)
                    {
                        float span = place.y - place.x;
                        if (pick <= span)
                        {
                            float start = place.x + pick;
                            return new List<Stretch> { new Stretch(from, start), new Stretch(start + width, to) };
                        }

                        pick -= span;
                    }
                }
            }

            return new List<Stretch> { new Stretch(from, to) };
        }

        /// <summary>
        /// Where a gap of this width may start, from <paramref name="first"/> to <paramref name="last"/>, keeping
        /// <paramref name="clearance"/> from every bend: the ranges left once each bend's window is cut out.
        /// </summary>
        private static List<Vector2> GapStarts(float first, float last, float width, List<float> bends, float clearance)
        {
            var places = new List<Vector2>();
            if (last < first)
            {
                return places;
            }

            places.Add(new Vector2(first, last));
            foreach (float bend in bends)
            {
                // A gap starting in (bend - clearance - width, bend + clearance) would come within the clearance of it.
                float low = bend - clearance - width, high = bend + clearance;
                var kept = new List<Vector2>();
                foreach (Vector2 place in places)
                {
                    if (place.x < low)
                    {
                        kept.Add(new Vector2(place.x, Mathf.Min(place.y, low)));
                    }

                    if (place.y > high)
                    {
                        kept.Add(new Vector2(Mathf.Max(place.x, high), place.y));
                    }
                }

                places = kept;
            }

            return places;
        }

        /// <summary>
        /// One oval island for each leg of each stretch of land, turned along its leg. Where two legs meet, both run on
        /// past the bend by <see cref="LevelGenSettings.BendOverlap"/>, so their union has no waist at the corner; where a
        /// stretch ends (behind the tee, past the hole, at a gap) the oval's tip sits exactly at the stretch's end, so a
        /// gap measured along the centreline is at least its drawn width.
        /// </summary>
        private static List<IslandOutline> Islands(SeededRandom random, LevelGenSettings settings, List<Stretch> land, List<float> bends, List<float> headings, List<Vector2> centreline, bool islandGreen)
        {
            // One width for the whole course, so its legs read as one island.
            float halfWidth = random.Centimetres(settings.IslandHalfWidth);
            float wobble = random.Centimetres(settings.IslandWobble);

            var islands = new List<IslandOutline>();
            for (int s = 0; s < land.Count; s++)
            {
                bool green = islandGreen && s == land.Count - 1;
                float width = green ? random.Centimetres(settings.IslandGreenHalfWidth) : halfWidth;
                float stretchWobble = green ? random.Centimetres(settings.IslandGreenWobble) : wobble;

                float legStart = 0f;
                for (int leg = 0; leg < headings.Count; leg++)
                {
                    float legEnd = leg < bends.Count ? bends[leg] : float.MaxValue;
                    float from = Mathf.Max(land[s].From, leg == 0 ? float.MinValue : legStart);
                    float to = Mathf.Min(land[s].To, legEnd);
                    if (to > from)
                    {
                        // Run on past a bend this piece ends or starts at, but never past the stretch's own ends.
                        float back = leg > 0 && from == legStart ? Mathf.Min(settings.BendOverlap, legStart - land[s].From) : 0f;
                        float front = leg < bends.Count && to == legEnd ? Mathf.Min(settings.BendOverlap, land[s].To - legEnd) : 0f;
                        Vector2 direction = Direction(headings[leg]);
                        Vector2 a = centreline[leg] + (direction * (from - legStart - back));
                        Vector2 b = centreline[leg] + (direction * (to - legStart + front));
                        Vector2 centre = (a + b) * 0.5f;
                        islands.Add(new IslandOutline((b - a).magnitude * 0.5f, width, stretchWobble, settings.WidthNoiseScale, centre.x, centre.y, headings[leg], random.Int(0, 999)));
                    }

                    legStart = legEnd;
                }
            }

            return islands;
        }

        /// <summary>The middle of the box around every island, widths included.</summary>
        private static Vector2 Middle(List<IslandOutline> islands)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (IslandOutline island in islands)
            {
                Vector2 direction = Direction(island.Heading);
                var centre = new Vector2(island.CenterX, island.CenterZ);
                float width = island.HalfWidth + (island.WidthWobble * 0.5f);
                foreach (Vector2 end in new[] { centre - (direction * island.HalfLength), centre + (direction * island.HalfLength) })
                {
                    min = Vector2.Min(min, end - (Vector2.one * width));
                    max = Vector2.Max(max, end + (Vector2.one * width));
                }
            }

            return (min + max) * 0.5f;
        }

        private static Vector2 Direction(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
