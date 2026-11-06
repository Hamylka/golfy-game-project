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

        private void Start()
        {
            terrain.Build();
            islandCamera.SetIsland(terrain.Outline);
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
    }
}
