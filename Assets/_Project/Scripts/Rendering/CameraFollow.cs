using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>What the followed ball is doing, as far as the camera cares.</summary>
    public enum FollowedBallState
    {
        /// <summary>Still, and ready for the next shot.</summary>
        AtRest,

        /// <summary>Flying or rolling under its own speed.</summary>
        Moving,

        /// <summary>The shot isn't over but there's nothing to chase: lost, or in the hole.</summary>
        Holding,
    }

    /// <summary>How the camera turns itself at rest. Every value is in <see cref="CameraRigSettings"/>.</summary>
    public readonly struct HeadingEase
    {
        /// <param name="smoothTime">How slowly the camera turns to its heading while easing onto a stopped ball, in seconds.</param>
        /// <param name="minTurn">A new heading closer than this to the current one, in degrees, is ignored, so a straight course never turns by a hair.</param>
        /// <param name="maxTwist">The furthest a player twist may turn the view from the automatic heading, in degrees.</param>
        public HeadingEase(float smoothTime, float minTurn, float maxTwist)
        {
            SmoothTime = smoothTime;
            MinTurn = minTurn;
            MaxTwist = maxTwist;
        }

        public float SmoothTime { get; }
        public float MinTurn { get; }
        public float MaxTwist { get; }
    }

    /// <summary>
    /// Moves the camera along the ground for the shot in play, at the player's zoom. It stays still until the ball
    /// reaches a band across the middle of the screen, then glides to keep it there. When the ball comes to rest,
    /// it eases until the ball sits low on screen with the course ahead, turns to the automatic heading, and zooms to the approach zoom (closer near the hole).
    ///
    /// A finger during a shot hands the camera to the player until the ball has stopped and the finger has lifted.
    /// Outside a shot, the camera is always the player's: nothing here runs, so a pan or an aim is never disturbed.
    /// During flight the camera only translates: the heading changes only at rest and on the reset view.
    /// </summary>
    public sealed class CameraFollow
    {
        // Below these the ease has nothing left to show, and ending it hands the camera back to the player.
        private const float ArrivalDistance = 0.01f;
        private const float ArrivalSpeed = 0.05f;
        private const float ArrivalTurn = 0.05f;
        private const float ArrivalZoom = 0.002f;

        // How far to either side a ball that hasn't reached the side band may go before the camera moves.
        private const float SideBackstop = 0.85f;

        private enum Phase
        {
            Idle,
            Following,
            Paused,
            Returning,
            Resetting,
        }

        private readonly CameraNavigator _navigator;
        private readonly Rect _zone;
        private readonly float _backstop;
        private readonly Vector2 _rest;
        private readonly float _followSmoothTime;
        private readonly float _returnSmoothTime;
        private readonly float _defaultZoom;
        private readonly float _resetSeconds;
        private readonly HeadingEase _ease;
        private Phase _phase = Phase.Idle;
        private Vector3 _velocity;
        private float _turnSpeed;
        private float _zoomSpeed;
        private float? _restZoom;
        private bool _reachedBand;
        private bool _reachedSides;
        private Vector3 _resetPivot;
        private Vector3 _resetFrom;
        private Vector3 _resetTo;
        private float _resetTurn;
        private float _resetProgress;

        /// <param name="navigator">Moves and turns the camera, and applies its limits.</param>
        /// <param name="zone">Where the ball may be without moving the camera, in viewport coordinates from -1 to 1.</param>
        /// <param name="backstop">The lowest the ball may go on screen before it has reached the zone.</param>
        /// <param name="rest">Where the ball ends up on screen once it has stopped, in the same coordinates.</param>
        /// <param name="followSmoothTime">How closely the camera trails a moving ball, in seconds. Lower is closer.</param>
        /// <param name="returnSmoothTime">How slowly the camera eases to a stopped ball, in seconds.</param>
        /// <param name="defaultZoom">The zoom a reset view eases to.</param>
        /// <param name="resetSeconds">How long a reset view takes.</param>
        public CameraFollow(CameraNavigator navigator, Rect zone, float backstop, Vector2 rest, float followSmoothTime, float returnSmoothTime, float defaultZoom = 0.5f, float resetSeconds = 0.35f, HeadingEase ease = default)
        {
            _defaultZoom = defaultZoom;
            _resetSeconds = resetSeconds;
            _navigator = navigator;
            _zone = zone;
            _backstop = backstop;
            _rest = rest;
            _followSmoothTime = followSmoothTime;
            _returnSmoothTime = returnSmoothTime;
            _ease = ease;
            Heading = navigator.Yaw;
        }

        /// <summary>True while easing to a stopped ball. The player's glide must not fight this.</summary>
        public bool IsReturning => _phase == Phase.Returning;

        /// <summary>True while the camera moves itself, to a stopped ball or to the default view.</summary>
        public bool IsEasing => _phase == Phase.Returning || _phase == Phase.Resetting;

        /// <summary>
        /// Picks the automatic heading for a ball at rest. Null keeps the camera at its yaw for good, as before
        /// headings existed.
        /// </summary>
        public CameraHeading Rule { get; set; }

        /// <summary>
        /// Picks the zoom for a ball at rest, closer near the hole. Null keeps the player's zoom at rest and the
        /// default zoom on the reset view, as before approach framing existed.
        /// </summary>
        public ApproachFraming Approach { get; set; }

        /// <summary>The automatic heading in effect, as a yaw in degrees. The view's yaw is this plus <see cref="Twist"/>.</summary>
        public float Heading { get; private set; }

        /// <summary>
        /// The player's turn away from the automatic heading, in degrees, within ±<see cref="HeadingEase.MaxTwist"/>.
        /// The next shot and the reset view clear it.
        /// </summary>
        public float Twist { get; private set; }

        /// <summary>
        /// The hook for a twist gesture: turns the view to the automatic heading plus this offset, around a ground
        /// point that stays where it is on screen. Ends any ease in progress, since the player took the camera.
        /// </summary>
        public void SetTwist(float degrees, Vector3 pivot)
        {
            // Only an ease of the camera's own is ended. A twist during a shot leaves the follow alone, or the
            // follow would take the next frame for a new shot and clear the twist.
            if (_phase == Phase.Returning || _phase == Phase.Resetting)
            {
                EnterPhase(Phase.Idle);
            }

            Twist = Mathf.Clamp(degrees, -_ease.MaxTwist, _ease.MaxTwist);
            _navigator.TurnTo(Heading + Twist, pivot);
        }

        /// <summary>
        /// Turns the view by a number of degrees from where it is now, around a ground point that stays put on screen,
        /// within ±<see cref="HeadingEase.MaxTwist"/> of the automatic heading. Counted from the view's real yaw, which
        /// can differ from the heading plus the twist (a new shot clears the twist before the view turns back).
        /// </summary>
        public void TwistBy(float degrees, Vector3 pivot)
        {
            SetTwist(Mathf.DeltaAngle(Heading, _navigator.Yaw) + degrees, pivot);
        }

        /// <summary>
        /// Eases to the default view: the default zoom (the approach zoom near the hole) and the automatic heading, with the ball at its rest spot, and
        /// clears the twist. The target is worked out once, here, so it holds still while the camera gets there. A
        /// finger or a shot ends it where it is.
        /// </summary>
        public void BeginReset(Vector3 ball)
        {
            EnterPhase(Phase.Resetting);
            Twist = 0f;
            ChooseHeading(ball);
            _resetPivot = ball;
            _resetTurn = Mathf.DeltaAngle(_navigator.Yaw, Heading);
            _resetTo = _navigator.PositionWith(RestZoom(ball) ?? _defaultZoom, ball, _rest, Heading);

            // Eased in the frame of the final heading, then turned back by what's left of the turn: with no turn this
            // is the straight ease it always was, and with one the ball travels across the screen as it would without.
            _resetFrom = CameraNavigator.Turned(_navigator.Position, _navigator.Yaw, Heading, ball);
            _resetProgress = 0f;
        }

        /// <summary>Call once a frame, with the ball's position free of the course's floating.</summary>
        public void Update(Vector3 ball, FollowedBallState state, bool fingerDown, float deltaTime)
        {
            UpdatePhase(ball, state, fingerDown);
            if (deltaTime <= 0f)
            {
                return;
            }

            if (_phase == Phase.Following)
            {
                // A held ball gets no target, so a camera still moving settles rather than stopping dead.
                Vector3 wanted = state == FollowedBallState.Moving ? IntoZone(ball) : Vector3.zero;
                EaseBy(wanted, _followSmoothTime, deltaTime);
            }
            else if (_phase == Phase.Resetting)
            {
                _resetProgress = Mathf.Min(1f, _resetProgress + (deltaTime / Mathf.Max(_resetSeconds, 0.0001f)));
                float eased = Mathf.SmoothStep(0f, 1f, _resetProgress);
                float yaw = Heading - ((1f - eased) * _resetTurn);
                Vector3 straight = Vector3.Lerp(_resetFrom, _resetTo, eased);
                _navigator.SetPose(CameraNavigator.Turned(straight, Heading, yaw, _resetPivot), yaw);
                if (_resetProgress >= 1f)
                {
                    EnterPhase(Phase.Idle);
                }
            }
            else if (_phase == Phase.Returning)
            {
                // Turns and zooms around the ball first, which keeps it where it is on screen, then eases it to its spot.
                float turnLeft = Turn(ball, deltaTime);
                float zoomLeft = ZoomStep(ball, deltaTime);
                float left = EaseBy(BringTo(ball, _rest), _returnSmoothTime, deltaTime);
                if (left < ArrivalDistance && _velocity.magnitude < ArrivalSpeed && turnLeft < ArrivalTurn && zoomLeft < ArrivalZoom)
                {
                    EnterPhase(Phase.Idle);
                }
            }
        }

        /// <summary>
        /// Puts the ball at its rest spot at once, for a new level, at the automatic heading, and ends anything in
        /// progress.
        /// </summary>
        public void PlaceOn(Vector3 ball)
        {
            EnterPhase(Phase.Idle);
            Twist = 0f;
            ChooseHeading(ball);
            float? zoom = RestZoom(ball);
            if (zoom.HasValue && Mathf.Abs(zoom.Value - _navigator.Zoom) > 1e-4f)
            {
                _navigator.ZoomTo(zoom.Value);
            }

            _navigator.TurnTo(Heading, ball);
            _navigator.Move(BringTo(ball, _rest));
        }

        /// <summary>The approach zoom for a ball at rest here, at the heading in effect, or null without approach framing.</summary>
        private float? RestZoom(Vector3 ball)
        {
            // Read at the heading, not the twist: the twist never changes the zoom.
            return Approach?.ZoomFor(_navigator, ball, _rest, Heading);
        }

        /// <summary>One frame of the zoom to the approach zoom, around the ball. Returns how much zoom is left.</summary>
        private float ZoomStep(Vector3 ball, float deltaTime)
        {
            if (!_restZoom.HasValue)
            {
                return 0f;
            }

            float target = _restZoom.Value;
            float zoom = _navigator.Zoom;
            if (Mathf.Abs(zoom - target) < 1e-5f)
            {
                _zoomSpeed = 0f;
                return 0f;
            }

            float next = Mathf.SmoothDamp(zoom, target, ref _zoomSpeed, Mathf.Max(_returnSmoothTime, 0.0001f), Mathf.Infinity, deltaTime);
            _navigator.ZoomAbout(ball, next);
            return Mathf.Abs(next - target);
        }

        /// <summary>
        /// Takes the rule's heading for a ball at rest here, unless it's within the dead band of the one in effect:
        /// a straight course then keeps its heading exactly, shot after shot.
        /// </summary>
        private void ChooseHeading(Vector3 ball)
        {
            if (Rule == null)
            {
                return;
            }

            float wanted = Rule.YawFor(new Vector2(ball.x, ball.z));
            if (Mathf.Abs(Mathf.DeltaAngle(Heading, wanted)) >= _ease.MinTurn)
            {
                Heading = Mathf.Repeat(wanted, 360f);
            }
        }

        /// <summary>One frame of the turn to the heading, around the ball. Returns how many degrees are left.</summary>
        private float Turn(Vector3 ball, float deltaTime)
        {
            float target = Heading + Twist;
            float yaw = _navigator.Yaw;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, target)) < 1e-4f)
            {
                _turnSpeed = 0f;
                return 0f;
            }

            float next = Mathf.SmoothDampAngle(yaw, target, ref _turnSpeed, Mathf.Max(_ease.SmoothTime, 0.0001f), Mathf.Infinity, deltaTime);
            _navigator.TurnTo(next, ball);
            return Mathf.Abs(Mathf.DeltaAngle(next, target));
        }

        private void UpdatePhase(Vector3 ball, FollowedBallState state, bool fingerDown)
        {
            if (_phase == Phase.Resetting)
            {
                // A shot or a finger takes the camera back at once, wherever the reset had got to.
                if (state == FollowedBallState.AtRest && !fingerDown)
                {
                    return;
                }

                EnterPhase(Phase.Idle);
                if (state == FollowedBallState.AtRest)
                {
                    return;
                }
            }

            if (state != FollowedBallState.AtRest)
            {
                if (_phase == Phase.Idle || _phase == Phase.Returning)
                {
                    // A new shot: the twist lasts until here, and the camera won't turn again before the ball stops.
                    _phase = Phase.Following;
                    _reachedBand = false;
                    _reachedSides = false;
                    Twist = 0f;
                }

                if (_phase == Phase.Following && fingerDown)
                {
                    EnterPhase(Phase.Paused);
                }

                return;
            }

            if (_phase == Phase.Following || _phase == Phase.Paused)
            {
                if (fingerDown)
                {
                    EnterPhase(Phase.Paused);
                }
                else
                {
                    // The heading is picked once, as the ball stops, so the camera has one target to ease to.
                    EnterPhase(Phase.Returning);
                    ChooseHeading(ball);
                    _restZoom = RestZoom(ball);
                }
            }
            else if (_phase == Phase.Returning && fingerDown)
            {
                // The player took over, perhaps to aim, and an aim must not start on a moving camera.
                EnterPhase(Phase.Idle);
            }
        }

        private void EnterPhase(Phase phase)
        {
            // The return carries on from the follow's speed, so the hand-over shows no step.
            if (phase != Phase.Returning)
            {
                _velocity = Vector3.zero;
            }

            _turnSpeed = 0f;
            _zoomSpeed = 0f;
            if (phase != Phase.Returning)
            {
                _restZoom = null;
            }

            _phase = phase;
        }

        /// <summary>Returns how far is left to go, once the camera's limits are applied.</summary>
        private float EaseBy(Vector3 wanted, float smoothTime, float deltaTime)
        {
            // Aimed at what the limits allow, so a limit stops the camera softly instead of cutting it short.
            Vector3 reachable = _navigator.Reachable(wanted);
            Vector3 position = _navigator.Position;
            Vector3 next = Vector3.SmoothDamp(position, position + reachable, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
            _navigator.Move(next - position);
            return reachable.magnitude;
        }

        private Vector3 IntoZone(Vector3 ball)
        {
            Vector2 now = _navigator.View.ToViewport(_navigator.Position, ball);

            // A shot starts below the band, since the ball rests low on screen. Holding it to the band from the start
            // would pull the camera backward at launch, so the band's lower edge only counts once the ball has been in.
            _reachedBand |= now.y >= _zone.yMin;
            float lowest = _reachedBand ? _zone.yMin : _backstop;

            // Sideways too: a ball resting off-centre, after a pan, must not make the camera jump at launch. The side
            // edges only count once the ball has been between them, and until then a wide backstop applies.
            _reachedSides |= now.x >= _zone.xMin && now.x <= _zone.xMax;
            float left = _reachedSides ? _zone.xMin : Mathf.Min(_zone.xMin, -SideBackstop);
            float right = _reachedSides ? _zone.xMax : Mathf.Max(_zone.xMax, SideBackstop);
            var inZone = new Vector2(Mathf.Clamp(now.x, left, right), Mathf.Clamp(now.y, lowest, _zone.yMax));
            return inZone == now ? Vector3.zero : BringTo(ball, inZone);
        }

        // Exact at the ball's own height: moving the camera moves the point under a viewport spot by the same amount.
        private Vector3 BringTo(Vector3 ball, Vector2 viewport)
        {
            return ball - _navigator.View.OnPlane(_navigator.Position, viewport, ball.y);
        }
    }
}
