using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Where the green sits, and which side its guard is on: the first step of the strategy, before the elevation.</summary>
    internal readonly struct GreenPlan
    {
        public GreenPlan(Vector2 centre, Vector2 guard, Vector2 safeTarget, Vector2 directOrigin, Vector2 approach)
        {
            Centre = centre;
            Guard = guard;
            SafeTarget = safeTarget;
            DirectOrigin = directOrigin;
            Approach = approach;
        }

        /// <summary>The green's centre. The hole stays where the centreline ends; the green moves away from its guard.</summary>
        public Vector2 Centre { get; }

        /// <summary>Unit vector from the green's centre toward where its guard sits.</summary>
        public Vector2 Guard { get; }

        /// <summary>The middle of the green's safe half, where the safe route ends.</summary>
        public Vector2 SafeTarget { get; }

        /// <summary>Where the direct line to the hole starts: the tee, or on a long par 5 where a drive leaves the ball.</summary>
        public Vector2 DirectOrigin { get; }

        /// <summary>Unit direction of the last leg, into the hole.</summary>
        public Vector2 Approach { get; }
    }

    /// <summary>What <see cref="CourseHazards.Place"/> hands back: the hazards in layout form, and the plan that explains them.</summary>
    internal sealed class HazardResult
    {
        public List<EllipseShape> Bunkers { get; } = new List<EllipseShape>();
        public List<PondLayout> Ponds { get; } = new List<PondLayout>();
        public CoursePlan Plan { get; set; }
    }

    /// <summary>
    /// The strategy of a generated course (M3 PR 6b): where the green and the hole sit, then the hazards, each placed for a
    /// reason (its <see cref="HazardRole"/>), and the safe route round them that <see cref="SafeRoute"/> keeps open.
    /// Everything draws from the course's own <b>surfaces stream</b>, never the stream of the outline and the elevation, so
    /// tuning hazards never moves an island or a slope. The draws are in a fixed order, and a candidate draws the same
    /// seven numbers whether it is accepted or not, so the stream stays aligned whatever the rules decide. Every decision
    /// compares whole centimetres (<see cref="Geo.Cm"/>).
    /// </summary>
    internal static class CourseHazards
    {
        /// <summary>
        /// The green and the pin, before the elevation. Draws: the guard's side (1 or 2 draws), then how far the hole sits
        /// toward the guard and toward the back of the green (2 draws).
        /// </summary>
        public static GreenPlan GreenAndPin(SeededRandom surfaces, CoursePath path, float length, bool islandGreen, LevelGenSettings s)
        {
            Vector2 hole = path.End;
            Vector2 approach = (hole - path.Points[path.Points.Count - 2]).normalized;

            // A drive leaves the ball where the second shot starts: on a par 5 that is not the tee.
            Vector2 origin = Geo.Cm(length) <= Geo.Cm(2f * s.FullReach) ? path.Start : path.PointAt(length - (2f * s.FullReach * 0.9f));
            Vector2 direct = (hole - origin).normalized;
            float theta = Vector2.SignedAngle(approach, direct);

            Vector2 guard;
            if (!islandGreen && Geo.Cm(Mathf.Abs(theta)) >= Geo.Cm(s.SideGuardThreshold))
            {
                // The direct line comes in from a side (a dogleg): the guard sits on the line, a little off it.
                guard = Geo.Rotate(-direct, surfaces.Degrees(new Vector2(-s.SideGuardJitter, s.SideGuardJitter)));
            }
            else
            {
                // It comes from the front: front-left or front-right. An island green draws the same, for a fixed draw count.
                float side = surfaces.Coin() ? 1f : -1f;
                guard = Geo.Rotate(-approach, side * surfaces.Degrees(s.FrontGuardAngle));
            }

            // The hole stays, the green moves away from the guard: the hole sits toward the guarded side, the safe half stays open.
            // `back` slides the hole along the green's side, toward the back of the course (a deviation from the plan's wording:
            // along the approach it could cancel the offset toward the guard).
            float pin = surfaces.Centimetres(s.PinOffset);
            float back = surfaces.Centimetres(s.PinAlong);
            Vector2 side2 = Geo.Left(guard);
            if (Vector2.Dot(side2, approach) < 0f)
            {
                side2 = -side2;
            }

            Vector2 offset = (guard * pin) + (side2 * back);
            float cap = Mathf.Min(s.GreenRadius.x, s.GreenRadius.y) - s.PinFromBorder - s.GreenWobble;
            if (Geo.Cm(offset.magnitude) > Geo.Cm(cap))
            {
                offset *= cap / offset.magnitude;
            }

            Vector2 centre = path.PointAt(length - s.GreenCentreShortOfHole) - offset;
            Vector2 safeTarget = centre - (guard * (0.45f * Mathf.Min(s.GreenRadius.x, s.GreenRadius.y)));
            return new GreenPlan(centre, guard, safeTarget, origin, approach);
        }

        /// <summary>
        /// The hazards, after the elevation. <paramref name="bare"/> is the course without hazards. The draw order, all on
        /// <paramref name="surfaces"/>: the hazard target; the green guard; each cuttable bend's corner hazard; the drive zone;
        /// the second guard; the flanks.
        /// </summary>
        public static HazardResult Place(SeededRandom surfaces, CourseLayout bare, GreenPlan green, int par, bool dogleg, bool islandGreen, IReadOnlyList<float> bends, LevelGenSettings s, FairwayShapeResult shapes = null)
        {
            return new HazardPlacer(surfaces, bare, green, par, dogleg, islandGreen, bends, s, shapes).Run();
        }
    }

    /// <summary>
    /// One course's hazard placement. The rules every candidate passes are in <see cref="Accept"/>; the role rules are in the
    /// <c>Rule</c> methods; the safe-route recheck is its last step.
    /// </summary>
    internal sealed class HazardPlacer
    {
        private readonly SeededRandom _random;
        private readonly CourseLayout _bare;
        private readonly CourseFields _fields;
        private readonly GreenPlan _green;
        private readonly int _par;
        private readonly bool _dogleg;
        private readonly bool _islandGreen;
        private readonly IReadOnlyList<float> _bends;
        private readonly LevelGenSettings _s;
        private readonly ParRules _rules;
        private readonly RouteWorld _world;

        // The fairway's rough islands and splits (F3.2), null on a plain course. Hazards keep off them.
        private readonly FairwayShapeResult _shapes;
        private readonly HazardOutline _greenOutline;
        private readonly float _length;
        private readonly Vector2 _tee;
        private readonly Vector2 _hole;

        private readonly List<HazardOutline> _placed = new List<HazardOutline>();
        private readonly List<ShotLine> _lines = new List<ShotLine>();
        private readonly List<HazardRole> _missing = new List<HazardRole>();

        // Where each placed corner hazard's cutting shot lands: later hazards keep those spots clear too.
        private readonly List<float> _cutLandings = new List<float>();

        // The numbers one candidate draws, always all seven.
        private struct Draw
        {
            public float Along, Across, Radius, Turn, Wobble;
            public int Aspect;
            public bool Flip;
            public bool Water;

            public float ShortRadius => Radius * Aspect / 100f;
        }

        /// <summary>Tests only: sees every hazard candidate as it is drawn, accepted or not. Null in play.</summary>
        internal static Action<EllipseShape> CandidateObserver;

        public HazardPlacer(SeededRandom random, CourseLayout bare, GreenPlan green, int par, bool dogleg, bool islandGreen, IReadOnlyList<float> bends, LevelGenSettings settings, FairwayShapeResult shapes = null)
        {
            _random = random;
            _bare = bare;
            _fields = new CourseFields(bare);
            _green = green;
            _par = par;
            _dogleg = dogleg;
            _islandGreen = islandGreen;
            _bends = bends;
            _s = settings;
            _rules = settings.Rules(par);
            _length = bare.Centreline.Length;
            _tee = bare.Tee;
            _hole = bare.Hole;
            _greenOutline = HazardOutline.Of(bare.Greens[0].Outline, false, HazardRole.GreenGuard);
            _shapes = shapes == null || shapes.Plain ? null : shapes;
            _world = _shapes?.World ?? new RouteWorld(_fields, bare.Centreline, settings, green.SafeTarget, green.Approach, par - 1 + settings.SafeRouteExtraShots);
        }

        public HazardResult Run()
        {
            var result = new HazardResult();
            List<Vector2> bareRoute = SafeRoute.Find(_world, new List<HazardOutline>(), out List<float> bareAlongs);

            int gapHazards = _s.GapCountsAsHazard && _world.Gaps.Count > 0 ? 1 : 0;
            int target = _random.Int(_rules.HazardCount.x, _rules.HazardCount.y);
            int upper = Mathf.Max(0, _rules.HazardCount.y - gapHazards);
            int wanted = Mathf.Max(0, target - gapHazards);

            // On a fork the direct line follows the risky branch (split fairway plan §3, Q5 A): two segments that bend on its centre.
            Vector2? directBend = _shapes?.DirectWaypoint;
            if (directBend != null)
            {
                _lines.Add(new ShotLine(_green.DirectOrigin, directBend.Value, ShotLineKind.Direct));
                _lines.Add(new ShotLine(directBend.Value, _hole, ShotLineKind.Direct));
            }
            else
            {
                _lines.Add(new ShotLine(_green.DirectOrigin, _hole, ShotLineKind.Direct));
            }

            // 1. The green's guard (the gap is the guard of an island green).
            if (!_islandGreen)
            {
                if (_placed.Count >= upper || !PlaceGreenGuard(HazardRole.GreenGuard))
                {
                    _missing.Add(HazardRole.GreenGuard);
                }
            }

            // 2. A hazard to carry at each dogleg corner that can be cut.
            if (_dogleg)
            {
                for (int bend = 0; bend < _bends.Count; bend++)
                {
                    if (!PlaceCorner(bend, upper))
                    {
                        // Only a cuttable corner needs one; PlaceCorner says so by adding its cut line first.
                        if (CutLineFor(bend, out _, out _, out _))
                        {
                            _missing.Add(HazardRole.CornerCarry);
                        }
                    }
                }
            }

            // 3. The drive zone, on a course with no corner to carry. On a fork the branch guard takes its place (F3.3): required,
            // with no chance draw, beside the risky branch.
            if (_shapes?.Spine != null)
            {
                if (_placed.Count >= upper || !PlaceBranchGuard())
                {
                    _missing.Add(HazardRole.BranchGuard);
                }
            }
            else if (_par >= 4)
            {
                int chance = _dogleg ? (_par == 5 ? _s.DriveZoneWithDoglegChance : 0) : _rules.DriveZoneChance;
                if (_random.Chance(chance) && !GapInDriveZone() && (_placed.Count >= upper || !PlaceDriveZone()))
                {
                    _missing.Add(HazardRole.DriveZone);
                }
            }

            // 4. A second guard, sand, on the other side of the approach.
            bool second = _random.Chance(_rules.SecondGuardChance);
            if (second && _placed.Count < wanted)
            {
                PlaceGreenGuard(HazardRole.SecondGreenGuard);
            }

            // 5. Flanks fill up the course beside the safe route's rest spots.
            List<float> zones = RestZones();
            for (int attempt = 0; _placed.Count < wanted && attempt < wanted + 2; attempt++)
            {
                PlaceFlank(zones);
            }

            if (_placed.Count + gapHazards < _rules.HazardCount.x)
            {
                _missing.Add(HazardRole.Flank);
            }

            // The route that is left, for the plan and PR 7.
            List<Vector2> route = bareRoute == null ? new List<Vector2>() : SafeRoute.Find(_world, _placed, out _);
            route ??= new List<Vector2>();
            for (int i = 1; i < route.Count; i++)
            {
                _lines.Add(new ShotLine(route[i - 1], route[i], ShotLineKind.Safe));
            }

            var infos = new List<HazardInfo>();
            int bunkers = 0, ponds = 0;
            foreach (HazardOutline h in _placed)
            {
                if (h.IsWater)
                {
                    infos.Add(new HazardInfo(h.Role, true, ponds++));
                    result.Ponds.Add(new PondLayout(h.Shape, h.WaterLevel));
                }
                else
                {
                    infos.Add(new HazardInfo(h.Role, false, bunkers++));
                    result.Bunkers.Add(h.Shape);
                }
            }

            result.Plan = new CoursePlan(infos, route, _lines, _missing, target, _s.GapCountsAsHazard, _green.SafeTarget, _green.Guard, _s.ShotCorridorHalfWidth, _dogleg, _islandGreen, _green.DirectOrigin, _shapes?.Shapes);
            return result;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Roles

        /// <summary>
        /// A green guard: hugging the green's border on the side the direct line comes from, its long side along the border.
        /// The second guard is a bunker turned 50 to 80 degrees away to the other side (and, on an island green, behind or beside).
        /// </summary>
        private bool PlaceGreenGuard(HazardRole role)
        {
            bool second = role == HazardRole.SecondGreenGuard;
            bool water = !second && _random.Chance(_s.GreenGuardWaterChance);

            return SlotWithFallback(role, water, new Vector2(-0.8f, 0.8f), w => w ? _s.GuardPondClearance : _s.GuardBunkerClearance, draw =>
            {
                Vector2 u = _green.Guard;
                if (second)
                {
                    // The first guard's side of the approach is the sign of the angle from the approach to it; go the other way.
                    float side = Mathf.Sign(Vector2.SignedAngle(-_green.Approach, _green.Guard));
                    float angle = 65f + (draw.Turn * 0.75f);
                    u = _islandGreen
                        ? Geo.Rotate(_green.Approach, (draw.Flip ? 1f : -1f) * (60f + draw.Turn))
                        : Geo.Rotate(_green.Guard, -side * angle);
                }

                Vector2 radius = new Vector2(draw.Radius, draw.ShortRadius);
                float extent = GreenExtent(u);
                Vector2 centre = _green.Centre + (u * (extent + draw.Across + draw.ShortRadius)) + (Geo.Left(u) * draw.Along);
                return new EllipseShape(centre, radius, draw.Wobble, Geo.HeadingOf(Geo.Left(u)) + draw.Turn);
            }, h => second || RuleGuardNearDirectLine(h));
        }

        private bool PlaceCorner(int bend, int upper)
        {
            if (!CutLineFor(bend, out Vector2 origin, out Vector2 cut, out float cutAlong))
            {
                return false;
            }

            if (_placed.Count >= upper)
            {
                return false;
            }

            bool water = _random.Chance(_s.CornerWaterChance);
            Vector2 d1 = Geo.PathDirection(_bare.Centreline, _bends[bend] - 0.5f);
            Vector2 d2 = Geo.PathDirection(_bare.Centreline, _bends[bend] + 0.5f);
            Vector2 inward = (d2 - d1).normalized;
            Vector2 tangent = (d1 + d2).normalized;
            Vector2 corner = _bare.Centreline.PointAt(_bends[bend]);

            // The cut line passes this far inside the corner; the hazard has to reach it, so it is drawn around that distance.
            float chord = Geo.DistanceToSegment(corner, origin, cut);
            bool placed = SlotWithFallback(HazardRole.CornerCarry, water, new Vector2(-4.5f, 0.5f), w => new Vector2(Mathf.Max(0.3f, chord - 0.8f), chord + 1.2f), draw =>
            {
                Vector2 centre = corner + (inward * draw.Across) + (d1 * draw.Along);
                // A pond points inward, so it runs off the inside edge across it instead of lying along it.
                return new EllipseShape(centre, new Vector2(draw.Radius, draw.ShortRadius), draw.Wobble, Geo.HeadingOf(draw.Water ? inward : tangent) + draw.Turn);
            }, h => RuleCarriesTheCut(h, origin, cut, cutAlong));
            if (placed)
            {
                _cutLandings.Add(cutAlong);
            }

            return placed;
        }

        /// <summary>
        /// A gap across the drive zone is the drive's hazard already (and its landing zone keeps the rest clear), so that role needs
        /// no other. Decided from the outline alone, like the rest of the structure.
        /// </summary>
        private bool GapInDriveZone()
        {
            foreach (Vector2 gap in _world.Gaps)
            {
                if (Geo.Cm(gap.x) < Geo.Cm(_s.FullReach) && Geo.Cm(gap.y + _s.GapLandingZone) > Geo.Cm(_s.CarryAt(0.75f)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool PlaceDriveZone()
        {
            bool water = _random.Chance(_s.DriveZoneWaterChance);
            Vector2 lo = new Vector2(_s.CarryAt(0.75f), _s.FullReach);
            Vector2 d0 = Geo.PathDirection(_bare.Centreline, 1f);
            bool cross = _s.DriveZoneStyle == DriveZoneStyle.Cross;
            Vector2 acrossRange = cross ? new Vector2(0.3f, 1.8f) : new Vector2(0.2f, 1.2f);

            return SlotWithFallback(HazardRole.DriveZone, water, lo, w => acrossRange, draw =>
            {
                float side = draw.Flip ? 1f : -1f;
                float offset = cross ? draw.Across : _bare.Fairway.HalfWidth + draw.ShortRadius + draw.Across;
                Vector2 centre = _tee + (d0 * draw.Along) + (Geo.Left(d0) * (side * offset));

                // Across the drive for a cross hazard, along the fairway for a flank.
                float heading = Geo.HeadingOf(d0) + (cross ? 90f : 0f) + draw.Turn;
                return new EllipseShape(centre, new Vector2(draw.Radius, draw.ShortRadius), draw.Wobble, heading);
            }, h => RuleOnTheDrive(h, d0, lo, cross));
        }

        /// <summary>
        /// The branch guard (F3.3, plan §3): a bunker, or water <see cref="FairwayShapeRules.BranchGuardWaterChance"/> of the time
        /// (falling back to sand), along the spine's held stretch, from 1.2 m inside the risky branch's outer edge to 0.3 m beyond it,
        /// turned along the branch. Its role rule: the risky branch's centre line passes within <see cref="LevelGenSettings.GuardLineReach"/>.
        /// The common rules keep it off the spine and the safe route.
        /// </summary>
        private bool PlaceBranchGuard()
        {
            FairwayPiece risky = _shapes.RiskyBranch;
            CoursePath path = _bare.Centreline;
            float ease = _s.FairwayShapes.ForkEase;
            float mid = (risky.From + risky.To) * 0.5f;
            float half = Mathf.Max(0f, ((risky.To - risky.From) * 0.5f) - ease);
            float outward = Mathf.Sign(risky.Offset);
            bool water = _random.Chance(_s.FairwayShapes.BranchGuardWaterChance);

            return SlotWithFallback(HazardRole.BranchGuard, water, new Vector2(-half, half), w => new Vector2(-1.2f, 0.3f), draw =>
            {
                float along = mid + draw.Along;
                Vector2 direction = Geo.PathDirection(path, along);
                Vector2 branch = path.PointAt(along) + (Geo.Left(direction) * risky.OffsetAt(along));
                Vector2 centre = branch + (Geo.Left(direction) * (outward * (risky.HalfWidthAt(along) + draw.ShortRadius + draw.Across)));
                return new EllipseShape(centre, new Vector2(draw.Radius, draw.ShortRadius), draw.Wobble, Geo.HeadingOf(direction) + draw.Turn);
            }, RuleNearRiskyBranch);
        }

        /// <summary>The risky branch's centre line, sampled every 25 cm over the fork, passes within the guard line reach (whole cm).</summary>
        private bool RuleNearRiskyBranch(HazardOutline h)
        {
            FairwayPiece risky = _shapes.RiskyBranch;
            CoursePath path = _bare.Centreline;
            float best = float.MaxValue;
            for (float s = risky.From; s <= risky.To + 0.001f; s += 0.25f)
            {
                Vector2 p = path.PointAt(s) + (Geo.Left(Geo.PathDirection(path, s)) * risky.OffsetAt(s));
                best = Mathf.Min(best, h.SignedDistance(p, _s.GuardLineReach + 1f));
            }

            return Geo.Cm(best) <= Geo.Cm(_s.GuardLineReach);
        }

        private void PlaceFlank(List<float> zones)
        {
            bool water = _random.Chance(_s.FlankWaterChance);
            int zone = _random.Int(0, zones.Count - 1);
            SlotWithFallback(HazardRole.Flank, water, new Vector2(-4f, 4f), w => w ? new Vector2(-0.5f, 1.5f) : new Vector2(0f, 1f), draw =>
            {
                float along = zones[zone] + draw.Along;

                // Beside the rest spot itself, which a fork moves onto its safe branch (exactly the centreline elsewhere).
                Vector2 centreline = _world.SpotPoint(along);
                Vector2 direction = Geo.PathDirection(_bare.Centreline, along);
                float side = draw.Flip ? 1f : -1f;
                Vector2 centre = centreline + (Geo.Left(direction) * (side * (_s.SafeLandingHalfWidth + RouteWorld.HazardMargin + draw.ShortRadius + draw.Across)));
                // A pond points across the course, toward the island edge, so it can run off it.
                return new EllipseShape(centre, new Vector2(draw.Radius, draw.ShortRadius), draw.Wobble, Geo.HeadingOf(direction) + (draw.Water ? 90f * side : 0f) + draw.Turn);
            }, null);
        }

        /// <summary>The distances along the centreline where the safe route rests, so flanks sit where balls come to rest.</summary>
        private List<float> RestZones()
        {
            var zones = new List<float>();
            SafeRoute.Find(_world, _placed, out List<float> alongs);
            if (alongs != null)
            {
                for (int i = 1; i < alongs.Count - 1; i++)
                {
                    zones.Add(alongs[i]);
                }
            }

            if (zones.Count == 0)
            {
                zones.Add(_length * 0.5f);
            }

            return zones;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Slots and candidates

        /// <summary>
        /// A slot for a kind, then for sand if it was water and found no place (<see cref="LevelGenSettings.WaterFallsBackToSand"/>).
        /// A pond needs more room than most of a 10 m wide island has, so without the fallback a third of the drive zones and a
        /// part of the green guards stayed empty. The second try draws its own candidates, so the stream stays aligned.
        /// </summary>
        private bool SlotWithFallback(HazardRole role, bool water, Vector2 alongRange, Func<bool, Vector2> acrossFor, Func<Draw, EllipseShape> build, Func<HazardOutline, bool> roleRule)
        {
            return Slot(role, water, alongRange, acrossFor(water), build, roleRule)
                || (water && _s.WaterFallsBackToSand && Slot(role, false, alongRange, acrossFor(false), build, roleRule));
        }

        private bool Slot(HazardRole role, bool water, Vector2 alongRange, Vector2 acrossRange, Func<Draw, EllipseShape> build, Func<HazardOutline, bool> roleRule)
        {
            for (int attempt = 0; attempt < _s.TriesPerSlot; attempt++)
            {
                Draw draw = DrawCandidate(water, alongRange, acrossRange);
                HazardOutline candidate = HazardOutline.Of(build(draw), water, role);
                CandidateObserver?.Invoke(candidate.Shape);
                if (Accept(candidate, roleRule))
                {
                    _placed.Add(candidate);
                    return true;
                }
            }

            return false;
        }

        /// <summary>The seven numbers of one candidate, in a fixed order, whether it ends up accepted or not.</summary>
        private Draw DrawCandidate(bool water, Vector2 alongRange, Vector2 acrossRange)
        {
            Vector2Int aspect = water ? _s.PondAspect : _s.BunkerAspect;
            return new Draw
            {
                Along = _random.Centimetres(alongRange),
                Across = _random.Centimetres(acrossRange),
                Radius = _random.Centimetres(water ? _s.PondRadius : _s.BunkerRadius),
                Aspect = _random.Int(aspect.x, aspect.y),
                Turn = _random.Degrees(new Vector2(-_s.HazardTurnJitter, _s.HazardTurnJitter)),
                Wobble = _random.Centimetres(water ? _s.PondWobble : _s.BunkerWobble),
                Flip = _random.Coin(),
                Water = water,
            };
        }

        /// <summary>
        /// The rules every hazard passes (plan section 5.3), cheapest first, then its role rule, the pond's rim, and last the
        /// safe route with the hazard in it.
        /// </summary>
        private bool Accept(HazardOutline h, Func<HazardOutline, bool> roleRule)
        {
            // Tee and hole, and with them their flat areas.
            float flat = _bare.FlatRadius + _bare.FlatBlend;
            if (Geo.Cm(h.SignedDistance(_tee, 20f)) < Geo.Cm(Mathf.Max(_s.HazardFromTee, flat))
                || Geo.Cm(h.SignedDistance(_hole, 20f)) < Geo.Cm(Mathf.Max(_s.HazardFromHole, flat)))
            {
                return false;
            }

            // The green keeps its clean border.
            if (Geo.Cm(h.Gap(_greenOutline)) < Geo.Cm(h.IsWater ? _s.PondFromGreen : _s.BunkerFromGreen))
            {
                return false;
            }

            if (!RuleOnLand(h) || !RuleOffShapes(h))
            {
                return false;
            }

            foreach (HazardOutline other in _placed)
            {
                if (Geo.Cm(h.Gap(other)) < Geo.Cm(_s.HazardSpacing))
                {
                    return false;
                }
            }

            if (!RuleGaps(h))
            {
                return false;
            }

            if (!h.IsWater && !RuleBunkerSlope(h))
            {
                return false;
            }

            if (roleRule != null && !roleRule(h))
            {
                return false;
            }

            if (h.IsWater && !RuleRim(h))
            {
                return false;
            }

            // Corner hazards placed before keep their cutting shot's landing.
            var withIt = new List<HazardOutline>(_placed) { h };
            foreach (float landing in _cutLandings)
            {
                if (!_world.CapsuleClear(landing, withIt))
                {
                    return false;
                }
            }

            // The safe route must survive the hazard. If the bare course has none, there is nothing to protect.
            if (_bareRouteExists())
            {
                if (SafeRoute.Find(_world, withIt, out _) == null)
                {
                    return false;
                }

                return true;
            }

            return true;
        }

        private bool? _bareExists;

        private bool _bareRouteExists()
        {
            _bareExists ??= SafeRoute.Find(_world, new List<HazardOutline>(), out _) != null;
            return _bareExists.Value;
        }

        // ---------------------------------------------------------------------------------------------------------------
        // The rules

        /// <summary>A bunker keeps off the edge; a pond either keeps off it or runs off it, with no sliver of land between.</summary>
        private bool RuleOnLand(HazardOutline h)
        {
            if (!h.IsWater)
            {
                if (Geo.Cm(_fields.Island(Geo.At(h.Centre))) > -Geo.Cm(_s.BunkerFromEdge))
                {
                    return false;
                }

                foreach (Vector2 p in h.Ring)
                {
                    if (Geo.Cm(_fields.Island(Geo.At(p))) > -Geo.Cm(_s.BunkerFromEdge))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (Geo.Cm(_fields.Island(Geo.At(h.Centre))) > -Geo.Cm(_s.PondEdgeCrossing))
            {
                return false;
            }

            // Each rim point is inland (clear of the edge), off the island, or in between. A point in between is fine only where
            // the rim crosses the edge, i.e. next to a point off the island; anywhere else it would leave a sliver of land.
            int count = HazardOutline.RingPoints;
            var state = new int[count];
            int inland = 0;
            for (int i = 0; i < count; i++)
            {
                int island = Geo.Cm(_fields.Island(Geo.At(h.Ring[i])));
                state[i] = island <= -Geo.Cm(_s.PondEdgeSliver) ? 0 : island >= Geo.Cm(_s.PondEdgeCrossing) ? 2 : 1;
                inland += state[i] == 0 ? 1 : 0;
            }

            // Walk each run of in-between points: it has to end at a point off the island on at least one side.
            for (int i = 0; i < count; i++)
            {
                if (state[i] != 1 || state[(i + count - 1) % count] == 1)
                {
                    continue;
                }

                int end = i;
                while (state[(end + 1) % count] == 1 && end - i < count)
                {
                    end++;
                }

                if (state[(i + count - 1) % count] != 2 && state[(end + 1) % count] != 2)
                {
                    return false;
                }
            }

            // A third of the rim on the island is enough to give the water a level.
            return inland >= count / 3;
        }

        /// <summary>
        /// A hazard never sits on a fairway shape (split fairway plan §3, director Q6 A), which it would erase: it keeps
        /// <see cref="LevelGenSettings.HazardSpacing"/>, and at least the fairway kept beside it (2 m), from a rough island's outline, and no point of it lies in a band's tip reach
        /// across the fairway's widest reach (its half width and half its wobble). Always true on a plain course.
        /// </summary>
        private bool RuleOffShapes(HazardOutline h)
        {
            if (_shapes == null)
            {
                return true;
            }

            float across = _s.FairwayHalfWidth + (_s.FairwayWobble * 0.5f);

            // The fairway kept beside a rough island is the passage on either side: a hazard on it would close it, so it keeps
            // that far from the oval, not only the spacing between two hazards.
            float fromOval = Mathf.Max(_s.HazardSpacing, _s.FairwayShapes.IslandFairwaySide);
            foreach (GroundKeepOut keepOut in _shapes.KeepOuts)
            {
                if (keepOut.Oval != null)
                {
                    if (Geo.Cm(h.Gap(keepOut.Oval)) < Geo.Cm(fromOval))
                    {
                        return false;
                    }

                    continue;
                }

                if (keepOut.IsSpine)
                {
                    // A fork's spine (F3.3): no point of the hazard on it, and no spine sample inside the hazard.
                    if (!OffSpine(h, keepOut))
                    {
                        return false;
                    }

                    continue;
                }

                if (InBand(h.Centre, keepOut, across))
                {
                    return false;
                }

                foreach (Vector2 p in h.Ring)
                {
                    if (InBand(p, keepOut, across))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool OffSpine(HazardOutline h, GroundKeepOut spine)
        {
            CoursePath path = _bare.Centreline;
            if (spine.Blocks(h.Centre, path))
            {
                return false;
            }

            foreach (Vector2 p in h.Ring)
            {
                if (spine.Blocks(p, path))
                {
                    return false;
                }
            }

            foreach (Vector2 p in spine.SpineSamples(path))
            {
                if (h.Contains(p))
                {
                    return false;
                }
            }

            return true;
        }

        private bool InBand(Vector2 p, GroundKeepOut band, float across)
        {
            int along = Geo.Cm(_bare.Centreline.Project(p, out float side));
            return along >= Geo.Cm(band.From) && along <= Geo.Cm(band.To) && Geo.Cm(Mathf.Abs(side)) <= Geo.Cm(across);
        }

        /// <summary>No water near a gap's tips, and nothing on the safe corridor just past a gap.</summary>
        private bool RuleGaps(HazardOutline h)
        {
            foreach (Vector2 gap in _world.Gaps)
            {
                if (h.IsWater)
                {
                    foreach (float along in new[] { gap.x, gap.y })
                    {
                        if (Geo.Cm(h.SignedDistance(_bare.Centreline.PointAt(along), _s.PondFromGapEdge + 1f)) < Geo.Cm(_s.PondFromGapEdge))
                        {
                            return false;
                        }
                    }
                }

                for (float s = gap.y; s <= gap.y + _s.GapLandingZone; s += RouteWorld.Grid)
                {
                    if (Geo.Cm(h.SignedDistance(_bare.Centreline.PointAt(s), _s.ShotCorridorHalfWidth + 1f)) < Geo.Cm(_s.ShotCorridorHalfWidth))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>A bunker sits where the broad ground is gentle: at its centre and four points of its border.</summary>
        private bool RuleBunkerSlope(HazardOutline h)
        {
            var points = new List<Vector2> { h.Centre };
            for (int i = 0; i < HazardOutline.RingPoints; i += 6)
            {
                points.Add(h.Ring[i]);
            }

            foreach (Vector2 p in points)
            {
                if (Geo.Cm(_fields.BroadSlope(p.x, p.y).magnitude * 100f) > Geo.Cm(_s.BunkerMaxSlope * 100f))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A pond sits on level enough ground and its water just below the lowest rim: the rule on the broad height of the
        /// rim points on the island, and the level taken from it. The drawn ground has facet noise on top (up to the edge
        /// relief near the edge), so the level is also kept 8 cm under the drawn rim, or the bank would be inverted there.
        /// </summary>
        public static bool RimLevel(CourseFields fields, LevelGenSettings s, HazardOutline h, out float level, out float broadSpread)
        {
            float broadLow = float.MaxValue, broadHigh = float.MinValue, drawnLow = float.MaxValue;
            int count = 0;
            foreach (Vector2 p in h.Ring)
            {
                if (fields.Island(Geo.At(p)) >= 0f)
                {
                    continue;
                }

                float broad = fields.BroadHeight(p.x, p.y);
                broadLow = Mathf.Min(broadLow, broad);
                broadHigh = Mathf.Max(broadHigh, broad);
                drawnLow = Mathf.Min(drawnLow, fields.Height(p.x, p.y));
                count++;
            }

            if (count == 0)
            {
                level = 0f;
                broadSpread = float.MaxValue;
                return false;
            }

            broadSpread = broadHigh - broadLow;
            level = Mathf.Min(broadLow - s.PondDrop, drawnLow - 0.08f);
            return true;
        }

        private bool RuleRim(HazardOutline h)
        {
            if (!RimLevel(_fields, _s, h, out float level, out float spread) || Geo.Cm(spread) > Geo.Cm(_s.PondMaxRimRise))
            {
                return false;
            }

            h.WaterLevel = level;
            return true;
        }

        /// <summary>A green guard is a hazard the direct line to the pin risks: it passes within a metre and a half of it.</summary>
        private bool RuleGuardNearDirectLine(HazardOutline h)
        {
            // On a fork, the direct line's last segment: from its bend on the risky branch.
            Vector2 a = _shapes?.DirectWaypoint ?? _green.DirectOrigin;
            Vector2 b = _hole;
            float best = float.MaxValue;
            float length = Vector2.Distance(a, b);
            for (float t = 0f; t <= length; t += 0.25f)
            {
                best = Mathf.Min(best, h.SignedDistance(Vector2.Lerp(a, b, t / length), _s.GuardLineReach + 1f));
            }

            return Geo.Cm(best) <= Geo.Cm(_s.GuardLineReach);
        }

        /// <summary>
        /// A corner hazard is one to carry to cut the corner: the cut line crosses it, it ends at least
        /// <see cref="LevelGenSettings.CutLandingMargin"/> before the shot lands, and the landing is clear.
        /// </summary>
        private bool RuleCarriesTheCut(HazardOutline h, Vector2 origin, Vector2 cut, float cutAlong)
        {
            float length = Vector2.Distance(origin, cut);
            float farEdge = -1f;
            for (float t = 0f; t <= length; t += 0.1f)
            {
                if (h.Contains(Vector2.Lerp(origin, cut, t / length)))
                {
                    farEdge = t;
                }
            }

            if (farEdge < 0f || Geo.Cm(farEdge) > Geo.Cm(_s.CarryAt(_s.CutPower) - _s.CutLandingMargin))
            {
                return false;
            }

            var withIt = new List<HazardOutline>(_placed) { h };
            return _world.CapsuleClear(cutAlong, withIt);
        }

        /// <summary>A drive-zone hazard is on the drive: a cross hazard is crossed by the straight line, a flank one comes close to it.</summary>
        private bool RuleOnTheDrive(HazardOutline h, Vector2 d0, Vector2 zone, bool cross)
        {
            float best = float.MaxValue;
            for (float t = zone.x; t <= zone.y; t += 0.1f)
            {
                float distance = h.SignedDistance(_tee + (d0 * t), _s.DriveLineReach + 1f);
                if (cross && distance < 0f)
                {
                    return true;
                }

                best = Mathf.Min(best, distance);
            }

            return !cross && Geo.Cm(best) <= Geo.Cm(_s.DriveLineReach);
        }

        // ---------------------------------------------------------------------------------------------------------------
        // Geometry of the roles

        /// <summary>How far the green reaches from its centre in a direction (its radius that way, the wobble left out).</summary>
        private float GreenExtent(Vector2 u)
        {
            Vector2 r = _bare.Greens[0].Outline.Radius;
            return 1f / Mathf.Sqrt(((u.x * u.x) / (r.x * r.x)) + ((u.y * u.y) / (r.y * r.y)));
        }

        /// <summary>
        /// The shot that cuts a dogleg's corner: from the tee (the first bend) or from where a drive would leave the ball (the
        /// second), to the point of the next leg a cutting shot's carry from it. False if the corner is too far to cut. Geometry
        /// only, no draw: the fork placement (F3.3) reads it too, so no cut line crosses a spine.
        /// </summary>
        internal static bool CutLine(CoursePath path, IReadOnlyList<float> bends, int bend, LevelGenSettings s, out Vector2 origin, out Vector2 cut, out float cutAlong)
        {
            float carry = s.CarryAt(s.CutPower);
            float bendAlong = bends[bend];
            origin = bend == 0 ? path.Start : path.PointAt(Mathf.Max(0f, bendAlong - (carry * 0.8f)));
            cut = origin;
            cutAlong = bendAlong;
            if (Geo.Cm(Vector2.Distance(origin, path.PointAt(bendAlong))) > Geo.Cm(carry))
            {
                return false;
            }

            float legEnd = bend + 1 < bends.Count ? bends[bend + 1] : path.Length;
            for (float t = bendAlong; t <= legEnd + 0.001f; t += 0.25f)
            {
                cutAlong = Mathf.Min(t, legEnd);
                cut = path.PointAt(cutAlong);
                if (Geo.Cm(Vector2.Distance(origin, cut)) >= Geo.Cm(carry))
                {
                    break;
                }
            }

            return true;
        }

        /// <summary>The corner's cut line (<see cref="CutLine"/>), added to the plan once.</summary>
        private bool CutLineFor(int bend, out Vector2 origin, out Vector2 cut, out float cutAlong)
        {
            if (!CutLine(_bare.Centreline, _bends, bend, _s, out origin, out cut, out cutAlong))
            {
                return false;
            }

            bool known = false;
            foreach (ShotLine line in _lines)
            {
                known |= line.Kind == ShotLineKind.Cut && line.From == origin;
            }

            if (!known)
            {
                _lines.Add(new ShotLine(origin, cut, ShotLineKind.Cut));
            }

            return true;
        }
    }
}
