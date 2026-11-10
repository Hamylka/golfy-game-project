using System.Collections.Generic;
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

    /// <summary>Where and when an arc first strikes an obstacle, before it meets the ground.</summary>
    public readonly struct ArcHit
    {
        public ArcHit(float time, Vector3 point, Vector3 normal, int index, PropMaterial material, bool isPole, bool fallThrough, float ballRadius)
        {
            FallThrough = fallThrough;
            Time = time;
            Point = point;
            Normal = normal;
            Index = index;
            Material = material;
            IsPole = isPole;
            Contact = point + (Vector3.up * ballRadius) - (normal * ballRadius);
        }

        public float Time { get; }

        /// <summary>The ball's lowest point at the hit, like <see cref="BallSimulation.Position"/>.</summary>
        public Vector3 Point { get; }

        /// <summary>Out of the obstacle, unit length.</summary>
        public Vector3 Normal { get; }

        public int Index { get; }
        public PropMaterial Material { get; }
        public bool IsPole { get; }

        /// <summary>A raised crown: the ball goes on into the leaves and falls out underneath.</summary>
        public bool FallThrough { get; }

        /// <summary>Where the ball touches the obstacle, on the obstacle's surface: where the preview's mark goes.</summary>
        public Vector3 Contact { get; }
    }

    /// <summary>
    /// The ball from one shot to the next, in the course's local space, at a fixed time step and with no Unity
    /// physics. The same shot from the same spot always plays out the same way, so a copy can predict a path.
    /// <see cref="Position"/> is the ball's lowest point, where it touches the ground: the view adds the radius.
    ///
    /// A shot from the green rolls. Any other shot follows a <see cref="BallisticArc"/>, then hops in shorter
    /// arcs until it rolls. Each landing and each rolling step asks the ground which surface it's on, and
    /// that decides water and the island's edge. A lost ball returns to where the shot was played.
    /// A ball that crosses the hole's rim drops in or lips out, as <see cref="HoleRim"/> decides.
    ///
    /// With <see cref="BallObstacles"/>, each arc also looks up front for the first obstacle it strikes
    /// before it lands (<see cref="FindHit"/>), and ends there instead: the ball bounces off with the
    /// obstacle's response and flies a new arc. A rolling ball is stopped or turned by the same shapes, the
    /// flag pole excepted. Without obstacles, nothing here runs and every shot plays exactly as before.
    /// </summary>
    public sealed class BallSimulation
    {
        // Only a guard against a prediction that never ends: friction stops any real putt long before this.
        private const float LongestPredictedRollSeconds = 60f;

        private readonly IBallGround _ground;
        private readonly ShotSettings _settings;
        private readonly BallObstacles _obstacles;

        // How far a rolling ball is set back off an obstacle it strikes, so the next step starts outside it.
        private const float RollingStandOff = 0.001f;

        private BallSimulation _predictor;

        private ArcHit _hit;
        private bool _hasHit;
        private int _ignoredObstacle = BallObstacles.None;
        private bool _flightObstaclesOff;

        private BallisticArc _arc;
        private ArcLanding _landing;
        private int _arcSteps;
        private float _accumulator;
        private float _lostTimer;
        private Vector3 _previousPosition;
        private Vector3 _shotOrigin;
        private Vector3 _holeBottom;
        private bool _insideRim;
        private float _lipTurnLeft;

        /// <param name="obstacles">What the ball can strike, or null for a course with nothing standing on it.</param>
        public BallSimulation(IBallGround ground, ShotSettings settings, Vector2 start, BallObstacles obstacles = null)
        {
            _ground = ground;
            _settings = settings;
            _obstacles = obstacles != null && obstacles.Count > 0 ? obstacles : null;
            Position = new Vector3(start.x, ground.Height(start.x, start.y), start.y);
            _previousPosition = Position;
            _shotOrigin = Position;
        }

        public BallState State { get; private set; } = BallState.Resting;
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }

        /// <summary>How many hops the current shot has made after its first landing.</summary>
        public int Bounces { get; private set; }

        /// <summary>How many obstacles the current shot has struck in flight.</summary>
        public int Hits { get; private set; }

        /// <summary>The current shot's latest hit in flight; meaningful once <see cref="Hits"/> is above 0.</summary>
        public ArcHit LastHit { get; private set; }

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

        /// <summary>
        /// Where the arc from <see cref="ArcFor"/> first meets the ground, by the same search a played shot uses.
        /// </summary>
        public ArcLanding PredictLanding(BallisticArc arc) => arc.FindLanding(_ground, _settings.LandingSearchStep);

        /// <summary>
        /// Whether the shot's first arc, from <see cref="ArcFor"/>, strikes an obstacle before its landing, and
        /// where: by the same search <see cref="Shoot"/> runs, so the preview stops exactly at the real hit.
        /// </summary>
        public bool PredictHit(BallisticArc arc, ArcLanding landing, out ArcHit hit)
        {
            return FindHit(arc, landing.Time, BallObstacles.None, out hit);
        }

        /// <summary>
        /// Fills <paramref name="path"/> with the ball's contact point at each step of the putt this shot would
        /// play, from where it rests until it stops, is holed or is lost. It plays the shot for real on a private
        /// copy, so it has every rule of a played shot, slopes and lip-outs included, and it can't disagree with
        /// one. The copy is reused, because this runs whenever the aim changes. Leaves only the start in the path
        /// if the shot wouldn't be a putt.
        /// </summary>
        public void PredictPutt(Vector2 direction, float power, List<Vector3> path)
        {
            path.Clear();
            path.Add(Position);
            if (!IsPutt)
            {
                return;
            }

            _predictor ??= new BallSimulation(_ground, _settings, new Vector2(Position.x, Position.z), _obstacles);
            _predictor.Position = Position;
            _predictor.Velocity = Vector3.zero;
            _predictor.State = BallState.Resting;
            if (!_predictor.Shoot(direction, power))
            {
                return;
            }

            int steps = Mathf.CeilToInt(LongestPredictedRollSeconds / _settings.FixedTimeStep);
            for (int i = 0; i < steps && _predictor.State == BallState.Rolling; i++)
            {
                _predictor.Step();
                path.Add(_predictor.Position);
            }
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
            Hits = 0;
            _ignoredObstacle = BallObstacles.None;
            _flightObstaclesOff = false;
            _insideRim = false;
            _lipTurnLeft = 0f;

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
            _landing = PredictLanding(arc);
            _hasHit = !_flightObstaclesOff && FindHit(arc, _landing.Time, _ignoredObstacle, out _hit);
            _arcSteps = 0;
            Velocity = arc.Velocity;
            State = BallState.Flying;
        }

        /// <summary>
        /// Sweeps the ball's center along the arc, up to its landing, for the first obstacle it enters. The arc
        /// is cut into straight chords at the landing search's step, and each chord is tested exactly against
        /// the shapes near it, so nothing between two steps is missed. A chord strays from the true arc by at
        /// most g·dt²/8 at its middle, 0.5 mm at 0.02 s, whatever the power, since only gravity bends the arc.
        /// The chords depend on the arc alone, so the shot and its preview cut the same ones.
        /// </summary>
        private bool FindHit(BallisticArc arc, float landingTime, int ignore, out ArcHit hit)
        {
            hit = default;
            if (_obstacles == null)
            {
                return false;
            }

            float step = _settings.LandingSearchStep;
            Vector3 lift = Vector3.up * _settings.BallRadius;
            Vector3 from = arc.PositionAt(0f) + lift;
            for (int i = 1; ; i++)
            {
                float start = (i - 1) * step;
                float end = Mathf.Min(i * step, landingTime);
                Vector3 to = arc.PositionAt(end) + lift;
                if (_obstacles.FirstHit(from, to, false, ignore, out ChordHit chord))
                {
                    float time = start + (chord.Fraction * (end - start));
                    hit = new ArcHit(time, arc.PositionAt(time), chord.Normal, chord.Index, chord.Material, chord.IsPole, chord.FallThrough, _settings.BallRadius);
                    return true;
                }

                if (end >= landingTime)
                {
                    return false;
                }

                from = to;
            }
        }

        private ObstacleResponse ResponseOf(bool isPole, PropMaterial material)
        {
            return isPole ? _settings.PoleResponse : _settings.ResponseOf(material);
        }

        private void StepFlight()
        {
            // The time comes from a count of steps, not a running sum, so rounding errors can't build up.
            float time = ++_arcSteps * _settings.FixedTimeStep;
            if (_hasHit && time >= _hit.Time)
            {
                Strike();
                return;
            }

            if (time >= _landing.Time)
            {
                Land();
                return;
            }

            Position = _arc.PositionAt(time);
            Velocity = _arc.VelocityAt(time);
        }

        /// <summary>
        /// The arc ends on an obstacle. The ball bounces off it like off the ground, with the obstacle's own
        /// response: hard wood and stone send it back, a soft bush takes most of its speed. A raised crown
        /// isn't bounced off: the ball goes on into the leaves, slowed, and falls out under the tree. The new
        /// arc leaves that obstacle out, so rounding at its surface can't strike it twice. The cap on hits ends any loop between obstacles: the
        /// last hit takes all the speed, and the ball drops, past every obstacle, to the ground below.
        /// </summary>
        private void Strike()
        {
            Position = _hit.Point;
            Vector3 velocity = _arc.VelocityAt(_hit.Time);
            Hits++;
            LastHit = _hit;
            _ignoredObstacle = _hit.Index;

            if (Hits >= _settings.MaxHits)
            {
                _flightObstaclesOff = true;
                velocity = Vector3.zero;
            }
            else if (_hit.FallThrough)
            {
                // Into the leaves rather than off them: slowed in its own direction, it falls through the crown.
                velocity *= _settings.ResponseOf(_hit.Material).AlongKeep;
            }
            else
            {
                velocity = ResponseOf(_hit.IsPole, _hit.Material).Apply(velocity, _hit.Normal);
            }

            FlyAlong(new BallisticArc(Position, velocity, _settings.Gravity));
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

            // A landing too fast to drop in just bounces: the ball meets the rim only once it rolls.
            if (IsOverHole() && MeetRim().Captured)
            {
                DropIntoHole();
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

            // Off the ground, so out of any obstacle: the next arc may strike the last one again, from below say.
            _ignoredObstacle = BallObstacles.None;
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
                    // A lip-out can use up its speed before leaving the hole, and nothing can rest on top of one.
                    if (IsOverHole())
                    {
                        DropIntoHole();
                    }
                    else
                    {
                        Stop();
                    }
                }

                // Otherwise it has only paused at the top of its travel, on ground too steep to hold it.
                return;
            }

            velocity *= (speed - friction) / speed;
            velocity = RideLip(velocity);

            float x = Position.x + (velocity.x * step);
            float z = Position.z + (velocity.y * step);
            if (_obstacles != null && StrikeWhileRolling(ref x, ref z, ref velocity))
            {
                return;
            }

            Velocity = new Vector3(velocity.x, 0f, velocity.y);
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
                    CrossRim();
                    break;
            }
        }

        /// <summary>
        /// Stops this step at the first obstacle the rolling ball meets, a millimetre off it, and bounces the
        /// speed off it along the ground. The rest of the step is dropped, which keeps a step a single move.
        /// Returns true when the ball comes to rest against the obstacle: pushed into it by a slope, it would
        /// otherwise strike it again on every step and never rest. The flag pole doesn't count here.
        /// </summary>
        private bool StrikeWhileRolling(ref float x, ref float z, ref Vector2 velocity)
        {
            Vector3 lift = Vector3.up * _settings.BallRadius;
            Vector3 from = Position + lift;
            Vector3 to = new Vector3(x, _ground.Height(x, z), z) + lift;
            if (!_obstacles.FirstHit(from, to, true, BallObstacles.None, out ChordHit chord))
            {
                return false;
            }

            Vector3 contact = Vector3.Lerp(from, to, chord.Fraction) + (chord.Normal * RollingStandOff);
            x = contact.x;
            z = contact.z;

            Vector3 bounced = ResponseOf(chord.IsPole, chord.Material).Apply(new Vector3(velocity.x, 0f, velocity.y), chord.Normal);
            velocity = new Vector2(bounced.x, bounced.z);
            if (velocity.magnitude >= _settings.ObstacleRestSpeed)
            {
                return false;
            }

            Position = new Vector3(x, _ground.Height(x, z), z);
            if (IsOverHole())
            {
                DropIntoHole();
            }
            else
            {
                Stop();
            }

            return true;
        }

        private void Stop()
        {
            Velocity = Vector3.zero;
            State = BallState.Resting;
        }

        private Vector2 FromHoleCenter => new Vector2(Position.x - _ground.Hole.x, Position.z - _ground.Hole.y);

        private bool IsOverHole() => FromHoleCenter.magnitude <= _settings.HoleCaptureRadius;

        private RimMeeting MeetRim() => HoleRim.Meet(FromHoleCenter, new Vector2(Velocity.x, Velocity.z), _settings);

        /// <summary>
        /// Decides once per pass over the hole. Without the latch, the speed a lip-out takes away would let the
        /// next step capture the same ball. A ball that leaves and rolls back meets the rim again.
        /// </summary>
        private void CrossRim()
        {
            if (!IsOverHole())
            {
                _insideRim = false;
                return;
            }

            if (_insideRim)
            {
                return;
            }

            RimMeeting meeting = MeetRim();
            if (meeting.Captured)
            {
                DropIntoHole();
                return;
            }

            _insideRim = true;
            _lipTurnLeft = meeting.TurnDegrees;
            Velocity *= meeting.SpeedKeep;
        }

        /// <summary>
        /// Spends a lip-out's turn over distance, at a fixed curve as tight as the hole, so the ball visibly
        /// rides round the rim at any speed and ends up with the same total turn.
        /// </summary>
        private Vector2 RideLip(Vector2 velocity)
        {
            if (_lipTurnLeft == 0f)
            {
                return velocity;
            }

            float most = velocity.magnitude * _settings.FixedTimeStep * _settings.LipOutMaxTurn / _settings.HoleCaptureRadius;
            float turn = Mathf.Clamp(_lipTurnLeft, -most, most);
            _lipTurnLeft -= turn;

            float radians = turn * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2((velocity.x * cos) - (velocity.y * sin), (velocity.x * sin) + (velocity.y * cos));
        }

        private void DropIntoHole()
        {
            Vector2 hole = _ground.Hole;
            _holeBottom = new Vector3(hole.x, _ground.Height(hole.x, hole.y) - _settings.HoleDepth, hole.y);
            Velocity = Vector3.zero;
            State = BallState.Holed;
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
