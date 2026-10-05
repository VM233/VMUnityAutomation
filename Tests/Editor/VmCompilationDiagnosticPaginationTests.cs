using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmCompilationDiagnosticPaginationTests
    {
        private const string SessionPrefix = "VMUnityAutomation.CompilationDiagnostics.";
        private static readonly string[] StateFields =
        {
            "_currentCompilationLogStartOffset", "_currentCompilationCaptureComplete",
            "_currentCompilationCaptureIssue", "_lastCompilationCaptureComplete",
            "_lastCompilationCaptureIssue", "_compilationSnapshotRevision",
        };
        private static readonly string[] SessionStrings =
        {
            SessionPrefix + "v1", SessionPrefix + "CaptureIssue.v2",
            SessionPrefix + "Revision.v1",
        };
        private readonly Dictionary<string, object> _savedFields = new();
        private readonly Dictionary<string, string> _savedSession = new();
        private ArrayList _published;
        private Hashtable _pending;
        private bool _captureComplete;

        [SetUp]
        public void PreserveOwnerProduct()
        {
            _published = new ArrayList((ICollection)Field("_compilationErrors").GetValue(null));
            _pending = new Hashtable((IDictionary)Field("_currentCompilationErrors").GetValue(null));
            foreach (string name in StateFields)
                _savedFields.Add(name, Field(name).GetValue(null));
            foreach (string key in SessionStrings)
                _savedSession.Add(key, SessionState.GetString(key, ""));
            _captureComplete = SessionState.GetBool(SessionPrefix + "CaptureComplete.v2", false);
            ((IList)Field("_compilationErrors").GetValue(null)).Clear();
            ((IDictionary)Field("_currentCompilationErrors").GetValue(null)).Clear();
            Field("_currentCompilationLogStartOffset").SetValue(null, -1L);
            Field("_currentCompilationCaptureComplete").SetValue(null, true);
            Field("_currentCompilationCaptureIssue").SetValue(null, "");
        }

        [TearDown]
        public void RestoreOwnerProduct()
        {
            var published = (IList)Field("_compilationErrors").GetValue(null);
            published.Clear();
            foreach (object entry in _published)
                published.Add(entry);
            var pending = (IDictionary)Field("_currentCompilationErrors").GetValue(null);
            pending.Clear();
            foreach (DictionaryEntry entry in _pending)
                pending.Add(entry.Key, entry.Value);
            foreach (var pair in _savedFields)
                Field(pair.Key).SetValue(null, pair.Value);
            foreach (var pair in _savedSession)
                SessionState.SetString(pair.Key, pair.Value);
            SessionState.SetBool(SessionPrefix + "CaptureComplete.v2", _captureComplete);
            _savedFields.Clear();
            _savedSession.Clear();
        }

        [Test]
        public void MoreThanTwoPagesExposeEveryNativeDiagnosticExactlyOnce()
        {
            Publish(425);
            Dictionary<string, object> page = Query(200);
            string revision = (string)page["snapshotRevision"];
            var lines = new HashSet<int>();
            var obsoleteLines = new HashSet<int>();
            int pageCount = 0;
            while (true)
            {
                pageCount++;
                Assert.That(page["entryTotal"], Is.EqualTo(425));
                Assert.That(page["deprecatedWarningTotal"], Is.EqualTo(425));
                Assert.That(page["snapshotRevision"], Is.EqualTo(revision));
                foreach (var entry in Entries(page, "entries"))
                    Assert.That(lines.Add((int)entry["line"]), Is.True);
                foreach (var entry in Entries(page, "deprecatedWarnings"))
                    Assert.That(obsoleteLines.Add((int)entry["line"]), Is.True);
                if (!(bool)page["truncated"])
                    break;
                Assert.That(pageCount, Is.LessThan(3));
                page = Query(200, (int)page["nextOffset"],
                    (int)page["nextDeprecatedOffset"], revision);
            }
            Assert.That(pageCount, Is.EqualTo(3));
            Assert.That(lines.Count, Is.EqualTo(425));
            Assert.That(obsoleteLines, Is.EquivalentTo(lines));
            Assert.That(page.ContainsKey("nextOffset"), Is.False);
            Assert.That(page.ContainsKey("nextDeprecatedOffset"), Is.False);
        }

        [Test]
        public void DefaultsFilteringAndOutOfRangeOffsetsKeepWholeProductCounts()
        {
            Publish(65, firstError: true);
            var page = (Dictionary<string, object>)VmAutomationConsoleCommands.GetCompilationErrors(new());
            Assert.That(page["count"], Is.EqualTo(50));
            Assert.That(Entries(page, "entries").Count, Is.EqualTo(50));
            Assert.That(Entries(page, "entries")[0]["line"], Is.EqualTo(16));
            var errors = (Dictionary<string, object>)VmAutomationConsoleCommands.GetCompilationErrors(
                new() { ["severity"] = "error" });
            Assert.That(Entries(errors, "entries").Count, Is.EqualTo(1));
            Assert.That(errors["entryTotal"], Is.EqualTo(1));
            Assert.That(((Dictionary<string, object>)errors["counts"])["warnings"], Is.EqualTo(64));
            var beyond = Query(200, int.MaxValue, int.MaxValue, (string)page["snapshotRevision"]);
            Assert.That(Entries(beyond, "entries"), Is.Empty);
            Assert.That(Entries(beyond, "deprecatedWarnings"), Is.Empty);
            Assert.That(beyond["truncated"], Is.False);
        }

        [Test]
        public void ObsoletePagesAdvanceIndependentlyOfACompletedSeverityPage()
        {
            Publish(225, firstError: true);
            var first = (Dictionary<string, object>)VmAutomationConsoleCommands.GetCompilationErrors(
                new() { ["count"] = 200, ["severity"] = "error" });
            Assert.That(first.ContainsKey("nextOffset"), Is.False);
            Assert.That(first["nextDeprecatedOffset"], Is.EqualTo(200));
            var next = (Dictionary<string, object>)VmAutomationConsoleCommands.GetCompilationErrors(
                new() { ["count"] = 200, ["severity"] = "error", ["offset"] = 1,
                    ["deprecatedOffset"] = first["nextDeprecatedOffset"],
                    ["snapshotRevision"] = first["snapshotRevision"] });
            Assert.That(Entries(next, "entries"), Is.Empty);
            Assert.That(Entries(next, "deprecatedWarnings").Count, Is.EqualTo(24));
            Assert.That(next["truncated"], Is.False);
        }

        [Test]
        public void NewPublicationRejectsContinuationOfTheOldSnapshot()
        {
            Publish(205);
            string revision = (string)Query(200)["snapshotRevision"];
            Publish(206);
            var error = Query(200, 200, 200, revision);
            Assert.That(error["errorCode"], Is.EqualTo("compilation_snapshot_changed"));
        }

        [Test]
        public void RevisionAndContinuationSurviveSessionStateRestoration()
        {
            Publish(225);
            var first = Query(200);
            ((IList)Field("_compilationErrors").GetValue(null)).Clear();
            Field("_compilationSnapshotRevision").SetValue(null, "");
            Invoke("RestoreCompilationDiagnostics");
            var next = Query(200, 200, 200, (string)first["snapshotRevision"]);
            Assert.That(Entries(next, "entries").Count, Is.EqualTo(25));
            Assert.That(Entries(next, "entries")[0]["line"], Is.EqualTo(1));
            Assert.That(next["truncated"], Is.False);
        }

        [Test]
        public void RetentionOverflowCannotPublishCompleteCountsIncludingAfterRestore()
        {
            Publish(1001);
            Assert.That(Query(200)["errorCode"], Is.EqualTo("compilation_diagnostics_incomplete"));
            Assert.That(VmAutomationConsoleCommands.GetCompilationDiagnosticsSummary()["captureComplete"], Is.False);
            Invoke("RestoreCompilationDiagnostics");
            Assert.That(Query(200)["errorCode"], Is.EqualTo("compilation_diagnostics_incomplete"));
        }

        [TestCase("count", 0)]
        [TestCase("count", 201)]
        [TestCase("count", 1.5)]
        [TestCase("count", "20")]
        [TestCase("count", true)]
        [TestCase("count", null)]
        [TestCase("offset", -1)]
        [TestCase("offset", 1)]
        [TestCase("deprecatedOffset", 1)]
        [TestCase("severity", "warn")]
        [TestCase("severity", null)]
        [TestCase("snapshotRevision", "")]
        [TestCase("unknown", 1)]
        public void InvalidQueriesAreRejectedBeforeAccessingTheProduct(string field, object value)
        {
            var result = (Dictionary<string, object>)VmAutomationConsoleCommands.GetCompilationErrors(
                new() { [field] = value });
            Assert.That(result["errorCode"], Is.EqualTo("invalid_arguments"));
        }

        [Test]
        public async Task PublicExecutorUsesTheSamePaginationAndClosedErrorContract()
        {
            Publish(205);
            var first = await VmAutomationExecutor.ExecuteAsync("vm_auto_compilation_errors",
                new Dictionary<string, object> { ["count"] = 200 });
            Assert.That(first.Ok, Is.True, first.Error?.Message);
            var page = (Dictionary<string, object>)first.Result;
            var next = await VmAutomationExecutor.ExecuteAsync("vm_auto_compilation_errors",
                new Dictionary<string, object> { ["offset"] = page["nextOffset"], ["deprecatedOffset"] = page["nextDeprecatedOffset"],
                    ["snapshotRevision"] = page["snapshotRevision"], ["count"] = 200 });
            Assert.That(next.Ok, Is.True, next.Error?.Message);
            Assert.That(((IList)((Dictionary<string, object>)next.Result)["entries"]).Count, Is.EqualTo(5));
            var rejected = await VmAutomationExecutor.ExecuteAsync("vm_auto_compilation_errors",
                new Dictionary<string, object> { ["typo"] = 1 });
            Assert.That(rejected.Ok, Is.False);
            Assert.That(rejected.Error.Code, Is.EqualTo("invalid_arguments"));
            Assert.That(VmAutomationCatalog.TryGetTool("vm_auto_compilation_errors", true, out var contract), Is.True);
            Assert.That((IEnumerable)contract["errorCodes"], Does.Contain("compilation_snapshot_changed"));
            Assert.That((IEnumerable)contract["errorCodes"], Does.Contain("compilation_diagnostics_incomplete"));
        }

        private static void Publish(int count, bool firstError = false)
        {
            var messages = new CompilerMessage[count];
            for (int index = 0; index < count; index++)
                messages[index] = new CompilerMessage
                {
                    file = "Packages/com.vm233.unity-automation/Tests/Editor/VmCompilationDiagnosticPaginationTests.cs",
                    line = index + 1, column = 1,
                    message = firstError && index == 0 ? "error CS1002: fixture diagnostic" :
                        "warning CS0618: fixture obsolete diagnostic",
                    type = firstError && index == 0 ? CompilerMessageType.Error : CompilerMessageType.Warning,
                };
            Invoke("OnAssemblyCompilationFinished",
                "Library/ScriptAssemblies/" + typeof(VmCompilationDiagnosticPaginationTests).Assembly.GetName().Name + ".dll",
                messages);
            Invoke("OnCompilationFinished", new object());
        }

        private static Dictionary<string, object> Query(int count, int offset = 0,
            int deprecatedOffset = 0, string revision = null)
        {
            var arguments = new Dictionary<string, object>
            {
                ["count"] = count, ["offset"] = offset, ["deprecatedOffset"] = deprecatedOffset,
            };
            if (revision != null)
                arguments["snapshotRevision"] = revision;
            return (Dictionary<string, object>)VmAutomationConsoleCommands.GetCompilationErrors(arguments);
        }

        private static List<Dictionary<string, object>> Entries(Dictionary<string, object> page, string field) =>
            (List<Dictionary<string, object>>)page[field];

        private static FieldInfo Field(string name) => typeof(VmAutomationConsoleCommands).GetField(
            name, BindingFlags.Static | BindingFlags.NonPublic);

        private static void Invoke(string name, params object[] arguments) =>
            typeof(VmAutomationConsoleCommands).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, arguments);
    }
}
