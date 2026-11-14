using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>
    /// The drifting cloud shadow (M4 PR 4.7): one soft darkening that travels slowly from the tee to the hole and back. It only sets the
    /// shader globals read by <c>ShadowTint.hlsl</c> (a world-space term in the land material), so it costs fragment math and no draw
    /// call, and nothing in the simulation reads it. It sits under the Course container, so the centre is taken in world space each frame
    /// and the shadow moves with the floating island.
    /// </summary>
    public sealed class CloudShadowView : MonoBehaviour
    {
        private static readonly int Params = Shader.PropertyToID("_CloudShadow");
        private static readonly int Centre = Shader.PropertyToID("_CloudShadowCentre");

        [SerializeField] private CloudShadowSettings settings;

        private Vector2 _tee;
        private Vector2 _hole;
        private bool _built;

        /// <summary>Remembers the course's tee and hole, the ends of the shadow's path.</summary>
        public void Build(CourseLayout layout)
        {
            _tee = layout.Tee;
            _hole = layout.Hole;
            _built = true;
        }

        /// <summary>The shadow's centre on the course's ground at this time: a smooth back-and-forth between tee and hole.</summary>
        public static Vector2 PathAt(Vector2 tee, Vector2 hole, float time, float period)
        {
            float along = 0.5f + (0.5f * Mathf.Sin(time * Mathf.PI * 2f / period));
            return Vector2.Lerp(tee, hole, along);
        }

        private void Update()
        {
            if (!_built || settings == null)
            {
                return;
            }

            Vector2 local = PathAt(_tee, _hole, Time.time, settings.Period);
            Vector3 world = transform.TransformPoint(new Vector3(local.x, 0f, local.y));
            Shader.SetGlobalVector(Params, new Vector4(settings.Strength, settings.Radius, settings.Stretch, 0f));
            Shader.SetGlobalVector(Centre, new Vector4(world.x, world.z, 0f, 0f));
        }

        private void OnDisable()
        {
            // With the strength at zero the shader leaves every pixel alone.
            Shader.SetGlobalVector(Params, Vector4.zero);
        }
    }
}
