using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Builds a course from a seed and a par: centreline, then the stretches of land along it (cut by a gap or an island
    /// green), then one island per leg of each stretch, then the fairway along the centreline and the green at the hole.
    /// Plain C# with one seeded <see cref="SeededRandom"/> passed down every step in a fixed order, so the same (seed,
    /// par, variant) always gives the same course. Elevation comes last of the draws, so it left every outline as it was. The green, the pin and the hazards (PR 6b,
    /// <see cref="CourseHazards"/>) draw from a second stream of their own, so they never move any of the above. The course
    /// checks (PR 7, which move a failing seed on to its next <c>variant</c>) plug in after these steps.
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

        // Salt for the surfaces stream (green, pin, hazards): its own random sequence next to the main one.
        private const int SurfacesStream = 6;

        /// <param name="props">
        /// False leaves the props out (the course checks place them only on the variant they keep, see <see cref="WithProps"/>).
        /// Everything else is the same either way: props come last, from a stream of their own.
        /// </param>
        public static CourseLayout Generate(int seed, int par, LevelGenSettings settings, int variant = 0, bool props = true)
        {
            CourseLayout course = GenerateBare(seed, par, settings, variant);
            return props ? WithProps(course, seed, par, variant, settings) : course;
        }

        /// <summary>The course with its props placed by <see cref="PropPlacement"/> (none without a prop kit in the settings).</summary>
        public static CourseLayout WithProps(CourseLayout course, int seed, int par, int variant, LevelGenSettings settings)
        {
            return settings.PropKit == null ? course : course.WithProps(PropPlacement.Place(course, seed, par, variant, settings));
        }

        private static CourseLayout GenerateBare(int seed, int par, LevelGenSettings settings, int variant)
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

            // A second stream for the green, the pin and the hazards, so tuning them never moves an island or a slope: the main
            // stream's draws (outline, noise offsets, elevation) stay exactly where they were. Read first.
            var surfaces = new SeededRandom(unchecked((int)CourseNoise.Hash(unchecked((int)CourseNoise.Hash(seed, par, variant)), SurfacesStream, 0)));
            GreenPlan green = CourseHazards.GreenAndPin(surfaces, path, length, islandGreen, settings);
            Vector2 greenCentre = green.Centre;

            // The noise offsets keep their place in the order; the elevation draws come after them, so adding elevation left
            // every outline of the earlier steps as it was.
            int fairwayOffset = random.Int(0, 999);
            int reliefOffset = random.Int(0, 999);
            ElevationProfile elevation = Elevation(random, rules, settings, length);
            Vector2 tilt = GreenTilt(random, settings);

            float fairwayEnd = FairwayEnd(length, green, islandGreen, settings);

            CourseLayout Build(IEnumerable<EllipseShape> bunkers, IEnumerable<PondLayout> ponds, CoursePlan plan) => new CourseLayout(
                islands,
                path,
                new FairwayLayout(-settings.FairwayBehindTee, fairwayEnd, settings.FairwayHalfWidth, settings.FairwayWobble, settings.StripeWidth, fairwayOffset, settings.FairwayCapDepth, settings.FairwayCapDepth),
                new[] { new GreenLayout(new EllipseShape(greenCentre, settings.GreenRadius, settings.GreenWobble), tilt) },
                bunkers,
                ponds,
                new GroundSwell[0],
                settings.WaterLevel,
                new GroundRelief(settings.CellSize, settings.AxisRelief, settings.EdgeRelief, settings.ReliefDistance, settings.ReliefNoiseScale, reliefOffset),
                tee,
                hole,
                settings.FlatRadius,
                settings.FlatBlend,
                new EdgeLayout(settings.SkirtDepth, settings.SkirtNoiseScale),
                alongCentreline: true,
                elevation: elevation,
                plan: plan);

            // The hazards read the ground without them (broad heights, the island, the green), then join it.
            CourseLayout bare = Build(new EllipseShape[0], new PondLayout[0], null);
            HazardResult hazards = CourseHazards.Place(surfaces, bare, green, par, dogleg, islandGreen, bends, settings);
            return Build(hazards.Bunkers, hazards.Ponds, hazards.Plan);
        }

        /// <summary>
        /// Where the fairway's rounded tip ends, by the green's guard (no draw, so no stream moves). A guard beside the green
        /// (at least <see cref="LevelGenSettings.FairwayIntoGreenGuardAngle"/> off the line back toward the tee) lets the fairway run
        /// into the green: its tip sits under the green, and the green's own border is the only seam. A guard in front of the green
        /// stops the fairway short, so the rough apron stays in front of it. An island green has no fairway joining it: apron.
        /// </summary>
        internal static float FairwayEnd(float length, GreenPlan green, bool islandGreen, LevelGenSettings settings)
        {
            float guardAngle = Vector2.Angle(green.Guard, -green.Approach);
            bool beside = !islandGreen && Geo.Cm(guardAngle) >= Geo.Cm(settings.FairwayIntoGreenGuardAngle);
            return beside ? length + settings.FairwayIntoGreen : length - settings.FairwayShortOfHole;
        }

        /// <summary>
        /// The ground's height along the centreline, as knots joined by <see cref="ElevationProfile"/>'s curves. The first
        /// knot (the tee, height 0) and the last (the green) are plateaus, so the tee and the green sit on level ground
        /// and the green's slope is only its tilt. Knots between are evenly spaced, which is what lets the curve's slope be
        /// bounded: with evenly spaced knots it never exceeds twice a step's rise over its run, so a step of at most
        /// <c>maxSlope * run / 2</c> keeps the profile under <c>maxSlope</c>. Heights are drawn in whole centimetres and
        /// every draw is allowed only where the green is still reachable from it within that step, so no range ever
        /// comes out empty. Draws, in order: the green's height, the inner knots' heights, then their side slopes in
        /// whole percent (0 at the tee and green knots).
        /// </summary>
        private static ElevationProfile Elevation(SeededRandom random, ParRules rules, LevelGenSettings settings, float length)
        {
            float teePlateau = settings.FlatRadius + settings.FlatBlend + settings.PlateauMargin;
            float greenPlateau = settings.GreenCentreShortOfHole + settings.GreenRadius.x + settings.PlateauMargin;
            float first = teePlateau;
            float last = length - greenPlateau;
            int intervals = Mathf.Max(1, Mathf.RoundToInt((last - first) / rules.KnotSpacing));
            float spacing = (last - first) / intervals;

            // The most one step may rise or fall, in whole centimetres.
            int step = Mathf.FloorToInt(settings.MaxAlongSlope * spacing * 0.5f * 100f);
            int reach = step * intervals;

            int green = Mathf.RoundToInt(random.Centimetres(rules.GreenHeight) * 100f);
            green = Mathf.Clamp(green, -reach, reach);

            // The box the inner knots stay in, wide enough to hold the green's own height.
            int relief = Mathf.Max(Mathf.RoundToInt(rules.FairwayRelief * 100f), Mathf.Abs(green));

            var heights = new int[intervals + 1];
            heights[intervals] = green;
            for (int k = 1; k < intervals; k++)
            {
                int previous = heights[k - 1];
                int remaining = intervals - k;
                int low = Mathf.Max(-relief, Mathf.Max(previous - step, green - (step * remaining)));
                int high = Mathf.Min(relief, Mathf.Min(previous + step, green + (step * remaining)));
                heights[k] = random.Int(low, high);
            }

            // Level at both plateaus; a side slope in between, changing by at most a step from knot to knot and reaching 0 again
            // at the green. The same reachability as the heights keeps every range non-empty.
            var cambers = new int[intervals + 1];
            for (int k = 1; k < intervals; k++)
            {
                int remaining = (intervals - k) * settings.MaxCamberStep;
                int limit = Mathf.Min(settings.MaxCamber, remaining);
                cambers[k] = random.Int(Mathf.Max(-limit, cambers[k - 1] - settings.MaxCamberStep), Mathf.Min(limit, cambers[k - 1] + settings.MaxCamberStep));
            }

            var knots = new List<ElevationKnot>();
            for (int k = 0; k <= intervals; k++)
            {
                knots.Add(new ElevationKnot(first + (spacing * k), heights[k] * 0.01f, cambers[k] * 0.01f));
            }

            return new ElevationProfile(knots, settings.LegBlend);
        }

        /// <summary>
        /// The green's tilt, the rise per metre along X and along Z, measured from the hole. A steepness in whole percent
        /// and a direction in whole degrees; the terrain already fades it in over the flat area around the hole.
        /// </summary>
        private static Vector2 GreenTilt(SeededRandom random, LevelGenSettings settings)
        {
            float steepness = random.Int(Mathf.RoundToInt(settings.GreenTilt.x), Mathf.RoundToInt(settings.GreenTilt.y)) * 0.01f;
            float radians = random.Int(0, 359) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * steepness;
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
