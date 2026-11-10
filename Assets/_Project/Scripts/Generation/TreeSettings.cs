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
    /// A hand-written list of where trees stand, for the fixed test course of <c>Sandbox_Par3</c>. The
    /// trees' proportions live in <see cref="PropKitSettings"/>; M4's placement rules produce the real
    /// lists from a seed.
    /// </summary>
    [CreateAssetMenu(fileName = "Trees", menuName = "Iso Golf/Trees")]
    public sealed class TreeSettings : ScriptableObject
    {
        [SerializeField] private int seed = 1;
        [SerializeField] private TreePlacement[] placements = Array.Empty<TreePlacement>();

        public int Seed => seed;
        public TreePlacement[] Placements => placements;
    }
}
