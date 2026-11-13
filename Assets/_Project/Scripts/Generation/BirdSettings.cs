using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Tunables of the perched birds (M4 PR 4.6): how many, where they perch, what sets them off, how they fly and when they come
    /// back. Visual only: nothing in the simulation, the checks or the aim preview reads any of it.
    /// </summary>
    [CreateAssetMenu(fileName = "BirdSettings", menuName = "Iso Golf/Birds")]
    public sealed class BirdSettings : ScriptableObject
    {
        [Header("Flock")]
        [SerializeField] private Vector2Int count = new Vector2Int(2, 5);
        [Tooltip("Neighbouring perches within this many metres of the flock's first one are used before a bird shares a perch.")]
        [SerializeField] private float flockSpread = 4f;
        [Tooltip("How far a bird sits below a crown's top, so it neither floats nor sinks into a crown whose faces are jittered.")]
        [SerializeField] private float perchSink = 0.05f;
        [Tooltip("Ground is tested this far around a perch: never a bunker, water, the green or the island's void within it.")]
        [SerializeField] private float perchClearance = 0.8f;
        [Tooltip("Grid step of the rough-ground fallback, used only when the course has no prop to perch on.")]
        [SerializeField] private float groundGrid = 1f;
        [Tooltip("The tee is kept free within this radius.")]
        [SerializeField] private float teeClearance = 3f;

        [Header("Behaviour")]
        [Tooltip("A ball flying or rolling this close (horizontally, in metres) sets a perched bird off.")]
        [SerializeField] private float triggerRadius = 3.5f;
        [Tooltip("Each bird leaves this many seconds after the trigger (a range, one draw per bird), so a flock never leaves in step.")]
        [SerializeField] private Vector2 takeOffDelay = new Vector2(0f, 0.4f);
        [SerializeField] private float flightSpeed = 5f;
        [Tooltip("Length of the take-off path in metres.")]
        [SerializeField] private float pathLength = 26f;
        [Tooltip("Height gained over the path, in metres.")]
        [SerializeField] private float climb = 7f;
        [Tooltip("Fraction of the path, from its far end, over which a bird shrinks to nothing (and grows back on its return), so it is gone and never pops.")]
        [Range(0.05f, 0.9f)] [SerializeField] private float fadeFraction = 0.35f;
        [Tooltip("Wing beats per second in flight.")]
        [SerializeField] private float flapRate = 7f;
        [Tooltip("Seconds away before a bird may come back, a range (one draw per bird). It waits while the ball is within 1.5 times the trigger radius.")]
        [SerializeField] private Vector2 returnDelay = new Vector2(8f, 14f);

        [Header("Look")]
        [Tooltip("Scale of the bird mesh (0.22 m long at 1).")]
        [SerializeField] private float birdScale = 1.4f;

        public Vector2Int Count => count;
        public float FlockSpread => flockSpread;
        public float PerchSink => perchSink;
        public float PerchClearance => perchClearance;
        public float GroundGrid => groundGrid;
        public float TeeClearance => teeClearance;
        public float TriggerRadius => triggerRadius;
        public Vector2 TakeOffDelay => takeOffDelay;
        public float FlightSpeed => flightSpeed;
        public float PathLength => pathLength;
        public float Climb => climb;
        public float FadeFraction => fadeFraction;
        public float FlapRate => flapRate;
        public Vector2 ReturnDelay => returnDelay;
        public float BirdScale => birdScale;
    }
}
