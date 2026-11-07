using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The generator's only source of chance: a seeded <see cref="System.Random"/>, drawn from in whole numbers only.
    /// Its integer sequence is the same managed algorithm in the Editor (Mono) and on the phone (IL2CPP), so every
    /// choice, a chance in percent, a length in centimetres or an angle in whole degrees, is the same on both. Floats
    /// only appear once a choice is made, so at worst they differ in their last bits.
    /// </summary>
    public sealed class SeededRandom
    {
        private readonly System.Random _random;

        public SeededRandom(int seed)
        {
            _random = new System.Random(seed);
        }

        /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public int Int(int min, int max)
        {
            return max <= min ? min : _random.Next(min, max + 1);
        }

        /// <summary>True <paramref name="percent"/> times in a hundred.</summary>
        public bool Chance(int percent)
        {
            return _random.Next(100) < percent;
        }

        /// <summary>True or false, half the time each.</summary>
        public bool Coin()
        {
            return _random.Next(2) == 0;
        }

        /// <summary>A length from the range's x to its y, in whole centimetres.</summary>
        public float Centimetres(Vector2 range)
        {
            return Int(Mathf.RoundToInt(range.x * 100f), Mathf.RoundToInt(range.y * 100f)) / 100f;
        }

        /// <summary>A value from the range's x to its y, in hundredths (a fraction of a course, say).</summary>
        public float Hundredths(Vector2 range)
        {
            return Centimetres(range);
        }

        /// <summary>An angle from the range's x to its y, in whole degrees.</summary>
        public float Degrees(Vector2 range)
        {
            return Int(Mathf.RoundToInt(range.x), Mathf.RoundToInt(range.y));
        }
    }
}
