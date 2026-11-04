using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmUiTransitionSourceTests
    {
        private string root, relative;

        [SetUp]
        public void SetUp()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            relative = "Temp/UiTransitionSourceFixture-" + Guid.NewGuid().ToString("N");
            root = Path.Combine(project, relative); Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string file in Directory.GetFiles(root)) File.Delete(file);
            Directory.Delete(root, false);
        }

        [UnityTest]
        public IEnumerator ActualNativeDisplayWriteIsUnregistered()
        {
            Write("A.cs", "using UnityEngine.UIElements; class FixtureA { void Apply(VisualElement x) { x.style.display = DisplayStyle.None; } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.Complete, Is.True); Assert.That(report.Passed, Is.False);
            Assert.That(report.Sites.Single(site => site.Kind == "Directdisplay").Registered, Is.False);
            Assert.That(report.Edges.Any(edge => edge.Kind == "StateTransition"), Is.True);
        }

        [UnityTest]
        public IEnumerator RealHelperAndNestedContentMutationHaveSourceContracts()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { void Apply(VisualElement x) { UiPresentation.Display(x, DisplayStyle.Flex, \"x\"); VM233.UIElements.UiPresentation.Replace(x, 1, () => x.Clear(), \"x\"); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.Passed, Is.True, string.Join("\n", report.Issues));
            Assert.That(report.Sites.Count, Is.EqualTo(3));
            Assert.That(report.Edges.Count(edge => edge.Kind == "StateTransition"), Is.EqualTo(6));
            Assert.That(report.RuntimeAccepted, Is.False);
        }

        [UnityTest]
        public IEnumerator NamedPublicationAndTargetMismatchAreDistinguished()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { VisualElement x; void Publish() { x.Clear(); } void Apply() { UiPresentation.Replace(x, 1, Publish, \"x\"); UiPresentation.Display(x, DisplayStyle.Flex, \"wrong\"); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.True);
            Assert.That(report.Sites.Single(site => site.Kind == "MismatchedSourceTarget").Registered, Is.False);
            Assert.That(report.Passed, Is.False);
        }

        [UnityTest]
        public IEnumerator LocalScopeCannotCertifyFullGraphAndIncludesAnAdjacentProducer()
        {
            Write("A.cs", "using UnityEngine.UIElements; class FixtureA { void Apply(VisualElement x) { FixtureB.Show(x); } }");
            Write("B.cs", "using UnityEngine.UIElements; class FixtureB { public static void Show(VisualElement x) { x.style.display = DisplayStyle.None; } }");
            var task = Start(relative + "/A.cs"); yield return Join(task);
            var report = task.Result;
            Assert.That(report.Passed, Is.False); Assert.That(report.SelectedPassed, Is.False);
            Assert.That(report.SelectedPaths, Does.Contain(relative + "/B.cs"));
            Assert.That(report.Coverage, Is.EqualTo("AffectedSourceGraphAndAdjacentCalls"));
        }

        [UnityTest]
        public IEnumerator UnrelatedMissingContractDoesNotBecomeALocalPassForTheWholeGraph()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { void Apply(VisualElement x) { UiPresentation.Display(x, DisplayStyle.Flex, \"x\"); } }");
            Write("B.cs", "using UnityEngine.UIElements; class FixtureB { void Apply(VisualElement x) { x.style.display = DisplayStyle.None; } }");
            var task = Start(relative + "/A.cs"); yield return Join(task);
            var report = task.Result;
            Assert.That(report.SelectedPassed, Is.True, string.Join("\n", report.Issues));
            Assert.That(report.Passed, Is.False); Assert.That(report.ScannedFiles, Is.EqualTo(2));
            Assert.That(report.SelectedPaths, Is.EquivalentTo(new[] { relative + "/A.cs" }));
        }

        private void Write(string name, string text) => File.WriteAllText(Path.Combine(root, name), text);
        private Task<VmUiTransitionSourceReport> Start(params string[] affected) => VmUiTransitionSourceReview.Start(
            new VmUiTransitionSourceRequest { SourceRoots = new[] { relative }, AffectedPaths = affected }, CancellationToken.None);
        private static IEnumerator Join(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
    }
}
