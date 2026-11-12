using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The flight a safe leg is planned with (strategic obstacles S1): from a rest spot, straight at the next, at the power whose
    /// arc comes down at the leg's end. <c>IsoGolf.Generation</c> can't see <c>BallSimulation</c>, so this mirrors
    /// <c>BallisticArc.ForCarry</c> from the copied shot numbers (<see cref="LevelGenSettings.LaunchAngle"/>, the carries), and
    /// <c>StrategicZoneTests.ArcHeightModelMatchesSimulation</c> holds it to the real arc.
    /// <para>
    /// On a fixed launch angle θ and a flat carry C, the ball's bottom stands h0 + x·tanθ·(1 − x/C) at x metres along: gravity
    /// cancels out (the launch speed is set from it), so the arc depends on the angle and the carry only. The carry that lands
    /// at the leg's end, at height h1 after L metres, follows in closed form: C = L / (1 − (h1 − h0) / (L·tanθ)). Rest spots are on
    /// fairway, whose carry scale is 1 (a test holds that too), so the carry is the power's carry as is.
    /// </para>
    /// </summary>
    public readonly struct PlannedArc
    {
        private readonly float _tan;

        public PlannedArc(Vector2 from, float fromHeight, Vector2 to, float toHeight, LevelGenSettings settings)
        {
            From = from;
            FromHeight = fromHeight;
            Length = Vector2.Distance(from, to);
            Direction = Length > 1e-6f ? (to - from) / Length : Vector2.right;
            _tan = Mathf.Tan(settings.LaunchAngle * Mathf.Deg2Rad);

            // A leg too steep for any carry to reach its end climbs at full power; one that drops further needs less than zero.
            float share = Length > 1e-6f ? 1f - ((toHeight - fromHeight) / (Length * _tan)) : 1f;
            float carry = share > 1e-6f ? Length / share : float.MaxValue;
            float span = settings.FullCarry - settings.MinCarry;
            Power = span > 1e-6f ? Mathf.Clamp01((carry - settings.MinCarry) / span) : 1f;
            Carry = settings.CarryAt(Power);
        }

        public Vector2 From { get; }
        public float FromHeight { get; }
        public float Length { get; }
        public Vector2 Direction { get; }

        /// <summary>The power the leg is played at, 0 to 1: what <c>CourseChecks.SafeLegsFly</c> plays.</summary>
        public float Power { get; }

        /// <summary>The flat carry of <see cref="Power"/>.</summary>
        public float Carry { get; }

        /// <summary>The height of the ball's bottom (its contact point) this many metres along the leg, in course space.</summary>
        public float HeightAt(float along) => FromHeight + (along * _tan * (1f - (along / Carry)));

        /// <summary>How far along the leg a point projects, in metres from its start.</summary>
        public float Along(Vector2 p) => Vector2.Dot(p - From, Direction);
    }
}
