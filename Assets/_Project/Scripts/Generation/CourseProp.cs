using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>The prop kinds the placement uses: vegetation and stone (PR 4.3), built props, traces and landmarks (PR 4.4).</summary>
    public enum PropKind
    {
        Olive,
        Cypress,
        Bush,
        Rock,
        RoundRock,
        Wall,
        Shed,
        Bench,
        Bridge,
        Rake,
        Bucket,
        Bicycle,
        Laundry,
        Chapel,
        Lighthouse,
    }

    /// <summary>
    /// Where a strategic prop stands (strategic obstacles plan §1, S2): a prop placed on the direct or cut line to make a shot a
    /// choice. <see cref="None"/> for every other prop. A strategic prop is a prop, not a hazard: no <see cref="HazardRole"/>, not in
    /// the hazard counts; play reads only its collision shapes.
    /// </summary>
    public enum StrategicZone
    {
        None,
        Spine,
        Split,
        RoughIsland,
        CutCorner,
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

        public CourseProp(PropKind kind, Vector3 root, Vector3 end, float scale, int seed, int cluster, IEnumerable<PropShape> shapes, float yawDegrees = 0f, StrategicZone zone = StrategicZone.None)
        {
            Zone = zone;
            YawDegrees = yawDegrees;
            Kind = kind;
            Root = root;
            End = end;
            Scale = scale;
            Seed = seed;
            Cluster = cluster;
            _shapes = new List<PropShape>(shapes).ToArray();
        }

        public PropKind Kind { get; }

        /// <summary>The strategic zone it stands in (S2), <see cref="StrategicZone.None"/> for every other prop. Not in the prop fingerprint.</summary>
        public StrategicZone Zone { get; }

        /// <summary>True for a strategic prop (S2): placed on its own sub-stream, last, and allowed in the direct and cut corridors.</summary>
        public bool Strategic => Zone != StrategicZone.None;

        /// <summary>The foot of the prop on the ground (for a wall, its start).</summary>
        public Vector3 Root { get; }

        /// <summary>
        /// Built props and traces: which way the prop faces, in whole degrees from +X toward +Z (its x axis). Walls and bridges
        /// take theirs from <see cref="End"/>.
        /// </summary>
        public float YawDegrees { get; }

        /// <summary>Walls and bridges only: the other end, on the ground. Equal to <see cref="Root"/> for the other kinds.</summary>
        public Vector3 End { get; }

        /// <summary>The kit's scale for trees and bushes; the width across in metres for rocks; 1 for walls.</summary>
        public float Scale { get; }

        /// <summary>The seed of the prop's own <see cref="System.Random"/>, which builds its mesh.</summary>
        public int Seed { get; }

        /// <summary>The cluster it belongs to, in the order the placement made them.</summary>
        public int Cluster { get; }

        public IReadOnlyList<PropShape> Shapes => _shapes ?? new PropShape[0];

        /// <summary>The kinds whose footprint is a line from <see cref="Root"/> to <see cref="End"/>.</summary>
        public static bool IsLine(PropKind kind) => kind == PropKind.Wall || kind == PropKind.Bridge;

        /// <summary>The kinds that stand on the ground as part of a built prop's trace (dropped with it).</summary>
        public static bool IsTrace(PropKind kind) => kind == PropKind.Bucket || kind == PropKind.Bicycle || kind == PropKind.Laundry || kind == PropKind.Rake;
    }
}
