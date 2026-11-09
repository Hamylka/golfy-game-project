using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("IsoGolf.Tests.EditMode")]

namespace IsoGolf.Generation
{
    /// <summary>The reason a hazard is where it is. The generator places them in this order, required roles first.</summary>
    public enum HazardRole
    {
        /// <summary>Beside or in front of the green, on the side the direct line comes from.</summary>
        GreenGuard,

        /// <summary>At a dogleg's inside corner, in the way of the shot that cuts it.</summary>
        CornerCarry,

        /// <summary>Across the drive from the tee on a course without a dogleg.</summary>
        DriveZone,

        /// <summary>A second bunker beside the green, turned away from the first guard.</summary>
        SecondGreenGuard,

        /// <summary>Fills up the course beside the safe route's rest spots.</summary>
        Flank,
    }

    public enum ShotLineKind
    {
        /// <summary>From where a drive leaves the ball straight at the hole.</summary>
        Direct,

        /// <summary>The shot that cuts a dogleg's corner.</summary>
        Cut,

        /// <summary>One leg of the safe route.</summary>
        Safe,
    }

    /// <summary>What a hazard is for. <see cref="Index"/> is into <see cref="CourseLayout.Ponds"/> if <see cref="IsWater"/>, otherwise into <see cref="CourseLayout.Bunkers"/>.</summary>
    public readonly struct HazardInfo
    {
        public HazardInfo(HazardRole role, bool isWater, int index)
        {
            Role = role;
            IsWater = isWater;
            Index = index;
        }

        public HazardRole Role { get; }
        public bool IsWater { get; }
        public int Index { get; }
    }

    /// <summary>A shot the strategy is built around, in the course's ground plane (X, Z).</summary>
    public readonly struct ShotLine
    {
        public ShotLine(Vector2 from, Vector2 to, ShotLineKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        public Vector2 From { get; }
        public Vector2 To { get; }
        public ShotLineKind Kind { get; }
    }

    /// <summary>
    /// What the generator meant by a course: the role of each hazard, the shots they were placed around, and the safe route
    /// that goes round them. Read-only data, <c>null</c> on hand-made courses. The course checks (PR 7) reroll an incomplete
    /// plan; the props (M4) keep off the shot lines.
    /// </summary>
    public sealed class CoursePlan
    {
        public CoursePlan(
            IEnumerable<HazardInfo> hazards,
            IEnumerable<Vector2> safeRoute,
            IEnumerable<ShotLine> lines,
            IEnumerable<HazardRole> missingRoles,
            int hazardTarget,
            bool gapCountsAsHazard,
            Vector2 safeTarget,
            Vector2 guardDirection,
            float shotCorridorHalfWidth,
            bool dogleg,
            bool islandGreen,
            Vector2 directOrigin)
        {
            Hazards = new List<HazardInfo>(hazards);
            SafeRoute = new List<Vector2>(safeRoute);
            Lines = new List<ShotLine>(lines);
            MissingRoles = new List<HazardRole>(missingRoles);
            HazardTarget = hazardTarget;
            GapCountsAsHazard = gapCountsAsHazard;
            SafeTarget = safeTarget;
            GuardDirection = guardDirection;
            ShotCorridorHalfWidth = shotCorridorHalfWidth;
            Dogleg = dogleg;
            IslandGreen = islandGreen;
            DirectOrigin = directOrigin;
        }

        public IReadOnlyList<HazardInfo> Hazards { get; }

        /// <summary>The tee, the rest spots and the safe target on the green, in order. Empty if the bare course has no safe route.</summary>
        public IReadOnlyList<Vector2> SafeRoute { get; }

        /// <summary>The direct line, each cuttable corner's cut line and the safe route's legs.</summary>
        public IReadOnlyList<ShotLine> Lines { get; }

        /// <summary>Required roles that found no place. PR 7 rerolls the course on any.</summary>
        public IReadOnlyList<HazardRole> MissingRoles { get; }

        /// <summary>The number of hazards the seed asked for, a gap included if it counts.</summary>
        public int HazardTarget { get; }

        public bool GapCountsAsHazard { get; }

        /// <summary>The middle of the green's safe half, where the safe route ends.</summary>
        public Vector2 SafeTarget { get; }

        /// <summary>The unit vector from the green's centre toward where its guard sits.</summary>
        public Vector2 GuardDirection { get; }

        /// <summary>Half the width of the strip M4 keeps clear of props along the shot lines.</summary>
        public float ShotCorridorHalfWidth { get; }

        /// <summary>True for a course with a dogleg (not for soft bends).</summary>
        public bool Dogleg { get; }

        /// <summary>True for a par 3 whose green is on an island of its own.</summary>
        public bool IslandGreen { get; }

        /// <summary>Where the direct line starts: the tee, or on a long par 5 where a drive leaves the ball.</summary>
        public Vector2 DirectOrigin { get; }

        public bool Complete => MissingRoles.Count == 0 && SafeRoute.Count > 1;
    }
}
