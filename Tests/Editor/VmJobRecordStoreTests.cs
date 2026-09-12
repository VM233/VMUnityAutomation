using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmJobRecordStoreTests
    {
        private string directory;
        private string aggregatePath;
        private List<Dictionary<string, object>> records;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VmJobRecords-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            aggregatePath = Path.Combine(directory, "jobs.json");
            records = new List<Dictionary<string, object>> { Record("first"), Record("second") };
            File.WriteAllText(aggregatePath, MiniJson.Serialize(records));
        }

        [TearDown]
        public void TearDown() => Directory.Delete(directory, true);

        [Test]
        public void MigrationPreservesEveryRecordAndRetiresTheAggregate()
        {
            var store = new VmAutomationJobRecordStore(aggregatePath);
            Assert.That(MiniJson.Serialize(store.Load()), Is.EqualTo(MiniJson.Serialize(records)));
            Assert.That(File.Exists(aggregatePath), Is.False);
            var reloaded = new VmAutomationJobRecordStore(aggregatePath);
            Assert.That(MiniJson.Serialize(reloaded.Load()), Is.EqualTo(MiniJson.Serialize(records)));
        }

        [Test]
        public void ProgressDoesNotReadOrRewriteAnUnchangedRecord()
        {
            var store = new VmAutomationJobRecordStore(aggregatePath);
            records = store.Load();
            string unchanged = store.RecordPath(records[1]);
            byte[] before = File.ReadAllBytes(unchanged);
            using (new FileStream(unchanged, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (new FileStream(Path.Combine(Path.ChangeExtension(aggregatePath, "records"), "index.json"),
                       FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                records[0]["status"] = "running";
                records[0]["progress"] = 0.75;
                store.PublishChanged(records, records[0]);
            }
            Assert.That(File.ReadAllBytes(unchanged), Is.EqualTo(before));
            var readback = new VmAutomationJobRecordStore(aggregatePath).Load();
            Assert.That(readback[0]["status"], Is.EqualTo("running"));
            Assert.That(readback[0]["progress"], Is.EqualTo(0.75));
            Assert.That(readback[0]["jobAccessToken"], Is.EqualTo("capability-first"));
        }

        [Test]
        public void MembershipAndOrderPublishBeforeRemovedRecordsRetire()
        {
            var store = new VmAutomationJobRecordStore(aggregatePath);
            records = store.Load();
            string removedPath = store.RecordPath(records[1]);
            var third = Record("third");
            records.RemoveAt(1);
            records.Insert(0, third);
            store.PublishChanged(records, third);
            Assert.That(File.Exists(removedPath), Is.False);
            Assert.That(MiniJson.Serialize(new VmAutomationJobRecordStore(aggregatePath).Load()),
                Is.EqualTo(MiniJson.Serialize(records)));
        }

        [Test]
        public void PublishedIndexOwnsRecoveryAfterAggregateRetirementWasInterrupted()
        {
            string original = File.ReadAllText(aggregatePath);
            var store = new VmAutomationJobRecordStore(aggregatePath);
            records = store.Load();
            records[0]["status"] = "canceled";
            store.PublishChanged(records, records[0]);
            File.WriteAllText(aggregatePath, original);
            var reloaded = new VmAutomationJobRecordStore(aggregatePath).Load();
            Assert.That(reloaded[0]["status"], Is.EqualTo("canceled"));
            Assert.That(File.Exists(aggregatePath), Is.False);
        }

        [Test]
        public void IdentityReuseKeepsTypeSeparationAndRejectsDuplicateMembership()
        {
            var store = new VmAutomationJobRecordStore(aggregatePath);
            records = store.Load();
            var replacement = Record("first");
            replacement["status"] = "succeeded";
            records[0] = replacement;
            store.PublishChanged(records, replacement);
            Assert.That(new VmAutomationJobRecordStore(aggregatePath).Load()[0]["status"], Is.EqualTo("succeeded"));

            var sameIdOtherType = Record("first");
            sameIdOtherType["jobType"] = "another-job-type";
            records.Add(sameIdOtherType);
            store.PublishChanged(records, sameIdOtherType);
            Assert.That(store.RecordPath(replacement), Is.Not.EqualTo(store.RecordPath(sameIdOtherType)));
            Assert.That(new VmAutomationJobRecordStore(aggregatePath).Load().Count, Is.EqualTo(3));

            records.Add(Record("first"));
            Assert.Throws<InvalidDataException>(() => store.PublishChanged(records, records[3]));
            Assert.That(new VmAutomationJobRecordStore(aggregatePath).Load().Count, Is.EqualTo(3));
        }

        private static Dictionary<string, object> Record(string id) => new()
        {
            { "jobType", "project-tool" }, { "jobId", id },
            { "jobAccessToken", "capability-" + id }, { "status", "queued" },
            { "request", new Dictionary<string, object> { { "text", "值\n\"quoted\"" }, { "value", 1.125 } } }
        };
    }
}
