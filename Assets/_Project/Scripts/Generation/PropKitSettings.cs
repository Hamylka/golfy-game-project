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
