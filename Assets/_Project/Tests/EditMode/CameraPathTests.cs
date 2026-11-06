using System.Collections.Generic;
using IsoGolf.Generation;
using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The zoomed-out camera along a course's centreline: straight courses framed exactly as before, the corridor as a
    /// path, and the dogleg fixture framed and followed on 9:16 and 9:21 screens.
    /// </summary>
    public class CameraPathTests
    {
        private const float ScreenFill = 0.94f;
        private const float FramedLength = 36f;
        private const float ClosestZoom = 0.3f;

        // The camera ships with no step back (1), so every course keeps the same scale. The dogleg tests allow one, to
        // check the step back works for when the director turns it on.
        private const float MaxStepBack = 1.5f;
        private const float Tolerance = 1e-3f;
        private const float Ball = 0.12f;

        private const string Par3Path = "Assets/_Project/Settings/Par3CourseShape.asset";
        private const string LongPath = "Assets/_Project/Settings/Par5SandboxCourseShape.asset";
        private const string DoglegPath = "Assets/_Project/Settings/DoglegCourseShape.asset";

        private static CameraView View(float aspect)
        {
            return new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, aspect);
        }

        private static (CourseLayout Layout, List<Vector3> Outline) Course(string path)
        {
            CourseLayout layout = AssetDatabase.LoadAssetAtPath<CourseShapeSettings>(path).ToLayout();
            var palette = AssetDatabase.LoadAssetAtPath<PaletteSettings>("Assets/_Project/Settings/Palette.asset");
            return (layout, TerrainBuilder.Build(layout, palette).Outline);
        }

        private static CourseFrame Frame(string path, float aspect)
        {
            (CourseLayout layout, List<Vector3> outline) = Course(path);
            return CameraFraming.FitCourse(outline, layout.Centreline.Points, View(aspect), ScreenFill, FramedLength, 0f, MaxStepBack);
        }

        private static Rect Limits(IReadOnlyList<Vector3> points)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 point in points)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minZ = Mathf.Min(minZ, point.z);
                maxZ = Mathf.Max(maxZ, point.z);
            }

            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        // ----- Straight courses keep their framing -----

        // Captured from develop before this change (commit a53e573), with the shipped camera settings.
        [TestCase(Par3Path, 9f / 16f, -27.3643f, 16.8725f, -3.8264f, 0f)]
        [TestCase(Par3Path, 9f / 21f, -33.1909f, 23.0510f, -5.1404f, 0f)]
        [TestCase(Par3Path, 1280f / 2800f, -31.6336f, 21.4574f, -4.8049f, 0f)]
        [TestCase(LongPath, 9f / 16f, -56.5881f, 22.9004f, -3.2590f, 44f)]
        [TestCase(LongPath, 9f / 21f, -65.5088f, 31.8979f, -5.5091f, 44f)]
        [TestCase(LongPath, 1280f / 2800f, -63.1309f, 29.5730f, -4.9454f, 44f)]
        public void StraightCourses_KeepTheirFraming(string path, float aspect, float x, float y, float z, float corridor)
        {
            CourseFrame frame = Frame(path, aspect);

            Assert.That((frame.Home - new Vector3(x, y, z)).magnitude, Is.LessThan(Tolerance));
            Assert.That(frame.CorridorLength, Is.EqualTo(corridor).Within(Tolerance));
            Assert.That(frame.Corridor[0], Is.EqualTo(Vector2.zero));
            if (corridor > 0f)
            {
                Assert.That(frame.Corridor.Count, Is.EqualTo(2), "a straight corridor is one leg");
                Assert.That(frame.Corridor[1].y, Is.EqualTo(0f).Within(Tolerance), "along +X");
            }
        }

        [TestCase(Par3Path)]
        [TestCase(LongPath)]
        public void StraightCourses_FrameTheSameWithOrWithoutTheirCentreline(string path)
        {
            (CourseLayout layout, List<Vector3> outline) = Course(path);
            CameraView view = View(9f / 16f);

            CourseFrame along = CameraFraming.FitCourse(outline, layout.Centreline.Points, view, ScreenFill, FramedLength);
            CourseFrame plain = CameraFraming.FitCourse(outline, view, ScreenFill, FramedLength);

            Assert.That((along.Home - plain.Home).magnitude, Is.LessThan(Tolerance));
            Assert.That(along.CorridorLength, Is.EqualTo(plain.CorridorLength).Within(Tolerance));
        }

        // ----- The corridor as a path -----

        /// <summary>The navigator's limits as they were before paths: a rectangle lerped from the corridor along +X.</summary>
        private static Vector3 OldClamp(Vector3 position, Vector3 home, in CameraView view, Rect limits, float closestHeight, float corridorLength, float fullLimitsZoom)
        {
            Vector3 homeCenter = view.OnPlane(home, Vector2.zero, 0f);
            float zoom = Mathf.InverseLerp(home.y, closestHeight, position.y);
            float reach = Mathf.Clamp01(zoom / fullLimitsZoom);
            float minX = Mathf.Lerp(homeCenter.x, limits.xMin, reach);
            float maxX = Mathf.Lerp(homeCenter.x + corridorLength, limits.xMax, reach);
            float minZ = Mathf.Lerp(homeCenter.z, limits.yMin, reach);
            float maxZ = Mathf.Lerp(homeCenter.z, limits.yMax, reach);
            Vector3 center = view.OnPlane(position, Vector2.zero, 0f);
            var limited = new Vector3(Mathf.Clamp(center.x, minX, maxX), center.y, Mathf.Clamp(center.z, minZ, maxZ));
            return position + (limited - center);
        }

        [TestCase(0f)]
        [TestCase(44f)]
        public void StraightPath_LimitsTheViewExactlyLikeTheOldCorridor(float corridorLength)
        {
            CameraView view = View(9f / 16f);
            (CourseLayout _, List<Vector3> outline) = Course(corridorLength > 0f ? LongPath : Par3Path);
            CourseFrame frame = CameraFraming.FitCourse(outline, view, ScreenFill, FramedLength);
            Rect limits = Limits(outline);
            const float fullLimitsZoom = 0.5f;
            var navigator = new CameraNavigator(frame.Home, view, limits, 0f, ClosestZoom, frame.Corridor, fullLimitsZoom);
            float closestHeight = frame.Home.y * ClosestZoom;

            var random = new System.Random(7);
            for (int i = 0; i < 300; i++)
            {
                navigator.ZoomTo((float)random.NextDouble());
                var wanted = new Vector3(
                    limits.xMin - 20f + ((float)random.NextDouble() * (limits.width + 40f)),
                    navigator.Position.y,
                    limits.yMin - 20f + ((float)random.NextDouble() * (limits.height + 40f)));
                Vector3 old = OldClamp(wanted, frame.Home, view, limits, closestHeight, frame.CorridorLength, fullLimitsZoom);

                navigator.Move(wanted - navigator.Position);

                Assert.That((navigator.Position - old).magnitude, Is.LessThan(Tolerance), $"step {i}, zoom {navigator.Zoom}");
            }
        }

        private static float DistanceToPolyline(Vector2 point, IReadOnlyList<Vector2> line)
        {
            if (line.Count == 1)
            {
                return (point - line[0]).magnitude;
            }

            float best = float.MaxValue;
            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector2 leg = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], leg) / leg.sqrMagnitude);
                best = Mathf.Min(best, (point - (line[i] + (leg * t))).magnitude);
            }

            return best;
        }

        private static List<Vector2> GroundCorridor(CourseFrame frame, in CameraView view)
        {
            Vector3 center = view.OnPlane(frame.Home, Vector2.zero, 0f);
            var ground = new List<Vector2>();
            foreach (Vector2 move in frame.Corridor)
            {
                ground.Add(new Vector2(center.x, center.z) + move);
            }

            return ground;
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 21f)]
        public void BentPath_ZoomedOut_TheViewOnlyTravelsAlongTheCorridor(float aspect)
        {
            CameraView view = View(aspect);
            (CourseLayout _, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, aspect);
            var navigator = new CameraNavigator(frame.Home, view, Limits(outline), 0f, ClosestZoom, frame.Corridor, 0.5f);
            List<Vector2> corridor = GroundCorridor(frame, view);

            var random = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                navigator.Move(new Vector3((float)(random.NextDouble() - 0.3) * 30f, 0f, (float)(random.NextDouble() - 0.5) * 30f));
                Vector3 center = view.OnPlane(navigator.Position, Vector2.zero, 0f);

                Assert.That(navigator.Position.y, Is.EqualTo(frame.Home.y).Within(Tolerance));
                Assert.That(DistanceToPolyline(new Vector2(center.x, center.z), corridor), Is.LessThan(Tolerance), $"step {i}");
            }

            // Pushed far along the course, it ends at the corridor's end, past the bend.
            navigator.Move(new Vector3(500f, 0f, -350f));
            Vector3 end = view.OnPlane(navigator.Position, Vector2.zero, 0f);
            Assert.That((new Vector2(end.x, end.z) - corridor[corridor.Count - 1]).magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void BentPath_ZoomingBackOut_LandsOnTheCorridor_AndZoomedInReachesTheLimits()
        {
            CameraView view = View(9f / 16f);
            (CourseLayout _, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, 9f / 16f);
            Rect limits = Limits(outline);
            var navigator = new CameraNavigator(frame.Home, view, limits, 0f, ClosestZoom, frame.Corridor, 0.5f);
            List<Vector2> corridor = GroundCorridor(frame, view);

            navigator.ZoomTo(0.6f);
            navigator.Move(new Vector3(1000f, 0f, 1000f));
            Vector3 corner = view.OnPlane(navigator.Position, Vector2.zero, 0f);
            Assert.That(corner.x, Is.EqualTo(limits.xMax).Within(0.01f), "the full limits from fullLimitsZoom on");
            Assert.That(corner.z, Is.EqualTo(limits.yMax).Within(0.01f));

            navigator.ZoomAround(new Vector2(0.4f, -0.3f), 100f);
            Vector3 center = view.OnPlane(navigator.Position, Vector2.zero, 0f);
            Assert.That(navigator.Zoom, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(DistanceToPolyline(new Vector2(center.x, center.z), corridor), Is.LessThan(Tolerance));
        }

        [Test]
        public void BentPath_Limits_OnlyGrowWithTheZoom()
        {
            CameraView view = View(9f / 16f);
            (CourseLayout _, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, 9f / 16f);
            var navigator = new CameraNavigator(frame.Home, view, Limits(outline), 0f, ClosestZoom, frame.Corridor, 0.5f);

            // A spot reachable at one zoom stays reachable closer in.
            var random = new System.Random(11);
            for (int i = 0; i < 100; i++)
            {
                float zoom = (float)random.NextDouble() * 0.5f;
                navigator.ZoomTo(zoom);
                navigator.Move(new Vector3((float)(random.NextDouble() - 0.5) * 80f, 0f, (float)(random.NextDouble() - 0.5) * 40f));
                Vector3 reached = view.OnPlane(navigator.Position, Vector2.zero, 0f);

                navigator.ZoomAround(Vector2.zero, 0.8f);
                Vector3 closer = view.OnPlane(navigator.Position, Vector2.zero, 0f);

                Assert.That((closer - reached).magnitude, Is.LessThan(0.01f), $"step {i}");
            }
        }

        // ----- The dogleg fixture -----

        private static float MiddleRowReach(IReadOnlyList<Vector3> outline, Vector3 camera, in CameraView view)
        {
            float reach = 0f;
            foreach (Vector3 point in outline)
            {
                Vector2 viewport = view.ToViewport(camera, point);
                if (Mathf.Abs(viewport.y) <= 0.1f)
                {
                    reach = Mathf.Max(reach, Mathf.Abs(viewport.x));
                }
            }

            return reach;
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 21f)]
        public void Dogleg_TheCorridorTurnsWithTheCourse_AndEndsWithTheHoleInView(float aspect)
        {
            CameraView view = View(aspect);
            (CourseLayout layout, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, aspect);
            IReadOnlyList<Vector2> corridor = frame.Corridor;

            Assert.That(corridor.Count, Is.EqualTo(3), "one bend");
            Assert.That(Vector2.SignedAngle(Vector2.right, corridor[1] - corridor[0]), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Vector2.SignedAngle(Vector2.right, corridor[2] - corridor[1]), Is.EqualTo(-35f).Within(0.01f));

            // The first view shows the tee, the last the hole, both inside the framed fill.
            var tee = new Vector3(layout.Tee.x, 0f, layout.Tee.y);
            var hole = new Vector3(layout.Hole.x, 0f, layout.Hole.y);
            Vector2 teeOnScreen = view.ToViewport(frame.Home, tee);
            Vector3 last = frame.Home + new Vector3(corridor[2].x, 0f, corridor[2].y);
            Vector2 holeOnScreen = view.ToViewport(last, hole);
            Assert.That(Mathf.Max(Mathf.Abs(teeOnScreen.x), Mathf.Abs(teeOnScreen.y)), Is.LessThan(ScreenFill));
            Assert.That(Mathf.Max(Mathf.Abs(holeOnScreen.x), Mathf.Abs(holeOnScreen.y)), Is.LessThan(ScreenFill));
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 21f)]
        public void Dogleg_AllowedToStepBack_KeepsTheIslandsWidthInView_AlongEachLeg(float aspect)
        {
            CameraView view = View(aspect);
            (CourseLayout layout, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, aspect);
            var corridor = new GroundPath(frame.Corridor);

            // Away from the bend, where a screen row would cross the corner between the legs.
            Vector2 bendView = frame.Corridor[1];
            for (float along = 0f; along <= corridor.Length; along += 1f)
            {
                Vector2 move = corridor.PointAt(along);
                if ((move - bendView).magnitude < (FramedLength * 0.25f) + 1f)
                {
                    continue;
                }

                Vector3 camera = frame.Home + new Vector3(move.x, 0f, move.y);
                Assert.That(MiddleRowReach(outline, camera, view), Is.LessThanOrEqualTo(1f), $"at {along} m");
            }
        }

        [TestCase(9f / 16f, 1.30f, 1.45f)]
        [TestCase(9f / 21f, 1.30f, 1.45f)]
        public void Dogleg_AllowedToStepBack_GoesOnlyAsFarAsItsDiagonalLegNeeds(float aspect, float least, float most)
        {
            // Keeping a 12 m wide leg at 45 degrees on screen in view costs the overview some scale, compared with the
            // straight long course. This pins how much, so a change to it shows. A narrower diagonal leg needs less.
            (CourseLayout _, List<Vector3> straightOutline) = Course(LongPath);
            CourseFrame straight = CameraFraming.FitCourse(straightOutline, View(aspect), ScreenFill, FramedLength);
            CourseFrame dogleg = Frame(DoglegPath, aspect);

            float scale = dogleg.Home.y / straight.Home.y;

            Assert.That(scale, Is.InRange(least, most));
        }

        [Test]
        public void ShippedCamera_NeverStepsBack_SoEveryCourseKeepsTheSameScale()
        {
            // CLAUDE.md: every par zooms out to the same scale, so the ball and props are the same size on every course.
            var settings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>("Assets/_Project/Settings/CameraRigSettings.asset");

            Assert.That(settings.DoglegMaxStepBack, Is.EqualTo(1f));
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 21f)]
        public void Dogleg_WithNoStepBack_KeepsTheScaleOfAStraightCourse_AndStillReachesTheHole(float aspect)
        {
            CameraView view = View(aspect);
            (CourseLayout layout, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame capped = CameraFraming.FitCourse(outline, layout.Centreline.Points, view, ScreenFill, FramedLength, 0f, 1f);
            (CourseLayout _, List<Vector3> straightOutline) = Course(LongPath);
            CourseFrame straight = CameraFraming.FitCourse(straightOutline, view, ScreenFill, FramedLength);

            // Both first stretches are as wide as the par 3, so the same scale within the islands' wobble.
            Assert.That(capped.Home.y / straight.Home.y, Is.EqualTo(1f).Within(0.05f));
            Vector2 end = capped.Corridor[capped.Corridor.Count - 1];
            Vector2 hole = view.ToViewport(capped.Home + new Vector3(end.x, 0f, end.y), new Vector3(layout.Hole.x, 0f, layout.Hole.y));
            Assert.That(Mathf.Max(Mathf.Abs(hole.x), Mathf.Abs(hole.y)), Is.LessThan(ScreenFill));
        }

        // ----- Following along the bend -----

        private static CameraFollow Follow(CourseFrame frame, CameraNavigator navigator, in CameraView view)
        {
            return new CameraFollow(navigator, view, Rect.MinMaxRect(-0.2f, -0.2f, 0.2f, 0.2f), -0.85f, new Vector2(0f, -0.4f), 0.08f, 0.6f, 0.5f, 0.35f, 10f, 0.25f);
        }

        private static Vector3 OnSecondLeg(CourseLayout layout, float metres)
        {
            Vector2 bend = layout.Centreline.Points[1];
            Vector2 point = bend + ((layout.Hole - bend).normalized * metres);
            return new Vector3(point.x, Ball, point.y);
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 21f)]
        public void RestingBall_OnTheSecondLeg_SitsLowAndToTheLeft_SoTheCourseAheadCrossesTheView(float aspect)
        {
            CameraView view = View(aspect);
            (CourseLayout layout, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, aspect);
            var navigator = new CameraNavigator(frame.Home, view, Limits(outline), 0f, ClosestZoom, frame.Corridor, 0.5f);
            CameraFollow follow = Follow(frame, navigator, view);
            Vector3 ball = OnSecondLeg(layout, 6f);

            navigator.ZoomTo(0.5f);
            follow.PlaceOn(ball);

            Vector2 onScreen = view.ToViewport(navigator.Position, ball);
            Assert.That(onScreen.y, Is.EqualTo(-0.4f).Within(0.01f));
            Assert.That(onScreen.x, Is.LessThan(-0.05f), "the leg heads right on screen, so the ball sits left");
            Assert.That(onScreen.x, Is.GreaterThanOrEqualTo(-0.25f - 0.01f));

            // The hole is ahead, up the screen and toward the middle.
            Vector2 hole = view.ToViewport(navigator.Position, new Vector3(layout.Hole.x, 0f, layout.Hole.y));
            Assert.That(hole.y, Is.GreaterThan(onScreen.y));
        }

        [Test]
        public void RestingBall_OnAStraightCourse_StaysAtTheRestSpot()
        {
            CameraView view = View(9f / 16f);
            (CourseLayout layout, List<Vector3> outline) = Course(LongPath);
            CourseFrame frame = Frame(LongPath, 9f / 16f);
            var navigator = new CameraNavigator(frame.Home, view, Limits(outline), 0f, ClosestZoom, frame.Corridor, 0.5f);
            CameraFollow follow = Follow(frame, navigator, view);
            var ball = new Vector3(0f, Ball, 0.3f);

            navigator.ZoomTo(0.5f);
            follow.PlaceOn(ball);

            Vector2 onScreen = view.ToViewport(navigator.Position, ball);
            Assert.That(onScreen.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(onScreen.y, Is.EqualTo(-0.4f).Within(1e-4f));
        }

        [TestCase(9f / 16f)]
        [TestCase(9f / 21f)]
        public void Follow_AlongTheBend_KeepsTheBallInView_AndResetMatchesTheRestSpot(float aspect)
        {
            CameraView view = View(aspect);
            (CourseLayout layout, List<Vector3> outline) = Course(DoglegPath);
            CourseFrame frame = Frame(DoglegPath, aspect);
            var navigator = new CameraNavigator(frame.Home, view, Limits(outline), 0f, ClosestZoom, frame.Corridor, 0.5f);
            CameraFollow follow = Follow(frame, navigator, view);

            // From the first leg, round the bend and up the second leg, as a long shot that bends would travel.
            Vector2 bend = layout.Centreline.Points[1];
            var start = new Vector3(bend.x - 12f, Ball, bend.y);
            navigator.ZoomTo(0.5f);
            follow.PlaceOn(start);
            const float frameTime = 1f / 60f;
            for (float metres = -12f; metres <= 16f; metres += 0.25f)
            {
                Vector3 ball = metres < 0f ? new Vector3(bend.x + metres, Ball + 1f, bend.y) : OnSecondLeg(layout, metres) + Vector3.up;
                follow.Update(ball, FollowedBallState.Moving, false, frameTime);
                Vector2 onScreen = view.ToViewport(navigator.Position, ball);
                Assert.That(Mathf.Abs(onScreen.x), Is.LessThan(1f), $"at {metres} m");
                Assert.That(Mathf.Abs(onScreen.y), Is.LessThan(1f), $"at {metres} m");
            }

            // At rest, the return eases to the same spot a reset view gives.
            Vector3 rest = OnSecondLeg(layout, 16f);
            for (float time = 0f; time < 8f; time += frameTime)
            {
                follow.Update(rest, FollowedBallState.AtRest, false, frameTime);
            }

            Vector3 returned = navigator.Position;
            follow.BeginReset(rest);
            for (float time = 0f; time < 1f; time += frameTime)
            {
                follow.Update(rest, FollowedBallState.AtRest, false, frameTime);
            }

            Assert.That((navigator.Position - returned).magnitude, Is.LessThan(0.05f));
        }
    }
}
