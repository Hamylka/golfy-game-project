using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.UI
{
    /// <summary>
    /// What the player sees while aiming: dots along the shot and a landing ring, or dots along the first part of
    /// a putt, and the power gauge. It reads the shot from <see cref="IShotInput"/> and its path from the ball's
    /// simulation, so it can't disagree with the shot that follows.
    ///
    /// This object sits at the origin of the course container, so the mesh's vertices are in the course's local
    /// space and the preview floats with the ball.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AimPreview : MonoBehaviour
    {
        // Above the other see-through pieces on the ground, so the hole or the ball's shadow can't draw over the preview.
        private const int SortingOrder = 1;

        [SerializeField] private BallView ball;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private AimPreviewSettings settings;
        [SerializeField] private PaletteSettings palette;

        private readonly AimMesh _geometry = new AimMesh();
        private readonly List<Vector3> _path = new List<Vector3>();
        private readonly List<Vector3> _dots = new List<Vector3>();

        private IShotInput _input;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private GroundDisc _ring;
        private Color32 _markColor;
        private Color32 _gaugeFillColor;
        private Color32 _gaugeBorderColor;

        private bool _hasAim;
        private Vector2 _aimedDirection;
        private float _aimedPower;
        private bool _ringVisible;

        private void Awake()
        {
            _input = ball.GetComponent<IShotInput>();
            _renderer = GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = SortingOrder;
            _renderer.enabled = false;

            _markColor = TerrainBuilder.ToVertexColor(palette.AimMark);
            _gaugeFillColor = TerrainBuilder.ToVertexColor(palette.GaugeFill);
            _gaugeBorderColor = TerrainBuilder.ToVertexColor(palette.GaugeBorder);

            _mesh = new Mesh { name = "Aim Preview" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        // After every Update, so it shows the aim as the input read it this frame, and the ball where it's drawn.
        private void LateUpdate()
        {
            BallSimulation simulation = ball.Simulation;
            if (!_input.IsAiming || simulation == null || simulation.State != BallState.Resting)
            {
                _renderer.enabled = false;
                _hasAim = false;
                return;
            }

            _ring ??= new GroundDisc(settings.RingRadius, settings.RingVertexRings, settings.RingSegments, ball.Ground.DrawnHeight, ball.Ground.IsOnIsland, settings.RingInnerRadius);

            Vector2 direction = _input.Direction;
            float power = _input.Power;

            // Predicting a long putt is by far the dearest part, and a still finger needs none of it again.
            if (!_hasAim || direction != _aimedDirection || power != _aimedPower)
            {
                Predict(simulation, direction, power);
                _hasAim = true;
                _aimedDirection = direction;
                _aimedPower = power;
            }

            Draw(power);
            _renderer.enabled = true;
        }

        private void Predict(BallSimulation simulation, Vector2 direction, float power)
        {
            if (simulation.IsPutt)
            {
                simulation.PredictPutt(direction, power, _path);
                DottedPath.AlongPath(_path, settings.PuttShare, settings.PuttDotSpacing, _dots);
                SeatDotsOnTheGround();
                _ringVisible = false;
                return;
            }

            BallisticArc arc = simulation.ArcFor(direction, power);
            ArcLanding landing = simulation.PredictLanding(arc);
            DottedPath.AlongArc(arc, landing, settings.ArcDotSpacing, ball.Radius, _dots);
            _ring.Place(landing.Point.x, landing.Point.z, settings.RingLift, _markColor);
            _ringVisible = true;
        }

        /// <summary>
        /// On the drawing rather than on the curve the ball rolls on, which can sit a little under a facet and would cut
        /// a dot in half. A dot's radius above the ground keeps its lower edge clear, however it faces the camera.
        /// </summary>
        private void SeatDotsOnTheGround()
        {
            for (int i = 0; i < _dots.Count; i++)
            {
                Vector3 dot = _dots[i];
                dot.y = ball.Ground.DrawnHeight(dot.x, dot.z) + settings.DotRadius;
                _dots[i] = dot;
            }
        }

        private void Draw(float power)
        {
            Transform view = sceneCamera.transform;
            Vector3 right = transform.InverseTransformDirection(view.right);
            Vector3 up = transform.InverseTransformDirection(view.up);

            _geometry.Clear();

            float depth = Vector3.Dot(ball.transform.position - view.position, view.forward);
            float metres = AimMesh.MetresPerScreenWidth(sceneCamera.fieldOfView, sceneCamera.aspect, depth);
            float height = settings.GaugeHeight * metres;
            float above = ball.Radius + (settings.GaugeGap * metres) + (height * 0.5f);
            _geometry.AddGauge(
                ball.transform.localPosition + (up * above),
                settings.GaugeWidth * metres,
                height,
                settings.GaugeBorder * metres,
                power,
                right,
                up,
                _gaugeBorderColor,
                _gaugeFillColor);

            // After the gauge, which sits right on the line of a shot aimed up the screen and mustn't hide it.
            for (int i = 0; i < _dots.Count; i++)
            {
                _geometry.AddDot(_dots[i], settings.DotRadius, settings.DotSegments, right, up, _markColor);
            }

            if (_ringVisible)
            {
                _geometry.AddDisc(_ring);
            }

            _mesh.Clear();
            _mesh.SetVertices(_geometry.Positions);
            _mesh.SetColors(_geometry.Colors);
            _mesh.SetTriangles(_geometry.Triangles, 0);
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }
    }
}
