using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>The integer weights of each fairway shape on one par (split fairway plan §1). They need not sum to 100: the draw is over their sum.</summary>
    [Serializable]
    public sealed class FairwayShapeWeights
    {
        [SerializeField, Min(0)] private int plain = 100;
        [SerializeField, Min(0)] private int roughIsland;
        [SerializeField, Min(0)] private int split;
        [Tooltip("Forks come in F3.3: until then a fork draw leaves the course plain.")]
        [SerializeField, Min(0)] private int fork;

        public int Plain => plain;
        public int RoughIsland => roughIsland;
        public int Split => split;
        public int Fork => fork;
        public int Total => plain + roughIsland + split + fork;

        public static FairwayShapeWeights Create(int plain, int roughIsland, int split, int fork) =>
            new FairwayShapeWeights { plain = plain, roughIsland = roughIsland, split = split, fork = fork };
    }

    /// <summary>What a par 4 or 5 shape's window is measured from. A par 3's shape has no window: anywhere its clearances allow.</summary>
    public enum ShapeWindowOrigin
    {
        /// <summary>The tee (the plan's first wording): the drive's landing. A par 5's second shape: a full reach further on.</summary>
        TeeOnly,

        /// <summary>The tee and every rest spot of the bare safe route: each shot's landing.</summary>
        EveryShot,

        /// <summary>No window: anywhere the clearances allow (the director's choice, F3.2: "C, partout").</summary>
        Anywhere,
    }

    /// <summary>
    /// What the fairway shapes may be and where they may go (split fairway plan §1, built in F3.2). Every length is in metres and
    /// drawn in whole centimetres; every chance and weight is an integer. Read by <see cref="FairwayShapePlacer"/>.
    /// </summary>
    [Serializable]
    public sealed class FairwayShapeRules
    {
        [Header("Weights per par")]
        [SerializeField] private FairwayShapeWeights par3 = FairwayShapeWeights.Create(80, 20, 0, 0);
        [SerializeField] private FairwayShapeWeights par4 = FairwayShapeWeights.Create(45, 20, 15, 20);
        [SerializeField] private FairwayShapeWeights par5 = FairwayShapeWeights.Create(35, 20, 15, 30);
        [Tooltip("Chance, in percent, that a par 5 draws a second shape on the second shot's stretch (its own sub-stream).")]
        [SerializeField, Range(0, 100)] private int secondShapeChance = 25;
        [Tooltip("Candidates tried per shape before the course stays plain. Each draws the same fixed list of numbers.")]
        [SerializeField, Range(1, 32)] private int triesPerShape = 8;

        [Tooltip("What a par 4 or 5 shape's window (below) is measured from.")]
        [SerializeField] private ShapeWindowOrigin windowOrigin = ShapeWindowOrigin.Anywhere;

        [Header("Rough island")]
        [Tooltip("The oval's length along the centreline.")]
        [SerializeField] private Vector2 islandLength = new Vector2(2.5f, 5f);
        [Tooltip("The oval's width across the centreline.")]
        [SerializeField] private Vector2 islandWidth = new Vector2(1.2f, 2f);
        [Tooltip("The oval's border wobble, as a fraction of its radius (like the bunkers').")]
        [SerializeField] private Vector2 islandWobble = new Vector2(0.12f, 0.25f);
        [Tooltip("On a par 4 or 5, the oval's centre lies this far from the shot's origin: a full drive's landing.")]
        [SerializeField] private Vector2 islandDriveWindow = new Vector2(26f, 31f);
        [Tooltip("Fairway kept on each side of the oval, at every point of it.")]
        [SerializeField, Min(0f)] private float islandFairwaySide = 2f;
        [Tooltip("The strip widens around the oval and narrows back over this length.")]
        [SerializeField, Min(0f)] private float islandWidenEase = 4f;
        [Tooltip("The widened strip's edge keeps this far inside the island's edge.")]
        [SerializeField, Min(0f)] private float islandEdgeMargin = 1f;

        [Header("Split")]
        [Tooltip("The rough band's length along the centreline, between the two tips.")]
        [SerializeField] private Vector2 bandLength = new Vector2(1.5f, 3f);
        [Tooltip("The band's centre lies this far from the shot's origin: past a lay-up, short of a full carry.")]
        [SerializeField] private Vector2 bandWindow = new Vector2(20f, 26f);

        [Header("Clearances")]
        [SerializeField, Min(0f)] private float fromTee = 6f;
        [Tooltip("From a gap's tips along the centreline.")]
        [SerializeField, Min(0f)] private float fromGap = 6f;
        [Tooltip("A split keeps further from a gap, so its band never reads as a second gap.")]
        [SerializeField, Min(0f)] private float splitFromGap = 10f;
        [Tooltip("From the start of the fairway's end tip, and from the end of its start tip.")]
        [SerializeField, Min(0f)] private float fromFairwayTips = 2f;
        [Tooltip("Two shapes on one par 5 keep this far apart.")]
        [SerializeField, Min(0f)] private float betweenShapes = 6f;

        public FairwayShapeWeights Weights(int par)
        {
            switch (par)
            {
                case 3: return par3;
                case 4: return par4;
                case 5: return par5;
                default: throw new ArgumentOutOfRangeException(nameof(par), par, "A course is a par 3, 4 or 5.");
            }
        }

        public int SecondShapeChance => secondShapeChance;
        public int TriesPerShape => triesPerShape;
        public ShapeWindowOrigin WindowOrigin => windowOrigin;
        public Vector2 IslandLength => islandLength;
        public Vector2 IslandWidth => islandWidth;
        public Vector2 IslandWobble => islandWobble;
        public Vector2 IslandDriveWindow => islandDriveWindow;
        public float IslandFairwaySide => islandFairwaySide;
        public float IslandWidenEase => islandWidenEase;
        public float IslandEdgeMargin => islandEdgeMargin;
        public Vector2 BandLength => bandLength;
        public Vector2 BandWindow => bandWindow;
        public float FromTee => fromTee;
        public float FromGap => fromGap;
        public float SplitFromGap => splitFromGap;
        public float FromFairwayTips => fromFairwayTips;
        public float BetweenShapes => betweenShapes;

        /// <summary>Tests only: replaces the weights of one par and the second-shape chance (on a copy of the settings).</summary>
        internal void SetWeights(int par, FairwayShapeWeights weights, int secondChance)
        {
            switch (par)
            {
                case 3: par3 = weights; break;
                case 4: par4 = weights; break;
                default: par5 = weights; break;
            }

            secondShapeChance = secondChance;
        }
    }
}
