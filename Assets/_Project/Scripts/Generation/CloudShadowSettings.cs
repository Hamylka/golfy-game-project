using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Tunables of the drifting cloud shadow (M4 PR 4.7): how dark, how big, how fast. Visual only.</summary>
    [CreateAssetMenu(fileName = "CloudShadowSettings", menuName = "Iso Golf/Cloud Shadow")]
    public sealed class CloudShadowSettings : ScriptableObject
    {
        [Tooltip("Share of the sun's diffuse light the shadow takes at its middle (0 = none). Director, PR 4.7: soft.")]
        [Range(0f, 0.6f)] [SerializeField] private float strength = 0.25f;
        [Tooltip("Radius of the shadow along the course, in metres.")]
        [Min(1f)] [SerializeField] private float radius = 13f;
        [Tooltip("How much narrower the shadow is across the course than along it (1 = round).")]
        [Min(1f)] [SerializeField] private float stretch = 1.6f;
        [Tooltip("Seconds for one trip from the tee to the hole and back.")]
        [Min(5f)] [SerializeField] private float period = 125f;

        public float Strength => strength;
        public float Radius => radius;
        public float Stretch => stretch;
        public float Period => period;
    }
}
