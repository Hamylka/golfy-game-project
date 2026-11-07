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

        [Header("Limits")]
        [Tooltip("How far past the islands, and past the course's centreline across a gap, the middle of the view may go, in metres. The same at every zoom and heading.")]
        [SerializeField, Min(0f)] private float islandMargin = 3f;

        [Header("Heading at rest")]
        [Tooltip("How slowly the camera turns to its automatic heading while it eases onto a stopped ball, in seconds.")]
        [SerializeField, Range(0.01f, 3f)] private float headingSmoothTime = 0.5f;

        [Tooltip("A new automatic heading closer than this to the one in effect is ignored, in degrees, so a straight course never turns by a hair.")]
        [SerializeField, Range(0f, 20f)] private float headingMinTurn = 5f;

        [Tooltip("Nearer the hole than this, in metres, the heading follows the ball's leg instead of facing the hole, whose direction means little that close.")]
        [SerializeField, Min(0f)] private float holeHeadingMinDistance = 0.3f;

        [Tooltip("How far, in degrees, the heading toward the hole may turn away from the ball's leg. 180 always faces the hole, even from past the green, where the course then runs down the screen. Lower keeps the course running up it.")]
        [SerializeField, Range(0f, 180f)] private float holeHeadingMaxTurn = 180f;

        [Tooltip("The furthest a player twist may turn the view from the automatic heading, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float maxTwist = 45f;

        [Tooltip("How far the two fingers must rotate before the view follows, in degrees, so a plain pinch doesn't wobble the heading.")]
        [SerializeField, Range(0f, 20f)] private float twistDeadZone = 4f;

        [Tooltip("Fingers closer than this on screen don't twist the view, since their angle is too noisy. Viewport units: 2 is the screen's width.")]
        [SerializeField, Range(0f, 0.5f)] private float twistMinSpread = 0.1f;

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

        [Tooltip("Near the hole, the camera at rest zooms in just enough to put the hole this high on screen, from -1 (bottom) to 1 (top). 0.24 is 62% up. Far from the hole that's the default zoom already.")]
        [SerializeField, Range(-0.5f, 0.9f)] private float approachHoleViewportY = 0.24f;

        [Tooltip("The closest that approach framing zooms, on the same scale as the default zoom (1 is the closest the player can zoom).")]
        [SerializeField, Range(0f, 1f)] private float approachClosestZoom = 1f;

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
        public float IslandMargin => islandMargin;
        public float HeadingSmoothTime => headingSmoothTime;
        public float HeadingMinTurn => headingMinTurn;
        public float HoleHeadingMinDistance => holeHeadingMinDistance;
        public float HoleHeadingMaxTurn => holeHeadingMaxTurn;
        public float MaxTwist => maxTwist;
        public float TwistDeadZone => twistDeadZone;
        public float TwistMinSpread => twistMinSpread;
        public Rect FollowZone => Rect.MinMaxRect(-followZoneSide, followZoneBottom, followZoneSide, followZoneTop);
        public float FollowBackstop => followBackstop;
        public float FollowSmoothTime => followSmoothTime;
        public Vector2 RestBallViewport => restBallViewport;
        public float ReturnSmoothTime => returnSmoothTime;
        public float ApproachHoleViewportY => approachHoleViewportY;
        public float ApproachClosestZoom => approachClosestZoom;
    }
}
