using System.Collections.Generic;
using IsoGolf.EditorTools;
using NUnit.Framework;

namespace IsoGolf.Tests
{
    public class TestRunReportTests
    {
        [Test]
        public void StartedReportSaysRunningAndKeepsItsStartTime()
        {
            var back = TestRunReport.FromJson(TestRunReport.Started(1234).ToJson());
            Assert.AreEqual(TestRunReport.StateRunning, back.State);
            Assert.AreEqual(1234, back.StartedUnixMs);
        }

        [Test]
        public void FinishedReportRoundTripsItsCounts()
        {
            var report = TestRunReport.Finished(1000, 5000, 7, 2, 1, 0, 3.5, "Failed", new[] { "A.B.One", "A.B.Two" });
            var back = TestRunReport.FromJson(report.ToJson());
            Assert.AreEqual(TestRunReport.StateFinished, back.State);
            Assert.AreEqual(5000, back.FinishedUnixMs);
            Assert.AreEqual(7, back.Passed);
            Assert.AreEqual(2, back.Failed);
            Assert.AreEqual(1, back.Skipped);
            Assert.AreEqual(0, back.Inconclusive);
            Assert.AreEqual(3.5f, back.DurationSeconds, 0.001f);
            Assert.AreEqual("Failed", back.ResultState);
            CollectionAssert.AreEqual(new[] { "A.B.One", "A.B.Two" }, back.FailedNames);
        }

        [Test]
        public void FailedNamesAreCappedSoTheFileStaysShort()
        {
            var many = new List<string>();
            for (int i = 0; i < 50; i++) many.Add("T" + i);
            var back = TestRunReport.FromJson(TestRunReport.Finished(0, 1, 0, 50, 0, 0, 1, "Failed", many).ToJson());
            Assert.AreEqual(TestRunReport.MaxFailedNames, back.FailedNames.Count);
        }

        [Test]
        public void JsonIsOneLineWithTheKeysTheWaitScriptReads()
        {
            string json = TestRunReport.Finished(10, 20, 3, 0, 0, 0, 0.1, "Passed", null).ToJson();
            StringAssert.DoesNotContain("\n", json);
            StringAssert.Contains("\"state\":\"finished\"", json);
            StringAssert.Contains("\"finishedUnixMs\":20", json);
            StringAssert.Contains("\"failed\":0", json);
        }
    }
}
