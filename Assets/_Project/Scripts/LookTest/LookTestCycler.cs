using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoGolf.LookTest
{
    /// <summary>
    /// Throwaway tool for comparing low poly options on a phone: a tap moves to the next camera and fairway
    /// setup, and the average frame time is logged. Delete it once the art direction is decided.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class LookTestCycler : MonoBehaviour
    {
        [Serializable]
        public struct State
        {
            public string Label;
            public Vector3 Position;
            public Vector3 Euler;
            public float FieldOfView;
            public bool Facets;
        }

        [SerializeField] private State[] states;
        [SerializeField] private GameObject stripesTerrain;
        [SerializeField] private GameObject facetsTerrain;

        private int _index;
        private int _frames;
        private float _seconds;

        public int StateCount => states.Length;

        public string Apply(int index)
        {
            State state = states[index];
            Camera view = GetComponent<Camera>();
            view.fieldOfView = state.FieldOfView;
            transform.SetPositionAndRotation(state.Position, Quaternion.Euler(state.Euler));
            stripesTerrain.SetActive(!state.Facets);
            facetsTerrain.SetActive(state.Facets);
            return state.Label;
        }

        private void Start()
        {
            // Android defaults to 30 fps, which would hide whether the phone can hold 60.
            Application.targetFrameRate = 60;
            Apply(_index);
        }

        private void Update()
        {
            _frames++;
            _seconds += Time.unscaledDeltaTime;
            if (_seconds >= 3f)
            {
                Debug.Log($"[LookTest] {states[_index].Label}: {_seconds * 1000f / _frames:F1} ms per frame over {_frames} frames");
                _frames = 0;
                _seconds = 0f;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasReleasedThisFrame)
            {
                _index = (_index + 1) % states.Length;
                Debug.Log($"[LookTest] Now showing: {Apply(_index)}");
                _frames = 0;
                _seconds = 0f;
            }
        }
    }
}
