using System;
using UnityEngine;

namespace IsoGolf.Rendering
{
    /// <summary>
    /// Decides what the fingers on screen mean for the camera: one finger pans, two fingers pinch and pan,
    /// and a touch claimed by something else (the ball) leaves the camera alone until every finger lifts.
    /// </summary>
    public sealed class CameraGestureRouter
    {
        private enum State
        {
            Idle,
            Panning,
            Pinching,
            Ignoring,
        }

        private readonly CameraNavigator _navigator;
        private readonly PanMomentum _momentum;
        private State _state = State.Idle;
        private Vector2 _previousA;
        private Vector2 _previousB;

        public CameraGestureRouter(CameraNavigator navigator, PanMomentum momentum)
        {
            _navigator = navigator;
            _momentum = momentum;
        }

        /// <summary>
        /// Call once per frame. Positions are viewport coordinates from -1 to 1. <paramref name="claim"/>
        /// is asked once, when a first finger lands, whether something else owns that touch.
        /// </summary>
        public void Update(int fingerCount, Vector2 a, Vector2 b, bool enabled, Func<Vector2, bool> claim, float deltaTime)
        {
            if (fingerCount == 0)
            {
                if (_state == State.Panning)
                {
                    _momentum.Release();
                }

                _state = State.Idle;
                UpdateGlide(enabled, deltaTime);
                return;
            }

            // Any finger on the screen ends the glide, including one about to start a shot.
            _momentum.Stop();

            // Stays ignored until every finger lifts, so a shot can't turn into a camera move
            // when a second finger lands or when gestures come back on mid-touch.
            if (_state == State.Ignoring)
            {
                return;
            }

            if (!enabled)
            {
                _state = State.Ignoring;
                return;
            }

            if (fingerCount == 1)
            {
                UpdateOneFinger(a, claim, deltaTime);
            }
            else if (fingerCount == 2)
            {
                UpdateTwoFingers(a, b);
            }
            else
            {
                // A third finger is probably the hand resting. Start fresh from the next valid count.
                _state = State.Idle;
            }
        }

        /// <summary>Ends the glide, for when something else takes over the camera.</summary>
        public void StopGlide()
        {
            _momentum.Stop();
        }

        private void UpdateGlide(bool enabled, float deltaTime)
        {
            if (!enabled)
            {
                _momentum.Stop();
                return;
            }

            Vector3 requested = _momentum.Step(deltaTime);
            if (requested != Vector3.zero)
            {
                _momentum.ApplyLimits(requested, _navigator.Move(requested));
            }
        }

        private void UpdateOneFinger(Vector2 position, Func<Vector2, bool> claim, float deltaTime)
        {
            switch (_state)
            {
                case State.Idle:
                    _state = claim != null && claim(position) ? State.Ignoring : State.Panning;
                    _momentum.ClearSamples();
                    break;
                case State.Panning:
                    _momentum.AddSample(_navigator.Pan(_previousA, position), deltaTime);
                    break;
                case State.Pinching:
                    // One finger lifted: carry on panning from where the other one is, without a jump.
                    _state = State.Panning;
                    _momentum.ClearSamples();
                    break;
            }

            _previousA = position;
        }

        private void UpdateTwoFingers(Vector2 a, Vector2 b)
        {
            if (_state == State.Pinching)
            {
                _navigator.ApplyGesture(_previousA, _previousB, a, b);
            }

            // Coming from one finger, the first frame only records positions, so the view doesn't jump.
            _state = State.Pinching;
            _previousA = a;
            _previousB = b;
        }
    }
}
