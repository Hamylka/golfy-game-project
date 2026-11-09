#if ISOGOLF_MEASURE
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// Phone-check only, compiled only with the scripting define ISOGOLF_MEASURE (never in a release build).
    /// Holds the seed and par picked on the device, which <see cref="LevelBootstrap"/> prefers over its Inspector
    /// values. Kept in PlayerPrefs so a restart of the app keeps the last course.
    /// </summary>
    public static class DebugCourseOverride
    {
        private const string SeedKey = "IsoGolf.DebugSeed";
        private const string ParKey = "IsoGolf.DebugPar";

        public static bool Has => PlayerPrefs.HasKey(SeedKey);

        public static int Seed => PlayerPrefs.GetInt(SeedKey, 0);

        public static int Par => Mathf.Clamp(PlayerPrefs.GetInt(ParKey, 3), 3, 5);

        public static void Set(int seed, int par)
        {
            PlayerPrefs.SetInt(SeedKey, Mathf.Max(0, seed));
            PlayerPrefs.SetInt(ParKey, Mathf.Clamp(par, 3, 5));
            PlayerPrefs.Save();
        }
    }
}
#endif
