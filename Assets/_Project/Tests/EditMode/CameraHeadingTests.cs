using System.Collections.Generic;
using IsoGolf.Gameplay;
using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace IsoGolf.Tests
{
    /// <summary>
    /// The camera's heading: the maths of a yaw, the automatic heading for a ball at rest, and the follow turning to it
    /// at rest only.
    /// </summary>
    public class CameraHeadingTests
    {
        private const float Frame = 1f / 60f;
        private const float Aspect = 9f / 16f;

        private static Vector2 Rotated(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(
                (direction.x * Mathf.Cos(radians)) - (direction.y * Mathf.Sin(radians)),
                (direction.x * Mathf.Sin(radians)) + (direction.y * Mathf.Cos(radians)));
        }

        // ----- Heading maths -----

        [Test]
        public void YawFor_PlusX_IsTheConventionsYaw()
        {
            Assert.That(IsoCameraConvention.YawFor(Vector2.right), Is.EqualTo(80f).Within(1e-4f));
            Assert.That(Quaternion.Angle(IsoCameraConvention.RotationAt(80f), IsoCameraConvention.Rotation), Is.LessThan(1e-3f));
        }

        [TestCase(-35f, 115f)]
        [TestCase(35f, 45f)]
        [TestCase(180f, -100f)]
        public void YawFor_ATurnedHeading_TurnsTheYawTheOtherWay(float turn, float yaw)
        {
            // A turn toward +Z is a turn left on screen, which lowers the yaw.
            Vector2 heading = Rotated(Vector2.right, turn);

            Assert.That(Mathf.DeltaAngle(IsoCameraConvention.YawFor(heading), yaw), Is.EqualTo(0f).Within(1e-3f));
            Assert.That((IsoCameraConvention.HeadingOf(yaw) - heading).magnitude, Is.LessThan(1e-4f));
        }

        [TestCase(0f)]
        [TestCase(-35f)]
        [TestCase(60f)]
        public void EveryHeading_RunsUpTheScreenWithTheSameLeanAsPlusX(float turn)
        {
            // A point ahead along the heading lands where a point ahead along +X lands at the straight yaw.
            Vector2 heading = Rotated(Vector2.right, turn);
            var straight = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, Aspect);
            var turned = new CameraView(IsoCameraConvention.YawFor(heading), IsoCameraConvention.VerticalFieldOfView, Aspect);
            var camera = new Vector3(0f, 15f, 0f);
            Vector3 centre = straight.OnPlane(camera, Vector2.zero, 0f);
            Vector3 aheadStraight = new Vector3(centre.x + 10f, 0f, centre.z);
            Vector3 turnedCamera = CameraNavigator.Turned(camera, straight.Yaw, turned.Yaw, Vector3.zero);
            Vector3 turnedCentre = turned.OnPlane(turnedCamera, Vector2.zero, 0f);
            Vector3 aheadTurned = turnedCentre + new Vector3(heading.x * 10f, 0f, heading.y * 10f);

            Vector2 a = straight.ToViewport(camera, aheadStraight);
            Vector2 b = turned.ToViewport(turnedCamera, aheadTurned);

            Assert.That((a - b).magnitude, Is.LessThan(1e-3f));
            Assert.That(a.x, Is.GreaterThan(0f), "a slight lean to the right");
        }

        [Test]
        public void WithYaw_KeepsThePitchAndTheLens_AndNeverRolls()
        {
            var view = new CameraView(IsoCameraConvention.Rotation, IsoCameraConvention.VerticalFieldOfView, Aspect);

            CameraView turned = view.WithYaw(123f);

            Assert.That(turned.Rotation.eulerAngles.x, Is.EqualTo(IsoCameraConvention.Pitch).Within(1e-3f));
            Assert.That(turned.Rotation.eulerAngles.z, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(turned.Yaw, Is.EqualTo(123f).Within(1e-3f));
            Assert.That(turned.TanHalfFov, Is.EqualTo(view.TanHalfFov));
            Assert.That(turned.Aspect, Is.EqualTo(view.Aspect));
        }

        [TestCase(30f)]
        [TestCase(-45f)]
        [TestCase(170f)]
        public void TurnTo_KeepsThePivotInPlaceOnScreen_AndTheZoom(float turn)
        {
            var course = new CameraCourse(CameraCourse.LongPath, Aspect);
            CameraNavigator navigator = course.Navigator();
            navigator.ZoomTo(0.5f);
            Vector3 pivot = navigator.View.OnPlane(navigator.Position, new Vector2(0.1f, -0.4f), CameraCourse.BallHeight);
            float zoom = navigator.Zoom;

            navigator.TurnTo(navigator.Yaw + turn, pivot);

            Vector2 after = navigator.View.ToViewport(navigator.Position, pivot);
            Assert.That(after.x, Is.EqualTo(0.1f).Within(1e-3f));
            Assert.That(after.y, Is.EqualTo(-0.4f).Within(1e-3f));
            Assert.That(navigator.Zoom, Is.EqualTo(zoom).Within(1e-5f));
            Assert.That(Mathf.DeltaAngle(navigator.Yaw, course.StartYaw + turn), Is.EqualTo(0f).Within(1e-3f));
        }

        // ----- The automatic heading -----

        [Test]
        public void FullPowerReach_IsAboutThirtyMetresOnFlatFairway()
        {
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);

            Assert.That(course.Reach, Is.InRange(27f, 33f));
            Assert.That(ShotReach.FullPowerDistance(course.Shot), Is.EqualTo(course.Reach), "deterministic");
        }

        private static readonly Vector2[] Bent = { new Vector2(-30f, 0f), new Vector2(0f, 0f), new Vector2(20f, -14f) };

        [Test]
        public void Heading_HoleWithinReach_FacesTheHole()
        {
            var rule = new CameraHeading(Bent, Bent[2], 30f, 1.5f);
            var ball = new Vector2(-5f, 2f);

            Vector2 heading = rule.For(ball);

            Assert.That((heading - (Bent[2] - ball).normalized).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void Heading_HoleOutOfReach_FollowsTheBallsLeg()
        {
            var rule = new CameraHeading(Bent, Bent[2], 10f, 1.5f);

            Assert.That((rule.For(new Vector2(-20f, 1f)) - Vector2.right).magnitude, Is.LessThan(1e-5f), "first leg");
            Assert.That((rule.For(new Vector2(4f, -1f)) - (Bent[2] - Bent[1]).normalized).magnitude, Is.LessThan(1e-5f), "second leg");
        }

        [Test]
        public void Heading_RightByTheHole_FollowsTheLeg_NotTheDirectionToTheHole()
        {
            var rule = new CameraHeading(Bent, Bent[2], 30f, 1.5f);
            Vector2 pastTheHole = Bent[2] + new Vector2(1f, -0.2f);

            Assert.That((rule.For(pastTheHole) - (Bent[2] - Bent[1]).normalized).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void Heading_TowardTheHole_TurnsNoFurtherFromTheLegThanItsLimit()
        {
            // Past the green, facing the hole would turn the view around and run the course down the screen.
            var rule = new CameraHeading(Bent, Bent[2], 30f, 1.5f, 45f);
            Vector2 leg = (Bent[2] - Bent[1]).normalized;
            Vector2 pastTheHole = Bent[2] + (leg * 6f) + new Vector2(0f, -1f);

            Vector2 heading = rule.For(pastTheHole);

            Assert.That(Mathf.Abs(Vector2.SignedAngle(leg, heading)), Is.EqualTo(45f).Within(1e-3f));
            Assert.That(Mathf.Sign(Vector2.SignedAngle(leg, heading)), Is.EqualTo(Mathf.Sign(Vector2.SignedAngle(leg, Bent[2] - pastTheHole))), "turned toward the hole's side");
        }

        [Test]
        public void Heading_OnAStraightCourse_LegIsPlusX_WhateverTheHolesOffset()
        {
            var rule = new CameraHeading(new[] { new Vector2(-37f, 0f), new Vector2(37.2f, 0.4f) }, new Vector2(37.2f, 0.4f), 30f, 1.5f);

            Assert.That(rule.FirstLeg, Is.EqualTo(Vector2.right));
            Assert.That(rule.For(new Vector2(-30f, 0f)), Is.EqualTo(Vector2.right));
        }

        [Test]
        public void Dogleg_FromTheTee_FacesTheFirstLeg_AndOnceInReach_TheHole()
        {
            var course = new CameraCourse(CameraCourse.DoglegPath, Aspect);
            Vector2 tee = course.Layout.Tee;
            Vector2 hole = course.Layout.Hole;
            Assert.That((hole - tee).magnitude, Is.GreaterThan(course.Reach), "the fixture's hole is out of reach from the tee");

            Assert.That(course.Rule.YawFor(tee), Is.EqualTo(80f).Within(1e-3f));

            Vector2 bend = course.Centreline[1];
            Assert.That((hole - bend).magnitude, Is.LessThan(course.Reach));
            Assert.That(course.Rule.YawFor(bend), Is.EqualTo(IsoCameraConvention.YawFor(hole - bend)).Within(1e-3f));
            Assert.That(course.Rule.YawFor(bend), Is.GreaterThan(100f), "turned right on screen, toward the second leg");
        }

        // ----- The follow turns at rest only -----

        private static void Run(CameraFollow follow, Vector3 ball, FollowedBallState state, float seconds, bool finger = false)
        {
            for (float time = 0f; time < seconds; time += Frame)
            {
                follow.Update(ball, state, finger, Frame);
            }
        }

        private static (CameraCourse Course, CameraNavigator Navigator, CameraFollow Follow) Dogleg()
        {
            var course = new CameraCourse(CameraCourse.DoglegPath, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = course.Follow(navigator);
            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);
            return (course, navigator, follow);
        }

        private static Vector3 Ground(Vector2 point, float height = CameraCourse.BallHeight)
        {
            return new Vector3(point.x, height, point.y);
        }

        [Test]
        public void Flight_NeverTurnsTheCamera_EvenWhereTheHeadingWouldChange()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            float yaw = navigator.Yaw;
            Vector2 bend = course.Centreline[1];

            for (float t = 0f; t <= 1f; t += Frame)
            {
                Vector2 ground = Vector2.Lerp(course.Layout.Tee, bend + new Vector2(4f, -2f), t);
                follow.Update(Ground(ground, CameraCourse.BallHeight + (8f * t * (1f - t))), FollowedBallState.Moving, false, Frame);
                Assert.That(navigator.Yaw, Is.EqualTo(yaw).Within(1e-4f), $"at {t}");
            }

            Run(follow, Ground(bend), FollowedBallState.Holding, 1f);
            Assert.That(navigator.Yaw, Is.EqualTo(yaw).Within(1e-4f), "a lost ball holds the heading too");
        }

        [Test]
        public void AtRest_TurnsToTheAutomaticHeading_WithTheBallOnScreen_ThenAtItsRestSpot()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            Vector3 rest = Ground(course.Centreline[1] + new Vector2(2f, -1f));
            Run(follow, rest + Vector3.up, FollowedBallState.Moving, 0.5f);
            float wanted = course.Rule.YawFor(new Vector2(rest.x, rest.z));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(navigator.Yaw, wanted)), Is.GreaterThan(20f));

            for (float time = 0f; time < 6f; time += Frame)
            {
                follow.Update(rest, FollowedBallState.AtRest, false, Frame);
                Vector2 onScreen = navigator.View.ToViewport(navigator.Position, rest);
                Assert.That(Mathf.Abs(onScreen.x), Is.LessThan(1f), $"at {time} s");
                Assert.That(Mathf.Abs(onScreen.y), Is.LessThan(1f), $"at {time} s");
            }

            Assert.That(Mathf.DeltaAngle(navigator.Yaw, wanted), Is.EqualTo(0f).Within(0.1f));
            Assert.That(follow.IsReturning, Is.False);
            Vector2 spot = navigator.View.ToViewport(navigator.Position, rest);
            Assert.That((spot - course.Rig.RestBallViewport).magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void AtRest_TheTurnIsEased_NotAJump()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            Vector3 rest = Ground(course.Centreline[1] + new Vector2(2f, -1f));
            Run(follow, rest + Vector3.up, FollowedBallState.Moving, 0.5f);
            float before = navigator.Yaw;

            follow.Update(rest, FollowedBallState.AtRest, false, Frame);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before, navigator.Yaw)), Is.LessThan(2f));
            Assert.That(follow.IsReturning, Is.True);
        }

        [Test]
        public void AFinger_DuringTheTurn_StopsItWhereItIs()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            Vector3 rest = Ground(course.Centreline[1] + new Vector2(2f, -1f));
            Run(follow, rest + Vector3.up, FollowedBallState.Moving, 0.5f);
            Run(follow, rest, FollowedBallState.AtRest, 0.2f);

            follow.Update(rest, FollowedBallState.AtRest, true, Frame);
            float stopped = navigator.Yaw;
            Run(follow, rest, FollowedBallState.AtRest, 2f);

            Assert.That(navigator.Yaw, Is.EqualTo(stopped).Within(1e-4f));
        }

        [Test]
        public void Reset_ClearsTheTwist_AndTurnsToTheAutomaticHeading()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            float heading = follow.Heading;
            follow.SetTwist(30f, course.Tee);
            Assert.That(Mathf.DeltaAngle(navigator.Yaw, heading + 30f), Is.EqualTo(0f).Within(1e-3f));
            navigator.ZoomTo(0.1f);

            follow.BeginReset(course.Tee);
            follow.Update(course.Tee, FollowedBallState.AtRest, false, Frame);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(navigator.Yaw, heading)), Is.GreaterThan(20f), "eased, not cut");
            Run(follow, course.Tee, FollowedBallState.AtRest, 1f);

            Assert.That(follow.Twist, Is.EqualTo(0f));
            Assert.That(Mathf.DeltaAngle(navigator.Yaw, heading), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(navigator.Zoom, Is.EqualTo(course.Rig.DefaultZoom).Within(0.01f));
            Vector2 spot = navigator.View.ToViewport(navigator.Position, course.Tee);
            Assert.That((spot - course.Rig.RestBallViewport).magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void Reset_OnTheSecondLeg_TurnsAndRestsTheBallAtItsSpot()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            Vector3 ball = Ground(course.Centreline[1] + new Vector2(3f, -2f));

            follow.BeginReset(ball);
            for (float time = 0f; time < 1f; time += Frame)
            {
                follow.Update(ball, FollowedBallState.AtRest, false, Frame);
            }

            Assert.That(Mathf.DeltaAngle(navigator.Yaw, course.Rule.YawFor(new Vector2(ball.x, ball.z))), Is.EqualTo(0f).Within(1e-3f));
            Vector2 spot = navigator.View.ToViewport(navigator.Position, ball);
            Assert.That((spot - course.Rig.RestBallViewport).magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void TheNextShot_ClearsTheTwist()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();
            follow.SetTwist(-20f, course.Tee);

            follow.Update(course.Tee + Vector3.up, FollowedBallState.Moving, false, Frame);

            Assert.That(follow.Twist, Is.EqualTo(0f));
            Assert.That(Mathf.DeltaAngle(navigator.Yaw, follow.Heading - 20f), Is.EqualTo(0f).Within(1e-3f), "but the view only turns back at rest");
        }

        [Test]
        public void Twist_StaysWithinItsLimit()
        {
            (CameraCourse course, CameraNavigator navigator, CameraFollow follow) = Dogleg();

            follow.SetTwist(80f, course.Tee);

            Assert.That(follow.Twist, Is.EqualTo(course.Rig.MaxTwist));
            Assert.That(Mathf.DeltaAngle(follow.Heading, navigator.Yaw), Is.EqualTo(course.Rig.MaxTwist).Within(1e-3f));
        }

        [Test]
        public void AStraightCourse_NeverTurnsByAHair()
        {
            // The par 3's hole sits 0.4 m to the side: facing it would be a turn of about a degree, inside the dead band.
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = course.Follow(navigator);
            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);
            var rest = new Vector3(0f, CameraCourse.BallHeight, -0.5f);

            Run(follow, rest + Vector3.up, FollowedBallState.Moving, 0.5f);
            Run(follow, rest, FollowedBallState.AtRest, 5f);

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(course.Rule.YawFor(new Vector2(rest.x, rest.z)), 80f)), Is.InRange(0.5f, course.Rig.HeadingMinTurn));
            Assert.That(navigator.View.Rotation, Is.EqualTo(IsoCameraConvention.Rotation));
        }

        [TestCase(0f, 1f)]
        [TestCase(-0.6f, 0.5f)]
        [TestCase(0.8f, -0.9f)]
        public void AShortPutt_FacesTheHole(float x, float z)
        {
            // A playtest on Main: a ball a metre from the hole, to its side, kept the course's heading.
            var course = new CameraCourse(CameraCourse.Par3Path, Aspect);
            CameraNavigator navigator = course.Navigator();
            CameraFollow follow = course.Follow(navigator);
            navigator.ZoomTo(course.Rig.DefaultZoom);
            follow.PlaceOn(course.Tee);
            var rest = new Vector3(course.Layout.Hole.x + x, CameraCourse.BallHeight, course.Layout.Hole.y + z);

            Run(follow, rest + Vector3.up, FollowedBallState.Moving, 0.5f);
            Run(follow, rest, FollowedBallState.AtRest, 6f);

            float towardHole = IsoCameraConvention.YawFor(course.Layout.Hole - new Vector2(rest.x, rest.z));
            Assert.That(Mathf.DeltaAngle(navigator.Yaw, towardHole), Is.EqualTo(0f).Within(0.1f));
            Vector2 hole = navigator.View.ToViewport(navigator.Position, course.Hole);
            Assert.That(hole.y, Is.GreaterThan(navigator.View.ToViewport(navigator.Position, rest).y), "the hole is up the screen");
        }

        // ----- One scale on every heading -----

        [TestCase(CameraCourse.Par3Path)]
        [TestCase(CameraCourse.LongPath)]
        [TestCase(CameraCourse.GapPath)]
        [TestCase(CameraCourse.DoglegPath)]
        public void EveryHeading_HasTheSameZoomedOutHeight_AndTheSameBallSize(string path)
        {
            var course = new CameraCourse(path, 9f / 21f);
            CameraNavigator navigator = course.Navigator();
            var line = new GroundPath(course.Centreline);
            Vector2 middle = line.PointAt(line.Length * 0.5f);
            var ball = new Vector3(middle.x, CameraCourse.BallHeight, middle.y);
            float defaultDistance = (navigator.PositionWith(course.Rig.DefaultZoom, ball, course.Rig.RestBallViewport) - ball).magnitude;
            int compared = 0;

            foreach (float yaw in course.Yaws(course.Rig.MaxTwist))
            {
                navigator.TurnTo(yaw, navigator.ViewCentre);
                navigator.ZoomTo(0f);
                Assert.That(navigator.Position.y, Is.EqualTo(course.Home.y).Within(1e-3f), $"zoomed out at {yaw}");

                // Where the limit holds the view back the ball sits elsewhere on screen, so only the views that put it
                // at its rest spot compare its distance.
                Vector3 rested = navigator.PositionWith(course.Rig.DefaultZoom, ball, course.Rig.RestBallViewport);
                Assert.That(rested.y, Is.EqualTo(Mathf.Lerp(course.Home.y, course.Home.y * course.Rig.ClosestZoom, course.Rig.DefaultZoom)).Within(1e-3f));
                if ((navigator.View.ToViewport(rested, ball) - course.Rig.RestBallViewport).magnitude < 1e-3f)
                {
                    Assert.That((rested - ball).magnitude, Is.EqualTo(defaultDistance).Within(1e-3f), $"ball at rest at {yaw}");
                    compared++;
                }
            }

            Assert.That(compared, Is.GreaterThan(1));
        }
    }
}
