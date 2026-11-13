using UnityEngine;

namespace IsoGolf.Generation
{
    public enum BirdState
    {
        Perched,
        TakeOff,
        Away,
        Return,
    }

    /// <summary>Where a bird is and how it holds its wings, in the course's local space.</summary>
    public readonly struct BirdPose
    {
        public BirdPose(Vector3 position, float yawDegrees, float pitchDegrees, float flap, float spread, float scale)
        {
            Position = position;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
            Flap = flap;
            Spread = spread;
            Scale = scale;
        }

        public Vector3 Position { get; }

        /// <summary>Degrees from +X toward +Z, like the kit's props.</summary>
        public float YawDegrees { get; }

        /// <summary>Nose up is positive.</summary>
        public float PitchDegrees { get; }

        /// <summary>Wing angle in radians about the body axis, positive up.</summary>
        public float Flap { get; }

        /// <summary>0 wings folded on the back, 1 fully spread.</summary>
        public float Spread { get; }

        /// <summary>1 at full size, 0 when it has shrunk to nothing at the far end of its path.</summary>
        public float Scale { get; }

        public bool Visible => Scale > 0.001f;
    }

    /// <summary>
    /// One bird's behaviour (M4 PR 4.6): perched, then, when a ball flying or rolling comes within the trigger radius, a take-off along a
    /// fixed path, a time away, and a return to the same perch. Plain C#, CPU only, no physics: the pose is a pure function of the time
    /// spent in the state and of the bird's seeds, so it is testable and the same on every device. Visual only: nothing reads it.
    ///
    /// The path is a straight line on the ground at the bird's seeded heading (turned round if the ball stands on that side), climbing
    /// along an ease-out curve. The bird shrinks to nothing over the last part of the path, so it is gone and never pops in or out near the
    /// course; its return is the same path run backwards, growing back and slowing as it lands.
    /// </summary>
    public sealed class BirdFlight
    {
        // Wing beat amplitude, in radians, and how much slower the return runs than the take-off.
        private const float FlapAmplitude = 0.9f;
        private const float ReturnSlower = 1.2f;

        private readonly BirdPerch _perch;
        private readonly BirdSettings _settings;
        private readonly float _length;
        private readonly float _flightTime;
        private Vector2 _direction;
        private float _clock;
        private float _time;

        public BirdFlight(BirdPerch perch, BirdSettings settings)
        {
            _perch = perch;
            _settings = settings;
            _length = Mathf.Max(1f, settings.PathLength);
            _flightTime = _length / Mathf.Max(0.1f, settings.FlightSpeed);
            _clock = perch.Phase / 256f / Mathf.Max(0.1f, settings.FlapRate);
            Pose = Perched();
        }

        public BirdState State { get; private set; }

        public BirdPose Pose { get; private set; }

        public BirdPerch Perch => _perch;

        /// <summary>Seconds spent in the current state (during a take-off, the wait before leaving counts).</summary>
        public float TimeInState => _time;

        /// <summary>
        /// Advances by <paramref name="dt"/> seconds. <paramref name="ball"/> is the ball's course-local position and
        /// <paramref name="moving"/> whether it is flying or rolling.
        /// </summary>
        public void Update(float dt, Vector3 ball, bool moving)
        {
            _clock += dt;
            _time += dt;

            switch (State)
            {
                case BirdState.Perched:
                    if (moving && Flat(ball - _perch.Position).magnitude <= _settings.TriggerRadius)
                    {
                        Vector2 heading = Direction(_perch.PathDegrees);
                        if (Vector2.Dot(Flat(ball - _perch.Position), heading) > 0f)
                        {
                            heading = -heading;
                        }

                        _direction = heading;
                        Enter(BirdState.TakeOff);
                    }

                    Pose = Perched();
                    break;

                case BirdState.TakeOff:
                    if (_time - _perch.TakeOffDelay >= _flightTime)
                    {
                        Enter(BirdState.Away);
                        Pose = Hidden();
                    }
                    else
                    {
                        Pose = Flying(_time - _perch.TakeOffDelay, false);
                    }

                    break;

                case BirdState.Away:
                    if (_time >= _perch.ReturnDelay && Flat(ball - _perch.Position).magnitude > _settings.TriggerRadius * 1.5f)
                    {
                        Enter(BirdState.Return);
                        Pose = Flying(0f, true);
                    }
                    else
                    {
                        Pose = Hidden();
                    }

                    break;

                case BirdState.Return:
                    if (_time >= _flightTime * ReturnSlower)
                    {
                        Enter(BirdState.Perched);
                        Pose = Perched();
                    }
                    else
                    {
                        Pose = Flying(_time, true);
                    }

                    break;
            }
        }

        private void Enter(BirdState state)
        {
            State = state;
            _time = 0f;
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        private static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        private BirdPose Perched() => new BirdPose(_perch.Position, _perch.YawDegrees, 0f, 0f, 0f, 1f);

        private BirdPose Hidden() => new BirdPose(PathPoint(_length), Yaw(false), 0f, 0f, 1f, 0f);

        private float Yaw(bool back)
        {
            Vector2 d = back ? -_direction : _direction;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        /// <summary>The point at <paramref name="distance"/> metres along the path: the ground line, and a height that rises fast, then levels.</summary>
        private Vector3 PathPoint(float distance)
        {
            float s = Mathf.Clamp01(distance / _length);
            float rise = _settings.Climb * (1f - ((1f - s) * (1f - s)));
            Vector2 flat = Flat(_perch.Position) + (_direction * distance);
            return new Vector3(flat.x, _perch.Position.y + rise, flat.y);
        }

        private float ScaleAt(float distance)
        {
            float s = Mathf.Clamp01(distance / _length);
            float fadeStart = 1f - _settings.FadeFraction;
            if (s <= fadeStart)
            {
                return 1f;
            }

            float k = Mathf.Clamp01((1f - s) / _settings.FadeFraction);
            return k * k * (3f - (2f * k));
        }

        /// <summary>The pose <paramref name="t"/> seconds into a take-off (before 0, still waiting on the perch) or a return.</summary>
        private BirdPose Flying(float t, bool returning)
        {
            if (!returning && t < 0f)
            {
                return Perched();
            }

            float distance;
            float pitch;
            if (returning)
            {
                // The path backwards, quick far away and slowing to land: d = L (1 - p)^2.
                float p = Mathf.Clamp01(t / (_flightTime * ReturnSlower));
                distance = _length * (1f - p) * (1f - p);
                float slope = Slope(distance);
                pitch = -Mathf.Atan(slope) * Mathf.Rad2Deg;
            }
            else
            {
                distance = Mathf.Min(_length, _settings.FlightSpeed * t);
                pitch = Mathf.Atan(Slope(distance)) * Mathf.Rad2Deg;
            }

            float beat = Mathf.Sin(_clock * _settings.FlapRate * Mathf.PI * 2f + (_perch.Phase / 256f * Mathf.PI * 2f));
            float flap = FlapAmplitude * beat;
            return new BirdPose(PathPoint(distance), Yaw(returning), pitch, flap, 1f, ScaleAt(distance));
        }

        private float Slope(float distance)
        {
            float s = Mathf.Clamp01(distance / _length);
            return _settings.Climb * 2f * (1f - s) / _length;
        }
    }
}
