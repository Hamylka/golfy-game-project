using IsoGolf.Generation;
using IsoGolf.Gameplay;
using IsoGolf.Rendering;
using IsoGolf.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoGolf.Tests
{
    /// <summary>Guards the structure of the build scene, which holds the look chosen on the phone.</summary>
    public class MainSceneTests
    {
        private const string MainPath = "Assets/_Project/Scenes/Main.unity";
        private const string BackgroundPath = "Assets/_Project/Art/Materials/Background.mat";
        private const string PerformancePath = "Assets/_Project/Settings/PerformanceSettings.asset";

        private Scene _previousActive;
        private Scene _main;
        private bool _openedHere;

        [SetUp]
        public void OpenMain()
        {
            _previousActive = SceneManager.GetActiveScene();
            _main = SceneManager.GetSceneByPath(MainPath);
            _openedHere = !_main.isLoaded;
            if (_openedHere)
            {
                _main = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Additive);
            }

            // RenderSettings reads the active scene, so the sky and the sun need Main to be active.
            SceneManager.SetActiveScene(_main);
        }

        [TearDown]
        public void CloseMain()
        {
            SceneManager.SetActiveScene(_previousActive);
            if (_openedHere)
            {
                EditorSceneManager.CloseScene(_main, true);
            }
        }

        [Test]
        public void MainIsTheOnlyBuildScene()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(1));
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(MainPath));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
        }

        [Test]
        public void TerrainFloatsInsideTheCourseContainer()
        {
            GameObject course = Root("Course");

            Assert.That(course.GetComponent<CourseFloat>(), Is.Not.Null);
            Assert.That(course.GetComponentInChildren<TerrainView>(true), Is.Not.Null);
        }

        [Test]
        public void CameraCloudsAndSunStayOutsideTheCourseContainer()
        {
            // They must not float with the course: that contrast is what makes the motion visible.
            Assert.That(Root("Island Camera").GetComponent<IslandCamera>(), Is.Not.Null);
            Assert.That(Root("Clouds").GetComponent<CloudView>(), Is.Not.Null);
            Assert.That(Root("Sun").GetComponent<Light>(), Is.Not.Null);
        }

        [Test]
        public void BootstrapBuildsTerrainCameraAndClouds()
        {
            var bootstrap = new SerializedObject(Root("Level").GetComponent<LevelBootstrap>());

            Assert.That(bootstrap.FindProperty("terrain").objectReferenceValue, Is.Not.Null);
            Assert.That(bootstrap.FindProperty("islandCamera").objectReferenceValue, Is.Not.Null);
            Assert.That(bootstrap.FindProperty("clouds").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void BallShadowAndHoleLiveDirectlyInTheCourseContainer()
        {
            // Directly, at the container's origin, because their positions and vertices are in its local space.
            Transform course = Root("Course").transform;

            foreach (Component piece in new Component[]
            {
                course.GetComponentInChildren<BallView>(true),
                course.GetComponentInChildren<BallShadowView>(true),
                course.GetComponentInChildren<HoleView>(true),
                course.GetComponentInChildren<AimPreview>(true),
            })
            {
                Assert.That(piece, Is.Not.Null);
                Assert.That(piece.transform.parent, Is.EqualTo(course), piece.name);
                Assert.That(piece.transform.localPosition, Is.EqualTo(Vector3.zero), piece.name);
                Assert.That(piece.transform.localRotation, Is.EqualTo(Quaternion.identity), piece.name);
                Assert.That(piece.transform.localScale, Is.EqualTo(Vector3.one), piece.name);
            }
        }

        [Test]
        public void BallUsesNoUnityPhysics()
        {
            GameObject course = Root("Course");

            Assert.That(course.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(course.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void BootstrapBuildsTheBallAndHole()
        {
            var bootstrap = new SerializedObject(Root("Level").GetComponent<LevelBootstrap>());

            Assert.That(bootstrap.FindProperty("ball").objectReferenceValue, Is.Not.Null);
            Assert.That(bootstrap.FindProperty("hole").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void BallTakesShotsFromTheSlingshot()
        {
            BallView ball = Root("Course").GetComponentInChildren<BallView>(true);
            var input = new SerializedObject(ball.GetComponent<SlingshotShotInput>());

            Assert.That(ball.GetComponent<ShotController>(), Is.Not.Null);
            Assert.That(input.FindProperty("shot").objectReferenceValue, Is.Not.Null);
            Assert.That(input.FindProperty("islandCamera").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void CameraFollowsTheBall()
        {
            BallView ball = Root("Course").GetComponentInChildren<BallView>(true);
            var follow = new SerializedObject(ball.GetComponent<BallCameraFollow>());

            Assert.That(follow.FindProperty("islandCamera").objectReferenceValue, Is.EqualTo(Root("Island Camera").GetComponent<IslandCamera>()));
        }

        [Test]
        public void AimPreviewReadsTheBallAndDrawsWithSeeThroughVertexColors()
        {
            AimPreview aim = Root("Course").GetComponentInChildren<AimPreview>(true);
            var preview = new SerializedObject(aim);

            Assert.That(preview.FindProperty("ball").objectReferenceValue, Is.EqualTo(Root("Course").GetComponentInChildren<BallView>(true)));
            Assert.That(preview.FindProperty("sceneCamera").objectReferenceValue, Is.EqualTo(Root("Island Camera").GetComponent<Camera>()));
            Assert.That(preview.FindProperty("settings").objectReferenceValue, Is.Not.Null);
            Assert.That(preview.FindProperty("palette").objectReferenceValue, Is.Not.Null);
            Assert.That(aim.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("GroundShadow"));
        }

        [Test]
        public void SkyAndSunMatchTheChosenLook()
        {
            var background = AssetDatabase.LoadAssetAtPath<Material>(BackgroundPath);

            Assert.That(RenderSettings.skybox, Is.EqualTo(background));
            Assert.That(RenderSettings.sun, Is.EqualTo(Root("Sun").GetComponent<Light>()));
            Assert.That(RenderSettings.sun.shadows, Is.Not.EqualTo(LightShadows.None));
        }

        [Test]
        public void FrameRateTargetIsSixty()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PerformanceSettings>(PerformancePath);
            var cap = new SerializedObject(Root("Level").GetComponent<FrameRateCap>());

            Assert.That(settings.TargetFrameRate, Is.EqualTo(60));
            Assert.That(cap.FindProperty("settings").objectReferenceValue, Is.EqualTo(settings));
        }

        private GameObject Root(string objectName)
        {
            foreach (GameObject root in _main.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            Assert.Fail($"{objectName} is not a root object of Main.unity.");
            return null;
        }
    }
}
