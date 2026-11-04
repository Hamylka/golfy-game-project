using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    public enum TreeKind
    {
        Cauliflower,
        Cypress,
    }

    /// <summary>One tree on the course. X runs from tee to hole, as in <see cref="CourseShapeSettings"/>.</summary>
    [Serializable]
    public struct TreePlacement
    {
        [SerializeField] private TreeKind kind;
        [SerializeField] private Vector2 position;
        [SerializeField, Min(0.1f)] private float scale;

        public TreePlacement(TreeKind kind, Vector2 position, float scale)
        {
            this.kind = kind;
            this.position = position;
            this.scale = scale;
        }

        public TreeKind Kind => kind;
        public Vector2 Position => position;
        public float Scale => scale;
    }

    /// <summary>
    /// Tree proportions, and a hand-written list of where trees stand. M4's placement rules will
    /// produce the list from a seed. Sizes are in metres, for a tree at scale 1.
    /// </summary>
    [CreateAssetMenu(fileName = "Trees", menuName = "Iso Golf/Trees")]
    public sealed class TreeSettings : ScriptableObject
    {
        [SerializeField] private int seed = 1;
        [SerializeField] private TreePlacement[] placements = Array.Empty<TreePlacement>();

        [Header("Cauliflower tree")]
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

        public int Seed => seed;
        public TreePlacement[] Placements => placements;
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
    }
}
