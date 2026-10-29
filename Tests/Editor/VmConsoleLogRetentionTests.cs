using System;
using VMUnityAutomation.Runtime;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmConsoleLogRetentionTests
    {
        private static readonly DateTime Origin = new DateTime(2026, 1, 1);

        [Test]
        public void EvictedWarningCannotBecomeACleanEmptyInterval()
        {
            var retention = new VmLogHistoryRetention(Origin);
            retention.RecordEviction(LogType.Warning, Origin.AddSeconds(1));
            Assert.That(retention.IsComplete(Origin, Origin.AddSeconds(3),
                type => type == LogType.Warning, out DateTime? first, out DateTime? last), Is.False);
            Assert.That(first, Is.EqualTo(Origin.AddSeconds(1)));
            Assert.That(last, Is.EqualTo(first));
        }

        [Test]
        public void InformationEvictionDoesNotEraseCompleteWarningEvidence()
        {
            var retention = new VmLogHistoryRetention(Origin);
            retention.RecordEviction(LogType.Log, Origin.AddSeconds(1));
            Assert.That(retention.IsComplete(Origin, null, type => type == LogType.Warning,
                out DateTime? first, out DateTime? last), Is.True);
            Assert.That(first, Is.Null);
            Assert.That(last, Is.Null);
            Assert.That(retention.IsComplete(Origin, null, type => true, out _, out _), Is.False);
        }

        [Test]
        public void NonoverlappingTimeRangesRemainComplete()
        {
            var retention = new VmLogHistoryRetention(Origin);
            retention.RecordEviction(LogType.Warning, Origin.AddSeconds(2));
            retention.RecordEviction(LogType.Warning, Origin.AddSeconds(4));
            Assert.That(retention.IsComplete(Origin, Origin.AddSeconds(1), type => true,
                out _, out _), Is.True);
            Assert.That(retention.IsComplete(Origin.AddSeconds(5), null, type => true,
                out _, out _), Is.True);
            Assert.That(retention.IsComplete(Origin.AddSeconds(4), null, type => true,
                out _, out _), Is.False);
        }

        [Test]
        public void ClearOrReloadCannotReconstructEarlierHistory()
        {
            var retention = new VmLogHistoryRetention(Origin);
            retention.RecordEviction(LogType.Warning, Origin.AddSeconds(1));
            retention.Reset(Origin.AddSeconds(2));
            Assert.That(retention.IsComplete(Origin, null, type => true, out _, out _), Is.False);
            Assert.That(retention.IsComplete(null, Origin, type => true, out _, out _), Is.False);
            Assert.That(retention.IsComplete(Origin.AddSeconds(2), null, type => true,
                out DateTime? first, out DateTime? last), Is.True);
            Assert.That(first, Is.Null);
            Assert.That(last, Is.Null);
        }

        [Test]
        public void NativeWarningLostBehindInformationStillMarksFilteredHistoryIncomplete()
        {
            VmAutomationConsoleCommands.EnsureListening();
            DateTime since = DateTime.Now;
            string marker = "VM Console retention warning " + Guid.NewGuid().ToString("N");
            LogAssert.Expect(LogType.Warning, marker);
            Debug.LogWarning(marker);
            for (int index = 0; index < 1001; index++) Debug.Log("VM Console retention information " + index);
            var result = (Dictionary<string, object>)VmAutomationConsoleCommands.Query(
                new Dictionary<string, object>
                {
                    ["since"] = since.ToString("o"), ["type"] = "warning", ["messageContains"] = marker
                });
            Assert.That(result["count"], Is.EqualTo(0));
            Assert.That(result["hasMore"], Is.False);
            Assert.That(result["truncated"], Is.True);
            Assert.That(result["pageTruncated"], Is.False);
            Assert.That(result["historyComplete"], Is.False);
        }

        [Test]
        public void NativeErrorFilterKeepsExceptionRetentionAndPreCaptureBoundaries()
        {
            VmAutomationConsoleCommands.EnsureListening();
            DateTime since = DateTime.Now;
            string marker = "VM Console retention exception " + Guid.NewGuid().ToString("N");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: " + marker);
            Debug.LogException(new InvalidOperationException(marker));
            for (int index = 0; index < 1001; index++) Debug.Log("VM Console retention exception information " + index);
            var arguments = new Dictionary<string, object>
                { ["since"] = since.ToString("o"), ["type"] = "error", ["messageContains"] = marker };
            var result = (Dictionary<string, object>)VmAutomationConsoleCommands.Query(arguments);
            Assert.That(result["truncated"], Is.True);
            Assert.That(result["historyComplete"], Is.False);
            arguments["since"] = DateTime.Now.ToString("o");
            result = (Dictionary<string, object>)VmAutomationConsoleCommands.Query(arguments);
            Assert.That(result["truncated"], Is.False);
            Assert.That(result["historyComplete"], Is.True);
            arguments["since"] = "2000-01-01T00:00:00";
            result = (Dictionary<string, object>)VmAutomationConsoleCommands.Query(arguments);
            Assert.That(result["historyComplete"], Is.False);
        }

        [Test]
        public void NativePaginationDoesNotMarkRecentHistoryIncomplete()
        {
            VmAutomationConsoleCommands.EnsureListening();
            DateTime since = DateTime.Now;
            string marker = "VM Console retention page " + Guid.NewGuid().ToString("N");
            Debug.Log(marker + " first");
            Debug.Log(marker + " second");
            var arguments = new Dictionary<string, object>
            {
                ["since"] = since.ToString("o"), ["type"] = "info", ["messageContains"] = marker,
                ["count"] = 1
            };
            var result = (Dictionary<string, object>)VmAutomationConsoleCommands.Query(arguments);
            Assert.That(result["count"], Is.EqualTo(1));
            Assert.That(result["totalMatches"], Is.EqualTo(2));
            Assert.That(result["historyComplete"], Is.True);
            Assert.That(result["pageTruncated"], Is.True);
            Assert.That(result["truncated"], Is.True);
            arguments["offset"] = result["nextOffset"];
            result = (Dictionary<string, object>)VmAutomationConsoleCommands.Query(arguments);
            Assert.That(result["historyComplete"], Is.True);
            Assert.That(result["pageTruncated"], Is.False);
            Assert.That(result["truncated"], Is.False);
        }
    }
}
