using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>Where and when an arc first meets the ground.</summary>
    public readonly struct ArcLanding
    {
        public ArcLanding(float time, Vector3 point, CourseSurface surface)
        {
            Time = time;
            Point = point;
            Surface = surface;
        }

        public float Time { get; }
        public Vector3 Point { get; }
        public CourseSurface Surface { get; }
    }

    /// <summary>
    /// A flight, as an exact formula of time rather than steps of integration. The aim preview and the ball
    /// read this same formula, which is what makes the preview match the real flight exactly, at any frame
    /// rate. There is no drag, wind or spin, so the shape is always a symmetric bell.
    /// </summary>
    public readonly struct BallisticArc
    {
        // Only a guard against a loop that never ends: any arc under gravity falls below the ground long before this.
        private const float LongestFlightSeconds = 60f;

        // Halves the search interval each time. From a 0.02 s interval this is far finer than a frame.
        private const int RefineSteps = 12;

        public BallisticArc(Vector3 start, Vector3 velocity, float gravity)
        {
            Start = start;
            Velocity = velocity;
            Gravity = gravity;
        }

        public Vector3 Start { get; }
        public Vector3 Velocity { get; }
        public float Gravity { get; }

        /// <summary>
        /// The arc that travels <paramref name="carry"/> metres before returning to its starting height. The
        /// range of a projectile is R = v² sin(2θ) / g, solved here for v. The power therefore sets a
        /// distance directly, which is what a player reads, and the speed follows from it.
        /// </summary>
        public static BallisticArc ForCarry(Vector3 start, Vector2 direction, float carry, float launchAngleDegrees, float gravity)
        {
            float angle = launchAngleDegrees * Mathf.Deg2Rad;
            float speed = Mathf.Sqrt(carry * gravity / Mathf.Sin(2f * angle));
            Vector2 along = direction.normalized * (speed * Mathf.Cos(angle));
            return new BallisticArc(start, new Vector3(along.x, speed * Mathf.Sin(angle), along.y), gravity);
        }

        public Vector3 PositionAt(float time)
        {
            Vector3 position = Start + (Velocity * time);
            position.y -= 0.5f * Gravity * time * time;
            return position;
        }

        public Vector3 VelocityAt(float time)
        {
            return new Vector3(Velocity.x, Velocity.y - (Gravity * time), Velocity.z);
        }

        /// <summary>
        /// Walks along the arc until it is at or below the ground, then narrows that interval down. The first
        /// check is one step in, because an arc starts on the ground. A bump thinner than one step could be
        /// missed, but a step is well under a terrain cell and the smooth ground has no features that small.
        /// </summary>
        public ArcLanding FindLanding(IBallGround ground, float searchStep)
        {
            float above = 0f;
            float below = searchStep;
            while (below < LongestFlightSeconds && !IsAtOrBelowGround(ground, below))
            {
                above = below;
                below += searchStep;
            }

            for (int i = 0; i < RefineSteps; i++)
            {
                float middle = (above + below) * 0.5f;
                if (IsAtOrBelowGround(ground, middle))
                {
                    below = middle;
                }
                else
                {
                    above = middle;
                }
            }

            Vector3 point = PositionAt(below);
            point.y = ground.LandingHeight(point.x, point.z);
            return new ArcLanding(below, point, ground.Surface(point.x, point.z));
        }

        private bool IsAtOrBelowGround(IBallGround ground, float time)
        {
            Vector3 point = PositionAt(time);
            return point.y <= ground.LandingHeight(point.x, point.z);
        }
    }
}
