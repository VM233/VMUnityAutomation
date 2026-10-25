using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUIBuilderDocumentOwnershipTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedTitleAndFullTypeRequireIdentity(bool useFullType)
        {
            var first = ScriptableObject.CreateInstance<EditorWindow>();
            EditorWindow second = null;
            try
            {
                second = ScriptableObject.CreateInstance<EditorWindow>();
                first.titleContent = new GUIContent("Owned selector fixture");
                second.titleContent = new GUIContent("Owned selector fixture");
                string query = useFullType ? first.GetType().FullName : first.titleContent.text;
                var selected = VmEditorWindowSelection.Select(new[] { second, first }, query, "", "",
                    out string error, out string code);
                Assert.That(selected, Is.Null);
                Assert.That(code, Is.EqualTo("editor_window_ambiguous"));
                Assert.That(error, Does.Contain("2 windows"));
                selected = VmEditorWindowSelection.Select(new[] { first, second }, query, "", "",
                    out error, out code);
                Assert.That(selected, Is.Null);
                Assert.That(code, Is.EqualTo("editor_window_ambiguous"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void ExactIdentityKeepsQueryAndCaptureOnTheSameOwner()
        {
            var first = ScriptableObject.CreateInstance<EditorWindow>();
            EditorWindow second = null;
            try
            {
                second = ScriptableObject.CreateInstance<EditorWindow>();
                first.titleContent = new GUIContent("Same title");
                second.titleContent = new GUIContent("Same title");
                first.rootVisualElement.Add(new Label("First owner"));
                second.rootVisualElement.Add(new Label("Second owner"));
                var args = new Dictionary<string, object> { { "instanceId", VmObjectId.Get(second) } };
                var selected = VmEditorWindowSelection.Resolve(args, out string error, out string code);
                Assert.That(selected, Is.SameAs(second), error);
                Assert.That(code, Is.Empty);
                var result = (Dictionary<string, object>)VmAutomationUIToolkitCommands.GetEditorUITree(args);
                var actual = (Dictionary<string, object>)result["window"];
                Assert.That(actual["instanceId"], Is.EqualTo(VmObjectId.Get(second)));
                var captureArgs = VmAutomationUIBuilderPreviewCommands.BuildScreenshotArguments(args, "owned.png", selected);
                Assert.That(captureArgs["instanceId"], Is.EqualTo(VmObjectId.Get(second)));
                Assert.That(captureArgs.ContainsKey("window"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void ExactIdentityRejectsConflictingTitleInsteadOfChoosingAnotherWindow()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.titleContent = new GUIContent("Actual title");
                var args = new Dictionary<string, object>
                {
                    { "instanceId", VmObjectId.Get(window) }, { "title", "Another title" },
                };
                Assert.That(VmEditorWindowSelection.Resolve(args, out _, out string code), Is.Null);
                Assert.That(code, Is.EqualTo("editor_window_selector_mismatch"));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [TestCase("")]
        [TestCase("0")]
        [TestCase("unknown")]
        public void InvalidIdentityDoesNotUseFocus(string identity)
        {
            Assert.That(VmEditorWindowSelection.Resolve(new Dictionary<string, object>
                { { "instanceId", identity } }, out _, out string code), Is.Null);
            Assert.That(code, Is.EqualTo("invalid_arguments"));
        }

        [Test]
        public void BuilderContractPublishesNativeAdoptionAndScreenshotIdentity()
        {
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("uitoolkit/builder-preview", out var schema), Is.True);
            var fields = (Dictionary<string, object>)schema["properties"];
            var adoption = (Dictionary<string, object>)fields["documentAdoption"];
            var adoptedFields = (Dictionary<string, object>)adoption["properties"];
            Assert.That(adoption["additionalProperties"], Is.False);
            Assert.That((System.Collections.IEnumerable)adoption["required"],
                Is.EquivalentTo(adoptedFields.Keys));
            Assert.That(adoptedFields.Keys, Does.Contain("nativeLoadReturned"));
            var nativeResult = (Dictionary<string, object>)adoptedFields["nativeLoadReturned"];
            Assert.That((System.Collections.IEnumerable)nativeResult["type"],
                Is.EquivalentTo(new[] { "boolean", "null" }));
            Assert.That((string)nativeResult["description"], Does.Contain("void"));
            Assert.That(adoptedFields.Keys, Does.Contain("loadAttempted"));
            Assert.That(adoptedFields.Keys, Does.Contain("before"));
            Assert.That(adoptedFields.Keys, Does.Contain("after"));
            var retirement = (Dictionary<string, object>)fields["windowRetirement"];
            Assert.That(retirement["additionalProperties"], Is.False);
            var retirementFields = (Dictionary<string, object>)retirement["properties"];
            Assert.That((System.Collections.IEnumerable)retirement["required"], Is.EquivalentTo(retirementFields.Keys));
            Assert.That(retirementFields.Keys, Is.EquivalentTo(new[] { "windowInstanceId", "state", "error" }));
            var previewInput = VmAutomationToolInputSchemaCatalog.Get("uitoolkit/builder-preview");
            var previewInputFields = (Dictionary<string, object>)previewInput["properties"];
            Assert.That(((Dictionary<string, object>)previewInputFields["closeCreatedWindow"])["type"], Is.EqualTo("boolean"));
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("screenshot/editor-window", out schema), Is.True);
            fields = (Dictionary<string, object>)schema["properties"];
            Assert.That(fields.Keys, Does.Contain("windowInstanceId"));
            var input = VmAutomationToolInputSchemaCatalog.Get("screenshot/editor-window");
            fields = (Dictionary<string, object>)input["properties"];
            Assert.That(((Dictionary<string, object>)fields["instanceId"])["type"], Is.EqualTo("string"));
            Assert.That(input.ContainsKey("anyOf"), Is.True);
        }

        [UnityTest]
        public IEnumerator NativeBuilderPreservesUnsavedDocumentThenAdoptsTheExactRequestedAsset()
        {
            // Real native owner and two imported test-owned assets. No user Builder is touched.
            var binding = new VmUIBuilderDocumentBinding();
            if (Resources.FindObjectsOfTypeAll(binding.WindowType).Length != 0)
                Assert.Ignore("An existing user UI Builder prevents this isolated native authoring test.");
            string folder = "Assets/VMUnityAutomationTests/BuilderOwnership_" + Guid.NewGuid().ToString("N");
            var previousFocus = EditorWindow.focusedWindow;
            EditorWindow owned = null;
            object controlledOpenDocument = null;
            PropertyInfo controlledDirty = null;
            try
            {
                Directory.CreateDirectory(folder);
                string firstPath = folder + "/First.uxml";
                string secondPath = folder + "/Second.uxml";
                const string xml = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><ui:VisualElement name=\"Fixture\" /></ui:UXML>";
                File.WriteAllText(firstPath, xml);
                File.WriteAllText(secondPath, xml);
                AssetDatabase.ImportAsset(firstPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(secondPath, ImportAssetOptions.ForceSynchronousImport);
                var firstAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(firstPath);
                var secondAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(secondPath);
                Assert.That(firstAsset, Is.Not.Null);
                Assert.That(secondAsset, Is.Not.Null);
                var first = new VmUIBuilderDocumentAdoption(firstPath, firstAsset);
                var firstFailure = first.Open(new Dictionary<string, object>());
                owned = first.Window;
                Assert.That(firstFailure, Is.Null);
                Assert.That(first.Adopted, Is.True);
                Assert.That(first.Receipt["createdWindow"], Is.True);
                Assert.That(first.RetireCreatedWindow(false)["state"], Is.EqualTo("not-requested"));
                Assert.That(owned != null, Is.True);
#if UNITY_2022_1_OR_NEWER
                Assert.That(first.Receipt["nativeLoadReturned"], Is.True);
#else
                Assert.That(first.Receipt["nativeLoadReturned"], Is.Null);
#endif
                Assert.That(first.Binding.Read(owned).Asset, Is.SameAs(firstAsset));
                string firstGuid = AssetDatabase.AssetPathToGUID(firstPath);
                File.WriteAllText(firstPath, xml.Replace("Fixture", "ReimportedFixture"));
                AssetDatabase.ImportAsset(firstPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                // Unity's actual reimport delayCall must publish while its original window is still alive.
                yield return null;
                var reimportWindows = Resources.FindObjectsOfTypeAll(binding.WindowType);
                Assert.That(reimportWindows.Length, Is.EqualTo(1));
                Assert.That(reimportWindows[0], Is.SameAs(owned));
                firstAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(firstPath);
                Assert.That(AssetDatabase.AssetPathToGUID(firstPath), Is.EqualTo(firstGuid));
                var samePathArgs = new Dictionary<string, object> { { "instanceId", VmObjectId.Get(owned) } };
                var reimported = new VmUIBuilderDocumentAdoption(firstPath, firstAsset);
                Assert.That(reimported.Open(samePathArgs), Is.Null);
                Assert.That(reimported.Window, Is.SameAs(owned));
                Assert.That(reimported.RetireCreatedWindow(true)["state"], Is.EqualTo("existing-preserved"));
                Assert.That(owned != null, Is.True);
                Assert.That(reimported.Receipt["loadAttempted"], Is.True);
#if UNITY_2022_1_OR_NEWER
                Assert.That(reimported.Receipt["nativeLoadReturned"], Is.True);
#else
                Assert.That(reimported.Receipt["nativeLoadReturned"], Is.Null);
#endif
                Assert.That(owned.rootVisualElement.Q("ReimportedFixture"), Is.Not.Null);
                Assert.That(owned.rootVisualElement.Q("Fixture"), Is.Null);
                var nativeDocument = first.Document;
                var active = nativeDocument.GetType().GetProperty("activeOpenUXMLFile", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(active, Is.Not.Null, "The native dirty-state stimulus must address the actual open UXML owner.");
                controlledOpenDocument = active.GetValue(nativeDocument);
                controlledDirty = controlledOpenDocument.GetType().GetProperty("hasUnsavedChanges", BindingFlags.Instance | BindingFlags.Public);
                Assert.That(controlledDirty?.SetMethod, Is.Not.Null);
                controlledDirty.SetValue(controlledOpenDocument, true);
                var dirtyRetirement = first.RetireCreatedWindow(true);
                Assert.That(dirtyRetirement["state"], Is.EqualTo("unsaved-preserved"));
                Assert.That(dirtyRetirement["error"], Is.Not.Empty);
                Assert.That(owned != null, Is.True);
                var dirtyClose = Assert.Throws<VmProjectToolException>(() => new VmEditorWindowCloseTool().Execute(
                    new VmEditorWindowCloseRequest { WindowInstanceId = VmObjectId.Get(owned) }));
                Assert.That(dirtyClose.ErrorCode, Is.EqualTo("editor_window_unsaved"));
                Assert.That(owned != null, Is.True);
                var args = new Dictionary<string, object> { { "instanceId", VmObjectId.Get(owned) } };
                var sameDirty = new VmUIBuilderDocumentAdoption(firstPath, firstAsset);
                var sameDirtyFailure = sameDirty.Open(args);
                Assert.That(sameDirtyFailure, Is.Not.Null);
                Assert.That(sameDirtyFailure["errorCode"], Is.EqualTo("ui_builder_unsaved_changes"));
                Assert.That(sameDirty.Receipt["loadAttempted"], Is.False);
                Assert.That(first.Binding.Read(owned).HasUnsavedChanges, Is.True);
                var rejected = new VmUIBuilderDocumentAdoption(secondPath, secondAsset);
                var failure = rejected.Open(args);
                Assert.That(failure, Is.Not.Null);
                Assert.That(failure["errorCode"], Is.EqualTo("ui_builder_unsaved_changes"));
                Assert.That(rejected.Receipt["loadAttempted"], Is.False);
                Assert.That(rejected.Receipt["nativeLoadReturned"], Is.Null);
                Assert.That(rejected.Window, Is.SameAs(owned));
                Assert.That(first.Binding.Read(owned).Asset, Is.SameAs(firstAsset));
                Assert.That(first.Binding.Read(owned).HasUnsavedChanges, Is.True);
                controlledDirty.SetValue(controlledOpenDocument, false);
                var second = new VmUIBuilderDocumentAdoption(secondPath, secondAsset);
                Assert.That(second.Open(args), Is.Null);
                Assert.That(second.Window, Is.SameAs(owned));
#if UNITY_2022_1_OR_NEWER
                Assert.That(second.Receipt["nativeLoadReturned"], Is.True);
#else
                Assert.That(second.Receipt["nativeLoadReturned"], Is.Null);
#endif
                Assert.That(second.Binding.Read(owned).Asset, Is.SameAs(secondAsset));
#if UNITY_2022_1_OR_NEWER
                Assert.That(second.Binding.Load(owned, firstAsset), Is.True);
#else
                Assert.That(second.Binding.Load(owned, firstAsset), Is.Null);
#endif
                failure = second.Observe("native-test-subject-changed");
                Assert.That(failure["errorCode"], Is.EqualTo("ui_builder_document_changed"));
                var after = (Dictionary<string, object>)second.Receipt["after"];
                Assert.That(after["assetGuid"], Is.EqualTo(AssetDatabase.AssetPathToGUID(firstPath)));
                controlledOpenDocument = null; controlledDirty = null;
                var closed = first.RetireCreatedWindow(true);
                Assert.That(closed["state"], Is.EqualTo("created-closed"), (string)closed["error"]);
                Assert.That(closed["windowInstanceId"], Is.EqualTo(first.Receipt["windowInstanceId"]));
                Assert.That(owned == null, Is.True);
                Assert.That(first.RetireCreatedWindow(true)["state"], Is.EqualTo("already-closed"));
            }
            finally
            {
                // All edits above belong to this fixture; this never saves or discards a user document.
                try
                {
                    if (controlledOpenDocument != null && controlledDirty?.SetMethod != null)
                        controlledDirty.SetValue(controlledOpenDocument, false);
                }
                finally
                {
                    try { if (owned != null) owned.Close(); }
                    finally
                    {
                        try { AssetDatabase.DeleteAsset(folder); }
                        finally { if (previousFocus != null) previousFocus.Focus(); }
                    }
                }
            }
            yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll(binding.WindowType).Length, Is.Zero,
                "Native reimport publication must not recreate the retired fixture window.");
        }
    }
}
