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

    /// <summary>
    /// Moves the camera along the ground for the shot in play, at the player's zoom. It stays still until the ball
    /// reaches a band across the middle of the screen, then glides to keep it there. When the ball comes to rest,
    /// it eases until the ball sits low on screen with the course ahead.
    ///
    /// A finger during a shot hands the camera to the player until the ball has stopped and the finger has lifted.
    /// Outside a shot, the camera is always the player's: nothing here runs, so a pan or an aim is never disturbed.
    /// </summary>
    public sealed class CameraFollow
    {
        // Below these the ease has nothing left to show, and ending it hands the camera back to the player.
        private const float ArrivalDistance = 0.01f;
        private const float ArrivalSpeed = 0.05f;

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
        private readonly CameraView _view;
        private readonly Rect _zone;
        private readonly float _backstop;
        private readonly Vector2 _rest;
        private readonly float _followSmoothTime;
        private readonly float _returnSmoothTime;
        private Phase _phase = Phase.Idle;
        private Vector3 _velocity;
        private bool _reachedBand;
        private bool _reachedSides;
        private readonly float _defaultZoom;
        private readonly float _resetSeconds;
        private Vector3 _resetFrom;
        private Vector3 _resetTo;
        private float _resetProgress;

        /// <param name="navigator">Moves the camera, and applies its limits.</param>
        /// <param name="view">The camera's fixed rotation and lens.</param>
        /// <param name="zone">Where the ball may be without moving the camera, in viewport coordinates from -1 to 1.</param>
        /// <param name="backstop">The lowest the ball may go on screen before it has reached the zone.</param>
        /// <param name="rest">Where the ball ends up on screen once it has stopped, in the same coordinates.</param>
        /// <param name="followSmoothTime">How closely the camera trails a moving ball, in seconds. Lower is closer.</param>
        /// <param name="returnSmoothTime">How slowly the camera eases to a stopped ball, in seconds.</param>
        /// <param name="defaultZoom">The zoom a reset view eases to.</param>
        /// <param name="resetSeconds">How long a reset view takes.</param>
        public CameraFollow(CameraNavigator navigator, in CameraView view, Rect zone, float backstop, Vector2 rest, float followSmoothTime, float returnSmoothTime, float defaultZoom = 0.5f, float resetSeconds = 0.35f)
        {
            _defaultZoom = defaultZoom;
            _resetSeconds = resetSeconds;
            _navigator = navigator;
            _view = view;
            _zone = zone;
            _backstop = backstop;
            _rest = rest;
            _followSmoothTime = followSmoothTime;
            _returnSmoothTime = returnSmoothTime;
        }

        /// <summary>True while easing to a stopped ball. The player's glide must not fight this.</summary>
        public bool IsReturning => _phase == Phase.Returning;

        /// <summary>True while the camera moves itself, to a stopped ball or to the default view.</summary>
        public bool IsEasing => _phase == Phase.Returning || _phase == Phase.Resetting;

        /// <summary>
        /// Eases to the default view: the default zoom, with the ball at its rest spot. The target is worked out once,
        /// here, so it holds still while the camera gets there. A finger or a shot ends it where it is.
        /// </summary>
        public void BeginReset(Vector3 ball)
        {
            EnterPhase(Phase.Resetting);
            _resetFrom = _navigator.Position;
            _resetTo = _navigator.PositionWith(_defaultZoom, ball, _rest);
            _resetProgress = 0f;
        }

        /// <summary>Call once a frame, with the ball's position free of the course's floating.</summary>
        public void Update(Vector3 ball, FollowedBallState state, bool fingerDown, float deltaTime)
        {
            UpdatePhase(state, fingerDown);
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
                _navigator.Move(Vector3.Lerp(_resetFrom, _resetTo, eased) - _navigator.Position);
                if (_resetProgress >= 1f)
                {
                    EnterPhase(Phase.Idle);
                }
            }
            else if (_phase == Phase.Returning)
            {
                float left = EaseBy(BringTo(ball, _rest), _returnSmoothTime, deltaTime);
                if (left < ArrivalDistance && _velocity.magnitude < ArrivalSpeed)
                {
                    EnterPhase(Phase.Idle);
                }
            }
        }

        /// <summary>Puts the ball at its rest spot at once, for a new level, and ends anything in progress.</summary>
        public void PlaceOn(Vector3 ball)
        {
            EnterPhase(Phase.Idle);
            _navigator.Move(BringTo(ball, _rest));
        }

        private void UpdatePhase(FollowedBallState state, bool fingerDown)
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
                    _phase = Phase.Following;
                    _reachedBand = false;
                    _reachedSides = false;
                }

                if (_phase == Phase.Following && fingerDown)
                {
                    EnterPhase(Phase.Paused);
                }

                return;
            }

            if (_phase == Phase.Following || _phase == Phase.Paused)
            {
                EnterPhase(fingerDown ? Phase.Paused : Phase.Returning);
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
            Vector2 now = _view.ToViewport(_navigator.Position, ball);

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
            return ball - _view.OnPlane(_navigator.Position, viewport, ball.y);
        }
    }
}
