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
        private VmUIBuilderDocumentBinding binding;
        private EditorWindow owned, previousFocus;
        private string folder;
        private object controlledOpenDocument;
        private PropertyInfo controlledDirty;
        private bool publicationObserved;
        private EditorApplication.CallbackFunction publicationObserver;

        private void BeginNativeFixture(string prefix)
        {
            var candidate = new VmUIBuilderDocumentBinding();
            if (Resources.FindObjectsOfTypeAll(candidate.WindowType).Length != 0)
                Assert.Ignore("An existing user UI Builder prevents this isolated native authoring test.");
            binding = candidate;
            previousFocus = EditorWindow.focusedWindow;
            folder = "Assets/VMUnityAutomationTests/" + prefix + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            TraceNative("fixture-created");
        }

        private void ObserveNextNativePublication()
        {
            Assert.That(publicationObserver, Is.Null, "The fixture owns only one outstanding native publication.");
            publicationObserved = false;
            publicationObserver = () => publicationObserved = true;
            EditorApplication.delayCall += publicationObserver;
        }

        private IEnumerator JoinNativePublication(string phase)
        {
            if (publicationObserver == null) yield break;
            for (int frame = 0; !publicationObserved && frame < 240; frame++) yield return null;
            Assert.That(publicationObserved, Is.True, "Native publication was not acknowledged: " + phase);
            EditorApplication.delayCall -= publicationObserver;
            publicationObserver = null;
            TraceNative(phase);
        }

        private void TraceNative(string phase) => TestContext.WriteLine(
            $"Builder ownership phase={phase} time={EditorApplication.timeSinceStartup:R} window={(owned == null ? "none" : VmObjectId.Get(owned))}");

        [UnityTearDown]
        public IEnumerator RetireNativeFixture()
        {
            if (binding == null) yield break;
            yield return JoinNativePublication("reimport-joined-before-retirement");
            try
            {
                if (controlledOpenDocument != null)
                    controlledDirty.SetValue(controlledOpenDocument, false);
                controlledOpenDocument = null; controlledDirty = null;
                if (owned != null) VmEditorWindowCloseTool.CloseClean(owned);
                TraceNative("native-window-retired");
                Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "The exact fixture asset folder must retire.");
            }
            finally
            {
                if (previousFocus != null) previousFocus.Focus();
            }
            ObserveNextNativePublication();
            yield return JoinNativePublication("retirement-published");
            Assert.That(Resources.FindObjectsOfTypeAll(binding.WindowType).Length, Is.Zero,
                "Native reimport publication must not recreate the retired fixture window.");
            binding = null; owned = null; previousFocus = null; folder = null;
        }

        [UnityTest]
        public IEnumerator NativeReimportEarlyExitRetiresThroughUnityTearDown()
        {
            BeginNativeFixture("BuilderEarlyExit_");
            string path = folder + "/EarlyExit.uxml";
            const string xml = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><ui:Label text=\"Before native publication\" /></ui:UXML>";
            File.WriteAllText(path, xml);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var adoption = new VmUIBuilderDocumentAdoption(path, AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path));
            var failure = adoption.Open(new Dictionary<string, object>());
            owned = adoption.Window;
            Assert.That(failure, Is.Null);
            Assert.That(adoption.Receipt["createdWindow"], Is.True);
            File.WriteAllText(path, xml.Replace("Before native publication", "After native publication"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            ObserveNextNativePublication();
            Assert.That(publicationObserved, Is.False);
            TraceNative("early-exit-with-reimport-pending");
            yield break;
        }

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
            Assert.That(((Dictionary<string, object>)previewInputFields["revealName"])["type"], Is.EqualTo("string"));
            Assert.That(fields.Keys, Does.Contain("previewTarget"));
            Assert.That(fields.Keys, Does.Contain("themePreview"));
            Assert.That(((Dictionary<string, object>)previewInputFields["themeStyleSheetPath"])["type"], Is.EqualTo("string"));
            Assert.That(((Dictionary<string, object>)previewInputFields["panelSettingsPath"])["type"], Is.EqualTo("string"));
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("screenshot/editor-window", out schema), Is.True);
            fields = (Dictionary<string, object>)schema["properties"];
            Assert.That(fields.Keys, Does.Contain("windowInstanceId"));
            var input = VmAutomationToolInputSchemaCatalog.Get("screenshot/editor-window");
            fields = (Dictionary<string, object>)input["properties"];
            Assert.That(((Dictionary<string, object>)fields["instanceId"])["type"], Is.EqualTo("string"));
            Assert.That(input.ContainsKey("anyOf"), Is.True);
        }

        [UnityTest]
        public IEnumerator NativeThemePreviewPublishesRequestedStylesAndRestoresItsStateAfterFailure()
        {
            BeginNativeFixture("BuilderTheme_");
            string path = folder + "/Theme.uxml", themePath = folder + "/Theme.tss", otherPath = folder + "/Other.tss";
            File.WriteAllText(path, "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><ui:VisualElement name=\"ThemeWitness\" /></ui:UXML>");
            File.WriteAllText(themePath, "@import url(\"unity-theme://default\"); #ThemeWitness { width: 177px; height: 31px; }");
            File.WriteAllText(otherPath, "@import url(\"unity-theme://default\"); #ThemeWitness { width: 93px; height: 31px; }");
            foreach (string assetPath in new[] { path, themePath, otherPath })
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var requested = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = requested;
            settings.referenceSpritePixelsPerUnit = 32;
            AssetDatabase.CreateAsset(settings, folder + "/PanelSettings.asset");
            ObserveNextNativePublication();
            yield return JoinNativePublication("theme-assets-published");
            object rejected = null;
            VmAutomationUIBuilderPreviewCommands.OpenUIBuilderPreview(new Dictionary<string, object>
                { { "uxmlPath", path }, { "themeStyleSheetPath", folder + "/Missing.tss" } }, value => rejected = value);
            Assert.That(((Dictionary<string, object>)rejected)["errorCode"], Is.EqualTo("ui_builder_theme_not_found"));
            VmAutomationUIBuilderPreviewCommands.OpenUIBuilderPreview(new Dictionary<string, object>
                { { "uxmlPath", path }, { "panelSettingsPath", folder + "/Missing.asset" } }, value => rejected = value);
            Assert.That(((Dictionary<string, object>)rejected)["errorCode"], Is.EqualTo("ui_builder_panel_settings_not_found"));
            Assert.That(Resources.FindObjectsOfTypeAll(binding.WindowType), Is.Empty, "Bad themes must reject before creating a Builder.");
            var adoption = new VmUIBuilderDocumentAdoption(path, AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path));
            var adoptionFailure = adoption.Open(new Dictionary<string, object>());
            owned = adoption.Window;
            Assert.That(adoptionFailure, Is.Null);
            var defaultFlag = typeof(StyleSheet).GetProperty("isDefaultStyleSheet", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool beforeFlag = (bool)defaultFlag.GetValue(requested);
            var scope = new VmUIBuilderThemeScope(binding, owned, requested, settings);
            var spritePixels = scope.Root.panel.GetType().GetProperty("referenceSpritePixelsPerUnit", BindingFlags.Instance | BindingFlags.Public);
            float beforePixels = (float)spritePixels.GetValue(scope.Root.panel);
            try
            {
                scope.Apply();
                yield return null;
                scope.Verify();
                Assert.That((float)spritePixels.GetValue(scope.Root.panel), Is.EqualTo(32));
                spritePixels.SetValue(scope.Root.panel, 48f);
                Assert.Throws<InvalidOperationException>(() => scope.Verify(), "A changed native Sprite scale must invalidate the preview.");
                spritePixels.SetValue(scope.Root.panel, 32f);
                scope.Verify();
                Assert.That(scope.Root.Q("ThemeWitness").resolvedStyle.width,
                    Is.EqualTo(177).Within(1f / EditorGUIUtility.pixelsPerPoint + .001f),
                    "Native layout rounds to the current physical pixel grid.");
                Assert.That(scope.Receipt["rootContainsTheme"], Is.True);
                using (var changed = new VmUIBuilderThemeScope(binding, owned, AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(otherPath)))
                {
                    changed.Apply();
                    Assert.Throws<InvalidOperationException>(() => scope.Verify(), "A different native theme must invalidate the first preview.");
                }
                scope.Verify();
                Assert.Throws<InvalidOperationException>(() => throw new InvalidOperationException("Interrupted preview witness"));
            }
            finally { scope.Dispose(); }
            Assert.That(scope.Receipt["restored"], Is.True);
            Assert.That(scope.Receipt["referenceSpritePixelsPerUnitRestored"], Is.True);
            Assert.That((float)spritePixels.GetValue(scope.Root.panel), Is.EqualTo(beforePixels));
            Assert.That((bool)defaultFlag.GetValue(requested), Is.EqualTo(beforeFlag));
            Assert.That(binding.Read(owned).HasUnsavedChanges, Is.False);
        }

        [UnityTest]
        public IEnumerator NativeBuilderPreservesUnsavedDocumentThenAdoptsTheExactRequestedAsset()
        {
            BeginNativeFixture("BuilderOwnership_");
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
            ObserveNextNativePublication();
            TraceNative("reimport-queued");
            yield return JoinNativePublication("reimport-published");
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
    }
}
