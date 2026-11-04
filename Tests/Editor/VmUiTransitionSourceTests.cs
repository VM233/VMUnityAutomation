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

        [Test]
        public void PublicToolRegistrationAdmitsItsNativeScopeAndExactCleanupContract()
        {
            var source = VmProjectToolRegistry.GetToolDetails(false).Single(value =>
                value.TryGetValue("toolName", out var name) && (string)name == "uitoolkit/transition-source-review");
            Assert.That(source.TryGetValue("validationError", out var error) ? error : "", Is.Null.Or.Empty);
            Assert.That(VmProjectToolRegistry.GetToolDetails(true).Any(value =>
                value.TryGetValue("toolName", out var name) && (string)name == "uitoolkit/transition-source-review"), Is.True);
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

        [UnityTest]
        public IEnumerator RepeatedCallsRetainLocationsAndDistinctOverloads()
        {
            Write("A.cs", "class FixtureA { void Apply() { FixtureB.Show(1); FixtureB.Show(2); FixtureB.Show(\"x\"); } }");
            Write("B.cs", "using UnityEngine.UIElements; class FixtureB { public static void Show(int value) { new VisualElement().style.display = DisplayStyle.None; } public static void Show(string value) { } }");
            var task = Start(relative + "/A.cs"); yield return Join(task);
            var report = task.Result;
            var calls = report.Edges.Where(edge => edge.Kind == "Call" && edge.ToOwner == "FixtureB").ToArray();
            Assert.That(calls.Length, Is.EqualTo(2));
            Assert.That(calls.Select(edge => edge.To).Distinct().Count(), Is.EqualTo(2));
            Assert.That(calls.SelectMany(edge => edge.Locations).Count(), Is.EqualTo(3));
            Assert.That(calls.SelectMany(edge => edge.Locations).Select(location => location.Site).Distinct().Count(), Is.EqualTo(3));
            Assert.That(report.SelectedPaths, Does.Contain(relative + "/B.cs"));
            Assert.That(report.SelectedPassed, Is.False);
        }

        [UnityTest]
        public IEnumerator PublicationCallbackDoesNotRegisterAnUnrelatedOverload()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { VisualElement x; void Publish() { x.Clear(); } void Publish(int value) { x.Clear(); } void Apply() { UiPresentation.Replace(x, 1, Publish, \"x\"); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            var mutations = report.Sites.Where(site => site.Kind == "HierarchyMutation").ToArray();
            Assert.That(mutations.Length, Is.EqualTo(2));
            Assert.That(mutations.Count(site => site.Registered), Is.EqualTo(1));
            Assert.That(report.Edges.Single(edge => edge.Kind == "PublicationCallback").To,
                Is.EqualTo(mutations.Single(site => site.Registered).CallerSymbol));
        }

        [UnityTest]
        public IEnumerator DistinctAnonymousCallersDoNotMergeTheirLocations()
        {
            Write("A.cs", "using System; class FixtureA { void Apply() { Action a = () => Console.WriteLine(1); Action b = () => Console.WriteLine(2); a(); b(); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            var calls = report.Edges.Where(edge => edge.Kind == "Call" && edge.ToOwner == "System.Console").ToArray();
            Assert.That(calls.Length, Is.EqualTo(2));
            Assert.That(calls.Select(edge => edge.From).Distinct().Count(), Is.EqualTo(2));
            Assert.That(calls.All(edge => edge.Locations.Count == 1), Is.True);
        }

        [UnityTest]
        public IEnumerator CompilerNameOfIsAnIntrinsicRatherThanAnUnresolvedCall()
        {
            Write("A.cs", "class FixtureA { string Name() => nameof(FixtureA); }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.CompilerIntrinsicCalls, Is.EqualTo(1));
            Assert.That(report.UnresolvedCallSites, Is.Empty);
            Assert.That(report.CallGraphComplete, Is.True);
            Assert.That(report.Passed, Is.True);
        }

        [UnityTest]
        public IEnumerator UnresolvedCallKeepsItsExactSiteAndRejectsIncompleteCoverage()
        {
            Write("A.cs", "class FixtureA { void Apply() { Missing.Call(); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.UnresolvedCallSites.Single().Expression, Is.EqualTo("Missing.Call()"));
            Assert.That(report.UnresolvedCalls, Is.EqualTo(1));
            Assert.That(report.CallGraphComplete, Is.False);
            Assert.That(report.Passed, Is.False);
        }

        private void Write(string name, string text) => File.WriteAllText(Path.Combine(root, name), text);

        [UnityTest]
        public IEnumerator NativeOwningCompilationBindsASourceValuePassedToItsCompiledSibling()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VmUiSourceContextProducer).Assembly);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] files = new[] { "VmUiSourceContextProducer.cs", "VmUiSourceContextValue.cs" }.Select(file =>
            {
                string path = Path.GetFullPath(Path.Combine(package.resolvedPath, "Tests", "Editor", file));
                Assert.That(path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
                return path.Substring(project.Length + 1).Replace('\\', '/');
            }).ToArray();
            var control = VmUiTransitionSourceReview.Start(new VmUiTransitionSourceRequest
            { SourceRoots = files, SemanticMode = VmUiTransitionSemanticMode.DeclaredFixture }, CancellationToken.None);
            yield return Join(control);
            Assert.That(control.Result.UnresolvedCalls, Is.GreaterThan(0), "The matched control must retain the source/metadata identity mismatch.");
            var task = VmUiTransitionSourceReview.Start(new VmUiTransitionSourceRequest { SourceRoots = files }, CancellationToken.None);
            yield return Join(task);
            var report = task.Result;
            Assert.That(report.Passed, Is.True, string.Join("\n", report.Issues));
            Assert.That(report.UnresolvedCalls, Is.Zero);
            Assert.That(report.ScannedFiles, Is.EqualTo(2));
            Assert.That(report.SemanticSourceFiles, Is.GreaterThan(2));
            Assert.That(report.SemanticAssemblies, Is.EquivalentTo(new[] { typeof(VmUiSourceContextProducer).Assembly.GetName().Name }));
            Assert.That(report.SemanticMode, Is.EqualTo("NativeAssemblies"));
            Assert.That(report.Edges.Single(edge => edge.Kind == "Call").ToOwner, Is.EqualTo(typeof(VmUiSourceContextConsumer).FullName));
        }

        [UnityTest]
        public IEnumerator NativeModeRejectsUncompiledSourcesWithoutAStandaloneFallback()
        {
            Write("A.cs", "class UncompiledFixture { }");
            var task = VmUiTransitionSourceReview.Start(new VmUiTransitionSourceRequest { SourceRoots = new[] { relative } }, CancellationToken.None);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.GetBaseException().Message, Does.Contain("no native owning compilation"));
        }

        private Task<VmUiTransitionSourceReport> Start(params string[] affected) => VmUiTransitionSourceReview.Start(
            new VmUiTransitionSourceRequest { SourceRoots = new[] { relative }, AffectedPaths = affected,
                SemanticMode = VmUiTransitionSemanticMode.DeclaredFixture }, CancellationToken.None);
        private static IEnumerator Join(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
    }
}
