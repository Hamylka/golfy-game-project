using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>One knot of an <see cref="ElevationProfile"/>: a distance along the centreline, the ground height there, and the side slope there.</summary>
    public readonly struct ElevationKnot
    {
        public ElevationKnot(float along, float height, float camber)
        {
            Along = along;
            Height = height;
            Camber = camber;
        }

        /// <summary>Metres from the tee, along the centreline.</summary>
        public float Along { get; }

        /// <summary>Ground height on the centreline, in metres.</summary>
        public float Height { get; }

        /// <summary>Rise per metre across the centreline, toward its left. 0.03 is a 3% side slope.</summary>
        public float Camber { get; }
    }

    /// <summary>
    /// The broad shape of a generated course's ground: a height and a side slope (camber) at a few knots along the
    /// centreline, joined by monotone cubic Hermite curves. The first and last knots are plateaus, flat before the first
    /// and after the last, so the tee and the green sit on level ground. An <see cref="Empty"/> profile adds exactly 0,
    /// which keeps the hand-made courses bit for bit as they were.
    /// </summary>
    public sealed class ElevationProfile
    {
        public static readonly ElevationProfile Empty = new ElevationProfile(new ElevationKnot[0], 0f);

        private readonly float[] _along;
        private readonly float[] _height;
        private readonly float[] _camber;
        private readonly float[] _heightTangent;
        private readonly float[] _camberTangent;

        public ElevationProfile(IEnumerable<ElevationKnot> knots, float legBlend)
        {
            var list = new List<ElevationKnot>(knots);
            int count = list.Count;
            _along = new float[count];
            _height = new float[count];
            _camber = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && list[i].Along <= list[i - 1].Along)
                {
                    throw new ArgumentException("Knots must run in increasing order along the centreline.", nameof(knots));
                }

                _along[i] = list[i].Along;
                _height[i] = list[i].Height;
                _camber[i] = list[i].Camber;
            }

            _heightTangent = Tangents(_along, _height);
            _camberTangent = Tangents(_along, _camber);
            LegBlend = legBlend;
        }

        /// <summary>True when there are no knots: the ground gets nothing from this profile.</summary>
        public bool IsEmpty => _along.Length == 0;

        public int KnotCount => _along.Length;

        public ElevationKnot Knot(int index) => new ElevationKnot(_along[index], _height[index], _camber[index]);

        /// <summary>How far, in metres, the two legs of a bend are blended: the profile is read from every leg that is nearly as close as the nearest.</summary>
        public float LegBlend { get; }

        /// <summary>The ground height on the centreline this far along it. Flat before the first knot and after the last.</summary>
        public float Height(float along) => Evaluate(_height, _heightTangent, along);

        /// <summary>The side slope this far along the centreline.</summary>
        public float Camber(float along) => Evaluate(_camber, _camberTangent, along);

        /// <summary>The height at a point: the profile at its distance along, plus the camber times its distance across.</summary>
        public float At(float along, float across) => Height(along) + (Camber(along) * across);

        /// <summary>
        /// Fritsch-Butland tangents: the weighted harmonic mean of the two neighbouring secants where the profile keeps
        /// climbing or keeps falling, and 0 at an extremum, so it never overshoots. 0 at both ends, which are plateaus.
        /// With evenly spaced knots a tangent is at most twice the smaller secant, so a step of rise R over run D never
        /// has a slope beyond 2R/D.
        /// </summary>
        private static float[] Tangents(float[] along, float[] values)
        {
            int count = along.Length;
            var tangents = new float[count];
            for (int i = 1; i < count - 1; i++)
            {
                float h0 = along[i] - along[i - 1];
                float h1 = along[i + 1] - along[i];
                float d0 = (values[i] - values[i - 1]) / h0;
                float d1 = (values[i + 1] - values[i]) / h1;
                if (d0 * d1 <= 0f)
                {
                    continue;
                }

                float w0 = (2f * h1) + h0;
                float w1 = h1 + (2f * h0);
                tangents[i] = (w0 + w1) / ((w0 / d0) + (w1 / d1));
            }

            return tangents;
        }

        private float Evaluate(float[] values, float[] tangents, float along)
        {
            int last = _along.Length - 1;
            if (last < 0)
            {
                return 0f;
            }

            if (along <= _along[0])
            {
                return values[0];
            }

            if (along >= _along[last])
            {
                return values[last];
            }

            int i = 0;
            while (along > _along[i + 1])
            {
                i++;
            }

            float width = _along[i + 1] - _along[i];
            float t = (along - _along[i]) / width;
            float t2 = t * t;
            float t3 = t2 * t;
            return (((2f * t3) - (3f * t2) + 1f) * values[i])
                + ((t3 - (2f * t2) + t) * width * tangents[i])
                + (((-2f * t3) + (3f * t2)) * values[i + 1])
                + ((t3 - t2) * width * tangents[i + 1]);
        }
    }
}
