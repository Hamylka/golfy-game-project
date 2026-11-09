using System.Collections.Generic;
using IsoGolf.Generation;
using IsoGolf.Rendering;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// Builds the level and points the camera at it. This lives in Gameplay because it's the one place
    /// that knows about both the terrain and the camera, which stay independent of each other.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] private TerrainView terrain;
        [SerializeField] private IslandCamera islandCamera;
        [Tooltip("Optional. Scenes without clouds keep their plain background.")]
        [SerializeField] private CloudView clouds;
        [Tooltip("Optional, with the hole. Sandbox scenes have no ball.")]
        [SerializeField] private BallView ball;
        [SerializeField] private HoleView hole;
        [Tooltip("Optional. Scenes without it keep their sun as authored.")]
        [SerializeField] private LevelSun sun;

        [Header("Generated course")]
        [Tooltip("Optional, needs the ball. When set, the course is generated from the seed and par below and checked; otherwise the terrain's hand-made shape is built.")]
        [SerializeField] private LevelGenSettings generator;
        [SerializeField] private int seed;
        [SerializeField, Range(3, 5)] private int par = 3;

        /// <summary>What the checked generation settled on, or null for a hand-made course.</summary>
        public CheckedCourseResult Generated { get; private set; }

        private void Start()
        {
            if (generator != null)
            {
                Generated = CheckedCourse.Generate(seed, par, generator, ball.Shot);
                Debug.Log($"Course seed {seed} par {par}: variant {Generated.Variant} kept after {Generated.Candidates} candidate(s), failed checks: {Generated.Failed}.");
                terrain.Build(Generated.Layout);
            }
            else
            {
                terrain.Build();
            }

            islandCamera.SetIsland(terrain.Outline, terrain.Layout.Centreline.Points, GroundEdges(terrain.Edge));
            if (ball != null)
            {
                // The visible ground, which the ball sits on, so the approach zoom frames a raised or lowered green right.
                var fields = new CourseFields(terrain.Layout);
                islandCamera.SetHole(terrain.Layout.Hole, ShotReach.FullPowerDistance(ball.Shot), fields.Height);
            }

            // Before the clouds, which bake their tones from the sun's direction.
            if (sun != null)
            {
                sun.Apply(SunHeading.CourseHeading(terrain.Layout.Centreline.Points, terrain.Layout.Tee, terrain.Layout.Hole));
            }

            // After the hole, which decides the headings the camera can take, and so the ground the clouds must cover.
            if (clouds != null)
            {
                clouds.Build(islandCamera.VisibleGround);
            }

            if (ball != null)
            {
                var ground = new BallGround(terrain.Layout);
                hole.Build(ground);
                ball.Build(ground);
                islandCamera.PlaceOn(ball.transform.localPosition);
            }
        }

        /// <summary>The island's edge on the ground, as the camera reads it.</summary>
        public static List<GroundSegment> GroundEdges(IReadOnlyList<EdgeSegment> edges)
        {
            var ground = new List<GroundSegment>(edges.Count);
            foreach (EdgeSegment edge in edges)
            {
                ground.Add(new GroundSegment(new Vector2(edge.A.x, edge.A.z), new Vector2(edge.B.x, edge.B.z)));
            }

            return ground;
        }
    }
}
