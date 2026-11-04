using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>How the clouds below the island are scattered, shaped and moved.</summary>
    [CreateAssetMenu(fileName = "CloudSettings", menuName = "Iso Golf/Cloud Settings")]
    public sealed class CloudSettings : ScriptableObject
    {
        [SerializeField] private int seed = 7;

        [Header("Main layer")]
        [Tooltip("Size of the square that holds at most one cluster, in metres.")]
        [Min(1f)] [SerializeField] private float cellSize = 17f;
        [Range(0f, 1f)] [SerializeField] private float occupancy = 0.78f;
        [SerializeField] private float lowest = -26f;
        [SerializeField] private float highest = -15f;

        [Header("Near band")]
        [Tooltip("Looking steeply down, the bottom of the screen sees only a small patch of the layer, so that patch gets its own tighter grid. Metres from the near edge of the layer.")]
        [Min(0f)] [SerializeField] private float nearBandLength = 40f;
        [Min(1f)] [SerializeField] private float nearCellSize = 9f;
        [Range(0f, 1f)] [SerializeField] private float nearOccupancy = 0.5f;
        [SerializeField] private float nearLowest = -32f;
        [SerializeField] private float nearHighest = -24f;

        [Header("Shape")]
        [Min(1)] [SerializeField] private int minBlobs = 4;
        [Min(1)] [SerializeField] private int maxBlobs = 7;
        [Min(0.1f)] [SerializeField] private float minClusterSize = 2.2f;
        [Min(0.1f)] [SerializeField] private float maxClusterSize = 3.8f;

        [Header("Tone")]
        [Tooltip("How much a facet must face the sun, from 0 to 1, before it starts lightening from the shade color.")]
        [Range(0f, 1f)] [SerializeField] private float shadeBelow = 0.2f;
        [Tooltip("Facing the sun this much or more gives the full light color.")]
        [Range(0f, 1f)] [SerializeField] private float lightAbove = 0.97f;
        [Tooltip("Random change in how much each facet faces the sun.")]
        [Range(0f, 0.5f)] [SerializeField] private float toneJitter = 0.03f;

        [Header("Drift")]
        [Min(0f)] [SerializeField] private float driftSpeed = 0.35f;
        [Tooltip("How far the layer moves each way across the screen before it turns back.")]
        [Min(0f)] [SerializeField] private float driftRange = 45f;

        [Header("Coverage")]
        [Tooltip("Extra metres beyond the framed view, so clusters straddling its edge still exist.")]
        [Min(0f)] [SerializeField] private float margin = 12f;

        public int Seed => seed;
        public float CellSize => cellSize;
        public float Occupancy => occupancy;
        public float Lowest => lowest;
        public float Highest => highest;
        public float NearBandLength => nearBandLength;
        public float NearCellSize => nearCellSize;
        public float NearOccupancy => nearOccupancy;
        public float NearLowest => nearLowest;
        public float NearHighest => nearHighest;
        public int MinBlobs => minBlobs;
        public int MaxBlobs => maxBlobs;
        public float MinClusterSize => minClusterSize;
        public float MaxClusterSize => maxClusterSize;
        public float ShadeBelow => shadeBelow;
        public float LightAbove => lightAbove;
        public float ToneJitter => toneJitter;
        public float DriftSpeed => driftSpeed;
        public float DriftRange => driftRange;
        public float Margin => margin;
    }
}
