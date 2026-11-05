using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>
    /// The ball on screen, driven by <see cref="BallSimulation"/>. It lives under the course container and only
    /// ever sets its local position, so the course's floating moves the ball without entering the simulation.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BallView : MonoBehaviour
    {
        [SerializeField] private ShotSettings shot;
        [SerializeField] private PlayPieceSettings settings;
        [SerializeField] private PaletteSettings palette;
        [SerializeField] private BallShadowView shadow;

        private BallGround _ground;
        private Mesh _mesh;
        private Vector3 _lastPosition;

        public BallSimulation Simulation { get; private set; }

        /// <summary>The ground the ball plays on, or null until <see cref="Build"/>.</summary>
        public BallGround Ground => _ground;

        /// <summary>How far the drawn ball's center sits above its contact point.</summary>
        public float Radius => settings.BallRadius;

        public void Build(BallGround ground)
        {
            _ground = ground;
            _mesh = PlayPieceBuilder.BuildBall(settings, palette).ToMesh("Ball");
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            // The shadow says where the ball is. A cast shadow would add a second one, off to the side.
            GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            shadow.Build(ground);
            ReturnToTee();
        }

        /// <summary>A fresh ball on the tee.</summary>
        public void ReturnToTee()
        {
            Simulation = new BallSimulation(_ground, shot, _ground.Tee);
            _lastPosition = Simulation.Position;
            Draw();
        }

        private void Update()
        {
            if (Simulation == null)
            {
                return;
            }

            Simulation.Advance(Time.deltaTime);
            Draw();
        }

        private void Draw()
        {
            Vector3 position = Simulation.InterpolatedPosition;
            BallState state = Simulation.State;

            // Only while moving under its own speed, so a lost ball's jump back doesn't spin it.
            if (state == BallState.Flying || state == BallState.Rolling)
            {
                transform.localRotation = BallSpin.Roll(transform.localRotation, position - _lastPosition, settings.BallRadius);
            }

            _lastPosition = position;
            transform.localPosition = position + (Vector3.up * settings.BallRadius);

            bool shadowVisible = state != BallState.InWater && state != BallState.Holed && _ground.IsOnIsland(position.x, position.z);
            shadow.Show(shadowVisible, position);
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }
    }
}
