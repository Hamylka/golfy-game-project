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

        [Tooltip("The zoom a level starts at. 0 is fully zoomed out, where a par 3 shows whole, and 1 is the closest.")]
        [SerializeField, Range(0f, 1f)] private float defaultZoom = 0.5f;

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

        [Header("Long courses")]
        [Tooltip("The most course one view shows along X, in metres. A longer course is framed in stretches this long.")]
        [SerializeField, Min(1f)] private float framedLength = 36f;

        [Tooltip("On a dogleg, how much further back the zoomed-out view may go so a leg running diagonally on screen keeps its whole width in view. 1 keeps every course at the same scale, and lets such a leg's sides leave the overview.")]
        [SerializeField, Range(1f, 2f)] private float doglegMaxStepBack = 1f;

        [Header("Following a shot")]
        [Tooltip("The top of the band a flying ball stays in. -1 is the bottom edge of the screen, 1 the top, so 0.2 is 60% up.")]
        [SerializeField, Range(0f, 1f)] private float followZoneTop = 0.2f;

        [Tooltip("The bottom of that band, which counts once the ball has reached it. -0.2 is 40% up the screen.")]
        [SerializeField, Range(-1f, 0f)] private float followZoneBottom = -0.2f;

        [Tooltip("How low a ball that hasn't reached the band may go before the camera moves, for a shot played backward.")]
        [SerializeField, Range(-1f, 0f)] private float followBackstop = -0.85f;

        [Tooltip("The camera moves once the ball is this far to either side, after it has been inside. 1 is the screen edge, 0.2 is 40% to 60% of the width.")]
        [SerializeField, Range(0f, 1f)] private float followZoneSide = 0.2f;

        [Tooltip("How closely the camera trails a moving ball, in seconds. Lower is closer.")]
        [SerializeField, Range(0.01f, 2f)] private float followSmoothTime = 0.08f;

        [Header("Ball at rest")]
        [Tooltip("Where a stopped ball ends up on screen, from -1 to 1. Must leave room below it for a full pull.")]
        [SerializeField] private Vector2 restBallViewport = new Vector2(0f, -0.4f);

        [Tooltip("On a course that bends, how far ahead of a stopped ball the camera reads the course's heading, in metres. 0 keeps the rest spot fixed.")]
        [SerializeField, Min(0f)] private float restLookAhead = 10f;

        [Tooltip("How far a bend may move the stopped ball to the side on screen, so the course ahead crosses the view. 1 is half the screen's width.")]
        [SerializeField, Range(0f, 0.5f)] private float restMaxShift = 0.25f;

        [Tooltip("How slowly the camera eases to a stopped ball, in seconds.")]
        [SerializeField, Range(0.01f, 3f)] private float returnSmoothTime = 0.6f;

        [Tooltip("How long the reset view button takes to ease the camera back to the default view, in seconds.")]
        [SerializeField, Range(0.05f, 2f)] private float resetSeconds = 0.35f;

        public float ScreenFill => screenFill;
        public float ResetSeconds => resetSeconds;
        public float ClosestZoom => closestZoom;
        public float DefaultZoom => defaultZoom;
        public float WheelZoomStep => wheelZoomStep;
        public float GlideTimeConstant => glideTimeConstant;
        public float GlideVelocityWindow => glideVelocityWindow;
        public float GlideMinReleaseSpeed => glideMinReleaseSpeed;
        public float GlideStopSpeed => glideStopSpeed;
        public float FramedLength => framedLength;
        public float DoglegMaxStepBack => doglegMaxStepBack;
        public Rect FollowZone => Rect.MinMaxRect(-followZoneSide, followZoneBottom, followZoneSide, followZoneTop);
        public float FollowBackstop => followBackstop;
        public float FollowSmoothTime => followSmoothTime;
        public Vector2 RestBallViewport => restBallViewport;
        public float RestLookAhead => restLookAhead;
        public float RestMaxShift => restMaxShift;
        public float ReturnSmoothTime => returnSmoothTime;
    }
}
