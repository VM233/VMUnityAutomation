#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationPolicyReviewTests
    {
        private const string FixturePath = "Assets/__VmCodePolicyReviewFixture.cs";

        [Test]
        public void IncrementalCompilerPublicationMatchesSynchronousOwner()
        {
#if UNITY_6000_6_OR_NEWER
            bool published = VmAutomationEditorCommands.TryGetPublishedRoslynAssemblies(out _, out _);
            var interrupted = VmAutomationEditorCommands.PublishRoslynAssemblies();
            try { Assert.That(interrupted.MoveNext(), Is.True); }
            finally { (interrupted as IDisposable)?.Dispose(); }
            Assert.That(VmAutomationEditorCommands.TryGetPublishedRoslynAssemblies(out _, out _), Is.EqualTo(published),
                "Disposing the core-only stage cannot publish a partial compiler pair.");
#endif
            var publication = VmAutomationEditorCommands.PublishRoslynAssemblies();
            var assemblies = new List<System.Reflection.Assembly>();
            try
            {
                while (publication.MoveNext()) assemblies.Add((System.Reflection.Assembly)publication.Current);
            }
            finally { (publication as IDisposable)?.Dispose(); }
            Assert.That(assemblies.Count, Is.EqualTo(2));
            Assert.That(assemblies[0].GetName().Name, Is.EqualTo("Microsoft.CodeAnalysis"));
            Assert.That(assemblies[1].GetName().Name, Is.EqualTo("Microsoft.CodeAnalysis.CSharp"));
            Assert.That(VmAutomationEditorCommands.TryGetRoslynAssemblies(out var csharp, out var core), Is.True);
            Assert.That(assemblies[0], Is.SameAs(core));
            Assert.That(assemblies[1], Is.SameAs(csharp));
            Assert.That(VmAutomationEditorCommands.TryGetPublishedRoslynAssemblies(out var publishedCSharp, out var publishedCore), Is.True);
            Assert.That(publishedCore, Is.SameAs(core));
            Assert.That(publishedCSharp, Is.SameAs(csharp));
        }

        [Test]
        public void PublishedCompilerBindsNativeAssemblyMetadata()
        {
            Assert.That(VmAutomationEditorCommands.TryGetRoslynAssemblies(out var csharp, out var core), Is.True);
            var referenceType = core.GetType("Microsoft.CodeAnalysis.MetadataReference", true);
            var properties = core.GetType("Microsoft.CodeAnalysis.MetadataReferenceProperties", true);
#if UNITY_6000_5_OR_NEWER
            string path = typeof(object).Assembly.GetLoadedAssemblyPath();
#else
            string path = typeof(object).Assembly.Location;
#endif
            var reference = referenceType.GetMethod("CreateFromFile", new[]
                { typeof(string), properties, core.GetType("Microsoft.CodeAnalysis.DocumentationProvider", true) })
                .Invoke(null, new[] { (object)path, Activator.CreateInstance(properties), null });
            var references = Array.CreateInstance(referenceType, 1);
            references.SetValue(reference, 0);
            var compilation = csharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation", true)
                .GetMethods().Single(method => method.Name == "Create" && method.GetParameters().Length == 4)
                .Invoke(null, new object[] { "Native Metadata Binding", null, references, null });
            var symbol = core.GetType("Microsoft.CodeAnalysis.Compilation", true).GetProperty("Assembly").GetValue(compilation);
            Assert.That(core.GetType("Microsoft.CodeAnalysis.ISymbol", true).GetProperty("Name").GetValue(symbol),
                Is.EqualTo("Native Metadata Binding"));
        }

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

        [TestCase("editor")]
        [TestCase("native-version")]
        [TestCase("inactive-player")]
        public void CodePolicyUsesNativeEditorPreprocessorContext(string context)
        {
            string[] symbols = UnityEditor.EditorUserBuildSettings.activeScriptCompilationDefines;
            Assert.That(VmAutomationEditorCommands.TryGetRoslynAssemblies(out var csharp, out _), Is.True);
            object options = VmAutomationEditorCommands.CreateRoslynParseOptions(csharp);
            var actualSymbols = ((IEnumerable)options.GetType().GetProperty("PreprocessorSymbolNames")
                .GetValue(options)).Cast<string>().ToArray();
            Assert.That(actualSymbols, Is.EqualTo(symbols));
            string symbol = context == "native-version"
                ? symbols.First(value => value.StartsWith("UNITY_", StringComparison.Ordinal) &&
                                         value.EndsWith("_OR_NEWER", StringComparison.Ordinal))
                : "UNITY_EDITOR";
            string source = context == "inactive-player"
                ? "#if UNITY_EDITOR\npublic class EditorType {}\n#else\npublic partial class First {}\npublic class Second {}\n#endif\n"
                : $"#if {symbol}\npublic partial class First {{}}\npublic class Second {{}}\n#endif\n";
            string fullPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), FixturePath);
            File.WriteAllText(fullPath, source);

            var result = (Dictionary<string, object>)VmAutomationCodePolicyReviewCommands.Review(
                new Dictionary<string, object> { { "paths", new List<object> { FixturePath } },
                    { "forbidPartial", true }, { "maxIssues", 100 } });

            Assert.That(result["success"], Is.True);
            Assert.That(result["passed"], Is.EqualTo(context == "inactive-player"));
            var rules = ((IEnumerable)result["issues"]).Cast<Dictionary<string, object>>()
                .Select(issue => issue["rule"].ToString()).ToArray();
            if (context != "inactive-player")
            {
                Assert.That(rules, Does.Contain("one-top-level-type-per-file"));
                Assert.That(rules, Does.Contain("partial-type"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CodePolicyExecutorPublishesDeclaredExecutionSuccess(bool hasPolicyIssue)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            File.WriteAllText(Path.Combine(projectRoot, FixturePath),
                hasPolicyIssue ? "public class First {}\npublic class Second {}\n" :
                    "public class First {}\n");

            VmAutomationInvocationResult invocation = await VmAutomationExecutor.ExecuteAsync(
                "code/policy-review", new Dictionary<string, object>
                {
                    { "paths", new List<object> { FixturePath } },
                    { "forbidPartial", true }
                }, Guid.NewGuid().ToString("N"));
            Assert.That(invocation.Ok, Is.True);
            var result = (Dictionary<string, object>)invocation.Result;
            Assert.That(result["success"], Is.True);
            Assert.That(result["passed"], Is.EqualTo(!hasPolicyIssue));
            Assert.That(result["scannedFiles"], Is.EqualTo(1));
            Assert.That((IEnumerable)result["errors"], Is.Empty);

            Assert.That(VmAutomationCatalog.TryGetTool("code/policy-review", true,
                out Dictionary<string, object> metadata), Is.True);
            var schema = (Dictionary<string, object>)metadata["outputSchema"];
            Assert.That((Dictionary<string, object>)schema["properties"], Contains.Key("success"));
            Assert.That((IEnumerable)schema["required"], Contains.Item("success"));
        }

        [TestCase("Unity.Entities.SystemBase", true)]
        [TestCase("global::Unity.Entities.SystemBase", true)]
        [TestCase("SystemBase", false)]
        [TestCase("Other.SystemBase", false)]
        public void CodePolicyDistinguishesEntitiesGenerationFromOrdinaryPartial(string baseType, bool allowed)
        {
            string fullPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), FixturePath);
            File.WriteAllText(fullPath, $"public partial class Example : {baseType} {{}}\n");
            var result = (Dictionary<string, object>)VmAutomationCodePolicyReviewCommands.Review(
                new Dictionary<string, object> { { "paths", new List<object> { FixturePath } }, { "forbidPartial", true } });
            Assert.That(result["success"], Is.True);
            Assert.That(result["passed"], Is.EqualTo(allowed));
            Assert.That(result["issueCount"], Is.EqualTo(allowed ? 0 : 1));
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
