using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace IsoGolf.EditorTools
{
    /// <summary>
    /// Writes <see cref="TestRunReport.RelativePath"/> when a test run starts and when it finishes, whoever started
    /// it (the MCP bridge or the Test Runner window). It only observes: it never starts, stops or changes a run.
    /// Registered again after every domain reload by InitializeOnLoad.
    /// </summary>
    [InitializeOnLoad]
    internal static class TestRunDoneFile
    {
        private static readonly TestRunnerApi Api;
        private static long startedUnixMs;

        static TestRunDoneFile()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.hideFlags = HideFlags.HideAndDontSave;
            Api.RegisterCallbacks(new Callbacks());
        }

        private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private static string FilePath()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(root, TestRunReport.RelativePath);
        }

        private static void Write(TestRunReport report)
        {
            try
            {
                string path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Write beside it then swap, so a reader never sees half a file.
                string temp = path + ".tmp";
                File.WriteAllText(temp, report.ToJson());
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                // Tooling only: a failure here must never break a test run.
                Debug.LogWarning("TestRunDoneFile could not write its file: " + e.Message);
            }
        }

        private static void CollectFailures(ITestResultAdaptor node, List<string> names)
        {
            if (names.Count >= TestRunReport.MaxFailedNames) return;
            if (!node.HasChildren)
            {
                if (node.TestStatus == TestStatus.Failed) names.Add(node.FullName);
                return;
            }
            foreach (ITestResultAdaptor child in node.Children) CollectFailures(child, names);
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                startedUnixMs = NowUnixMs();
                Write(TestRunReport.Started(startedUnixMs));
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var failures = new List<string>();
                CollectFailures(result, failures);
                long now = NowUnixMs();
                // A domain reload during the run would have lost the start time: fall back to the finish time.
                long started = startedUnixMs != 0 ? startedUnixMs : now;
                Write(TestRunReport.Finished(started, now, result.PassCount, result.FailCount, result.SkipCount,
                    result.InconclusiveCount, result.Duration, result.ResultState, failures));
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
