using System;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>How many props a par gets: low density, clusters with empty ground between them (M4 plan, Q3 A).</summary>
    [Serializable]
    public sealed class PropDensity
    {
        [Tooltip("Clusters on the course, from the fewest to the most. Fewer if the island has no room for them.")]
        [SerializeField] private Vector2Int clusters = new Vector2Int(2, 3);
        [Tooltip("Props in a cluster, from the fewest to the most. A theme may ask for fewer.")]
        [SerializeField] private Vector2Int members = new Vector2Int(2, 6);
        [Tooltip("Clusters' centres keep at least this far apart, in metres.")]
        [SerializeField, Min(0f)] private float clusterSpacing = 7f;

        public Vector2Int Clusters => clusters;
        public Vector2Int Members => members;
        public float ClusterSpacing => clusterSpacing;

        public static PropDensity Create(Vector2Int clusters, Vector2Int members, float clusterSpacing)
        {
            return new PropDensity { clusters = clusters, members = members, clusterSpacing = clusterSpacing };
        }
    }

    /// <summary>
    /// The prop placement's tunables (M4 PR 4.3, <see cref="PropPlacement"/>): the density per par, the keep-out zones and
    /// the themes. Lengths in metres, chances and weights in whole numbers.
    /// </summary>
    [Serializable]
    public sealed class PropRules
    {
        [Header("Density per par")]
        [SerializeField] private PropDensity par3 = PropDensity.Create(new Vector2Int(2, 3), new Vector2Int(2, 6), 7f);
        [SerializeField] private PropDensity par4 = PropDensity.Create(new Vector2Int(3, 5), new Vector2Int(2, 6), 8f);
        [SerializeField] private PropDensity par5 = PropDensity.Create(new Vector2Int(4, 7), new Vector2Int(2, 6), 8f);

        [Header("Keep-out zones")]
        [Tooltip("No prop within this far of the tee.")]
        [SerializeField, Min(0f)] private float teeKeepOut = 3f;
        [Tooltip("No prop within this far of the green's border.")]
        [SerializeField, Min(0f)] private float greenKeepOut = 1.5f;
        [Tooltip("No prop within this far of a bunker's or a pond's border (round rim rocks excepted).")]
        [SerializeField, Min(0f)] private float hazardKeepOut = 0.5f;
        [Tooltip("A prop's foot keeps at least this far inside the island's edge.")]
        [SerializeField, Min(0f)] private float edgeMargin = 0.4f;
        [Tooltip("A crown may hang this share of its radius out over the edge.")]
        [SerializeField, Range(0f, 1f)] private float crownOverEdge = 0.5f;
        [Tooltip("Kept free for M5's next island: this far along the centreline past the hole.")]
        [SerializeField, Min(0f)] private float endPastHole = 6f;
        [Tooltip("And a fan this wide around +X from the hole, in degrees.")]
        [SerializeField, Range(0f, 180f)] private float endFan = 60f;

        [Header("Clusters")]
        [Tooltip("A cluster starts only where this much free ground surrounds it, so the zone is twice as wide.")]
        [SerializeField, Min(0f)] private float clusterStartRoom = 1f;
        [Tooltip("Spacing of the grid of possible cluster starts.")]
        [SerializeField, Min(0.25f)] private float startGrid = 1f;
        [Tooltip("Members stand at most this far from their cluster's centre.")]
        [SerializeField, Min(0.5f)] private float clusterRadius = 3f;
        [Tooltip("Tries for each member before it is given up.")]
        [SerializeField, Range(1, 32)] private int memberTries = 10;
        [Tooltip("Two props keep their footprints' radii, times this, apart (below 1 crowns may touch).")]
        [SerializeField, Range(0.3f, 1.5f)] private float memberSpacing = 0.85f;
        [Tooltip("Starts a cluster tries before it is given up, when one can't hold two props.")]
        [SerializeField, Range(1, 8)] private int clusterAttempts = 3;
        [Tooltip("A cypress line runs along the course, turned at most this many degrees.")]
        [SerializeField, Range(0, 90)] private int cypressLineTurn = 15;

        [Header("Themes")]
        [Tooltip("Weights of the inland themes: olive grove, cypress line along a wall, bushes and rocks.")]
        [SerializeField] private Vector3Int inlandWeights = new Vector3Int(45, 25, 30);
        [Tooltip("Share of bushes in the bushes and rocks theme, in percent; the others are rocks.")]
        [SerializeField, Range(0, 100)] private int bushShare = 80;
        [Tooltip("A start this close to the island's edge may take the edge theme (rocks or a short wall along it).")]
        [SerializeField, Min(0f)] private float edgeBand = 3f;
        [SerializeField, Range(0, 100)] private int edgeThemeChance = 20;
        [Tooltip("The edge theme stays this far from any pond, so it never stands where water meets the edge.")]
        [SerializeField, Min(0f)] private float edgeFromPond = 3f;
        [Tooltip("Chance that an edge cluster is a short wall rather than rocks.")]
        [SerializeField, Range(0, 100)] private int edgeWallChance = 40;
        [Tooltip("A start this close to a pond may take the rim theme (round white rocks on the rim).")]
        [SerializeField, Min(0f)] private float pondNear = 3.5f;
        [SerializeField, Range(0, 100)] private int rimThemeChance = 80;
        [Tooltip("Free ground between a rim rock and the pond's border, from the least to the most.")]
        [SerializeField] private Vector2 rimGap = new Vector2(0.05f, 0.4f);

        [Header("Sizes")]
        [SerializeField] private Vector2 oliveScale = new Vector2(0.5f, 0.8f);
        [SerializeField] private Vector2 cypressScale = new Vector2(0.8f, 1.1f);
        [SerializeField] private Vector2 bushScale = new Vector2(0.8f, 1.25f);
        [Tooltip("Rocks' width across, in metres.")]
        [SerializeField] private Vector2 rockSize = new Vector2(0.3f, 0.9f);
        [SerializeField] private Vector2 roundRockSize = new Vector2(0.3f, 0.6f);
        [Tooltip("Cypresses in a line stand this far apart.")]
        [SerializeField] private Vector2 cypressSpacing = new Vector2(1.3f, 1.7f);
        [SerializeField] private Vector2 wallLength = new Vector2(2.5f, 4.5f);
        [Tooltip("The wall beside a cypress line runs this far to its side.")]
        [SerializeField, Min(0f)] private float wallBesideLine = 1.1f;

        [Header("Built props (M4 PR 4.4)")]
        [Tooltip("Built clusters per par, extra to the density's clusters: a shed or a bench with its trace. Fewer if the island has no room.")]
        [SerializeField] private Vector2Int builtClustersPar3 = new Vector2Int(0, 1);
        [SerializeField] private Vector2Int builtClustersPar4 = new Vector2Int(1, 2);
        [SerializeField] private Vector2Int builtClustersPar5 = new Vector2Int(1, 2);
        [Tooltip("Chance in percent that a built cluster is a shed; the others are a bench.")]
        [SerializeField, Range(0, 100)] private int shedShare = 50;
        [Tooltip("Chance in percent that a shed or bench gets its trace: a bicycle or laundry by a shed, a bucket by a bench.")]
        [SerializeField, Range(0, 100)] private int builtTraceChance = 80;
        [Tooltip("Chance in percent that a shed's trace is a bicycle on its wall; the others hang laundry behind it.")]
        [SerializeField, Range(0, 100)] private int bicycleShare = 50;
        [Tooltip("Greenery (bushes, olives) around a built prop, from the fewest to the most.")]
        [SerializeField] private Vector2Int builtGreenery = new Vector2Int(0, 2);
        [Tooltip("Traces on the whole course, at most (shed and bench traces and the rake).")]
        [SerializeField, Range(0, 4)] private int traceMax = 2;
        [Tooltip("Chance in percent that a course with a bunker leaves a rake in one.")]
        [SerializeField, Range(0, 100)] private int rakeChance = 50;
        [Tooltip("Chance in percent, per pond with room for it, that a bridge crosses it. At most one per course.")]
        [SerializeField, Range(0, 100)] private int bridgeChance = 33;
        [Tooltip("The bridge's deck reaches this far past the pond's border on each bank.")]
        [SerializeField, Min(0.3f)] private float bridgeBank = 0.7f;
        [Tooltip("The longest span a bridge crosses, in metres, border to border.")]
        [SerializeField, Min(0.5f)] private float bridgeMaxSpan = 6f;
        [Tooltip("Chance in percent that a course has a landmark, a chapel or a lighthouse.")]
        [SerializeField, Range(0, 100)] private int landmarkChance = 50;
        [Tooltip("Chance in percent that the landmark is a lighthouse; the others are chapels.")]
        [SerializeField, Range(0, 100)] private int lighthouseShare = 50;
        [Tooltip("A landmark's foot stands this far inside the island's edge, from the least to the most.")]
        [SerializeField] private Vector2 landmarkEdgeBand = new Vector2(0.8f, 2.4f);
        [Tooltip("A promontory: at most this share, in percent, of a ring around the spot is island.")]
        [SerializeField, Range(0, 100)] private int landmarkIslandShare = 60;
        [Tooltip("Radius of that ring, in metres.")]
        [SerializeField, Min(0.5f)] private float landmarkRing = 3f;
        [Tooltip("A landmark stands at least this far from the course's centreline, the play axis.")]
        [SerializeField, Min(0f)] private float landmarkFromCentreline = 3.5f;
        [Tooltip("Built props' scale range, in hundredths.")]
        [SerializeField] private Vector2 builtScale = new Vector2(0.92f, 1.12f);

        [Header("Shadows")]
        [Tooltip("Where shadows fall, in degrees from the course's heading (positive toward +Z), as Main's sun casts them. Read from the scene by a test.")]
        [SerializeField] private float shadowHeading = -125f;
        [Tooltip("A shadow's length on flat ground per metre of height, from the sun's pitch.")]
        [SerializeField, Min(0f)] private float shadowLength = 0.78f;

        public PropDensity Density(int par)
        {
            switch (par)
            {
                case 3: return par3;
                case 4: return par4;
                case 5: return par5;
                default: throw new ArgumentOutOfRangeException(nameof(par), par, "A course is a par 3, 4 or 5.");
            }
        }

        /// <summary>Built clusters (a shed or a bench with its trace) a par gets, extra to its density's clusters.</summary>
        public Vector2Int BuiltClusters(int par)
        {
            switch (par)
            {
                case 3: return builtClustersPar3;
                case 4: return builtClustersPar4;
                case 5: return builtClustersPar5;
                default: throw new ArgumentOutOfRangeException(nameof(par), par, "A course is a par 3, 4 or 5.");
            }
        }

        public int ShedShare => shedShare;
        public int BuiltTraceChance => builtTraceChance;
        public int BicycleShare => bicycleShare;
        public Vector2Int BuiltGreenery => builtGreenery;
        public int TraceMax => traceMax;
        public int RakeChance => rakeChance;
        public int BridgeChance => bridgeChance;
        public float BridgeBank => bridgeBank;
        public float BridgeMaxSpan => bridgeMaxSpan;
        public int LandmarkChance => landmarkChance;
        public int LighthouseShare => lighthouseShare;
        public Vector2 LandmarkEdgeBand => landmarkEdgeBand;
        public int LandmarkIslandShare => landmarkIslandShare;
        public float LandmarkRing => landmarkRing;
        public float LandmarkFromCentreline => landmarkFromCentreline;
        public Vector2 BuiltScale => builtScale;
        public float TeeKeepOut => teeKeepOut;
        public float GreenKeepOut => greenKeepOut;
        public float HazardKeepOut => hazardKeepOut;
        public float EdgeMargin => edgeMargin;
        public float CrownOverEdge => crownOverEdge;
        public float EndPastHole => endPastHole;
        public float EndFan => endFan;
        public float ClusterStartRoom => clusterStartRoom;
        public float StartGrid => startGrid;
        public float ClusterRadius => clusterRadius;
        public int MemberTries => memberTries;
        public float MemberSpacing => memberSpacing;
        public int ClusterAttempts => clusterAttempts;
        public int CypressLineTurn => cypressLineTurn;
        public Vector3Int InlandWeights => inlandWeights;
        public float EdgeBand => edgeBand;
        public int EdgeThemeChance => edgeThemeChance;
        public int BushShare => bushShare;
        public float EdgeFromPond => edgeFromPond;
        public int EdgeWallChance => edgeWallChance;
        public float PondNear => pondNear;
        public int RimThemeChance => rimThemeChance;
        public Vector2 RimGap => rimGap;
        public Vector2 OliveScale => oliveScale;
        public Vector2 CypressScale => cypressScale;
        public Vector2 BushScale => bushScale;
        public Vector2 RockSize => rockSize;
        public Vector2 RoundRockSize => roundRockSize;
        public Vector2 CypressSpacing => cypressSpacing;
        public Vector2 WallLength => wallLength;
        public float WallBesideLine => wallBesideLine;
        public float ShadowHeading => shadowHeading;
        public float ShadowLength => shadowLength;
    }
}
