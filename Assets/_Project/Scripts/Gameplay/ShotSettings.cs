using System;
using IsoGolf.Generation;
using UnityEngine;

namespace IsoGolf.Gameplay
{
    /// <summary>How one surface treats a ball that lands or rolls on it.</summary>
    [Serializable]
    public struct SurfaceResponse
    {
        [Tooltip("Share of the speed into the ground that comes back out in a bounce. 0 means no bounce.")]
        [SerializeField, Range(0f, 1f)] private float restitution;

        [Tooltip("Share of the speed along the ground kept through a bounce.")]
        [SerializeField, Range(0f, 1f)] private float tangentKeep;

        [Tooltip("How fast rolling slows down, in m/s². To let a ball stop, this must beat the slope's pull.")]
        [SerializeField, Min(0.01f)] private float rollingDeceleration;

        [Tooltip("Share of the normal carry for an arc played from this surface. 1 is no penalty. Putts ignore it.")]
        [SerializeField, Range(0.1f, 1f)] private float carryScale;

        public SurfaceResponse(float restitution, float tangentKeep, float rollingDeceleration, float carryScale)
        {
            this.restitution = restitution;
            this.tangentKeep = tangentKeep;
            this.rollingDeceleration = rollingDeceleration;
            this.carryScale = carryScale;
        }

        public float Restitution => restitution;
        public float TangentKeep => tangentKeep;
        public float RollingDeceleration => rollingDeceleration;
        public float CarryScale => carryScale;
    }

    /// <summary>Everything tunable about a shot. These are starting values, for the M2 tuning PR to judge on the phone.</summary>
    [CreateAssetMenu(fileName = "ShotSettings", menuName = "Iso Golf/Shot Settings")]
    public sealed class ShotSettings : ScriptableObject
    {
        [Header("Simulation")]
        [Tooltip("Seconds per simulation step. Changing it slightly changes where a roll ends.")]
        [SerializeField, Min(0.001f)] private float fixedTimeStep = 1f / 60f;

        [Header("Flight")]
        [SerializeField, Min(0.1f)] private float gravity = 9.81f;
        [Tooltip("Degrees above the horizon. 45 carries furthest for a given speed; lower is flatter and quicker.")]
        [SerializeField, Range(10f, 80f)] private float launchAngle = 40f;
        [Tooltip("Carry over level ground at the lowest power, in metres.")]
        [SerializeField, Min(0.1f)] private float minCarry = 1.5f;
        [Tooltip("Carry over level ground at full power, in metres.")]
        [SerializeField, Min(0.1f)] private float maxCarry = 22f;
        [Tooltip("Seconds between checks when looking for where an arc meets the ground, before refining the point.")]
        [SerializeField, Min(0.001f)] private float landingSearchStep = 0.02f;

        [Header("Putting")]
        [Tooltip("How far the lowest power rolls on level green, in metres.")]
        [SerializeField, Min(0.01f)] private float minPuttDistance = 0.3f;
        [Tooltip("How far full power rolls on level green, in metres.")]
        [SerializeField, Min(0.01f)] private float maxPuttDistance = 10f;

        [Header("Bounce")]
        [Tooltip("Below this speed out of the ground, in m/s, the ball stops bouncing and rolls.")]
        [SerializeField, Min(0f)] private float minBounceSpeed = 0.8f;
        [SerializeField, Min(0)] private int maxBounces = 3;

        [Header("Rolling")]
        [Tooltip("Acceleration per unit of slope, in m/s². A sphere rolling without slipping feels 5/7 of gravity.")]
        [SerializeField, Min(0f)] private float slopePull = 7f;
        [SerializeField] private SurfaceResponse rough = new SurfaceResponse(0.2f, 0.4f, 3f, 0.75f);
        [SerializeField] private SurfaceResponse fairway = new SurfaceResponse(0.3f, 0.55f, 1.6f, 1f);
        [SerializeField] private SurfaceResponse green = new SurfaceResponse(0.25f, 0.5f, 1.2f, 1f);
        [SerializeField] private SurfaceResponse bunker = new SurfaceResponse(0f, 0.1f, 8f, 0.5f);

        [Header("Hole")]
        [Tooltip("How close to the hole's center the ball's contact point must be, in metres.")]
        [SerializeField, Min(0.01f)] private float holeCaptureRadius = 0.25f;
        [Tooltip("Fastest capture for a line through the hole's center, in m/s. Faster lips out.")]
        [SerializeField, Min(0f)] private float centerCaptureSpeed = 2.6f;
        [Tooltip("Fastest capture for a line that only touches the rim, in m/s. Slower is always captured.")]
        [SerializeField, Min(0f)] private float rimCaptureSpeed = 1f;
        [Tooltip("Most a lip-out can turn the ball, in degrees. It also sets the curve: this much turn along one hole radius.")]
        [SerializeField, Range(0f, 120f)] private float lipOutMaxTurn = 70f;
        [Tooltip("Share of its speed the ball keeps through a lip-out on a central line. A graze keeps it all.")]
        [SerializeField, Range(0f, 1f)] private float lipOutSpeedKeep = 0.6f;
        [Tooltip("How far below the ground the captured ball settles, in metres.")]
        [SerializeField, Min(0f)] private float holeDepth = 0.35f;
        [SerializeField, Min(0.01f)] private float holeDropSpeed = 1.5f;

        [Header("Lost ball")]
        [Tooltip("Seconds between losing the ball and its return to where the shot was played.")]
        [SerializeField, Min(0f)] private float lostSeconds = 1f;
        [Tooltip("How fast the ball sinks in water, in m/s.")]
        [SerializeField, Min(0f)] private float sinkSpeed = 0.6f;
        [Tooltip("How quickly water stops the ball's movement along the surface, per second.")]
        [SerializeField, Min(0f)] private float waterDrag = 5f;

        [Header("Shot gesture")]
        [Tooltip("How close to the ball a touch must start, as a share of the screen width. Letting go this close cancels the shot. The ball is tiny on screen, so this is much bigger than the ball.")]
        [SerializeField, Range(0.01f, 0.3f)] private float touchRadius = 0.08f;
        [Tooltip("How far from the ball the finger reaches full power, as a share of the screen width. The ball's spot on screen must leave this much room.")]
        [SerializeField, Range(0.1f, 0.6f)] private float fullPowerPull = 0.3f;
        [Tooltip("Seconds the ball must be held before a pull counts. 0 means no wait.")]
        [SerializeField, Min(0f)] private float holdSeconds;

        public float FixedTimeStep => fixedTimeStep;
        public float Gravity => gravity;
        public float LaunchAngle => launchAngle;
        public float LandingSearchStep => landingSearchStep;
        public float MinBounceSpeed => minBounceSpeed;
        public int MaxBounces => maxBounces;
        public float SlopePull => slopePull;
        public float HoleCaptureRadius => holeCaptureRadius;
        public float CenterCaptureSpeed => centerCaptureSpeed;
        public float RimCaptureSpeed => rimCaptureSpeed;
        public float LipOutMaxTurn => lipOutMaxTurn;
        public float LipOutSpeedKeep => lipOutSpeedKeep;
        public float HoleDepth => holeDepth;
        public float HoleDropSpeed => holeDropSpeed;
        public float LostSeconds => lostSeconds;
        public float SinkSpeed => sinkSpeed;
        public float WaterDrag => waterDrag;
        public float TouchRadius => touchRadius;
        public float FullPowerPull => fullPowerPull;
        public float HoldSeconds => holdSeconds;

        /// <summary>The carry over level ground at a power from 0 to 1.</summary>
        public float CarryFor(float power) => Mathf.Lerp(minCarry, maxCarry, power);

        /// <summary>How far a putt rolls on level green at a power from 0 to 1.</summary>
        public float PuttDistanceFor(float power) => Mathf.Lerp(minPuttDistance, maxPuttDistance, power);

        public SurfaceResponse ResponseOf(CourseSurface surface)
        {
            switch (surface)
            {
                case CourseSurface.Fairway:
                    return fairway;
                case CourseSurface.Green:
                    return green;
                case CourseSurface.Bunker:
                    return bunker;
                default:
                    return rough;
            }
        }
    }
}
