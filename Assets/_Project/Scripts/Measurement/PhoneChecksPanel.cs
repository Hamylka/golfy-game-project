#if ISOGOLF_MEASURE
using System.Globalization;
using IsoGolf.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IsoGolf.Measurement
{
    /// <summary>
    /// Phone-check only (compiled only with ISOGOLF_MEASURE, so never in a release build). A small IMGUI strip near
    /// the top of the screen: previous/next seed, par 3/4/5, and a live frame-time readout. Changing the course
    /// reloads the scene, where <see cref="LevelBootstrap"/> picks up <see cref="DebugCourseOverride"/>. Frame times
    /// are also logged to logcat every 5 s as FRAMES lines (mean / p95 / max over the last 300 frames, in ms).
    /// </summary>
    public sealed class PhoneChecksPanel : MonoBehaviour
    {
        private const int Window = 300;

        private readonly float[] _dt = new float[Window];
        private int _count;
        private float _logAt;
        private string _stats = "";
        private int _seed = 1;
        private int _par = 3;
        private GUIStyle _button;
        private GUIStyle _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<PhoneChecksPanel>() != null)
            {
                return;
            }

            var host = new GameObject("PhoneChecksPanel");
            DontDestroyOnLoad(host);
            host.AddComponent<PhoneChecksPanel>();
        }

        private void Start()
        {
            if (DebugCourseOverride.Has)
            {
                _seed = DebugCourseOverride.Seed;
                _par = DebugCourseOverride.Par;
            }
        }

        private void Update()
        {
            _dt[_count % Window] = Time.unscaledDeltaTime * 1000f;
            _count++;
            if (Time.unscaledTime < _logAt)
            {
                return;
            }

            _logAt = Time.unscaledTime + 5f;
            int n = Mathf.Min(_count, Window);
            var copy = new float[n];
            float sum = 0f;
            float max = 0f;
            for (int i = 0; i < n; i++)
            {
                copy[i] = _dt[i];
                sum += copy[i];
                max = Mathf.Max(max, copy[i]);
            }

            System.Array.Sort(copy);
            float p95 = copy[Mathf.Min(n - 1, Mathf.CeilToInt(n * 0.95f) - 1)];
            CultureInfo c = CultureInfo.InvariantCulture;
            _stats = (sum / n).ToString("F1", c) + " ms mean, p95 " + p95.ToString("F1", c) + ", max " + max.ToString("F1", c);
            Debug.Log("FRAMES seed " + _seed + " par " + _par + " n " + n + " mean " + (sum / n).ToString("F2", c)
                + " p95 " + p95.ToString("F2", c) + " max " + max.ToString("F2", c));
        }

        private void OnGUI()
        {
            if (_button == null)
            {
                int size = Mathf.RoundToInt(Screen.height * 0.018f);
                _button = new GUIStyle(GUI.skin.button) { fontSize = size };
                _label = new GUIStyle(GUI.skin.label) { fontSize = size, normal = { textColor = Color.white } };
            }

            float h = Screen.height * 0.04f;
            float w = Screen.width / 8f;
            float y = Screen.height * 0.12f;
            if (GUI.Button(new Rect(0f, y, w, h), "<", _button))
            {
                Apply(_seed - 1, _par);
            }

            GUI.Label(new Rect(w, y, w * 2f, h), "seed " + _seed, _label);
            if (GUI.Button(new Rect(w * 3f, y, w, h), ">", _button))
            {
                Apply(_seed + 1, _par);
            }

            for (int p = 3; p <= 5; p++)
            {
                string text = p == _par ? "[P" + p + "]" : "P" + p;
                if (GUI.Button(new Rect(w * (p + 1), y, w, h), text, _button))
                {
                    Apply(_seed, p);
                }
            }

            GUI.Label(new Rect(0f, y + h, Screen.width, h), _stats, _label);
        }

        private void Apply(int seed, int par)
        {
            _seed = Mathf.Max(0, seed);
            _par = par;
            DebugCourseOverride.Set(_seed, _par);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
#endif
