#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationPolicyReviewTests
    {
        private const string FixturePath = "Assets/__VmCodePolicyReviewFixture.cs";

        [TearDown]
        public void TearDown()
        {
            string fullPath = Path.Combine(Path.GetFullPath(Path.Combine(
                Application.dataPath, "..")), FixturePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        [Test]
        public void CodePolicyReportsStructuralAndCallerRules()
        {
            string fullPath = Path.Combine(Path.GetFullPath(Path.Combine(
                Application.dataPath, "..")), FixturePath);
            File.WriteAllText(fullPath,
                "using System;\n" +
                "[System.SerializableAttribute]\n" +
                "public partial class First {\n" +
                "  void FixedUpdate() { var t = UnityEngine.Time.fixedTime; " +
                "UnityEditor.AssetDatabase.LoadAssetAtPath<ItemConfig>(\"Assets/X.asset\"); }\n" +
                "}\n" +
                "public class Second {}\n\n");

            var result = (Dictionary<string, object>)
                VmAutomationCodePolicyReviewCommands.Review(
                    new Dictionary<string, object>
                    {
                        { "paths", new List<object> { FixturePath } },
                        { "forbiddenMethodNames", new List<object> { "FixedUpdate" } },
                        { "forbiddenMemberAccesses", new List<object> { "UnityEngine.Time.fixedTime" } },
                        { "forbiddenGenericInvocations", new List<object>
                            { "LoadAssetAtPath<ItemConfig>" } },
                        { "maxIssues", 100 }
                    });

            Assert.That(result["success"], Is.True);
            Assert.That(result["passed"], Is.False);
            var rules = ((IEnumerable)result["issues"]).Cast<Dictionary<string, object>>()
                .Select(issue => issue["rule"].ToString()).ToArray();
            Assert.That(rules, Does.Contain("one-top-level-type-per-file"));
            Assert.That(rules, Does.Contain("partial-type"));
            Assert.That(rules, Does.Contain("serializable-attribute-style"));
            Assert.That(rules, Does.Contain("forbidden-method"));
            Assert.That(rules, Does.Contain("forbidden-member-access"));
            Assert.That(rules, Does.Contain("forbidden-generic-invocation"));
            Assert.That(rules, Does.Contain("eof-newline"));
        }

        [TestCase("UxmlElement")]
        [TestCase("UnityEngine.UIElements.UxmlElement")]
        [TestCase("global::UnityEngine.UIElements.UxmlElementAttribute")]
        public void CodePolicyAllowsUxmlElementSourceGenerationPartial(
            string attributeName)
        {
            string fullPath = Path.Combine(Path.GetFullPath(Path.Combine(
                Application.dataPath, "..")), FixturePath);
            File.WriteAllText(fullPath,
                $"[{attributeName}]\n" +
                "public partial class GeneratedVisualElement {}\n");

            var result = (Dictionary<string, object>)
                VmAutomationCodePolicyReviewCommands.Review(
                    new Dictionary<string, object>
                    {
                        { "paths", new List<object> { FixturePath } },
                        { "forbidPartial", true },
                        { "maxIssues", 100 }
                    });

            Assert.That(result["success"], Is.True);
            Assert.That(result["passed"], Is.True);
            Assert.That(result["issueCount"], Is.EqualTo(0));
        }

        [TestCase("file:../Package", true)]
        [TestCase("../Package", true)]
        [TestCase("Packages/com.example", true)]
        [TestCase("https://github.com/example/repo.git#0123456789012345678901234567890123456789", false)]
        public void DependencyPolicyClassifiesLocalIdentifiers(string identifier,
            bool expected)
        {
            Assert.That(VmAutomationDependencyPolicyReviewCommands
                .IsLocalIdentifier(identifier), Is.EqualTo(expected));
        }

        [Test]
        public void DependencyPolicyAcceptsSubpathContentFingerprint()
        {
            const string revision =
                "0123456789012345678901234567890123456789";
            const string contentFingerprint =
                "abcdefabcdefabcdefabcdefabcdefabcdefabcd";
            const string manifest =
                "https://github.com/example/repo.git?path=Packages/Example#" + revision;
            const string resolved = "com.example.package@" + manifest;

            Assert.That(VmAutomationDependencyPolicyReviewCommands
                .ResolvedGitPackageMatchesPolicy(manifest, resolved,
                    contentFingerprint, revision), Is.True);
            Assert.That(VmAutomationDependencyPolicyReviewCommands
                .ResolvedGitPackageMatchesPolicy(manifest, resolved, "", revision),
                Is.False);
            Assert.That(VmAutomationDependencyPolicyReviewCommands
                .ResolvedGitPackageMatchesPolicy(manifest,
                    resolved.Replace(revision,
                        "1111111111111111111111111111111111111111"),
                    contentFingerprint, revision), Is.False);
        }

        [Test]
        public void DependencyPolicyKeepsRootPackageFingerprintStrict()
        {
            const string revision =
                "0123456789012345678901234567890123456789";
            const string manifest =
                "https://github.com/example/repo.git#" + revision;
            const string resolved = "com.example.package@" + manifest;

            Assert.That(VmAutomationDependencyPolicyReviewCommands
                .ResolvedGitPackageMatchesPolicy(manifest, resolved, revision,
                    revision), Is.True);
            Assert.That(VmAutomationDependencyPolicyReviewCommands
                .ResolvedGitPackageMatchesPolicy(manifest, resolved,
                    "abcdefabcdefabcdefabcdefabcdefabcdefabcd", revision),
                Is.False);
        }

        private sealed class ItemConfig
        {
        }
    }
}
#endif
