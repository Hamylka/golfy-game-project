using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The grass fringe along the island edge: overlapping tufts that fold over the edge like thatch.
    /// Sizes are in metres. The defaults are the look chosen on the phone in M1.4.
    /// </summary>
    [CreateAssetMenu(fileName = "Tufts", menuName = "Iso Golf/Tufts")]
    public sealed class TuftSettings : ScriptableObject
    {
        [SerializeField] private int seed = 1;

        [Tooltip("No two tufts are closer than this. Keep it at or under the smallest half width, so tufts always overlap.")]
        [SerializeField, Min(0.05f)] private float minSpacing = 0.13f;

        [Tooltip("Half a tuft's width along the edge.")]
        [SerializeField] private Vector2 halfWidth = new Vector2(0.13f, 0.2f);

        [Tooltip("How far back onto the grass a tuft starts.")]
        [SerializeField] private Vector2 inset = new Vector2(0.08f, 0.16f);

        [Tooltip("How far the crease on the edge rises above the grass.")]
        [SerializeField] private Vector2 rise = new Vector2(0.015f, 0.05f);

        [Tooltip("How far the tip hangs down over the earth band.")]
        [SerializeField] private Vector2 drop = new Vector2(0.06f, 0.15f);

        [Tooltip("How far the tip sits out from the edge. More than zero, so the face tilts up and catches the light.")]
        [SerializeField] private Vector2 flare = new Vector2(0.03f, 0.07f);

        [Tooltip("How wide a tuft's tip is, as a fraction of its half width. Zero is a sharp point.")]
        [SerializeField, Range(0f, 0.6f)] private float tipWidth = 0.5f;

        [SerializeField, Range(0f, 1f)] private float lightChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float darkChance = 0.2f;

        [Tooltip("Off is cheaper, and keeps a fringe of tiny shadows off the earth band.")]
        [SerializeField] private bool castShadows;

        public int Seed => seed;
        public float MinSpacing => minSpacing;
        public Vector2 HalfWidth => halfWidth;
        public Vector2 Inset => inset;
        public Vector2 Rise => rise;
        public Vector2 Drop => drop;
        public Vector2 Flare => flare;
        public float TipWidth => tipWidth;
        public float LightChance => lightChance;
        public float DarkChance => darkChance;
        public bool CastShadows => castShadows;
    }
}
