using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>What the generator may do on one par. Chances are in percent; places along a course are fractions of its length.</summary>
    [Serializable]
    public sealed class ParRules
    {
        [Tooltip("Length from tee to hole along the centreline, in metres.")]
        [SerializeField] private Vector2 length = new Vector2(18f, 26f);

        [Header("Shape")]
        [Tooltip("Chance of a dogleg, in percent. The others get soft bends.")]
        [SerializeField, Range(0, 100)] private int doglegChance;
        [Tooltip("Chance that a dogleg turns a second time, in percent.")]
        [SerializeField, Range(0, 100)] private int secondDoglegChance;
        [Tooltip("How many soft bends a course without a dogleg gets. 0 keeps it straight along +X.")]
        [SerializeField, Range(0, 2)] private int softBends;

        [Header("Gaps")]
        [Tooltip("Chance of one gap of open sky across the course, in percent.")]
        [SerializeField, Range(0, 100)] private int gapChance;
        [Tooltip("Chance that the green sits on its own island, in percent.")]
        [SerializeField, Range(0, 100)] private int islandGreenChance;

        [Header("Elevation")]
        [Tooltip("The fairway rises and dips this far from the tee height at most, in metres. A knot is drawn within it.")]
        [SerializeField, Min(0f)] private float fairwayRelief = 0.5f;
        [Tooltip("The green sits this far above (positive) or below the tee, in metres, from the shortest to the longest. Capped by what the slope limit allows.")]
        [SerializeField] private Vector2 greenHeight = new Vector2(-0.6f, 0.6f);
        [Tooltip("About how far apart the height knots sit along the centreline, in metres. At least one interval.")]
        [SerializeField, Min(1f)] private float knotSpacing = 6f;

        [Header("Hazards")]
        [Tooltip("Hazards on the course, from the fewest to the most. A gap counts as one when gapCountsAsHazard is set.")]
        [SerializeField] private Vector2Int hazardCount = new Vector2Int(3, 5);
        [Tooltip("Chance of a second bunker beside the green, in percent.")]
        [SerializeField, Range(0, 100)] private int secondGuardChance = 35;
        [Tooltip("Chance of a hazard in the drive zone of a course without a dogleg, in percent. A par 3 has none.")]
        [SerializeField, Range(0, 100)] private int driveZoneChance = 100;

        public Vector2 Length => length;
        public int DoglegChance => doglegChance;
        public int SecondDoglegChance => secondDoglegChance;
        public int SoftBends => softBends;
        public int GapChance => gapChance;
        public int IslandGreenChance => islandGreenChance;
        public float FairwayRelief => fairwayRelief;
        public Vector2 GreenHeight => greenHeight;
        public float KnotSpacing => knotSpacing;
        public Vector2Int HazardCount => hazardCount;
        public int SecondGuardChance => secondGuardChance;
        public int DriveZoneChance => driveZoneChance;

        public static ParRules Create(Vector2 length, int doglegChance, int secondDoglegChance, int softBends, int gapChance, int islandGreenChance, float fairwayRelief = 0.5f, Vector2 greenHeight = default, float knotSpacing = 6f, Vector2Int hazardCount = default, int secondGuardChance = 35, int driveZoneChance = 100)
        {
            return new ParRules
            {
                length = length,
                doglegChance = doglegChance,
                secondDoglegChance = secondDoglegChance,
                softBends = softBends,
                gapChance = gapChance,
                islandGreenChance = islandGreenChance,
                fairwayRelief = fairwayRelief,
                greenHeight = greenHeight == default ? new Vector2(-0.6f, 0.6f) : greenHeight,
                knotSpacing = knotSpacing,
                hazardCount = hazardCount == default ? new Vector2Int(3, 5) : hazardCount,
                secondGuardChance = secondGuardChance,
                driveZoneChance = driveZoneChance,
            };
        }
    }

    /// <summary>
    /// The ranges the course generator draws from. The decisions behind them are in <c>CLAUDE.md</c> (Key systems:
    /// Level generator); everything here can be tuned without touching the code. Relief, surfaces and hazards come in
    /// later PRs and keep today's flat ground for now.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelGenSettings", menuName = "Iso Golf/Level Gen Settings")]
    public sealed class LevelGenSettings : ScriptableObject
    {
        [Header("Pars")]
        [SerializeField] private ParRules par3 = ParRules.Create(new Vector2(18f, 26f), 0, 0, 0, 0, 15, 0.5f, new Vector2(-0.6f, 0.6f), 6f, new Vector2Int(2, 4), 25, 0);
        [SerializeField] private ParRules par4 = ParRules.Create(new Vector2(40f, 55f), 50, 0, 1, 60, 0, 1f, new Vector2(-1.2f, 1.2f), 8f, new Vector2Int(3, 5), 35, 100);
        [SerializeField] private ParRules par5 = ParRules.Create(new Vector2(65f, 85f), 50, 50, 2, 33, 0, 1.2f, new Vector2(-1.5f, 1.5f), 9f, new Vector2Int(3, 6), 50, 100);

        [Header("Bends")]
        [Tooltip("How far a dogleg turns, in degrees.")]
        [SerializeField] private Vector2 doglegTurn = new Vector2(15f, 35f);
        [Tooltip("How far a soft bend turns, in degrees.")]
        [SerializeField] private Vector2 softTurn = new Vector2(3f, 8f);
        [Tooltip("No leg ever heads further than this from +X, in degrees.")]
        [SerializeField, Range(0f, 45f)] private float maxHeading = 35f;
        [Tooltip("The first leg's heading on a course that bends, in degrees either side of +X. A par 3 is always along +X.")]
        [SerializeField, Range(0f, 15f)] private float firstLegHeading = 4f;
        [Tooltip("Where a single bend sits, as a fraction of the course from the tee.")]
        [SerializeField] private Vector2 singleBendPlace = new Vector2(0.45f, 0.65f);
        [Tooltip("Where the first of two bends sits.")]
        [SerializeField] private Vector2 firstOfTwoBendsPlace = new Vector2(0.3f, 0.42f);
        [Tooltip("Where the second of two bends sits.")]
        [SerializeField] private Vector2 secondOfTwoBendsPlace = new Vector2(0.62f, 0.75f);

        [Header("Islands")]
        [Tooltip("Half the width of an island, before its wobble, in metres.")]
        [SerializeField] private Vector2 islandHalfWidth = new Vector2(4.6f, 5.2f);
        [Tooltip("How much the width wobbles, in metres.")]
        [SerializeField] private Vector2 islandWobble = new Vector2(2.2f, 3.2f);
        [SerializeField, Min(0f)] private float widthNoiseScale = 0.18f;
        [Tooltip("How far each leg's island runs on past a bend, so the legs join without a waist, in metres.")]
        [SerializeField, Min(0f)] private float bendOverlap = 5f;
        [Tooltip("Ground behind the tee, to the island's tip, in metres.")]
        [SerializeField, Min(1f)] private float behindTee = 3.5f;
        [Tooltip("Ground past the hole, to the island's tip, in metres. M5's next island goes beyond it.")]
        [SerializeField, Min(1f)] private float pastHole = 4.5f;

        [Header("Gaps")]
        [Tooltip("Width of a gap across a par 4 or 5, tip to tip along the centreline, in metres.")]
        [SerializeField] private Vector2 gapWidth = new Vector2(4f, 10f);
        [Tooltip("Width of the gap in front of an island green, in metres.")]
        [SerializeField] private Vector2 islandGreenGapWidth = new Vector2(4f, 7f);
        [Tooltip("A gap starts at least this far from the tee, in metres, so it is never right in front of it.")]
        [SerializeField, Min(0f)] private float gapFromTee = 15f;
        [Tooltip("A par 4 or 5's gap ends at least this far before the hole, in metres.")]
        [SerializeField, Min(0f)] private float gapFromHole = 10f;
        [Tooltip("A gap keeps at least this far from a dogleg's bend, in metres, so no island is a sliver.")]
        [SerializeField, Min(0f)] private float gapFromBend = 8f;
        [Tooltip("The same for a soft bend, which barely turns, in metres. The islands either side never run on past the gap.")]
        [SerializeField, Min(0f)] private float gapFromSoftBend = 1f;
        [Tooltip("An island green starts this far before the hole, in metres.")]
        [SerializeField, Min(1f)] private float islandGreenBeforeHole = 5.5f;
        [Tooltip("Half the width of an island green, before its wobble, in metres.")]
        [SerializeField] private Vector2 islandGreenHalfWidth = new Vector2(4.2f, 4.8f);
        [Tooltip("An island green's wobble, in metres.")]
        [SerializeField] private Vector2 islandGreenWobble = new Vector2(1f, 1.6f);
        [Tooltip("A par 3 with an island green keeps at least this much ground ahead of the tee, in metres.")]
        [SerializeField, Min(0f)] private float islandGreenFromTee = 10f;

        [Header("Fairway and green")]
        [Tooltip("The fairway starts this far behind the tee, inside the island's tip, in metres.")]
        [SerializeField, Min(0f)] private float fairwayBehindTee = 2f;
        [Tooltip("The fairway ends this far short of the hole, under the green's front, in metres.")]
        [SerializeField, Min(0f)] private float fairwayShortOfHole = 3.7f;
        [SerializeField, Min(0.5f)] private float fairwayHalfWidth = 2.5f;
        [SerializeField, Min(0f)] private float fairwayWobble = 1.6f;
        [SerializeField, Min(0.1f)] private float stripeWidth = 1f;
        [Tooltip("The green's half sizes along X and Z, in metres.")]
        [SerializeField] private Vector2 greenRadius = new Vector2(3f, 2.7f);
        [SerializeField, Min(0f)] private float greenWobble = 0.12f;
        [Tooltip("How far the green's centre sits short of the hole, along the centreline, in metres.")]
        [SerializeField] private float greenCentreShortOfHole = 1.4f;

        [Header("Ground")]
        [SerializeField, Min(0.25f)] private float cellSize = 1f;
        [SerializeField, Min(0f)] private float axisRelief = 0.05f;
        [SerializeField, Min(0f)] private float edgeRelief = 0.4f;
        [SerializeField, Min(0.1f)] private float reliefDistance = 5f;
        [SerializeField, Min(0f)] private float reliefNoiseScale = 0.55f;
        [SerializeField] private float waterLevel = -0.22f;
        [SerializeField, Min(0f)] private float flatRadius = 1.2f;
        [SerializeField, Min(0.01f)] private float flatBlend = 0.8f;

        [Header("Elevation")]
        [Tooltip("The steepest slope along the centreline, as a fraction (0.09 is 9%). Knots are drawn so the profile never exceeds it.")]
        [SerializeField, Range(0.01f, 0.2f)] private float maxAlongSlope = 0.09f;
        [Tooltip("The steepest side slope, in whole percent.")]
        [SerializeField, Range(0, 10)] private int maxCamber = 4;
        [Tooltip("The most the side slope may change from one knot to the next, in whole percent. It adds to the slope along the centreline off the centreline, so it keeps the fairway near 10%.")]
        [SerializeField, Range(1, 10)] private int maxCamberStep = 2;
        [Tooltip("The green's tilt, from the gentlest to the steepest, in whole percent.")]
        [SerializeField] private Vector2 greenTilt = new Vector2(2f, 6f);
        [Tooltip("The profile stays flat this far beyond the tee's and the green's flat areas, in metres, so they sit on level ground.")]
        [SerializeField, Min(0f)] private float plateauMargin = 1f;
        [Tooltip("Around a bend the profile is read from every leg this close (in metres) to the nearest one and blended, so it has no step.")]
        [SerializeField, Min(0.1f)] private float legBlend = 4f;

        [Header("Edge")]
        [SerializeField] private Vector2 skirtDepth = new Vector2(0.9f, 1.8f);
        [SerializeField, Min(0f)] private float skirtNoiseScale = 0.7f;

        // Hazards, the green's pin and the safe route (M3 PR 6b). The decisions are in CLAUDE.md (Key systems: Level generator,
        // Hazards). Lengths in metres, angles in degrees, chances in percent.
        [Header("Shot model (copies of ShotSettings; LevelGenShotModelTests keeps them equal)")]
        [Tooltip("Carry at zero power on flat ground. Equals ShotSettings.minCarry.")]
        [SerializeField, Min(0.1f)] private float minCarry = 1f;
        [Tooltip("Carry at full power on flat ground. Equals ShotSettings.maxCarry.")]
        [SerializeField, Min(0.1f)] private float fullCarry = 26f;
        [Tooltip("How far a full-power shot goes, carry and roll. Within 1 m of ShotReach.FullPowerDistance.")]
        [SerializeField, Min(0.1f)] private float fullReach = 32f;
        [Tooltip("The power a golfer uses to cut a dogleg's corner.")]
        [SerializeField, Range(0f, 1f)] private float cutPower = 0.9f;
        [Tooltip("The power that must be enough to carry a gap from the lay-up spot.")]
        [SerializeField, Range(0f, 1f)] private float layupPower = 0.7f;
        [Tooltip("The longest step of the safe route, as the crow flies.")]
        [SerializeField, Min(1f)] private float safeStep = 24f;

        [Header("Hazards")]
        [Tooltip("A gap (or an island green's gap) counts as one of the course's hazards.")]
        [SerializeField] private bool gapCountsAsHazard = true;
        [Tooltip("Chance that a hazard guarding the green is water, in percent.")]
        [SerializeField, Range(0, 100)] private int greenGuardWaterChance = 30;
        [Tooltip("The same for the hazard at a dogleg's corner.")]
        [SerializeField, Range(0, 100)] private int cornerWaterChance = 60;
        [Tooltip("The same for the hazard in the drive zone.")]
        [SerializeField, Range(0, 100)] private int driveZoneWaterChance = 35;
        [Tooltip("The same for the hazards that fill up the course.")]
        [SerializeField, Range(0, 100)] private int flankWaterChance = 20;
        [Tooltip("Cross: the drive zone's hazard reaches over the centreline. Flank: it sits beside the fairway.")]
        [SerializeField] private DriveZoneStyle driveZoneStyle = DriveZoneStyle.Cross;
        [Tooltip("Chance of a drive zone hazard on a par 5 with a dogleg, in percent.")]
        [SerializeField, Range(0, 100)] private int driveZoneWithDoglegChance = 40;
        [Tooltip("A pond that finds no place is tried again as a bunker, so the role is not left empty. Off: the slot stays empty, as in the written plan.")]
        [SerializeField] private bool waterFallsBackToSand = true;
        [Tooltip("Candidates tried for each hazard before its slot stays empty.")]
        [SerializeField, Range(1, 64)] private int triesPerSlot = 24;
        [Tooltip("A bunker's long half-axis.")]
        [SerializeField] private Vector2 bunkerRadius = new Vector2(0.9f, 1.6f);
        [Tooltip("A bunker's short half-axis as a percentage of the long one.")]
        [SerializeField] private Vector2Int bunkerAspect = new Vector2Int(55, 85);
        [SerializeField] private Vector2 bunkerWobble = new Vector2(0.12f, 0.25f);
        [Tooltip("A pond's long half-axis.")]
        [SerializeField] private Vector2 pondRadius = new Vector2(1.8f, 3.4f);
        [SerializeField] private Vector2Int pondAspect = new Vector2Int(45, 75);
        [SerializeField] private Vector2 pondWobble = new Vector2(0.15f, 0.3f);
        [Tooltip("How far a hazard's heading may stray from its role's, in degrees either side.")]
        [SerializeField, Range(0f, 90f)] private float hazardTurnJitter = 20f;
        [SerializeField, Min(0f)] private float hazardFromTee = 6f;
        [SerializeField, Min(0f)] private float hazardFromHole = 3f;
        [Tooltip("Free ground between two hazards.")]
        [SerializeField, Min(0f)] private float hazardSpacing = 1.2f;
        [Tooltip("A bunker keeps this far from the island's edge.")]
        [SerializeField, Min(0f)] private float bunkerFromEdge = 0.6f;
        [Tooltip("A pond either keeps this far from the edge or runs off it, never leaving a sliver of land.")]
        [SerializeField, Min(0f)] private float pondEdgeSliver = 1.2f;
        [Tooltip("A pond that runs off the edge crosses it by at least this much.")]
        [SerializeField, Min(0f)] private float pondEdgeCrossing = 0.6f;
        [Tooltip("A pond keeps this far from the tips of the islands either side of a gap.")]
        [SerializeField, Min(0f)] private float pondFromGapEdge = 3f;
        [Tooltip("No hazard on the safe corridor within this far past a gap's far edge.")]
        [SerializeField, Min(0f)] private float gapLandingZone = 6f;
        [Tooltip("A bunker's broad slope, rise per metre.")]
        [SerializeField, Range(0.01f, 0.3f)] private float bunkerMaxSlope = 0.08f;
        [Tooltip("The most a pond's rim may vary in height.")]
        [SerializeField, Min(0f)] private float pondMaxRimRise = 0.35f;
        [Tooltip("The water sits this far below the lowest point of the rim.")]
        [SerializeField, Min(0f)] private float pondDrop = 0.22f;
        [Tooltip("Free ground between the green's border and a bunker that guards it.")]
        [SerializeField] private Vector2 guardBunkerClearance = new Vector2(0.3f, 1f);
        [Tooltip("The same for a pond.")]
        [SerializeField] private Vector2 guardPondClearance = new Vector2(1f, 2f);
        [Tooltip("The least free ground between the green's border and a bunker.")]
        [SerializeField, Min(0f)] private float bunkerFromGreen = 0.3f;
        [SerializeField, Min(0f)] private float pondFromGreen = 1f;
        [Tooltip("A green guard comes at least this close to the direct line to the pin.")]
        [SerializeField, Min(0f)] private float guardLineReach = 1.5f;
        [Tooltip("The corner hazard ends at least this far before the cut shot lands.")]
        [SerializeField, Min(0f)] private float cutLandingMargin = 2f;
        [Tooltip("Flank style: the drive zone's hazard comes at least this close to the drive line.")]
        [SerializeField, Min(0f)] private float driveLineReach = 1.5f;

        [Header("Green and pin")]
        [Tooltip("Beyond this angle between the last leg and the direct line the guard sits on the line's side, in degrees.")]
        [SerializeField, Min(0f)] private float sideGuardThreshold = 8f;
        [SerializeField, Min(0f)] private float sideGuardJitter = 15f;
        [Tooltip("A guard in front of the green sits this far to one side of the approach, in degrees.")]
        [SerializeField] private Vector2 frontGuardAngle = new Vector2(15f, 40f);
        [Tooltip("The hole sits this far from the green's centre toward the guard, in metres.")]
        [SerializeField] private Vector2 pinOffset = new Vector2(0.5f, 1f);
        [Tooltip("The hole also sits this far toward the back of the green (negative: the front).")]
        [SerializeField] private Vector2 pinAlong = new Vector2(-0.4f, 0.6f);
        [Tooltip("The hole keeps this far from the green's border, so the flat ground around it stays on the green.")]
        [SerializeField, Min(0f)] private float pinFromBorder = 1.5f;

        [Header("Safe route")]
        [Tooltip("Shots the safe route may take beyond par - 1. The plan says 0; with 0 about one par 5 in eight has no route at all because of a gap close to the green, so the default is 1 (a route of at most par shots to the safe target).")]
        [SerializeField, Range(0, 3)] private int safeRouteExtraShots = 1;
        [Tooltip("A rest spot's landing capsule starts this far behind it, along the centreline.")]
        [SerializeField, Min(0f)] private float safeLandingBack = 5f;
        [SerializeField, Min(0f)] private float safeLandingAhead = 1.5f;
        [SerializeField, Min(0.1f)] private float safeLandingHalfWidth = 1.5f;
        [Tooltip("The capsule keeps this far inside the island's edge.")]
        [SerializeField, Min(0f)] private float edgeMargin = 0.8f;
        [Tooltip("The lay-up shot lands this far past a gap's far edge at least.")]
        [SerializeField, Min(0f)] private float gapLandingMargin = 2f;
        [Tooltip("Half the width of the strip along the shot lines that M4 keeps free of props.")]
        [SerializeField, Min(0f)] private float shotCorridorHalfWidth = 2f;

        [Header("Course checks (read by Gameplay's CourseChecks)")]
        [Tooltip("Variants tried per seed and par before the generator keeps the one that failed the fewest checks.")]
        [SerializeField, Range(1, 32)] private int checkVariants = 8;
        [Tooltip("Some approach from the safe route's last rest spot must hole or stop on the green this close to the hole.")]
        [SerializeField, Min(0.1f)] private float restNearHole = 2f;
        [Tooltip("The approach is tried straight at the hole and this many degrees either side.")]
        [SerializeField, Range(0f, 5f)] private float restCheckAim = 1f;
        [Tooltip("Powers of the approach. The low end is small because the last rest spot is often a lay-up a few metres short.")]
        [SerializeField] private Vector2 restCheckPower = new Vector2(0.02f, 1f);
        [Tooltip("Power step of the simulated check shots.")]
        [SerializeField, Range(0.005f, 0.1f)] private float checkPowerStep = 0.02f;
        [Tooltip("The weakest shot tried across a gap; the strongest is the lay-up power.")]
        [SerializeField, Range(0f, 1f)] private float gapCheckMinPower = 0.3f;

        /// <summary>The rules for a par from 3 to 5.</summary>
        public ParRules Rules(int par)
        {
            switch (par)
            {
                case 3: return par3;
                case 4: return par4;
                case 5: return par5;
                default: throw new ArgumentOutOfRangeException(nameof(par), par, "A course is a par 3, 4 or 5.");
            }
        }

        public Vector2 DoglegTurn => doglegTurn;
        public Vector2 SoftTurn => softTurn;
        public float MaxHeading => maxHeading;
        public float FirstLegHeading => firstLegHeading;
        public Vector2 SingleBendPlace => singleBendPlace;
        public Vector2 FirstOfTwoBendsPlace => firstOfTwoBendsPlace;
        public Vector2 SecondOfTwoBendsPlace => secondOfTwoBendsPlace;
        public Vector2 IslandHalfWidth => islandHalfWidth;
        public Vector2 IslandWobble => islandWobble;
        public float WidthNoiseScale => widthNoiseScale;
        public float BendOverlap => bendOverlap;
        public float BehindTee => behindTee;
        public float PastHole => pastHole;
        public Vector2 GapWidth => gapWidth;
        public Vector2 IslandGreenGapWidth => islandGreenGapWidth;
        public float GapFromTee => gapFromTee;
        public float GapFromHole => gapFromHole;
        public float GapFromBend => gapFromBend;
        public float GapFromSoftBend => gapFromSoftBend;
        public float IslandGreenBeforeHole => islandGreenBeforeHole;
        public Vector2 IslandGreenHalfWidth => islandGreenHalfWidth;
        public Vector2 IslandGreenWobble => islandGreenWobble;
        public float IslandGreenFromTee => islandGreenFromTee;
        public float FairwayBehindTee => fairwayBehindTee;
        public float FairwayShortOfHole => fairwayShortOfHole;
        public float FairwayHalfWidth => fairwayHalfWidth;
        public float FairwayWobble => fairwayWobble;
        public float StripeWidth => stripeWidth;
        public Vector2 GreenRadius => greenRadius;
        public float GreenWobble => greenWobble;
        public float GreenCentreShortOfHole => greenCentreShortOfHole;
        public float CellSize => cellSize;
        public float AxisRelief => axisRelief;
        public float EdgeRelief => edgeRelief;
        public float ReliefDistance => reliefDistance;
        public float ReliefNoiseScale => reliefNoiseScale;
        public float WaterLevel => waterLevel;
        public float FlatRadius => flatRadius;
        public float FlatBlend => flatBlend;
        public float MaxAlongSlope => maxAlongSlope;
        public int MaxCamber => maxCamber;
        public int MaxCamberStep => maxCamberStep;
        public Vector2 GreenTilt => greenTilt;
        public float PlateauMargin => plateauMargin;
        public float LegBlend => legBlend;
        public Vector2 SkirtDepth => skirtDepth;
        public float SkirtNoiseScale => skirtNoiseScale;

        public float MinCarry => minCarry;
        public float FullCarry => fullCarry;
        public float FullReach => fullReach;
        public float CutPower => cutPower;
        public float LayupPower => layupPower;
        public float SafeStep => safeStep;

        /// <summary>The carry of a shot at this power on flat ground, like <c>ShotSettings.CarryFor</c>.</summary>
        public float CarryAt(float power) => Mathf.Lerp(minCarry, fullCarry, power);

        public bool GapCountsAsHazard => gapCountsAsHazard;
        public int GreenGuardWaterChance => greenGuardWaterChance;
        public int CornerWaterChance => cornerWaterChance;
        public int DriveZoneWaterChance => driveZoneWaterChance;
        public int FlankWaterChance => flankWaterChance;
        public DriveZoneStyle DriveZoneStyle => driveZoneStyle;
        public int DriveZoneWithDoglegChance => driveZoneWithDoglegChance;
        public int TriesPerSlot => triesPerSlot;
        public bool WaterFallsBackToSand => waterFallsBackToSand;
        public Vector2 BunkerRadius => bunkerRadius;
        public Vector2Int BunkerAspect => bunkerAspect;
        public Vector2 BunkerWobble => bunkerWobble;
        public Vector2 PondRadius => pondRadius;
        public Vector2Int PondAspect => pondAspect;
        public Vector2 PondWobble => pondWobble;
        public float HazardTurnJitter => hazardTurnJitter;
        public float HazardFromTee => hazardFromTee;
        public float HazardFromHole => hazardFromHole;
        public float HazardSpacing => hazardSpacing;
        public float BunkerFromEdge => bunkerFromEdge;
        public float PondEdgeSliver => pondEdgeSliver;
        public float PondEdgeCrossing => pondEdgeCrossing;
        public float PondFromGapEdge => pondFromGapEdge;
        public float GapLandingZone => gapLandingZone;
        public float BunkerMaxSlope => bunkerMaxSlope;
        public float PondMaxRimRise => pondMaxRimRise;
        public float PondDrop => pondDrop;
        public Vector2 GuardBunkerClearance => guardBunkerClearance;
        public Vector2 GuardPondClearance => guardPondClearance;
        public float BunkerFromGreen => bunkerFromGreen;
        public float PondFromGreen => pondFromGreen;
        public float GuardLineReach => guardLineReach;
        public float CutLandingMargin => cutLandingMargin;
        public float DriveLineReach => driveLineReach;
        public float SideGuardThreshold => sideGuardThreshold;
        public float SideGuardJitter => sideGuardJitter;
        public Vector2 FrontGuardAngle => frontGuardAngle;
        public Vector2 PinOffset => pinOffset;
        public Vector2 PinAlong => pinAlong;
        public float PinFromBorder => pinFromBorder;
        public int SafeRouteExtraShots => safeRouteExtraShots;
        public float SafeLandingBack => safeLandingBack;
        public float SafeLandingAhead => safeLandingAhead;
        public float SafeLandingHalfWidth => safeLandingHalfWidth;
        public float EdgeMargin => edgeMargin;
        public float GapLandingMargin => gapLandingMargin;
        public float ShotCorridorHalfWidth => shotCorridorHalfWidth;
        public int CheckVariants => checkVariants;
        public float RestNearHole => restNearHole;
        public float RestCheckAim => restCheckAim;
        public Vector2 RestCheckPower => restCheckPower;
        public float CheckPowerStep => checkPowerStep;
        public float GapCheckMinPower => gapCheckMinPower;
    }

    /// <summary>Where a drive-zone hazard sits on a straight par 4 or 5.</summary>
    public enum DriveZoneStyle
    {
        /// <summary>Over one side of the fairway and across the centreline: carry it or lay up short.</summary>
        Cross,

        /// <summary>Beside the fairway, in the rough.</summary>
        Flank,
    }
}
