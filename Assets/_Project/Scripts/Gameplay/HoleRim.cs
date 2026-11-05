using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>What happens when a moving ball's contact point first crosses the rim of the hole.</summary>
    public readonly struct RimMeeting
    {
        public RimMeeting(bool captured, float turnDegrees, float speedKeep)
        {
            Captured = captured;
            TurnDegrees = turnDegrees;
            SpeedKeep = speedKeep;
        }

        public bool Captured { get; }

        /// <summary>The total turn of a lip-out, signed, with positive from +X toward +Z. It always leads away from the center.</summary>
        public float TurnDegrees { get; }

        /// <summary>The share of its speed the ball keeps through a lip-out.</summary>
        public float SpeedKeep { get; }
    }

    /// <summary>
    /// The rule for capture and lip-out, with no randomness: the same line at the same speed always ends the
    /// same way, so a player can learn it and a copy of the simulation can predict it.
    ///
    /// Everything comes from how deep the ball's line cuts across the hole: 1 through the center, 0 on a line
    /// that only touches the rim. That depth is the chord's share of the diameter, and a real ball's time to
    /// fall in grows with that chord, so a central hit is forgiven more speed.
    /// </summary>
    public static class HoleRim
    {
        /// <param name="fromCenter">The contact point relative to the hole's center, in the ground plane.</param>
        /// <param name="velocity">The ball's velocity in the ground plane.</param>
        public static RimMeeting Meet(Vector2 fromCenter, Vector2 velocity, ShotSettings settings)
        {
            float speed = velocity.magnitude;
            if (speed <= 0f)
            {
                return new RimMeeting(true, 0f, 1f);
            }

            // The 2D cross product of the direction with the way to the center: its size is how far the line
            // passes from the center, and its sign says on which side.
            Vector2 direction = velocity / speed;
            float side = (direction.y * fromCenter.x) - (direction.x * fromCenter.y);
            float offset = Mathf.Clamp01(Mathf.Abs(side) / settings.HoleCaptureRadius);
            float depth = Mathf.Sqrt(1f - (offset * offset));

            float limit = Mathf.Lerp(settings.RimCaptureSpeed, settings.CenterCaptureSpeed, depth);
            if (speed <= limit)
            {
                return new RimMeeting(true, 0f, 1f);
            }

            // 2 * offset * depth is sin(2a) for a line at angle a to the rim's normal, as in a reflection: nothing
            // for a dead-center hit or a graze, most in between. Speed past the limit carries the ball through the turn.
            float turn = settings.LipOutMaxTurn * 2f * offset * depth * (limit / speed);

            // With the center on the ball's left, side is positive, and turning away from it is clockwise.
            float signedTurn = side > 0f ? -turn : turn;
            return new RimMeeting(false, signedTurn, Mathf.Lerp(1f, settings.LipOutSpeedKeep, depth));
        }
    }
}
