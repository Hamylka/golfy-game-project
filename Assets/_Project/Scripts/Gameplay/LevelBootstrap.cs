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

        private void Start()
        {
            terrain.Build();
            islandCamera.SetIsland(terrain.Outline);
        }
    }
}
