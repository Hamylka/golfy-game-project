#if ISOGOLF_MEASURE || ISOGOLF_RELEASE_LOG
using System.Globalization;
using System.IO;
using System.Text;
using IsoGolf.Gameplay;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace IsoGolf.Measurement
{
    /// <summary>
    /// Measurement-only. Compiled only with the scripting define ISOGOLF_MEASURE, which a normal build never sets.
    /// It plays the game by itself on the device through synthetic touches (so the real input, camera follow and
    /// aim preview all run) and writes one CSV row per frame: frame timings, the state of the shot and the
    /// profiler's sample of the AimPreview.Predict marker. Scene 0 runs first, then scene 1.
    /// </summary>
    public sealed class PhoneMeasurement : MonoBehaviour
    {
        private const float WarmUpSeconds = 4f;
        private const float SceneSeconds = 150f;
        private const float AimSeconds = 3f;

        private static bool _puttOnlyFlag;

        private enum Mode { Rest = 0, AimPutt = 1, AimArc = 2, Flight = 3 }

        private readonly StringBuilder _csv = new StringBuilder(1 << 20);
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private readonly System.Random _random = new System.Random(12345);

        private ProfilerRecorder _predict;
        private int _lastPredictCount;
        private BallView _ball;
        private Camera _camera;
        private Touchscreen _screen;
        private float _sceneStart;
        private int _sceneIndex;
        private int _shots;

        private bool _touching;
        private bool _touchEnd;
        private float _aimStart;
        private bool _wasPutt;
        private float _releasePull;
        private float _restSince;
        private bool _puttOnly;
        private ProfilerRecorder _draws, _batches, _setPass, _tris, _butterflyPose, _birdRefresh;
        private readonly System.Collections.Generic.List<float>[] _dt = { new System.Collections.Generic.List<float>(), new System.Collections.Generic.List<float>(), new System.Collections.Generic.List<float>(), new System.Collections.Generic.List<float>() };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            // Autoplay only when asked: adb push a file named measure.txt holding "<par> <seed>" to the app's files folder.
            string flag = Path.Combine(Application.persistentDataPath, "measure.txt");
            if (!File.Exists(flag))
            {
                return;
            }

            string[] parts = File.ReadAllText(flag).Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            _puttOnlyFlag = parts.Length >= 3 && parts[2] == "putt";
            if (parts.Length >= 2)
            {
                DebugCourseOverride.Set(int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[0], CultureInfo.InvariantCulture));
            }

            var host = new GameObject("PhoneMeasurement");
            DontDestroyOnLoad(host);
            host.AddComponent<PhoneMeasurement>();
        }

        private void Start()
        {
            _predict = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AimPreview.Predict", 4);
            _puttOnly = _puttOnlyFlag;
            _draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _butterflyPose = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "ButterflyView.Pose", 4);
            _birdRefresh = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BirdsView.Refresh", 4);
            _csv.AppendLine("# device=" + SystemInfo.deviceModel + " gpu=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType
                + " res=" + Screen.width + "x" + Screen.height + " target=" + Application.targetFrameRate + " dev=" + Debug.isDebugBuild);
            _csv.AppendLine("scene,t,mode,putt,dtMs,cpuMainMs,cpuRenderMs,gpuMs,predictSamples,predictUs,ballState,shot,draws,batches,setPass,tris,butterflyUs,birdUs");
            Begin();
        }

        private void Begin()
        {
            _sceneStart = Time.unscaledTime;
            _ball = null;
            _touching = false;
            _touchEnd = false;
            _restSince = Time.unscaledTime;
        }

        private void Update()
        {
            if (_ball == null && !Find())
            {
                return;
            }

            float t = Time.unscaledTime - _sceneStart;
            if (t > SceneSeconds)
            {
                NextScene();
                return;
            }

            BallSimulation sim = _ball.Simulation;
            Mode mode = Mode.Rest;
            bool putt = sim.IsPutt;

            if (_puttOnly && t >= WarmUpSeconds && !_touching && !_touchEnd && sim.State == BallState.Resting && (_placedAtShot != _shots || !putt))
            {
                PlaceOnGreen();
                sim = _ball.Simulation;
                putt = sim.IsPutt;
                _restSince = Time.unscaledTime;
            }

            if (t >= WarmUpSeconds)
            {
                mode = Drive(sim, putt);
            }

            Log(t, mode, putt, sim.State);
        }

        private int _placedAtShot = -1;

        // Putt autoplay: a ball on the green 2-5 m from the hole, a different bearing each time, so only putts are played.
        private void PlaceOnGreen()
        {
            Vector2 hole = _ball.Ground.Hole;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float d = 5f - (attempt / 10 * 1f);
                float a = (float)(_random.NextDouble() * System.Math.PI * 2);
                Vector2 p = hole + (new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d);
                if (_ball.Ground.Surface(p.x, p.y) == IsoGolf.Generation.CourseSurface.Green)
                {
                    _ball.PlaceForMeasure(p);
                    break;
                }
            }

            _placedAtShot = _shots;
        }

        private bool Find()
        {
            _ball = FindAnyObjectByType<BallView>();
            _camera = Camera.main;
            _screen = Touchscreen.current;
            return _ball != null && _ball.Simulation != null && _camera != null && _screen != null;
        }

        private Mode Drive(BallSimulation sim, bool putt)
        {
            Vector2 ballScreen = _camera.WorldToScreenPoint(_ball.transform.position);
            float width = Screen.width;

            if (_touching)
            {
                float k = Time.unscaledTime - _aimStart;
                if (k < AimSeconds)
                {
                    // A finger that keeps moving, so the aim changes every frame and Predict runs every frame.
                    float angle = Mathf.Sin(k * 1.7f) * 0.45f;
                    float pull = 0.5f + (0.5f * Mathf.Sin(k * 2.3f));
                    Touch(TouchPhase.Moved, FingerAt(ballScreen, angle, Mathf.Lerp(0.15f, 0.9f, pull), width));
                    return _wasPutt ? Mode.AimPutt : Mode.AimArc;
                }

                Touch(TouchPhase.Moved, FingerAt(ballScreen, (float)((_random.NextDouble() - 0.5) * 0.2), _releasePull, width));
                _touching = false;
                _touchEnd = true;
                return _wasPutt ? Mode.AimPutt : Mode.AimArc;
            }

            if (_touchEnd)
            {
                Touch(TouchPhase.Ended, ballScreen);
                _touchEnd = false;
                _shots++;
                _restSince = Time.unscaledTime;
                return Mode.Flight;
            }

            if (sim.State != BallState.Resting)
            {
                _restSince = Time.unscaledTime;
                return Mode.Flight;
            }

            if (Time.unscaledTime - _restSince < 1.5f)
            {
                return Mode.Rest;
            }

            // Grab the ball.
            _wasPutt = putt;
            _aimStart = Time.unscaledTime;
            _releasePull = putt ? 0.35f + ((float)_random.NextDouble() * 0.2f) : 0.55f + ((float)_random.NextDouble() * 0.4f);
            _touching = true;
            Touch(TouchPhase.Began, ballScreen);
            return putt ? Mode.AimPutt : Mode.AimArc;
        }

        // Away from the hole on screen, rotated by angle (radians), so the shot goes roughly toward it.
        private Vector2 FingerAt(Vector2 ballScreen, float angle, float pull, float width)
        {
            Vector3 hole = _ball.transform.parent.TransformPoint(new Vector3(_ball.Ground.Hole.x, 0f, _ball.Ground.Hole.y));
            Vector2 toHole = ((Vector2)_camera.WorldToScreenPoint(hole) - ballScreen).normalized;
            Vector2 away = Rotate(-toHole, angle);
            return ballScreen + (away * (0.08f + (pull * 0.22f)) * width);
        }

        private static Vector2 Rotate(Vector2 v, float a)
        {
            float c = Mathf.Cos(a);
            float s = Mathf.Sin(a);
            return new Vector2((v.x * c) - (v.y * s), (v.x * s) + (v.y * c));
        }

        private void Touch(TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(_screen, new TouchState { touchId = 1, phase = phase, position = position, pressure = 1f });
        }

        private void Log(float t, Mode mode, bool putt, BallState state)
        {
            FrameTimingManager.CaptureFrameTimings();
            uint got = FrameTimingManager.GetLatestTimings(1, _timing);
            double main = got > 0 ? _timing[0].cpuMainThreadFrameTime : 0;
            double render = got > 0 ? _timing[0].cpuRenderThreadFrameTime : 0;
            double gpu = got > 0 ? _timing[0].gpuFrameTime : 0;

            int count = _predict.Count;
            int fresh = count - _lastPredictCount;
            _lastPredictCount = count;
            double us = fresh > 0 ? _predict.GetSample(count - 1).Value / 1000.0 : 0;

            CultureInfo c = CultureInfo.InvariantCulture;
            _csv.Append(_sceneIndex).Append(',').Append(t.ToString("F3", c)).Append(',').Append((int)mode).Append(',')
                .Append(putt ? 1 : 0).Append(',').Append((Time.unscaledDeltaTime * 1000f).ToString("F3", c)).Append(',')
                .Append(main.ToString("F3", c)).Append(',').Append(render.ToString("F3", c)).Append(',')
                .Append(gpu.ToString("F3", c)).Append(',').Append(fresh).Append(',').Append(us.ToString("F1", c)).Append(',')
                .Append((int)state).Append(",").Append(_shots).Append(",").Append(Last(_draws)).Append(",").Append(Last(_batches)).Append(",")
                .Append(Last(_setPass)).Append(",").Append(Last(_tris)).Append(",").Append((Last(_butterflyPose) / 1000.0).ToString("F1", c)).Append(",")
                .Append((Last(_birdRefresh) / 1000.0).ToString("F1", c)).AppendLine();
            if (t >= 5f)
            {
                _dt[(int)mode].Add(Time.unscaledDeltaTime * 1000f);
            }
        }

        private static long Last(ProfilerRecorder r) => r.Valid ? r.LastValue : 0;

        private void Summarise()
        {
            string[] names = { "rest", "aimPutt", "aimArc", "flight" };
            for (int m = 0; m < 4; m++)
            {
                var l = _dt[m];
                if (l.Count == 0)
                {
                    continue;
                }

                l.Sort();
                Debug.Log("MEASURE_SUMMARY " + names[m] + " frames " + l.Count + " dt p50 " + l[l.Count / 2].ToString("F2") + " p99 " + l[Mathf.Min(l.Count - 1, l.Count * 99 / 100)].ToString("F2") + " max " + l[l.Count - 1].ToString("F2") + " ms");
            }
        }

        private void NextScene()
        {
            if (_touching)
            {
                Touch(TouchPhase.Ended, Vector2.zero);
                _touching = false;
            }

            _sceneIndex++;
            if (_sceneIndex >= SceneManager.sceneCountInBuildSettings)
            {
                string path = Path.Combine(Application.persistentDataPath, "phone_checks.csv");
                File.WriteAllText(path, _csv.ToString());
                Summarise();
                Debug.Log("MEASURE_DONE " + path);
                enabled = false;
                return;
            }

            SceneManager.LoadScene(_sceneIndex);
            Begin();
        }
    }
}
#endif
