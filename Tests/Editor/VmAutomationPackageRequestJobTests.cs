using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationPackageRequestJobTests
    {
        [TestCase("packages/search")]
        [TestCase("packages/add")]
        [TestCase("packages/remove")]
        public void PackageEntryPublishesAnImmediateDurableJobContract(string route)
        {
            Assert.That(VmAutomationBuiltInRouteDescriptorRegistry.TryGet(route, out var descriptor), Is.True);
            Assert.That(descriptor.IsDeferred, Is.False);
            Assert.That((Dictionary<string, object>)descriptor.OutputSchema["properties"],
                Contains.Key("jobId"));
            Assert.That((Dictionary<string, object>)descriptor.OutputSchema["properties"], Contains.Key("jobAccessToken"));
            Assert.That((Dictionary<string, object>)descriptor.OutputSchema["properties"], Contains.Key("pollRoute"));
            Assert.That((Dictionary<string, object>)descriptor.InputSchema["properties"],
                Contains.Key("idempotencyKey"));
            Assert.That(descriptor.Profile.Transaction.Durability,
                Is.EqualTo(VmTransactionMechanics.Durability.ReloadResumableJob));
            Assert.That(descriptor.Profile.Transaction.RollbackKind,
                Is.EqualTo(VmTransactionMechanics.RollbackKind.None));
        }

        [Test]
        public void SearchReceiptPrecedesNativeIssuanceAndReusesItsIdentity()
        {
            var request = new Dictionary<string, object>
            {
                { "query", "com.unity.pipeline" },
                { "idempotencyKey", "package-receipt-test-" + Guid.NewGuid().ToString("N") },
                { "_agentId", "package-receipt-test" },
            };
            var receipt = (Dictionary<string, object>)VmAutomationPackageRequestJobRunner.Start("packages/search", request);
            string jobId = (string)receipt["jobId"];
            var access = new Dictionary<string, object>
            {
                { "jobId", jobId }, { "jobAccessToken", receipt["jobAccessToken"] },
            };
            try
            {
                VmAutomationWorkspaceJob owned = VmAutomationWorkspaceJobStore.Find(jobId);
                Assert.That(owned.ClientAdopted, Is.False);
                Assert.That(owned.PackageRequestIssued, Is.False);
                Assert.That(owned.Request["offset"], Is.EqualTo(0));
                Assert.That(owned.Request["limit"], Is.EqualTo(50));
                var reused = (Dictionary<string, object>)VmAutomationPackageRequestJobRunner.Start("packages/search", request);
                Assert.That(reused["jobId"], Is.EqualTo(jobId));
                Assert.That(receipt["pollRoute"], Is.EqualTo("jobs/get"));
            }
            finally
            {
                VmAutomationWorkspaceJobRunner.Cancel(access);
                Assert.That(VmAutomationWorkspaceJobStore.Find(jobId).Status, Is.EqualTo("canceled"));
            }
        }

        [TestCase(0, 1, 1, true, 1)]
        [TestCase(1, 2, 2, false, -1)]
        [TestCase(5, 1, 0, false, -1)]
        public void SearchProjectsOnlyTheSelectedNativeMetadataPage(int offset, int limit,
            int expectedCount, bool hasMore, int nextOffset)
        {
            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages().Take(3).ToArray();
            Assert.That(packages.Length, Is.EqualTo(3));
            var request = new Dictionary<string, object>
            {
                { "query", "com.unity.pipeline" }, { "offset", offset }, { "limit", limit },
            };
            Dictionary<string, object> result = VmAutomationPackageRequestJobRunner.BuildSearchResult(packages, request);
            var page = (IList)result["results"];
            Assert.That(page.Count, Is.EqualTo(expectedCount));
            Assert.That(result["total"], Is.EqualTo(3));
            Assert.That(result["hasMore"], Is.EqualTo(hasMore));
            Assert.That(result["nextOffset"], Is.EqualTo(nextOffset < 0 ? null : (object)nextOffset));
            for (int index = 0; index < page.Count; index++)
                Assert.That(((Dictionary<string, object>)page[index])["name"], Is.EqualTo(packages[offset + index].name));
        }

        [Test]
        public void LostNativeCompletionPublishesUncertaintyWithItsOriginalInput()
        {
            var job = new VmAutomationWorkspaceJob
            {
                JobId = "package-reload-test", Operation = "packages/search",
                Request = new Dictionary<string, object> { { "query", "com.unity.pipeline" } },
                PackageRequestIssuedAt = DateTime.UtcNow,
            };
            Dictionary<string, object> error = VmAutomationPackageRequestJobRunner.RecoverInterruptedRequest(job);
            Assert.That(VmAutomationResponse.TryGetError(error, out _, out string code, out _), Is.True);
            Assert.That(code, Is.EqualTo("package_request_outcome_uncertain_after_reload"));
            Assert.That(MiniJson.Serialize(error), Does.Contain("package-reload-test").And.Contain("com.unity.pipeline"));
            Assert.That(job.PackageRequestCompleted, Is.False);
        }

        [Test]
        public void CompletedMutationPersistsItsNativeProductBeforeReload()
        {
            DateTime completedAt = DateTime.UtcNow;
            var job = new VmAutomationWorkspaceJob
            {
                JobId = "completed-package-test", JobAccessToken = "test-token",
                JobType = VmAutomationPackageRequestJobRunner.JobType, Operation = "packages/add",
                OwnerAgentId = "test", Request = new Dictionary<string, object> { { "identifier", "com.test.package@1.0.0" } },
                Status = "running", Phase = VmAutomationWorkspaceJobRunner.RefreshingAssetsPhase,
                CreatedAt = completedAt, UpdatedAt = completedAt,
                PackageRequestIssued = true, PackageRequestCompleted = true,
                PackageRequestIssuedAt = completedAt, PackageRequestCompletedAt = completedAt,
                TransactionState = new Dictionary<string, object>
                {
                    { "nativeCompletion", new Dictionary<string, object> { { "version", "1.0.0" } } },
                    { "declaration", new Dictionary<string, object> { { "lockVersion", "1.0.0" } } },
                },
            };
            var restoredValues = (Dictionary<string, object>)MiniJson.Deserialize(MiniJson.Serialize(job.ToDictionary()));
            VmAutomationWorkspaceJob restored = VmAutomationWorkspaceJob.FromDictionary(restoredValues);
            Assert.That(restored.PackageRequestCompleted, Is.True);
            Assert.That(restored.PackageRequestCompletedAt, Is.EqualTo(completedAt));
            Assert.That(MiniJson.Serialize(restored.TransactionState), Is.EqualTo(MiniJson.Serialize(job.TransactionState)));
            Assert.That(restored.Phase, Is.EqualTo(VmAutomationWorkspaceJobRunner.RefreshingAssetsPhase));
        }

        [Test]
        public void AdditionRejectsARegisteredProductFromAnotherNativeCompletion()
        {
            var completion = new Dictionary<string, object>
            {
                { "version", "1.0.0" }, { "packageId", "com.test.package@1.0.0" }, { "source", "Registry" },
            };
            var state = new Dictionary<string, object>
            {
                { "manifestDependency", "1.0.0" }, { "lockVersion", "1.0.0" },
                { "resolvedVersion", "1.0.0" }, { "resolvedPackageId", "com.test.package@0.9.0" },
                { "resolvedSource", "Registry" },
            };
            Assert.That(VmAutomationPackageRequestJobRunner.MatchesNativeCompletion("packages/add", completion, state), Is.False);
            state["resolvedPackageId"] = completion["packageId"];
            Assert.That(VmAutomationPackageRequestJobRunner.MatchesNativeCompletion("packages/add", completion, state), Is.True);
        }

        [TestCase("packages/update-git", 0.0, false)]
        [TestCase("packages/update-git", 299.999, false)]
        [TestCase("packages/update-git", 300.0, true)]
        [TestCase("packages/resolve", 299.999, false)]
        [TestCase("packages/resolve", 300.0, true)]
        public void RegistrationDeadlineUsesItsOwnPhaseClock(string operation,
            double registrationSeconds, bool expected)
        {
            DateTime issued = new DateTime(2026, 10, 3, 2, 44, 34, DateTimeKind.Utc);
            DateTime completed = issued.AddSeconds(364.318);
            var job = new VmAutomationWorkspaceJob
            {
                Operation = operation, PackageRequestIssuedAt = issued,
                PackageRequestCompletedAt = completed,
            };
            DateTime phaseStart = operation == "packages/update-git" ? completed : issued;
            Assert.That(VmAutomationWorkspaceJobRunner.ShouldFailPackageAdoption(job, false,
                phaseStart.AddSeconds(registrationSeconds)), Is.EqualTo(expected));
            Assert.That(VmAutomationWorkspaceJobRunner.ShouldFailPackageAdoption(job, true,
                phaseStart.AddSeconds(900)), Is.False);
        }

        [Test]
        public void NativeAdditionProductComesFromTheOriginalResult()
        {
            PackageInfo package = PackageInfo.GetAllRegisteredPackages().First();
            Dictionary<string, object> completion = VmAutomationPackageRequestJobRunner.BuildAddCompletion(package);
            Assert.That(completion["name"], Is.EqualTo(package.name));
            Assert.That(completion["packageId"], Is.EqualTo(package.packageId));
            Assert.That(completion["version"], Is.EqualTo(package.version));
            Assert.That(completion["source"], Is.EqualTo(package.source.ToString()));
        }

        [Test]
        public void FinalDeclarationRejectsDriftWhileRegisteredMetadataIsUnchanged()
        {
            var frozen = new Dictionary<string, object> { { "manifestDependency", "1.0.0" } };
            var final = new Dictionary<string, object>
            {
                { "manifestDependency", "1.0.0" }, { "resolvedVersion", "1.0.0" },
            };
            Assert.That(VmAutomationPackageRequestJobRunner.MatchesDeclaration(frozen, final), Is.True);
            final["manifestDependency"] = "2.0.0";
            Assert.That(VmAutomationPackageRequestJobRunner.MatchesDeclaration(frozen, final), Is.False);
        }

        [Test]
        public void RemovalOwnsTheDirectDependencyEvenWhenAnotherPackageStillRequiresIt()
        {
            var state = new Dictionary<string, object>
            {
                { "manifestDependency", "" }, { "lockVersion", "1.0.0" },
                { "resolvedVersion", "1.0.0" },
            };
            Assert.That(VmAutomationPackageRequestJobRunner.MatchesNativeCompletion("packages/remove", null, state), Is.True);
            state["manifestDependency"] = "1.0.0";
            Assert.That(VmAutomationPackageRequestJobRunner.MatchesNativeCompletion("packages/remove", null, state), Is.False);
        }
    }
}
