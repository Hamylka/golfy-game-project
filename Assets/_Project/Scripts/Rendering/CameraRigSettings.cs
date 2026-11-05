using UnityEngine;

namespace IsoGolf.Rendering
{
    [CreateAssetMenu(fileName = "CameraRigSettings", menuName = "Iso Golf/Camera Rig Settings")]
    public sealed class CameraRigSettings : ScriptableObject
    {
        [Tooltip("How much of the screen the framed island fills along the tighter axis. 1 touches the edge.")]
        [SerializeField, Range(0.5f, 1f)] private float screenFill = 0.94f;

        [Tooltip("Closest zoom, as a fraction of the zoomed out distance. Lower means closer.")]
        [SerializeField, Range(0.05f, 1f)] private float closestZoom = 0.3f;

        [Tooltip("Mouse wheel zoom per scroll step, for the Editor and WebGL. 0.1 means 10%.")]
        [SerializeField, Range(0.01f, 0.5f)] private float wheelZoomStep = 0.1f;

        [Header("Glide after a drag")]
        [Tooltip("Seconds for the speed to fall to about 37%. Longer glides further.")]
        [SerializeField, Range(0.05f, 2f)] private float glideTimeConstant = 0.5f;

        [Tooltip("How many seconds before release count toward the release speed.")]
        [SerializeField, Range(0.02f, 0.3f)] private float glideVelocityWindow = 0.1f;

        [Tooltip("Slower releases don't glide at all, in metres per second.")]
        [SerializeField, Min(0f)] private float glideMinReleaseSpeed = 0.2f;

        [Tooltip("The glide ends below this speed, in metres per second.")]
        [SerializeField, Min(0.001f)] private float glideStopSpeed = 0.03f;

        public float ScreenFill => screenFill;
        public float ClosestZoom => closestZoom;
        public float WheelZoomStep => wheelZoomStep;
        public float GlideTimeConstant => glideTimeConstant;
        public float GlideVelocityWindow => glideVelocityWindow;
        public float GlideMinReleaseSpeed => glideMinReleaseSpeed;
        public float GlideStopSpeed => glideStopSpeed;
    }
}
