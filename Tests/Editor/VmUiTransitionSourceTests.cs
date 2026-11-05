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
        public IEnumerator NativeHierarchyMutationCannotEscapeInventory()
        {
            Write("A.cs", "using UnityEngine.UIElements; class FixtureA { void Apply(VisualElement x) { x.hierarchy.Add(new Label()); x.hierarchy.Insert(0, new Label()); x.hierarchy.Clear(); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.False);
            Assert.That(task.Result.Sites.Count(site => site.Kind == "HierarchyMutation" && !site.Registered), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator NativeTaskDriverOwnsItsBoundCallbackAndRejectsUnownedBypass()
        {
            const string driver = "using System; using System.Threading.Tasks; using UnityEngine.UIElements; using VM233.UIElements; [UiTransitionDriver(nameof(Progress),nameof(Stop))] class Driver { float Progress => .5f; void Stop() {} public Task Run(Action publish) { publish(); return Task.CompletedTask; } } class FixtureA { VisualElement x; void Publish(){ x.hierarchy.Add(new Label()); } void Apply(Driver driver){ driver.Run(Publish); } ";
            Write("A.cs", driver + "}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "NativeDriverCallback"), Is.True);
            Write("A.cs", driver + "void Bypass(){ Publish(); } }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator NativeCallbacksRequireTaskLifetimeAndValidDriverMembers()
        {
            const string declarations = "using System; using System.Threading.Tasks; using UnityEngine.UIElements; using VM233.UIElements; ";
            Write("A.cs", declarations + "[UiTransitionDriver(nameof(Progress),nameof(Stop))] class Driver { float Progress => .5f; void Stop() {} public void Run(Action publish){publish();} } class FixtureA { void Apply(Driver driver,VisualElement x){driver.Run(()=>x.Clear());} }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
            Write("A.cs", declarations + "[UiTransitionDriver(\"Missing\",nameof(Stop))] class Driver { void Stop() {} public Task Run(Action publish){publish();return Task.CompletedTask;} } class FixtureA { void Apply(Driver driver,VisualElement x){driver.Run(()=>x.Clear());} }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "NativeDriverInvocation").Registered, Is.False);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator NativeTaskDriverCanOwnAnAwaitedAsynchronousCallback()
        {
            Write("A.cs", "using System; using System.Threading.Tasks; using UnityEngine.UIElements; using VM233.UIElements; [UiTransitionDriver(nameof(Progress),nameof(Stop))] class Driver { float Progress=>.5f; void Stop(){} public async Task Run(Func<Task> publish){await publish();} } class FixtureA { void Apply(Driver driver,VisualElement x){driver.Run(async()=>{await Task.Yield();x.Clear();});} }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
        }

        [UnityTest]
        public IEnumerator NativeTaskDriverCannotOwnAsyncVoidOrADeferredBypass()
        {
            const string driver = "using System; using System.Threading.Tasks; using UnityEngine.UIElements; using VM233.UIElements; [UiTransitionDriver(nameof(Progress),nameof(Stop))] class Driver { float Progress=>.5f; void Stop(){} public Task Run(Action publish){publish();return Task.CompletedTask;} } ";
            Write("A.cs", driver + "class FixtureA { void Apply(Driver driver,VisualElement x){driver.Run(async()=>{await Task.Yield();x.Clear();});} }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "NativeDriverInvocation").Registered, Is.False);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
            Write("A.cs", driver + "class FixtureA { VisualElement x; Action later; FixtureA(){later=Publish;} void Publish(){x.Clear();} void Apply(Driver driver){driver.Run(Publish);} }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator TaskFactoryDoesNotQualifyATaskLifetimeOrRetirement()
        {
            const string declarations = "using System;using System.Threading.Tasks;using UnityEngine.UIElements;using VM233.UIElements;";
            Write("A.cs", declarations + "[UiTransitionDriver(nameof(Progress),nameof(Stop))]class Driver{float Progress=>.5f;void Stop(){}public TaskFactory Run(Action publish){publish();return Task.Factory;}}class FixtureA{void Apply(Driver driver,VisualElement x){driver.Run(()=>x.Clear());}}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
            Write("A.cs", declarations + "[UiTransitionDriver(nameof(Progress),nameof(Completion))]class FixtureA{float Progress=>.5f;TaskFactory Completion=>Task.Factory;void Apply(VisualElement x){x.Clear();}}");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
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
            Assert.That(report.Edges.Count(edge => edge.Kind == "StateTransition"),
                Is.EqualTo(report.RuntimeProtocol.Visibility.Length + report.RuntimeProtocol.Content.Length));
            Assert.That(report.RuntimeProtocol.Content.Any(arc => arc.To == "WaitingReplacementLayout"), Is.True);
            Assert.That(report.RuntimeProtocol.Content.Any(arc => arc.To == "Replacing"), Is.True);
            Assert.That(report.Edges.Any(edge => edge.Kind == "StateTransition" && edge.To.EndsWith("/incoming", StringComparison.Ordinal)), Is.False);
            Assert.That(report.RuntimeAccepted, Is.False);
        }

        [UnityTest]
        public IEnumerator LayoutVisibilityUsesTheActualRuntimeProtocolAndTarget()
        {
            Write("A.cs", "using UnityEngine.UIElements;using VM233.UIElements;class FixtureA{void Apply(VisualElement x){UiPresentation.Visibility(x,Visibility.Hidden,\"x\");UiPresentation.Visibility(x,StyleKeyword.Null,\"wrong\");}}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "Visibility").Registered, Is.True);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "MismatchedSourceTarget").Registered, Is.False);
            Assert.That(task.Result.Passed, Is.False);
        }

        [UnityTest]
        public IEnumerator ActualScopeConstructionOwnsItsConcreteRendererAndRejectsBypass()
        {
            const string renderer = "using System;using System.Threading;using System.Threading.Tasks;using UnityEngine;using UnityEngine.UIElements;using VM233.UIElements;class Renderer:IUiTransitionRenderer{public Task<IUiTransitionImage> CaptureAsync(VisualElement root,Func<Rect> bounds,CancellationToken token){root.Add(new Label());return Task.FromResult<IUiTransitionImage>(null);}public Task WaitForRenderedFrameAsync(CancellationToken token)=>Task.CompletedTask;}class FixtureA{void Bind(VisualElement root){new UiTransitionScope(root,()=>.16f,()=>10,()=>0,new Renderer());}";
            Write("A.cs", renderer + "}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Edges.Count(edge => edge.Kind == "RendererCallback"), Is.EqualTo(2));
            Assert.That(task.Result.Sites.Single(site => site.Kind == "TransitionRendererBinding").Registered, Is.True);
            Write("A.cs", renderer + "void Bypass(VisualElement root){new Renderer().CaptureAsync(root,()=>default,CancellationToken.None);}}");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator AnUnboundRendererOrSameNamedScopeCannotConferOwnership()
        {
            const string renderer = "using System;using System.Threading;using System.Threading.Tasks;using UnityEngine;using UnityEngine.UIElements;using VM233.UIElements;class Renderer:IUiTransitionRenderer{public Task<IUiTransitionImage> CaptureAsync(VisualElement root,Func<Rect> bounds,CancellationToken token){root.Add(new Label());return Task.FromResult<IUiTransitionImage>(null);}public Task WaitForRenderedFrameAsync(CancellationToken token)=>Task.CompletedTask;}";
            Write("A.cs", renderer);
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
            Write("A.cs", renderer + "namespace VM233.UIElements{class UiTransitionScope{public UiTransitionScope(VisualElement root,IUiTransitionRenderer renderer){}}}class FixtureA{void Bind(VisualElement root){new UiTransitionScope(root,new Renderer());}}");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "RendererCallback"), Is.False);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
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
        public IEnumerator UnpublishedHierarchyHelpersRetainTheirActualSourceTargets()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { void Init(VisualElement x) { UiPresentation.Clear(x, \"x\"); UiPresentation.Remove(x, \"x\"); UiPresentation.Clear(x, \"wrong\"); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.Sites.Count(site => site.Registered), Is.EqualTo(2));
            Assert.That(report.Sites.Single(site => site.Kind == "MismatchedSourceTarget").Registered, Is.False);
            Assert.That(report.Passed, Is.False);
        }

        [UnityTest]
        public IEnumerator NativeDriverRequiresRealPhaseAndRetirementMembers()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; [UiTransitionDriver(nameof(Progress), nameof(Stop))] class FixtureA { float Progress => .5f; void Stop() { } void Apply(VisualElement x) { x.style.display = DisplayStyle.None; } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            var site = report.Sites.Single();
            Assert.That(site.Registered, Is.True, site.RegistrationIssue);
            Assert.That(site.DriverPhaseMember, Does.Contain("Progress"));
            Assert.That(site.DriverRetirementMember, Does.Contain("Stop"));
            Assert.That(report.Passed, Is.True, string.Join("\n", report.Issues));
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; [UiTransitionDriver(\"Missing\", nameof(Stop))] class FixtureA { void Stop(int value) { } void Apply(VisualElement x) { x.style.display = DisplayStyle.None; } }");
            task = Start(); yield return Join(task);
            site = task.Result.Sites.Single();
            Assert.That(site.Registered, Is.False);
            Assert.That(site.RegistrationIssue, Does.Contain("resolve"));
            Assert.That(task.Result.Passed, Is.False);
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
        public IEnumerator PublicationTraversesRealSynchronousHelpersAndRejectsABypass()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { VisualElement x; void Rows() { x.Clear(); x.Add(new Label()); } void Publish() { Rows(); } void Apply() { UiPresentation.Replace(x, 1, () => Publish(), \"x\"); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Sites.Count(site => site.Kind == "HierarchyMutation" && site.Registered), Is.EqualTo(2));
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { VisualElement x; void Rows() { x.Clear(); } void Publish() { Rows(); } void Apply() { UiPresentation.Replace(x, 1, Publish, \"x\"); } void Bypass() { Rows(); } }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator DeferredMethodGroupCannotBorrowACapturedPublicationContext()
        {
            Write("A.cs", "using System; using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { VisualElement x; Action later; FixtureA() { later = Publish; } void Publish() { x.Clear(); } void Apply() { UiPresentation.Replace(x, 1, Publish, \"x\"); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "DeferredCallback"), Is.True);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator ConstructorLambdasAreNotSynchronousInitialMutation()
        {
            Write("A.cs", "using System; using UnityEngine.UIElements; class FixtureA { Action later; FixtureA(VisualElement x) { later = () => x.style.display = DisplayStyle.None; } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single().Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator CheckedInitializationFollowsActualOverridesAndRejectsADirectBypass()
        {
            const string declarations = "using UnityEngine.UIElements; using VM233.UIElements; abstract class FixtureBase { protected abstract void Bind(VisualElement x); public void Initialize(VisualElement x) { UiPresentation.Initialize(x, () => Bind(x), \"x\"); } } class FixtureChild : FixtureBase { protected override void Bind(VisualElement x) { Rows(x); } void Rows(VisualElement x) { x.Clear(); x.Add(new Label()); } ";
            Write("A.cs", declarations + "}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Edges.Count(edge => edge.Kind == "VirtualDispatch"), Is.EqualTo(1));
            Assert.That(task.Result.Sites.Count(site => site.Kind == "HierarchyMutation" && site.Registered), Is.EqualTo(2));
            Write("A.cs", declarations + "void Bypass(VisualElement x) { Rows(x); } }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.False);
            Assert.That(task.Result.Sites.Where(site => site.Kind == "HierarchyMutation").All(site => !site.Registered), Is.True);
        }

        [UnityTest]
        public IEnumerator AsyncCallbacksAndAsyncHelpersCannotBorrowPublicationCoverage()
        {
            Write("A.cs", "using System.Threading.Tasks; using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { VisualElement x; void Apply() { UiPresentation.Initialize(x, async () => { await Task.Yield(); x.Clear(); }, \"x\"); UiPresentation.Replace(x, 1, () => Later(), \"x\"); } async void Later() { await Task.Yield(); x.Add(new Label()); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.False);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "Initialization").Registered, Is.False);
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "AsyncCall"), Is.True);
            Assert.That(task.Result.Sites.Where(site => site.Kind == "HierarchyMutation").All(site => !site.Registered), Is.True);
        }

        [UnityTest]
        public IEnumerator CheckedInterfaceBindingRejectsAnImplementationBypass()
        {
            const string declarations = "using UnityEngine.UIElements; using VM233.UIElements; interface IFixture { void Bind(VisualElement x); } class FixtureChild : IFixture { public void Bind(VisualElement x) { x.Clear(); } } class FixtureOwner { void Initialize(VisualElement x, IFixture consumer) { UiPresentation.Initialize(x, () => consumer.Bind(x), \"x\"); } ";
            Write("A.cs", declarations + "}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "InterfaceDispatch"), Is.True);
            Write("A.cs", declarations + "void Bypass(VisualElement x, FixtureChild consumer) { consumer.Bind(x); } }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Single(site => site.Kind == "HierarchyMutation").Registered, Is.False);
        }

        [UnityTest]
        public IEnumerator UncalledInheritedInterfaceMembersDoNotConsumeDispatchResolutions()
        {
            string members = string.Concat(Enumerable.Range(0, 150).Select(index => "void M" + index + "() {} "));
            string types = string.Concat(Enumerable.Range(0, 100).Select(index => "class Fixture" + index + ": IUnused {} "));
            Write("A.cs", "interface IUnused { " + members + "} " + types);
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "InterfaceDispatch"), Is.False);
        }

        [UnityTest]
        public IEnumerator CalledGenericInterfaceBindsItsInheritedImplementation()
        {
            Write("A.cs", "using UnityEngine.UIElements;using VM233.UIElements;interface IFixture<T>{void Bind(VisualElement x,T value);}class FixtureBase{public void Bind(VisualElement x,int value){x.Clear();}}class FixtureChild:FixtureBase,IFixture<int>{}class FixtureOwner{void Init(VisualElement x,IFixture<int> consumer){UiPresentation.Initialize(x,()=>consumer.Bind(x,1),\"x\");}}");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
            Assert.That(task.Result.Edges.Any(edge => edge.Kind == "InterfaceDispatch" && edge.ToOwner == "FixtureBase"), Is.True);
        }

        [UnityTest]
        public IEnumerator ConstructionDoesNotAuthorizeMutationOfAnExternalPresentedHost()
        {
            Write("A.cs", "using UnityEngine.UIElements; class FixtureA { FixtureA(VisualElement existing) { existing.Clear(); } } class FixtureElement : VisualElement { public FixtureElement() { Add(new Label()); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.False);
            Assert.That(task.Result.Sites.Single(site => site.Owner == "FixtureA").Registered, Is.False);
            Assert.That(task.Result.Sites.Single(site => site.Owner == "FixtureElement").Registered, Is.True);
        }

        [UnityTest]
        public IEnumerator NativeParameterIdentityAcceptsReorderedNamedHelperArguments()
        {
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { void Apply(VisualElement x) { UiPresentation.Replace(publish: () => x.Clear(), identity: 1, targetExpression: \"x\", element: x); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
        }

        [UnityTest]
        public IEnumerator ActualStyleOwnersDistinguishPresentationFromColourClasses()
        {
            Write("A.uss", ".closed { display: none; } .colour { color: red; }");
            Write("A.cs", "using UnityEngine.UIElements; class FixtureA { void Apply(VisualElement x) { x.EnableInClassList(\"closed\", true); x.EnableInClassList(\"colour\", true); } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.StyleSources, Has.Length.EqualTo(1));
            var site = task.Result.Sites.Single();
            Assert.That(site.Kind, Is.EqualTo("PresentationClassChange"));
            Assert.That(site.StyleSources.Single(), Does.EndWith("A.uss:1"));
            Assert.That(site.Registered, Is.False);
            Write("A.cs", "using UnityEngine.UIElements; using VM233.UIElements; class FixtureA { void Apply(VisualElement x) { UiPresentation.Replace(x, 1, () => x.EnableInClassList(\"closed\", true), \"x\"); } }");
            task = Start(); yield return Join(task);
            Assert.That(task.Result.Passed, Is.True, string.Join("\n", task.Result.Issues));
        }

        [UnityTest]
        public IEnumerator ActualDocumentEnableAndRootReplacementRemainLifecycleSites()
        {
            Write("A.cs", "using UnityEngine.UIElements; class FixtureA { void Apply(UIDocument x) { x.enabled = false; x.visualTreeAsset = null; } }");
            var task = Start(); yield return Join(task);
            Assert.That(task.Result.Sites.Count(site => site.Kind == "DocumentLifecycle" && !site.Registered), Is.EqualTo(2));
            Assert.That(task.Result.Passed, Is.False);
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
        public IEnumerator ArrayAndFieldDelegatesBindTheirActualInvokeMethod()
        {
            Write("A.cs", "using System; class FixtureA { Action[] commands; Action command; void Apply() { commands[0](); command(); } }");
            var task = Start(); yield return Join(task);
            var report = task.Result;
            Assert.That(report.Passed, Is.True, string.Join("\n", report.Issues));
            Assert.That(report.UnresolvedCalls, Is.Zero);
            Assert.That(report.DelegateCallSites.Count, Is.EqualTo(2));
            var call = report.Edges.Single(edge => edge.Kind == "Call");
            Assert.That(call.ToOwner, Is.EqualTo("System.Action"));
            Assert.That(call.To, Does.Contain("Invoke()"));
            Assert.That(call.Locations.Count, Is.EqualTo(2));
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
            new VmUiTransitionSourceRequest { SourceRoots = new[] { relative }, StyleRoots = new[] { relative }, AffectedPaths = affected,
                SemanticMode = VmUiTransitionSemanticMode.DeclaredFixture }, CancellationToken.None);
        private static IEnumerator Join(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }
    }
}
