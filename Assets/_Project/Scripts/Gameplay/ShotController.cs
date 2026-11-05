using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>Plays the shots an <see cref="IShotInput"/> releases, and starts over once the ball is holed.</summary>
    [RequireComponent(typeof(BallView))]
    public sealed class ShotController : MonoBehaviour
    {
        // Named here rather than in a settings asset, because M5's level flow replaces this return to the tee.
        private const float HoledSecondsBeforeTee = 2f;

        private BallView _ball;
        private IShotInput _input;
        private float _holedFor;

        private void Awake()
        {
            _ball = GetComponent<BallView>();
            _input = GetComponent<IShotInput>();
        }

        private void OnEnable()
        {
            _input.ShotReleased += Play;
        }

        private void OnDisable()
        {
            _input.ShotReleased -= Play;
        }

        private void Update()
        {
            BallSimulation ball = _ball.Simulation;
            if (ball == null || ball.State != BallState.Holed)
            {
                _holedFor = 0f;
                return;
            }

            _holedFor += Time.deltaTime;
            if (_holedFor >= HoledSecondsBeforeTee)
            {
                _ball.ReturnToTee();
            }
        }

        private void Play(Vector2 direction, float power)
        {
            _ball.Simulation.Shoot(direction, power);
        }
    }
}
