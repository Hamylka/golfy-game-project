using IsoGolf.Rendering;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IsoGolf.Tests
{
    /// <summary>
    /// Measures shimmer. Renders the sandbox, moves the camera by a whole number of game pixels and
    /// renders again. With perfect snapping, the second image is the first one shifted: any pixel
    /// that differs is a pixel that would flicker while panning.
    /// </summary>
    public class PixelShimmerTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox_PixelPipeline.unity";
        private const int PixelsPerUnit = 24;
        private const float CameraDistance = 40f;

        // Smooth URP/Lit shading rounds differently by one level from frame to frame, invisible to the eye.
        // It should disappear with the toon ramp in M1.3; lower this to 0 then.
        private const int MaxChannelNoise = 2;

        // A few pixels flip along one 2:1 edge of the test island, most likely a rasterization tie: the edge
        // runs almost exactly through pixel centres. Measured at 3 to 8 of about 160,000. This is the budget, not a goal.
        private const int MaxUnstablePixels = 16;

        private static readonly Vector2Int[] Moves =
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(3, 2),
            new Vector2Int(17, -9),
            new Vector2Int(-40, 25),
            new Vector2Int(120, -60),
        };

        private Scene _scene;
        private Scene _previousActive;
        private bool _openedScene;
        private GameObject _cameraObject;
        private Camera _camera;
        private UniversalAdditionalCameraData _cameraData;
        private RenderTexture _target;
        private Vector3 _basePosition;

        [SetUp]
        public void SetUp()
        {
            _previousActive = SceneManager.GetActiveScene();
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _openedScene = !_scene.isLoaded;
            if (_openedScene)
            {
                _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            // Lighting comes from the active scene.
            SceneManager.SetActiveScene(_scene);

            PixelFrame frame = PixelGrid.Solve(new Vector2Int(1080, 2400), 270, PixelsPerUnit, 1);
            _target = new RenderTexture(
                frame.RenderSize.x,
                frame.RenderSize.y,
                GraphicsFormat.R8G8B8A8_SRGB,
                SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil))
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1,
            };
            _target.Create();

            // A temporary camera, so the scene's own camera and the saved scene are left alone.
            _cameraObject = new GameObject("Shimmer Test Camera") { hideFlags = HideFlags.HideAndDontSave };
            _cameraObject.transform.rotation = IsoCameraConvention.Rotation;
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = frame.OrthographicSize;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.white;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.targetTexture = _target;
            _cameraData = _cameraObject.AddComponent<UniversalAdditionalCameraData>();
            _cameraData.renderPostProcessing = false;

            Vector3 requested = -(IsoCameraConvention.Rotation * Vector3.forward) * CameraDistance;
            _basePosition = PixelGrid.Snap(requested, IsoCameraConvention.Rotation, PixelsPerUnit).Position;
        }

        [TearDown]
        public void TearDown()
        {
            _camera.targetTexture = null;
            Object.DestroyImmediate(_cameraObject);
            _target.Release();
            Object.DestroyImmediate(_target);

            SceneManager.SetActiveScene(_previousActive);
            if (_openedScene)
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        [Test]
        public void GeometryOnly_ShiftsExactly_WhenTheCameraMovesByWholePixels()
        {
            _cameraData.renderShadows = false;

            AssertStable("geometry only");
        }

        [Test]
        public void WithShadows_ShiftsExactly_WhenTheCameraMovesByWholePixels()
        {
            _cameraData.renderShadows = true;

            AssertStable("with shadows");
        }

        [Test]
        public void SubPixelMove_IsDetected_SoTheOtherTestsCanFail()
        {
            _cameraData.renderShadows = true;
            Color32[] reference = RenderReference();

            Color32[] moved = RenderAt(new Vector2(0.5f, 0.25f));
            int differing = CountVisibleDifferences(reference, moved, Vector2Int.zero, out int compared);

            TestContext.WriteLine($"half-pixel move, no compensation: {differing} of {compared} pixels differ");
            Assert.That(differing, Is.GreaterThan(MaxUnstablePixels * 10));
        }

        private void AssertStable(string label)
        {
            Color32[] reference = RenderReference();

            foreach (Vector2Int move in Moves)
            {
                Color32[] moved = RenderAt(move);
                int differing = CountVisibleDifferences(reference, moved, move, out int compared);

                TestContext.WriteLine($"{label}, move ({move.x}, {move.y}): {differing} of {compared} pixels differ");
                Assert.That(differing, Is.LessThanOrEqualTo(MaxUnstablePixels), $"move ({move.x}, {move.y})");
            }
        }

        private Color32[] RenderReference()
        {
            // The first render after loading can come out wrong while the scene warms up
            // (seen once as every island pixel differing), so it is thrown away.
            RenderAt(Vector2.zero);
            return RenderAt(Vector2.zero);
        }

        private Color32[] RenderAt(Vector2 texels)
        {
            Vector3 view = Quaternion.Inverse(IsoCameraConvention.Rotation) * _basePosition;
            view.x += texels.x / PixelsPerUnit;
            view.y += texels.y / PixelsPerUnit;
            _cameraObject.transform.position = IsoCameraConvention.Rotation * view;

            _camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;
            var readback = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            Color32[] pixels = readback.GetPixels32();
            Object.DestroyImmediate(readback);
            return pixels;
        }

        private int CountVisibleDifferences(Color32[] reference, Color32[] moved, Vector2Int move, out int compared)
        {
            int width = _target.width;
            int height = _target.height;
            int differing = 0;
            compared = 0;

            for (int y = 0; y < height; y++)
            {
                int movedY = y - move.y;
                if (movedY < 0 || movedY >= height)
                {
                    continue;
                }

                for (int x = 0; x < width; x++)
                {
                    int movedX = x - move.x;
                    if (movedX < 0 || movedX >= width)
                    {
                        continue;
                    }

                    // Moving the camera right moves the picture left.
                    Color32 a = reference[(y * width) + x];
                    Color32 b = moved[(movedY * width) + movedX];
                    compared++;
                    if (Mathf.Abs(a.r - b.r) > MaxChannelNoise
                        || Mathf.Abs(a.g - b.g) > MaxChannelNoise
                        || Mathf.Abs(a.b - b.b) > MaxChannelNoise)
                    {
                        differing++;
                    }
                }
            }

            return differing;
        }
    }
}
