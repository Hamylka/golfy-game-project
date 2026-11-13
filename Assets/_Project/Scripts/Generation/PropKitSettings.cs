using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Proportions of the procedural props, in metres for a prop at scale 1 (M4 PR 4.1). The tree values
    /// are the ones <see cref="TreeSettings"/> held before the kit, so hand-placed trees keep their shape.
    /// </summary>
    [CreateAssetMenu(fileName = "PropKit", menuName = "Iso Golf/Prop kit")]
    public sealed class PropKitSettings : ScriptableObject
    {
        [Header("Olive tree")]
        [SerializeField, Min(0.1f)] private float trunkHeight = 0.9f;
        [SerializeField, Min(0.01f)] private float trunkRadius = 0.16f;
        [SerializeField, Min(1)] private int blobCount = 5;
        [SerializeField, Min(0.1f)] private float blobRadius = 0.75f;
        [Tooltip("How far the outer blobs sit from the middle of the crown.")]
        [SerializeField, Min(0f)] private float crownSpread = 0.72f;
        [Tooltip("Random change in each blob's corners, as a fraction of its radius.")]
        [SerializeField, Range(0f, 0.4f)] private float blobJitter = 0.13f;
        [Tooltip("Chance that a facet takes the darker neighbouring shade.")]
        [SerializeField, Range(0f, 1f)] private float darkFacetChance = 0.2f;

        [Header("Cypress")]
        [SerializeField, Min(0.1f)] private float cypressHeight = 3.6f;
        [SerializeField, Min(0.05f)] private float cypressRadius = 0.5f;
        [SerializeField, Range(6, 8)] private int cypressSides = 7;
        [SerializeField, Min(0f)] private float cypressTrunkHeight = 0.25f;

        [Header("Round bush")]
        [SerializeField, Min(0.1f)] private float bushRadius = 0.5f;
        [Tooltip("Size of the two small blobs at the sides, as a fraction of the main one.")]
        [SerializeField, Range(0.3f, 0.9f)] private float bushSatelliteSize = 0.6f;
        [SerializeField, Range(0f, 0.4f)] private float bushJitter = 0.14f;

        [Header("Rocks")]
        [Tooltip("Width of a rock at scale 1 is its size argument; this is how much flatter than a sphere it sits.")]
        [SerializeField, Range(0.4f, 1f)] private float rockHeightRatio = 0.65f;
        [SerializeField, Range(0f, 0.5f)] private float rockJitter = 0.22f;
        [Tooltip("Round white rocks by water are smoother than ordinary rocks.")]
        [SerializeField, Range(0f, 0.3f)] private float roundRockJitter = 0.06f;

        [Header("Dry-stone wall")]
        [SerializeField, Min(0.05f)] private float wallBlockHeight = 0.2f;
        [SerializeField, Min(1)] private int wallCourses = 3;
        [SerializeField, Min(0.1f)] private float wallBlockLength = 0.45f;
        [Tooltip("Each block's length varies by up to this fraction.")]
        [SerializeField, Range(0f, 0.6f)] private float wallBlockLengthVariation = 0.35f;
        [SerializeField, Min(0.1f)] private float wallThickness = 0.38f;
        [Tooltip("Random change in each block's corners, in metres.")]
        [SerializeField, Range(0f, 0.08f)] private float wallJitter = 0.025f;

        [Header("Shed (M4 PR 4.4)")]
        [SerializeField, Min(0.5f)] private float shedLength = 2.3f;
        [SerializeField, Min(0.5f)] private float shedWidth = 1.7f;
        [SerializeField, Min(0.5f)] private float shedWallHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float shedRoofRise = 0.7f;
        [SerializeField, Min(0f)] private float shedOverhang = 0.15f;

        [Header("Bench")]
        [SerializeField, Min(0.5f)] private float benchLength = 1.4f;

        [Header("Stone bridge")]
        [SerializeField, Min(0.5f)] private float bridgeWidth = 1.2f;
        [Tooltip("How high the deck's middle rises over its ends.")]
        [SerializeField, Min(0f)] private float bridgeRise = 0.45f;
        [Tooltip("Height of the deck over the banks, before the rise.")]
        [SerializeField, Min(0.1f)] private float bridgeDeck = 0.35f;
        [SerializeField, Min(0.2f)] private float bridgeBlockLength = 0.8f;
        [Tooltip("The bridge's stone reaches this far below the lower bank, so it stands in the water.")]
        [SerializeField, Min(0.1f)] private float bridgeDepth = 0.8f;

        [Header("Landmarks")]
        [SerializeField, Min(0.5f)] private float chapelLength = 2.1f;
        [SerializeField, Min(0.5f)] private float chapelWidth = 1.5f;
        [SerializeField, Min(0.5f)] private float chapelWallHeight = 1.6f;
        [SerializeField, Min(0.5f)] private float chapelTowerHeight = 2.7f;
        [SerializeField, Min(0.3f)] private float lighthouseRadius = 0.8f;
        [SerializeField, Min(1f)] private float lighthouseHeight = 3.5f;

        // The tall obstacles' collision shapes come from these values alone, never from a seed, so the placement knows their
        // footprint before it draws anything (tall obstacles, Q5 B: a course with no spot draws nothing new).
        [Header("Tall obstacles")]
        [SerializeField, Min(1f)] private float pineHeight = 6f;
        [SerializeField, Min(0.5f)] private float pineCrownRadius = 2.2f;
        [Tooltip("Height of the flat crown, from its underside to the top.")]
        [SerializeField, Min(0.3f)] private float pineCrownDepth = 2f;
        [SerializeField, Min(0.05f)] private float pineTrunkRadius = 0.28f;
        [Tooltip("Reach of the clump's foliage from its middle.")]
        [SerializeField, Min(0.5f)] private float clumpRadius = 1.35f;
        [SerializeField, Min(1f)] private float clumpHeight = 5.5f;
        [SerializeField, Min(0.5f)] private float dovecoteRadius = 1f;
        [Tooltip("Height of the dovecote's wall; its roof rises above it to the full height.")]
        [SerializeField, Min(1f)] private float dovecoteWallHeight = 4.3f;
        [SerializeField, Min(1f)] private float dovecoteHeight = 5.5f;

        public float ShedLength => shedLength;
        public float ShedWidth => shedWidth;
        public float ShedWallHeight => shedWallHeight;
        public float ShedRoofRise => shedRoofRise;
        public float ShedOverhang => shedOverhang;
        public float BenchLength => benchLength;
        public float BridgeWidth => bridgeWidth;
        public float BridgeRise => bridgeRise;
        public float BridgeDeck => bridgeDeck;
        public float BridgeBlockLength => bridgeBlockLength;
        public float BridgeDepth => bridgeDepth;
        public float ChapelLength => chapelLength;
        public float ChapelWidth => chapelWidth;
        public float ChapelWallHeight => chapelWallHeight;
        public float ChapelTowerHeight => chapelTowerHeight;
        public float LighthouseRadius => lighthouseRadius;
        public float LighthouseHeight => lighthouseHeight;
        public float PineHeight => pineHeight;
        public float PineCrownRadius => pineCrownRadius;
        public float PineCrownDepth => pineCrownDepth;
        public float PineTrunkRadius => pineTrunkRadius;
        public float ClumpRadius => clumpRadius;
        public float ClumpHeight => clumpHeight;
        public float DovecoteRadius => dovecoteRadius;
        public float DovecoteWallHeight => dovecoteWallHeight;
        public float DovecoteHeight => dovecoteHeight;
        public float TrunkHeight => trunkHeight;
        public float TrunkRadius => trunkRadius;
        public int BlobCount => blobCount;
        public float BlobRadius => blobRadius;
        public float CrownSpread => crownSpread;
        public float BlobJitter => blobJitter;
        public float DarkFacetChance => darkFacetChance;
        public float CypressHeight => cypressHeight;
        public float CypressRadius => cypressRadius;
        public int CypressSides => cypressSides;
        public float CypressTrunkHeight => cypressTrunkHeight;
        public float BushRadius => bushRadius;
        public float BushSatelliteSize => bushSatelliteSize;
        public float BushJitter => bushJitter;
        public float RockHeightRatio => rockHeightRatio;
        public float RockJitter => rockJitter;
        public float RoundRockJitter => roundRockJitter;
        public float WallBlockHeight => wallBlockHeight;
        public int WallCourses => wallCourses;
        public float WallBlockLength => wallBlockLength;
        public float WallBlockLengthVariation => wallBlockLengthVariation;
        public float WallThickness => wallThickness;
        public float WallJitter => wallJitter;
    }
}
