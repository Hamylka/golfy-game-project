using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>The prop kinds the placement uses (PR 4.3). Built props, traces and the landmark come in PR 4.4.</summary>
    public enum PropKind
    {
        Olive,
        Cypress,
        Bush,
        Rock,
        RoundRock,
        Wall,
    }

    /// <summary>
    /// One prop on a generated course, as plain data in the course's local space: what it is, where it stands, the seed
    /// its mesh is built from, and the collision shapes that mesh gives. The shapes are fitted to the built mesh by
    /// <see cref="PropBuilder"/>, and the terrain view builds the same mesh from the same seed, so what the ball strikes
    /// and what is drawn are one and the same prop.
    /// </summary>
    public readonly struct CourseProp
    {
        private readonly PropShape[] _shapes;

        public CourseProp(PropKind kind, Vector3 root, Vector3 end, float scale, int seed, int cluster, IEnumerable<PropShape> shapes)
        {
            Kind = kind;
            Root = root;
            End = end;
            Scale = scale;
            Seed = seed;
            Cluster = cluster;
            _shapes = new List<PropShape>(shapes).ToArray();
        }

        public PropKind Kind { get; }

        /// <summary>The foot of the prop on the ground (for a wall, its start).</summary>
        public Vector3 Root { get; }

        /// <summary>Walls only: the other end, on the ground. Equal to <see cref="Root"/> for the other kinds.</summary>
        public Vector3 End { get; }

        /// <summary>The kit's scale for trees and bushes; the width across in metres for rocks; 1 for walls.</summary>
        public float Scale { get; }

        /// <summary>The seed of the prop's own <see cref="System.Random"/>, which builds its mesh.</summary>
        public int Seed { get; }

        /// <summary>The cluster it belongs to, in the order the placement made them.</summary>
        public int Cluster { get; }

        public IReadOnlyList<PropShape> Shapes => _shapes ?? new PropShape[0];
    }
}
