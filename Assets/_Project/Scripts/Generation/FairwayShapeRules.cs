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
        [SerializeField, Min(0)] private int fork;

        [Tooltip("Of plain's weight, the share at the top of its range that draws a fork instead (F3.3, director: \"A, 2,5 m\"). " +
                 "It moves no other shape's range, so rough islands and splits stay as drawn.")]
        [SerializeField, Min(0)] private int forkFromPlain;

        /// <summary>The weight that draws plain: <see cref="PlainRange"/> less what forks take from its top.</summary>
        public int Plain => Mathf.Max(0, plain - forkFromPlain);
        public int RoughIsland => roughIsland;
        public int Split => split;

        /// <summary>The weight that draws a fork: its own range plus the top of plain's.</summary>
        public int Fork => fork + Mathf.Min(forkFromPlain, plain);

        /// <summary>Plain's whole range, the first in the draw, before forks take its top: the ranges after it never move.</summary>
        public int PlainRange => plain;

        /// <summary>How much of plain's range draws a fork.</summary>
        public int ForkFromPlain => Mathf.Min(forkFromPlain, plain);

        public int Total => plain + roughIsland + split + fork;

        public static FairwayShapeWeights Create(int plain, int roughIsland, int split, int fork, int forkFromPlain = 0) =>
            new FairwayShapeWeights { plain = plain, roughIsland = roughIsland, split = split, fork = fork, forkFromPlain = forkFromPlain };
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
        [SerializeField] private FairwayShapeWeights par4 = FairwayShapeWeights.Create(45, 20, 15, 20, 45);
        [SerializeField] private FairwayShapeWeights par5 = FairwayShapeWeights.Create(35, 20, 15, 30, 35);
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

        [Header("Fork")]
        [Tooltip("The spine's held length along the centreline, where both branches run at their full offset.")]
        [SerializeField] private Vector2 spineLength = new Vector2(8f, 14f);
        [Tooltip("The spine's width at its narrowest point between the eases (rough between the two branches).")]
        [SerializeField] private Vector2 spineWidth = new Vector2(3.5f, 4f);
        [Tooltip("The safe branch's width at its narrowest point (the wider branch, which carries the safe route).")]
        [SerializeField] private Vector2 safeBranchWidth = new Vector2(2.4f, 2.6f);
        [Tooltip("The risky branch's width at its narrowest point (the narrower one, which carries the direct line).")]
        [SerializeField] private Vector2 riskyBranchWidth = new Vector2(2.2f, 2.4f);
        [Tooltip("The branches open from the main strip and close back into it over this length, at each end of the spine.")]
        [SerializeField, Min(0.5f)] private float forkEase = 4f;
        [Tooltip("The share of the fairway's edge wobble a branch carries (director, Q2 A).")]
        [SerializeField, Range(0f, 1f)] private float branchWobbleScale = 0.25f;
        [Tooltip("A branch's outer edge keeps this far inside the island's edge (the bulge makes room for it).")]
        [SerializeField, Min(0f)] private float forkEdgeMargin = 1f;
        [Tooltip("The island bulge eases in and out over this length beyond the fork (director: \"A, renflement\").")]
        [SerializeField, Min(0.5f)] private float bulgeEase = 4f;
        [Tooltip("The most the island may bulge on one side; a fork needing more finds no place there (director: \"A, 2,5 m\"; 2.0 m is the fallback if the outline reads too wide).")]
        [SerializeField, Min(0f)] private float maxBulge = 2.5f;
        [Tooltip("Chance, in percent, that the branch guard is water (falling back to sand like the other roles).")]
        [SerializeField, Range(0, 100)] private int branchGuardWaterChance = 20;
        [Tooltip("A fork rejoins at least this far before the fairway's end tip starts (plan §1: the cap depth).")]
        [SerializeField, Min(0f)] private float forkBeforeEndTip = 2.5f;

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
        public Vector2 SpineLength => spineLength;
        public Vector2 SpineWidth => spineWidth;
        public Vector2 SafeBranchWidth => safeBranchWidth;
        public Vector2 RiskyBranchWidth => riskyBranchWidth;
        public float ForkEase => forkEase;
        public float BranchWobbleScale => branchWobbleScale;
        public float ForkEdgeMargin => forkEdgeMargin;
        public float BulgeEase => bulgeEase;
        public float MaxBulge => maxBulge;
        public int BranchGuardWaterChance => branchGuardWaterChance;
        public float ForkBeforeEndTip => forkBeforeEndTip;
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

        /// <summary>Tests only: replaces the fork's bulge cap and edge margin (on a copy of the settings), for the sensitivity measurement.</summary>
        internal void SetForkRoom(float bulge, float edgeMargin, Vector2 spine)
        {
            maxBulge = bulge;
            forkEdgeMargin = edgeMargin;
            spineWidth = spine;
        }
    }
}
