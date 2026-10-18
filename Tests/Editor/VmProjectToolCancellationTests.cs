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

        [Test]
        public void OriginatingAgentRequiresTheCurrentRunningProjectToolStep()
        {
            WithOwners((first, second) =>
            {
                CurrentField.SetValue(null, null);
                Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId);
                CurrentField.SetValue(null, Guid.NewGuid().ToString("N"));
                Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId);
                CurrentField.SetValue(null, first["jobId"]);
                foreach (string status in new[] { "queued", "succeeded", "failed", "canceled", "interrupted" })
                {
                    first["status"] = status;
                    Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId, status);
                }
                first["status"] = "running";
                first["jobType"] = "execute-code";
                Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId);
            });
        }

        [Test]
        public void OriginatingAgentRejectsMissingOrMalformedOwnerRecords()
        {
            WithOwners((first, second) =>
            {
                first.Remove("ownerAgentId");
                Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId);
                foreach (object owner in new object[] { null, "", " ", 42 })
                {
                    first["ownerAgentId"] = owner;
                    Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId);
                }
            });
        }

        [Test]
        public void OriginatingAgentUsesTheCurrentOwnerInsteadOfBusinessPayload()
        {
            WithOwners((first, second) =>
            {
                var payload = VmAutomationInvocationArguments.ToolPayload(new Dictionary<string, object>
                { ["_agentId"] = "payload-agent", ["value"] = "business-value" });
                Assert.That(payload.ContainsKey("_agentId"), Is.False);
                Assert.That(payload["value"], Is.EqualTo("business-value"));
                Assert.That(VmProjectToolExecutionContext.OriginatingAgentId, Is.EqualTo("first-agent"));
                CurrentField.SetValue(null, second["jobId"]);
                Assert.That(VmProjectToolExecutionContext.OriginatingAgentId, Is.EqualTo("second-agent"));
                CurrentField.SetValue(null, null);
                Assert.Throws<InvalidOperationException>(() => _ = VmProjectToolExecutionContext.OriginatingAgentId);
            });
        }

        private static void WithOwners(Action<Dictionary<string, object>, Dictionary<string, object>> action)
        {
            FieldInfo current = CurrentField;
            object previous = current.GetValue(null);
            var jobs = (List<Dictionary<string, object>>)typeof(VmAutomationPersistentJobRunner)
                .GetField("Jobs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var first = Job(); var second = Job();
            first["jobType"] = second["jobType"] = "project-tool";
            first["ownerAgentId"] = "first-agent"; second["ownerAgentId"] = "second-agent";
            jobs.Add(first); jobs.Add(second);
            try
            {
                current.SetValue(null, first["jobId"]);
                action(first, second);
            }
            finally
            {
                jobs.Remove(first); jobs.Remove(second);
                current.SetValue(null, previous);
            }
        }

        private static Dictionary<string, object> Job() => new()
        {
            { "jobId", Guid.NewGuid().ToString("N") }, { "status", "running" },
            { "cancellationRequested", false }
        };
    }
}
