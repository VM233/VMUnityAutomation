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

        private sealed class ItemConfig
        {
        }
    }
}
#endif
