using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoGolf.EditorTools
{
    /// <summary>
    /// The small JSON file that tells an outside process (a shell script waiting in the background) how the last
    /// Unity test run went, so an agent does not have to poll the MCP bridge for it. The bridge itself writes
    /// no results file. Plain data and no Editor API, so it is unit-tested.
    /// </summary>
    [Serializable]
    public sealed class TestRunReport
    {
        /// <summary>Relative to the project root. Under Temp, so it is never committed and dies with the Editor.</summary>
        public const string RelativePath = "Temp/IsoGolfTestRun/last-run.json";
        public const string StateRunning = "running";
        public const string StateFinished = "finished";
        /// <summary>A failing run lists at most this many test names, so the file stays short.</summary>
        public const int MaxFailedNames = 20;

        [SerializeField] private string state;
        [SerializeField] private long startedUnixMs;
        [SerializeField] private long finishedUnixMs;
        [SerializeField] private int passed;
        [SerializeField] private int failed;
        [SerializeField] private int skipped;
        [SerializeField] private int inconclusive;
        [SerializeField] private float durationSeconds;
        [SerializeField] private string resultState;
        [SerializeField] private string[] failedNames;

        public string State => state;
        public long StartedUnixMs => startedUnixMs;
        public long FinishedUnixMs => finishedUnixMs;
        public int Passed => passed;
        public int Failed => failed;
        public int Skipped => skipped;
        public int Inconclusive => inconclusive;
        public float DurationSeconds => durationSeconds;
        public string ResultState => resultState;
        public IReadOnlyList<string> FailedNames => failedNames;

        public static TestRunReport Started(long startedUnixMs)
        {
            return new TestRunReport { state = StateRunning, startedUnixMs = startedUnixMs, failedNames = new string[0] };
        }

        public static TestRunReport Finished(long startedUnixMs, long finishedUnixMs, int passed, int failed,
            int skipped, int inconclusive, double durationSeconds, string resultState, IReadOnlyList<string> failedNames)
        {
            int count = Math.Min(failedNames == null ? 0 : failedNames.Count, MaxFailedNames);
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = failedNames[i];
            return new TestRunReport
            {
                state = StateFinished,
                startedUnixMs = startedUnixMs,
                finishedUnixMs = finishedUnixMs,
                passed = passed,
                failed = failed,
                skipped = skipped,
                inconclusive = inconclusive,
                durationSeconds = (float)durationSeconds,
                resultState = resultState,
                failedNames = names,
            };
        }

        /// <summary>Compact JSON on one line: the wait script reads it with sed.</summary>
        public string ToJson() => JsonUtility.ToJson(this);

        public static TestRunReport FromJson(string json) => JsonUtility.FromJson<TestRunReport>(json);
    }
}
