using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>The 12 corners and 20 faces that low poly blobs, in trees and clouds, start from.</summary>
    internal static class Icosahedron
    {
        private static readonly float T = (1f + Mathf.Sqrt(5f)) * 0.5f;

        public static readonly Vector3[] Corners =
        {
            new Vector3(-1f, T, 0f), new Vector3(1f, T, 0f), new Vector3(-1f, -T, 0f), new Vector3(1f, -T, 0f),
            new Vector3(0f, -1f, T), new Vector3(0f, 1f, T), new Vector3(0f, -1f, -T), new Vector3(0f, 1f, -T),
            new Vector3(T, 0f, -1f), new Vector3(T, 0f, 1f), new Vector3(-T, 0f, -1f), new Vector3(-T, 0f, 1f),
        };

        public static readonly int[] Faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
    }
}
