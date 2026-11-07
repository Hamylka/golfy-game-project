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

        public Vector2 Length => length;
        public int DoglegChance => doglegChance;
        public int SecondDoglegChance => secondDoglegChance;
        public int SoftBends => softBends;
        public int GapChance => gapChance;
        public int IslandGreenChance => islandGreenChance;
        public float FairwayRelief => fairwayRelief;
        public Vector2 GreenHeight => greenHeight;
        public float KnotSpacing => knotSpacing;

        public static ParRules Create(Vector2 length, int doglegChance, int secondDoglegChance, int softBends, int gapChance, int islandGreenChance, float fairwayRelief = 0.5f, Vector2 greenHeight = default, float knotSpacing = 6f)
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
        [SerializeField] private ParRules par3 = ParRules.Create(new Vector2(18f, 26f), 0, 0, 0, 0, 15, 0.5f, new Vector2(-0.6f, 0.6f), 6f);
        [SerializeField] private ParRules par4 = ParRules.Create(new Vector2(40f, 55f), 50, 0, 1, 60, 0, 1f, new Vector2(-1.2f, 1.2f), 8f);
        [SerializeField] private ParRules par5 = ParRules.Create(new Vector2(65f, 85f), 50, 50, 2, 33, 0, 1.2f, new Vector2(-1.5f, 1.5f), 9f);

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
    }
}
