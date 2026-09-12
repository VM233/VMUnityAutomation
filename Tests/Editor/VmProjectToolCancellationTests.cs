using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmProjectToolCancellationTests
    {
        [Test]
        public void CaptureRequiresTheRunningJobBoundary()
        {
            FieldInfo current = CurrentField;
            object previous = current.GetValue(null);
            try
            {
                current.SetValue(null, null);
                Assert.Throws<InvalidOperationException>(() =>
                    VmProjectToolExecutionContext.CaptureCancellationCheck());
            }
            finally { current.SetValue(null, previous); }
        }

        [Test]
        public void CapturedCheckKeepsItsOwnerAcrossCallbacksAndOtherJobs()
        {
            FieldInfo current = CurrentField;
            object previous = current.GetValue(null);
            var jobs = (List<Dictionary<string, object>>)typeof(VmAutomationPersistentJobRunner)
                .GetField("Jobs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var first = Job();
            var second = Job();
            jobs.Add(first);
            jobs.Add(second);
            try
            {
                current.SetValue(null, first["jobId"]);
                Func<bool> check = VmProjectToolExecutionContext.CaptureCancellationCheck();
                current.SetValue(null, null);
                Assert.That(check(), Is.False);
                current.SetValue(null, second["jobId"]);
                second["cancellationRequested"] = true;
                Assert.That(check(), Is.False, "Another job must not cancel this owner.");
                first["cancellationRequested"] = true;
                Assert.That(check(), Is.True);
                first["cancellationRequested"] = false;
                foreach (string status in new[] { "succeeded", "failed", "canceled", "interrupted" })
                {
                    first["status"] = status;
                    Assert.That(check(), Is.True, status);
                }
            }
            finally
            {
                jobs.Remove(first);
                jobs.Remove(second);
                current.SetValue(null, previous);
            }
        }

        private static FieldInfo CurrentField => typeof(VmAutomationPersistentJobRunner)
            .GetField("currentJobId", BindingFlags.Static | BindingFlags.NonPublic);

        private static Dictionary<string, object> Job() => new()
        {
            { "jobId", Guid.NewGuid().ToString("N") }, { "status", "running" },
            { "cancellationRequested", false }
        };
    }
}
