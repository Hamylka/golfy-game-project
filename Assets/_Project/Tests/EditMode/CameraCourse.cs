using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Generation;
using IsoGolf.Rendering;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// A test course with its camera set up the way <see cref="IslandCamera"/> and <see cref="LevelBootstrap"/> set it up
    /// in a scene, from the shipped settings, but without a scene.
    /// </summary>
    public sealed class CameraCourse
    {
        public const string Par3Path = "Assets/_Project/Settings/Par3CourseShape.asset";
        public const string LongPath = "Assets/_Project/Settings/Par5SandboxCourseShape.asset";
        public const string GapPath = "Assets/_Project/Settings/GapCourseShape.asset";
        public const string DoglegPath = "Assets/_Project/Settings/DoglegCourseShape.asset";

        /// <summary>The courses of Main, Sandbox_LongCourse, Sandbox_Gap and Sandbox_Dogleg.</summary>
        public static readonly string[] ScenePaths = { Par3Path, LongPath, GapPath, DoglegPath };

        public const float BallHeight = 0.12f;

        // How far apart the view centres are that the shadow fade check tries, in metres.
        private const float CentreStep = 2f;

        public CameraCourse(string path, float aspect, float? holeMaxTurn = null)
            : this(AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(path).ToLayout(), aspect, holeMaxTurn)
        {
        }

        /// <summary>The same set-up for any course layout, a generated one say.</summary>
        public CameraCourse(CourseLayout layout, float aspect, float? holeMaxTurn = null)
        {
            Rig = AssetDatabase.LoadAssetAtPath<CameraRigSettings>("Assets/_Project/Settings/CameraRigSettings.asset");
            Shot = AssetDatabase.LoadAssetAtPath<ShotSettings>("Assets/_Project/Settings/ShotSettings.asset");
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            Layout = layout;
            TerrainResult terrain = TerrainBuilder.Build(Layout, palette);
            Outline = terrain.Outline;
            Centreline = Layout.Centreline.Points;
            Area = new IslandArea(LevelBootstrap.GroundEdges(terrain.Edge), Centreline, Rig.IslandMargin);
            Reach = ShotReach.FullPowerDistance(Shot);
            Rule = new CameraHeading(Centreline, Layout.Hole, Reach, Rig.HoleHeadingMinDistance, holeMaxTurn ?? Rig.HoleHeadingMaxTurn);

            StartYaw = IsoCameraConvention.YawFor(Rule.FirstLeg);
            Quaternion rotation = Mathf.Abs(Mathf.DeltaAngle(StartYaw, IsoCameraConvention.StraightYaw)) < 1e-3f
                ? IsoCameraConvention.Rotation
                : IsoCameraConvention.RotationAt(StartYaw);
            View = new CameraView(rotation, IsoCameraConvention.VerticalFieldOfView, aspect);
            Home = CameraFraming.FitCourse(Outline, Centreline, View, Rig.ScreenFill, Rig.FramedLength);
        }

        public CameraRigSettings Rig { get; }
        public ShotSettings Shot { get; }
        public CourseLayout Layout { get; }
        public List<Vector3> Outline { get; }
        public IReadOnlyList<Vector2> Centreline { get; }
        public IslandArea Area { get; }
        public float Reach { get; }
        public CameraHeading Rule { get; }
        public float StartYaw { get; }
        public CameraView View { get; }
        public Vector3 Home { get; }

        public Vector3 Tee => new Vector3(Layout.Tee.x, BallHeight, Layout.Tee.y);

        public Vector3 Hole => new Vector3(Layout.Hole.x, 0f, Layout.Hole.y);

        public CameraNavigator Navigator()
        {
            return new CameraNavigator(Home, View, Area, 0f, Rig.ClosestZoom);
        }

        public CameraFollow Follow(CameraNavigator navigator, bool withRule = true)
        {
            var ease = new HeadingEase(Rig.HeadingSmoothTime, Rig.HeadingMinTurn, Rig.MaxTwist);
            return new CameraFollow(navigator, Rig.FollowZone, Rig.FollowBackstop, Rig.RestBallViewport, Rig.FollowSmoothTime, Rig.ReturnSmoothTime, Rig.DefaultZoom, Rig.ResetSeconds, ease)
            {
                Rule = withRule ? Rule : null,
            };
        }

        /// <summary>Every yaw the camera can take on this course, with no twist (the twist gesture comes in PR 3d).</summary>
        public List<float> Yaws(float twist = 0f)
        {
            return IslandCamera.ReachableYaws(Rule, Area, StartYaw, twist);
        }

        /// <summary>
        /// The farthest point of the island a fully zoomed out camera shows, over every view centre the area allows
        /// (on a 2 m grid) at each of these yaws: outline points on screen, earth band included, and the island's
        /// ground where the screen's edges cut it.
        /// </summary>
        public float FarthestFramedPoint(IEnumerable<float> yaws, out float atYaw, out Vector2 atCentre, float zoom = 0f)
        {
            Vector3 centre = View.OnPlane(Home, Vector2.zero, 0f);
            float worst = 0f;
            atYaw = StartYaw;
            atCentre = new Vector2(centre.x, centre.z);
            foreach (float yaw in yaws)
            {
                CameraView view = View.WithYaw(yaw);
                Vector3 offset = (CameraNavigator.Turned(Home, View.Yaw, yaw, centre) - centre) * Mathf.Lerp(1f, Rig.ClosestZoom, zoom);
                Rect bounds = Area.Bounds;
                for (float x = Mathf.Floor(bounds.xMin); x <= bounds.xMax; x += CentreStep)
                {
                    for (float z = Mathf.Floor(bounds.yMin); z <= bounds.yMax; z += CentreStep)
                    {
                        var spot = new Vector2(x, z);
                        if (!Area.Contains(spot))
                        {
                            continue;
                        }

                        float far = Farthest(offset + new Vector3(x, 0f, z), view);
                        if (far > worst)
                        {
                            worst = far;
                            atYaw = yaw;
                            atCentre = spot;
                        }
                    }
                }
            }

            return worst;
        }

        /// <summary>
        /// The farthest point of a framed stretch at this yaw: the course's last <see cref="CameraRigSettings.FramedLength"/>
        /// metres along the yaw's heading (the whole course if it's shorter), centred on screen at the one zoomed-out
        /// height, as the framing frames the first stretch along +X. Outline points, earth band included.
        /// </summary>
        public float FarthestFramedStretchPoint(float yaw)
        {
            Vector2 heading = IsoCameraConvention.HeadingOf(yaw);
            float maxAlong = float.MinValue;
            foreach (Vector3 point in Outline)
            {
                maxAlong = Mathf.Max(maxAlong, Vector2.Dot(new Vector2(point.x, point.z), heading));
            }

            var stretch = new List<Vector3>();
            foreach (Vector3 point in Outline)
            {
                if (Vector2.Dot(new Vector2(point.x, point.z), heading) >= maxAlong - Rig.FramedLength)
                {
                    stretch.Add(point);
                }
            }

            // Framed by the framing's own fit, then moved along its view direction to the one zoomed-out height, which
            // keeps the stretch centred on screen.
            CameraView view = View.WithYaw(yaw);
            Vector3 fit = CameraFraming.Fit(stretch, view, Rig.ScreenFill);
            Vector3 forward = view.Rotation * Vector3.forward;
            Vector3 camera = fit + (forward * ((Home.y - fit.y) / forward.y));

            float far = 0f;
            foreach (Vector3 point in stretch)
            {
                far = Mathf.Max(far, (point - camera).magnitude);
            }

            return far;
        }

        private float Farthest(Vector3 camera, CameraView view)
        {
            float far = 0f;
            foreach (Vector3 point in Outline)
            {
                Vector2 viewport = view.ToViewport(camera, point);
                if (Mathf.Abs(viewport.x) <= 1f && Mathf.Abs(viewport.y) <= 1f)
                {
                    far = Mathf.Max(far, (point - camera).magnitude);
                }
            }

            // Only the screen's top edge and the upper halves of its sides: the ground nearer the bottom is closer.
            const int Steps = 12;
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps;
                foreach (Vector2 edge in new[] { new Vector2((2f * t) - 1f, 1f), new Vector2(1f, t), new Vector2(-1f, t) })
                {
                    Vector3 ground = view.OnPlane(camera, edge, 0f);
                    if (Area.OnIsland(new Vector2(ground.x, ground.z)))
                    {
                        far = Mathf.Max(far, (ground - camera).magnitude);
                    }
                }
            }

            return far;
        }
    }
}
