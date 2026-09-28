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
    [Category("DependencyMetaRoot")]
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmDependencyMetaRootTests
    {
        [TestCase("Library", false)]
        [TestCase("Library", true)]
        [TestCase("Temp", false)]
        [TestCase("Temp", true)]
        public void ExplicitRootAuditsOwnedDescendantsAndKeepsChildExclusions(
            string ancestor, bool missingMeta)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relativeRoot = ancestor + "/VMUnityAutomation/MetaPolicyFixtures/" +
                                  Guid.NewGuid().ToString("N");
            string root = Path.Combine(projectRoot, relativeRoot);
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "Owner.txt"), "owned source\n");
                File.WriteAllText(Path.Combine(root, "Owner.txt.meta"),
                    "fileFormatVersion: 2\nguid: 01111111111111111111111111111111\n");
                Directory.CreateDirectory(Path.Combine(root, "Data"));
                File.WriteAllText(Path.Combine(root, "Data.meta"),
                    "fileFormatVersion: 2\nguid: 02222222222222222222222222222222\nfolderAsset: yes\n");
                File.WriteAllText(Path.Combine(root, "Data/Leaf.txt"), "owned child\n");
                if (!missingMeta)
                    File.WriteAllText(Path.Combine(root, "Data/Leaf.txt.meta"),
                        "fileFormatVersion: 2\nguid: 03333333333333333333333333333333\n");
                foreach (string excluded in new[] { ".git", "Documentation~", "bin" })
                {
                    Directory.CreateDirectory(Path.Combine(root, excluded));
                    File.WriteAllText(Path.Combine(root, excluded, "Unowned.txt"), "ignored child\n");
                }

                var result = (Dictionary<string, object>)VmAutomationDependencyPolicyReviewCommands.Review(
                    new Dictionary<string, object>
                    {
                        { "includeResolved", false },
                        { "metaRoots", new List<object> { relativeRoot } },
                        { "maxIssues", 100 }
                    });
                Assert.That(result["scannedMetaRecords"], Is.EqualTo(missingMeta ? 5 : 6));
                Assert.That((IEnumerable)result["errors"], Is.Empty);
                var ownedIssues = ((IEnumerable)result["issues"]).Cast<Dictionary<string, object>>()
                    .Where(issue => issue["subject"].ToString().StartsWith(relativeRoot + "/",
                        StringComparison.Ordinal)).ToArray();
                Assert.That(ownedIssues.Length, Is.EqualTo(missingMeta ? 1 : 0));
                if (missingMeta)
                    Assert.That(ownedIssues[0]["rule"], Is.EqualTo("missing-meta"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
#endif
