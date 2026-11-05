using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>The sizes of the ball, its shadow, the hole and the flag, as drawn. The simulation has its own values in ShotSettings.</summary>
    [CreateAssetMenu(fileName = "PlayPieces", menuName = "Iso Golf/Play Pieces")]
    public sealed class PlayPieceSettings : ScriptableObject
    {
        [Header("Ball")]
        [Tooltip("In metres. Larger than a real ball, so it stays readable on the phone.")]
        [SerializeField, Min(0.01f)] private float ballRadius = 0.12f;

        [Header("Ground discs")]
        [SerializeField, Min(0.01f)] private float shadowRadius = 0.11f;
        [Tooltip("How far the shadow floats above the ground, in metres, so it never sinks into a facet. It must stay above the hole's lift.")]
        [SerializeField, Min(0f)] private float shadowLift = 0.02f;
        [SerializeField, Min(0.01f)] private float holeRadius = 0.25f;
        [SerializeField, Min(0f)] private float holeLift = 0.01f;
        [Tooltip("Rings between a disc's center and its rim. More rings follow a crease between facets more closely.")]
        [SerializeField, Min(1)] private int discRings = 3;
        [SerializeField, Min(3)] private int discSegments = 16;

        [Header("Flag")]
        [SerializeField, Min(0.1f)] private float poleHeight = 1.8f;
        [SerializeField, Min(0.001f)] private float poleRadius = 0.03f;
        [SerializeField, Min(0.01f)] private float flagLength = 0.6f;
        [SerializeField, Min(0.01f)] private float flagHeight = 0.4f;
        [Tooltip("How far the crease down the flag sits out of its plane, in metres, so its two facets take different tones.")]
        [SerializeField, Min(0f)] private float flagFold = 0.06f;

        public float BallRadius => ballRadius;
        public float ShadowRadius => shadowRadius;
        public float ShadowLift => shadowLift;
        public float HoleRadius => holeRadius;
        public float HoleLift => holeLift;
        public int DiscRings => discRings;
        public int DiscSegments => discSegments;
        public float PoleHeight => poleHeight;
        public float PoleRadius => poleRadius;
        public float FlagLength => flagLength;
        public float FlagHeight => flagHeight;
        public float FlagFold => flagFold;
    }
}
