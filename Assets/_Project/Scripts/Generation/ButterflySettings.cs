using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// Tunables of the butterflies (M4 PR 4.7): how many, how far they loop round a bush, how fast, how big. Visual only: nothing in the
    /// simulation, the checks or the aim preview reads any of it.
    /// </summary>
    [CreateAssetMenu(fileName = "ButterflySettings", menuName = "Iso Golf/Butterflies")]
    public sealed class ButterflySettings : ScriptableObject
    {
        [Tooltip("How many butterflies a course has (a range, one draw), at most one per bush.")]
        [SerializeField] private Vector2Int count = new Vector2Int(1, 3);
        [Tooltip("Height of a loop's centre above the bush's root, in metres.")]
        [SerializeField] private float loopHeight = 0.9f;
        [Tooltip("Radius of the loop round the bush, in metres (a range, one draw per butterfly).")]
        [SerializeField] private Vector2 loopRadius = new Vector2(0.9f, 1.9f);
        [Tooltip("How far a butterfly rises and drops along its loop, in metres (a range).")]
        [SerializeField] private Vector2 rise = new Vector2(0.25f, 0.55f);
        [Tooltip("Loop speed in radians per second (a range).")]
        [SerializeField] private Vector2 loopSpeed = new Vector2(0.35f, 0.65f);
        [Tooltip("Wing beats per second.")]
        [SerializeField] private float flapRate = 4f;
        [Tooltip("Wing span from the body, in metres.")]
        [SerializeField] private float wingSize = 0.16f;

        public Vector2Int Count => count;
        public float LoopHeight => loopHeight;
        public Vector2 LoopRadius => loopRadius;
        public Vector2 Rise => rise;
        public Vector2 LoopSpeed => loopSpeed;
        public float FlapRate => flapRate;
        public float WingSize => wingSize;
    }
}
