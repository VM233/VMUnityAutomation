using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmFolderOrganizationReviewTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "VMFolderReview-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets/Scripts"));
        }

        [TearDown]
        public void TearDown()
        {
            string link = Path.Combine(root, "Assets/Scripts/Linked");
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root, true);
        }

        [Test]
        public void ThresholdIsStrictAndMetaCompanionsAreNotContent()
        {
            FileAt("Assets/Scripts/A.cs");
            FileAt("Assets/Scripts/A.cs.meta");
            FileAt("Assets/Scripts/B.cs");
            FileAt("Assets/Scripts/B.cs.meta");
            var exact = Review(Policy());
            Assert.That(exact.Passed, Is.True);
            Assert.That(exact.IgnoredMetaFiles, Is.EqualTo(2));
            FileAt("Assets/Scripts/C.cs");
            var crowded = Review(Policy());
            Assert.That(crowded.RequiresReview, Is.True);
            Assert.That(crowded.Issues.Single().DirectFiles, Is.EqualTo(3));
            Assert.That(crowded.Issues[0].SampleFiles, Is.EqualTo(new[] { "A.cs", "B.cs", "C.cs" }));
        }

        [Test]
        public void OutsideRootsAndSimilarPrefixesNeverEnterTheScan()
        {
            FileAt("Assets/Scripts/A.cs");
            FileAt("Assets/ScriptsExtra/Outside.cs");
            FileAt("Assets/External/Outside.cs");
            FileAt("Packages/External/Outside.cs");
            var result = Review(Policy());
            Assert.That(result.ScannedDirectories, Is.EqualTo(1));
            Assert.That(result.ScannedEntries, Is.EqualTo(1));
            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void DirectSubfolderCrowdingIsIndependentOfRecursiveFileTotals()
        {
            FileAt("Assets/Scripts/A/One.cs");
            FileAt("Assets/Scripts/A/Nested/Two.cs");
            FileAt("Assets/Scripts/B/Three.cs");
            FileAt("Assets/Scripts/C/Four.cs");
            var result = Review(Policy());
            Assert.That(result.IssueCount, Is.EqualTo(1));
            Assert.That(result.Issues[0].Path, Is.EqualTo("Assets/Scripts"));
            Assert.That(result.Issues[0].DirectFiles, Is.Zero);
            Assert.That(result.Issues[0].DirectSubfolders, Is.EqualTo(3));
        }

        [Test]
        public void ExplicitExcludedDirectoryIsNeitherCountedNorTraversed()
        {
            FileAt("Assets/Scripts/Vendor/One.cs");
            FileAt("Assets/Scripts/Vendor/Two.cs");
            FileAt("Assets/Scripts/Vendor/Three.cs");
            FileAt("Assets/Scripts/Vendor2/Owned.cs");
            var config = Policy();
            config["excludePaths"] = new JArray("Assets/Scripts/Vendor");
            var result = Review(config);
            Assert.That(result.ScannedDirectories, Is.EqualTo(2));
            Assert.That(result.ExcludedDirectories, Is.EqualTo(1));
            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void MoreSpecificFolderOwnsThresholdWithoutDuplicateTraversal()
        {
            FileAt("Assets/Scripts/Generated/A.cs");
            FileAt("Assets/Scripts/Generated/B.cs");
            FileAt("Assets/Scripts/Generated/C.cs");
            var config = Policy();
            ((JArray)config["folders"]).Add(Folder("Assets/Scripts/Generated", 5, 2));
            var result = Review(config);
            Assert.That(result.ScannedDirectories, Is.EqualTo(2));
            Assert.That(result.ScannedEntries, Is.EqualTo(4));
            Assert.That(result.Passed, Is.True);
        }

        [Test]
        public void RequestedPathsOnlyNarrowTheConfiguredScope()
        {
            FileAt("Assets/Scripts/Target/A.cs");
            FileAt("Assets/Scripts/Other/A.cs");
            FileAt("Assets/Scripts/Other/B.cs");
            FileAt("Assets/Scripts/Other/C.cs");
            var result = VmFolderOrganizationReview.Review(root, Policy().ToString(), new[] { "Assets/Scripts/Target" });
            Assert.That(result.ScannedDirectories, Is.EqualTo(1));
            Assert.That(result.Passed, Is.True);
            Assert.Throws<InvalidDataException>(() => VmFolderOrganizationReview.Review(root, Policy().ToString(), new[] { "Assets" }));
        }

        [Test]
        public void ReturnLimitDoesNotPretendTheCompleteReviewPassed()
        {
            foreach (string folder in new[] { "A", "B", "C" })
                foreach (string file in new[] { "1.cs", "2.cs", "3.cs" })
                    FileAt("Assets/Scripts/" + folder + "/" + file);
            var result = VmFolderOrganizationReview.Review(root, Policy().ToString(), maxIssues: 1);
            Assert.That(result.IssueCount, Is.EqualTo(4));
            Assert.That(result.Issues.Count, Is.EqualTo(1));
            Assert.That(result.Truncated, Is.True);
            Assert.That(result.Passed, Is.False);
            Assert.That(result.ScannedDirectories, Is.EqualTo(4));
        }

        [TestCase("../External")]
        [TestCase("Assets/../External")]
        [TestCase("C:/External")]
        [TestCase("/External")]
        [TestCase("Assets//Scripts")]
        [TestCase("Missing")]
        public void InvalidRootCannotProduceACleanReport(string path)
        {
            var config = Policy();
            config["folders"] = new JArray(Folder(path, 2, 2));
            Assert.Throws<InvalidDataException>(() => Review(config));
        }

        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("\"2\"")]
        [TestCase("10001")]
        public void InvalidThresholdIsRejectedAtItsConfigurationOwner(string value)
        {
            var config = Policy();
            config["folders"][0]["maxDirectFiles"] = JToken.Parse(value);
            Assert.Throws<InvalidDataException>(() => Review(config));
        }

        [Test]
        public void EmptyUnknownDuplicateAndExcessDepthPoliciesAreRejected()
        {
            foreach (string config in new[]
            {
                "{}", "{\"schemaVersion\":1,\"folders\":[]}",
                "{\"schemaVersion\":1,\"schemaVersion\":1,\"folders\":[]}",
                Policy().ToString() + "{}", new string('[', 10) + new string(']', 10)
            })
                Assert.Throws<InvalidDataException>(() => VmFolderOrganizationReview.Review(root, config));
            var typo = Policy();
            typo["folderz"] = new JArray();
            Assert.Throws<InvalidDataException>(() => Review(typo));
            var excluded = Policy();
            excluded["excludePaths"] = new JArray("Assets/Scripts");
            Assert.Throws<InvalidDataException>(() => Review(excluded));
        }

        [Test]
        public void DirectoryBudgetRejectsWideTreesBeforeUnboundedEnqueue()
        {
            for (int index = 0; index < 4096; index++)
                Directory.CreateDirectory(Path.Combine(root, "Assets/Scripts/D" + index));
            Assert.Throws<InvalidDataException>(() => Review(Policy()));
        }

        [Test]
        public void EntryBudgetDoesNotPublishAnIncompleteSuccess()
        {
            for (int index = 0; index <= 32768; index++)
                FileAt("Assets/Scripts/F" + index);
            Assert.Throws<InvalidDataException>(() => Review(Policy()));
        }

        [Test]
        public void FilenameExamplesAreBoundedAndDeterministic()
        {
            for (int index = 11; index >= 0; index--) FileAt("Assets/Scripts/F" + index.ToString("D2") + ".cs");
            var result = Review(Policy());
            Assert.That(result.Issues[0].SampleFiles.Length, Is.EqualTo(8));
            Assert.That(result.Issues[0].SampleFiles[0], Is.EqualTo("F00.cs"));
            Assert.That(result.Issues[0].SampleFiles[7], Is.EqualTo("F07.cs"));
        }

        [Test]
        public void ExternalDirectoryLinksAreSkippedAndCannotBecomeSelectedRoots()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Ignore("This junction fixture uses native Windows PowerShell.");
            FileAt("External/OwnedFixture.cs");
            string link = Path.Combine(root, "Assets/Scripts/Linked");
            string target = Path.Combine(root, "External");
            string command = "New-Item -ItemType Junction -Path '" + link.Replace("'", "''") +
                "' -Target '" + target.Replace("'", "''") + "' | Out-Null";
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell/v1.0/powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -EncodedCommand " +
                            Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command)),
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                Assert.That(process.WaitForExit(10000), Is.True, "Junction creation exceeded the fixture budget.");
                Assert.That(process.ExitCode, Is.Zero);
            }
            var result = Review(Policy());
            Assert.That(result.SkippedLinks, Is.EqualTo(1));
            Assert.That(result.ScannedDirectories, Is.EqualTo(1));
            var linkedRoot = Policy();
            linkedRoot["folders"] = new JArray(Folder("Assets/Scripts/Linked", 2, 2));
            Assert.Throws<InvalidDataException>(() => Review(linkedRoot));
        }

        private VmFolderOrganizationResult Review(JObject policy) =>
            VmFolderOrganizationReview.Review(root, policy.ToString());

        [Test]
        public void MissingPolicyUsesDefaultsWithoutTraversingVendorOrArbitraryRoots()
        {
            FileAt("Assets/Scripts/Combat/A.cs");
            FileAt("Assets/Plugins/External/One.cs");
            FileAt("Assets/Other/Resources/Outside.asset");
            FileAt("Packages/External/Resources/Outside.asset");
            var result = VmFolderOrganizationPolicy.Review(root);
            Assert.That(result.ConfigurationSource, Is.EqualTo("defaults"));
            Assert.That(result.Roots, Is.EqualTo(new[] { "Assets", "Assets/Scripts" }));
            Assert.That(result.ScannedDirectories, Is.EqualTo(3));
            Assert.That(result.ScannedEntries, Is.EqualTo(5));
            Assert.That(result.Passed, Is.True);
            Assert.That(File.Exists(Path.Combine(root, VmFolderOrganizationReview.ConfigurationPath)), Is.False);
        }

        [TestCase("Resources")]
        [TestCase("GameResources")]
        [TestCase("Game Resources")]
        public void DefaultsReviewEachConventionalResourceRootRecursively(string folder)
        {
            FileAt("Assets/" + folder + "/Images/UI/Icon.asset");
            var result = VmFolderOrganizationPolicy.Review(root);
            Assert.That(result.Roots, Does.Contain("Assets/" + folder));
            Assert.That(result.ScannedDirectories, Is.EqualTo(5));
        }

        [Test]
        public void DefaultAssetsRootDetectsFilesAndFoldersWhileKeepingPluginsOpaque()
        {
            for (int index = 0; index < 9; index++) FileAt("Assets/Loose" + index + ".asset");
            for (int index = 0; index < 15; index++) FileAt("Assets/Group" + index + "/Inner.asset");
            FileAt("Assets/Plugins/Vendor/Untouched.cs");
            var result = VmFolderOrganizationPolicy.Review(root);
            Assert.That(result.ScannedDirectories, Is.EqualTo(2));
            Assert.That(result.IssueCount, Is.EqualTo(1));
            var finding = result.Issues.Single();
            Assert.That(finding.Path, Is.EqualTo("Assets"));
            Assert.That(finding.DirectFiles, Is.EqualTo(9));
            Assert.That(finding.DirectSubfolders, Is.EqualTo(17));
            Assert.That(finding.MaxDirectFiles, Is.EqualTo(8));
            Assert.That(finding.MaxDirectSubfolders, Is.EqualTo(16));
        }

        [Test]
        public void ShallowAssetsDoesNotAuthorizeExplicitVendorRequests()
        {
            FileAt("Assets/Plugins/External/One.cs");
            Assert.Throws<InvalidDataException>(() =>
                VmFolderOrganizationPolicy.Review(root, new[] { "Assets/Plugins" }));
            var result = VmFolderOrganizationPolicy.Review(root, new[] { "Assets" });
            Assert.That(result.ScannedDirectories, Is.EqualTo(1));
        }

        [Test]
        public void AuthoredPolicyReplacesDefaultsAndRetainsItsExcludedVendorSubtree()
        {
            FileAt("Assets/Scripts/OutsideDefault.cs");
            FileAt("Assets/Owned/Feature/A.cs");
            FileAt("Assets/Owned/Vendor/A.cs");
            var policy = Policy();
            policy["folders"] = new JArray(Folder("Assets/Owned", 2, 2));
            policy["excludePaths"] = new JArray("Assets/Owned/Vendor");
            WritePolicy(policy.ToString());
            var result = VmFolderOrganizationPolicy.Review(root);
            Assert.That(result.ConfigurationSource, Is.EqualTo("project"));
            Assert.That(result.Roots, Is.EqualTo(new[] { "Assets/Owned" }));
            Assert.That(result.ScannedDirectories, Is.EqualTo(2));
            Assert.That(result.ExcludedDirectories, Is.EqualTo(1));
            Assert.That(result.Passed, Is.True);
        }

        [TestCase("{}")]
        [TestCase("")]
        public void InvalidAuthoredPolicyNeverSilentlyUsesDefaults(string policy)
        {
            WritePolicy(policy);
            Assert.Throws<InvalidDataException>(() => VmFolderOrganizationPolicy.Review(root));
        }

        [TestCase("\"false\"")]
        [TestCase("0")]
        [TestCase("null")]
        public void RecursivePolicyRejectsNonBooleanValues(string value)
        {
            var policy = Policy();
            policy["folders"][0]["recursive"] = JToken.Parse(value);
            Assert.Throws<InvalidDataException>(() => Review(policy));
        }

        [Test]
        public void ShallowThresholdOverrideDoesNotCancelAnExplicitRecursiveAncestor()
        {
            FileAt("Assets/Scripts/Feature/Deep/A.cs");
            var policy = Policy();
            var shallow = Folder("Assets/Scripts/Feature", 1, 1);
            shallow["recursive"] = false;
            ((JArray)policy["folders"]).Add(shallow);
            var result = Review(policy);
            Assert.That(result.ScannedDirectories, Is.EqualTo(3));
            Assert.That(result.Passed, Is.True);
        }

        private void WritePolicy(string policy)
        {
            Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
            File.WriteAllText(Path.Combine(root, VmFolderOrganizationReview.ConfigurationPath), policy);
        }

        private static JObject Policy() => new JObject
        {
            ["schemaVersion"] = 1,
            ["folders"] = new JArray(Folder("Assets/Scripts", 2, 2)),
            ["excludePaths"] = new JArray()
        };

        private static JObject Folder(string path, int files, int directories) => new JObject
            { ["path"] = path, ["maxDirectFiles"] = files, ["maxDirectSubfolders"] = directories };

        private void FileAt(string relative)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Empty);
        }
    }
}
