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

        public float ScreenFill => screenFill;
        public float ClosestZoom => closestZoom;
        public float WheelZoomStep => wheelZoomStep;
    }
}
