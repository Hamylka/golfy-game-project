using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The border of small prisms along the island edge. Sizes are in metres. A rise or drop of zero
    /// leaves that side of the tuft flat, so the same builder can make upright, hanging or two-way tufts.
    /// </summary>
    [CreateAssetMenu(fileName = "Tufts", menuName = "Iso Golf/Tufts")]
    public sealed class TuftSettings : ScriptableObject
    {
        [SerializeField] private int seed = 1;

        [Tooltip("No two tufts are closer than this. Average spacing comes out about a third longer.")]
        [SerializeField, Min(0.05f)] private float minSpacing = 0.32f;

        [Tooltip("Smallest and largest distance from a tuft's middle to its corners, along the edge.")]
        [SerializeField] private Vector2 radius = new Vector2(0.2f, 0.34f);

        [Tooltip("How far the point rises above the grass.")]
        [SerializeField] private Vector2 rise = new Vector2(0.08f, 0.28f);

        [Tooltip("How far the point hangs down over the earth band.")]
        [SerializeField] private Vector2 drop = new Vector2(0.12f, 0.45f);

        [Tooltip("How far a tuft's middle sits outside the edge line.")]
        [SerializeField] private Vector2 overhang = new Vector2(0f, 0.1f);

        [Tooltip("Chance of a tuft having four sides rather than three.")]
        [SerializeField, Range(0f, 1f)] private float fourSidedChance = 0.5f;

        [SerializeField, Range(0f, 1f)] private float lightChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float darkChance = 0.25f;

        [Tooltip("Off is cheaper, and keeps a fringe of tiny shadows off the ground.")]
        [SerializeField] private bool castShadows = true;

        public int Seed => seed;
        public float MinSpacing => minSpacing;
        public Vector2 Radius => radius;
        public Vector2 Rise => rise;
        public Vector2 Drop => drop;
        public Vector2 Overhang => overhang;
        public float FourSidedChance => fourSidedChance;
        public float LightChance => lightChance;
        public float DarkChance => darkChance;
        public bool CastShadows => castShadows;
    }
}
