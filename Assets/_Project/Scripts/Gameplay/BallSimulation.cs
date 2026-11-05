using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    public enum BallState
    {
        Resting,
        Flying,
        Rolling,
        InWater,
        OffIsland,
        Holed,
    }

    /// <summary>
    /// The ball from one shot to the next, in the course's local space, at a fixed time step and with no Unity
    /// physics. The same shot from the same spot always plays out the same way, so a copy can predict a path.
    /// <see cref="Position"/> is the ball's lowest point, where it touches the ground: the view adds the radius.
    ///
    /// A shot from the green rolls. Any other shot follows a <see cref="BallisticArc"/>, then hops in shorter
    /// arcs until it rolls. Each landing and each rolling step asks the ground which surface it's on, and
    /// that decides water and the island's edge. A lost ball returns to where the shot was played.
    /// </summary>
    public sealed class BallSimulation
    {
        private readonly IBallGround _ground;
        private readonly ShotSettings _settings;

        private BallisticArc _arc;
        private ArcLanding _landing;
        private int _arcSteps;
        private float _accumulator;
        private float _lostTimer;
        private Vector3 _previousPosition;
        private Vector3 _shotOrigin;
        private Vector3 _holeBottom;

        public BallSimulation(IBallGround ground, ShotSettings settings, Vector2 start)
        {
            _ground = ground;
            _settings = settings;
            Position = new Vector3(start.x, ground.Height(start.x, start.y), start.y);
            _previousPosition = Position;
            _shotOrigin = Position;
        }

        public BallState State { get; private set; } = BallState.Resting;
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }

        /// <summary>How many hops the current shot has made after its first landing.</summary>
        public int Bounces { get; private set; }

        /// <summary>Whether a shot from here is a putt. The green's drawn border is the strict limit.</summary>
        public bool IsPutt => _ground.Surface(Position.x, Position.z) == CourseSurface.Green;

        /// <summary>Between the last two steps, for drawing smoothly when frames and steps don't line up.</summary>
        public Vector3 InterpolatedPosition => Vector3.Lerp(_previousPosition, Position, _accumulator / _settings.FixedTimeStep);

        /// <summary>
        /// The arc a shot from here would follow, shortened by the surface it's played from. The penalty lives
        /// here so the aim preview, which draws this arc, shows it too. <see cref="Shoot"/> flies the same arc.
        /// </summary>
        public BallisticArc ArcFor(Vector2 direction, float power)
        {
            float lie = _settings.ResponseOf(_ground.Surface(Position.x, Position.z)).CarryScale;
            float carry = _settings.CarryFor(Mathf.Clamp01(power)) * lie;
            return BallisticArc.ForCarry(Position, direction, carry, _settings.LaunchAngle, _settings.Gravity);
        }

        /// <summary>Plays a shot. Refused, returning false, unless the ball is at rest and the direction is real.</summary>
        public bool Shoot(Vector2 direction, float power)
        {
            if (State != BallState.Resting || direction == Vector2.zero)
            {
                return false;
            }

            _shotOrigin = Position;
            Bounces = 0;

            if (IsPutt)
            {
                // Stopping distance on level ground is d = v² / 2a, solved for v, so power sets a distance as it does in flight.
                float deceleration = _settings.ResponseOf(CourseSurface.Green).RollingDeceleration;
                float speed = Mathf.Sqrt(2f * deceleration * _settings.PuttDistanceFor(Mathf.Clamp01(power)));
                Vector2 along = direction.normalized * speed;
                Velocity = new Vector3(along.x, 0f, along.y);
                State = BallState.Rolling;
                return true;
            }

            FlyAlong(ArcFor(direction, power));
            return true;
        }

        /// <summary>Runs as many fixed steps as this much time holds, and keeps the rest for next time.</summary>
        public void Advance(float deltaTime)
        {
            _accumulator += deltaTime;
            while (_accumulator >= _settings.FixedTimeStep)
            {
                _accumulator -= _settings.FixedTimeStep;
                Step();
            }
        }

        public void Step()
        {
            _previousPosition = Position;
            switch (State)
            {
                case BallState.Flying:
                    StepFlight();
                    break;
                case BallState.Rolling:
                    StepRoll();
                    break;
                case BallState.InWater:
                    StepSink();
                    break;
                case BallState.OffIsland:
                    StepFall();
                    break;
                case BallState.Holed:
                    StepDrop();
                    break;
            }
        }

        private void FlyAlong(BallisticArc arc)
        {
            _arc = arc;
            _landing = arc.FindLanding(_ground, _settings.LandingSearchStep);
            _arcSteps = 0;
            Velocity = arc.Velocity;
            State = BallState.Flying;
        }

        private void StepFlight()
        {
            // The time comes from a count of steps, not a running sum, so rounding errors can't build up.
            float time = ++_arcSteps * _settings.FixedTimeStep;
            if (time >= _landing.Time)
            {
                Land();
                return;
            }

            Position = _arc.PositionAt(time);
            Velocity = _arc.VelocityAt(time);
        }

        private void Land()
        {
            Position = _landing.Point;
            Velocity = _arc.VelocityAt(_landing.Time);

            switch (_landing.Surface)
            {
                case CourseSurface.Water:
                    EnterWater();
                    return;
                case CourseSurface.OffIsland:
                    FallOff();
                    return;
            }

            if (TryCapture())
            {
                return;
            }

            Bounce(_landing.Surface);
        }

        /// <summary>
        /// Splits the landing speed into the part into the ground and the part along it. Each surface scales
        /// them separately, and the felt ground's normal means a slope kicks the ball the way it leans.
        /// </summary>
        private void Bounce(CourseSurface surface)
        {
            SurfaceResponse response = _settings.ResponseOf(surface);
            Vector3 normal = _ground.Normal(Position.x, Position.z);

            // The curve the ball sits on and the curve it feels differ a little, so a shallow landing can
            // briefly appear to be leaving the felt ground. That counts as no speed into the ground.
            float into = Mathf.Min(Vector3.Dot(Velocity, normal), 0f);
            Vector3 along = (Velocity - (normal * into)) * response.TangentKeep;
            float bounceSpeed = -into * response.Restitution;

            if (bounceSpeed < _settings.MinBounceSpeed || Bounces >= _settings.MaxBounces)
            {
                Velocity = new Vector3(along.x, 0f, along.z);
                State = BallState.Rolling;
                return;
            }

            Bounces++;
            FlyAlong(new BallisticArc(Position, along + (normal * bounceSpeed), _settings.Gravity));
        }

        /// <summary>
        /// One step of rolling. The slope's pull and the surface's friction both change the speed, and friction
        /// can only slow the ball down, never push it back. So the ball stops dead wherever friction beats the
        /// pull, and no slow creep or jitter needs a threshold to hide it.
        /// </summary>
        private void StepRoll()
        {
            float step = _settings.FixedTimeStep;
            float deceleration = _settings.ResponseOf(_ground.Surface(Position.x, Position.z)).RollingDeceleration;
            Vector2 pull = -_ground.Slope(Position.x, Position.z) * _settings.SlopePull;

            Vector2 velocity = new Vector2(Velocity.x, Velocity.z) + (pull * step);
            float speed = velocity.magnitude;
            float friction = deceleration * step;

            if (speed <= friction)
            {
                Velocity = Vector3.zero;
                if (pull.magnitude <= deceleration)
                {
                    Stop();
                }

                // Otherwise it has only paused at the top of its travel, on ground too steep to hold it.
                return;
            }

            velocity *= (speed - friction) / speed;
            Velocity = new Vector3(velocity.x, 0f, velocity.y);

            float x = Position.x + (velocity.x * step);
            float z = Position.z + (velocity.y * step);
            Position = new Vector3(x, _ground.Height(x, z), z);

            switch (_ground.Surface(x, z))
            {
                case CourseSurface.Water:
                    EnterWater();
                    break;
                case CourseSurface.OffIsland:
                    FallOff();
                    break;
                default:
                    TryCapture();
                    break;
            }
        }

        private void Stop()
        {
            Velocity = Vector3.zero;
            State = BallState.Resting;
        }

        private bool TryCapture()
        {
            Vector2 hole = _ground.Hole;
            Vector2 toHole = new Vector2(Position.x - hole.x, Position.z - hole.y);
            float speed = new Vector2(Velocity.x, Velocity.z).magnitude;
            if (toHole.magnitude > _settings.HoleCaptureRadius || speed > _settings.HoleCaptureSpeed)
            {
                return false;
            }

            _holeBottom = new Vector3(hole.x, _ground.Height(hole.x, hole.y) - _settings.HoleDepth, hole.y);
            Velocity = Vector3.zero;
            State = BallState.Holed;
            return true;
        }

        private void StepDrop()
        {
            Position = Vector3.MoveTowards(Position, _holeBottom, _settings.HoleDropSpeed * _settings.FixedTimeStep);
        }

        private void EnterWater()
        {
            Velocity = new Vector3(Velocity.x, 0f, Velocity.z);
            _lostTimer = 0f;
            State = BallState.InWater;
        }

        private void StepSink()
        {
            float step = _settings.FixedTimeStep;
            Velocity *= Mathf.Exp(-_settings.WaterDrag * step);
            Position += (Velocity * step) + (Vector3.down * (_settings.SinkSpeed * step));
            TickLostTimer();
        }

        private void FallOff()
        {
            _arc = new BallisticArc(Position, Velocity, _settings.Gravity);
            _arcSteps = 0;
            _lostTimer = 0f;
            State = BallState.OffIsland;
        }

        private void StepFall()
        {
            float time = ++_arcSteps * _settings.FixedTimeStep;
            Position = _arc.PositionAt(time);
            Velocity = _arc.VelocityAt(time);
            TickLostTimer();
        }

        private void TickLostTimer()
        {
            _lostTimer += _settings.FixedTimeStep;
            if (_lostTimer < _settings.LostSeconds)
            {
                return;
            }

            // Both, so drawing doesn't sweep the ball across the course between the two spots.
            Position = _shotOrigin;
            _previousPosition = _shotOrigin;
            Stop();
        }
    }
}
