using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitAssetCommands;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUIBuilderDocumentAdoption
    {
        internal EditorWindow Window { get; private set; }
        internal bool Adopted { get; private set; }
        internal VmUIBuilderDocumentBinding Binding { get; private set; }
        internal UnityEngine.Object Document { get; private set; }
        internal readonly VisualTreeAsset RequestedAsset;
        internal readonly Dictionary<string, object> Receipt;

        internal VmUIBuilderDocumentAdoption(string path, VisualTreeAsset requested)
        {
            RequestedAsset = requested;
            Receipt = new Dictionary<string, object>
            {
                { "windowInstanceId", null }, { "createdWindow", false },
                { "requestedAssetPath", path }, { "requestedAssetGuid", AssetDatabase.AssetPathToGUID(path) },
                { "requestedAssetInstanceId", VmObjectId.Get(requested) },
                { "loadAttempted", false }, { "nativeLoadReturned", null },
                { "adopted", false }, { "state", "not-attempted" },
                { "before", null }, { "after", null },
                { "failurePhase", "" }, { "failureType", "" },
            };
        }

        internal Dictionary<string, object> Open(Dictionary<string, object> args)
        {
            try { Binding = new VmUIBuilderDocumentBinding(); }
            catch (Exception failure) { return Fail("binding", "ui_builder_api_unavailable", failure); }
            try
            {
                if (args != null && args.ContainsKey("instanceId"))
                {
                    Window = VmEditorWindowSelection.Resolve(args, out string error, out string code);
                    if (Window == null) return Fail("window-selection", code, error);
                    if (!Binding.WindowType.IsInstanceOfType(Window))
                        return Fail("window-selection", "ui_builder_window_type_mismatch", "The exact window is not a native UI Builder.");
                }
                else
                {
                    var windows = UnityEngine.Resources.FindObjectsOfTypeAll(Binding.WindowType);
                    if (windows.Length > 1)
                        return Fail("window-selection", "editor_window_ambiguous", "Multiple native UI Builder windows exist. Pass instanceId from uitoolkit/windows.");
                    if (windows.Length == 1) Window = (EditorWindow)windows[0];
                    else
                    {
                        Window = Binding.Show();
                        Receipt["createdWindow"] = Window != null;
                    }
                }
                if (Window == null)
                    return Fail("window-selection", "editor_window_not_found", "The native UI Builder did not return a window.");
                Receipt["windowInstanceId"] = VmObjectId.Get(Window);
                Window.Focus();
                var before = Binding.Read(Window);
                Document = before.Document;
                Receipt["before"] = before.Product();
                Receipt["after"] = before.Product();
                if (before.HasUnsavedChanges || before.WindowHasUnsavedChanges)
                    return Fail("preflight", "ui_builder_unsaved_changes", "The bound UI Builder has unsaved changes. Save or discard them explicitly before adopting the imported source.");
                Receipt["loadAttempted"] = true;
                bool? accepted;
                try { accepted = Binding.Load(Window, RequestedAsset); }
                catch (Exception failure)
                {
                    try { Receipt["after"] = Binding.Read(Window).Product(); }
                    catch (Exception readFailure)
                    {
                        return Fail("native-load", "ui_builder_document_load_failed", new AggregateException(failure, readFailure));
                    }
                    return Fail("native-load", "ui_builder_document_load_failed", failure);
                }
                Receipt["nativeLoadReturned"] = accepted;
                var after = Binding.Read(Window);
                Receipt["after"] = after.Product();
                if (accepted == false)
                    return Fail("native-load", "ui_builder_document_rejected", "The bound native UI Builder rejected LoadDocument. Its actual document is recorded in documentAdoption.after.");
                if (after.Document != Document || !Matches(after))
                    return Fail("native-adoption", "ui_builder_document_mismatch", "The native UI Builder completed LoadDocument without adopting the exact requested document in the bound owner.");
                Adopted = true;
                Receipt["adopted"] = true;
                Receipt["state"] = "adopted";
                return null;
            }
            catch (Exception failure) { return Fail("document-observation", "ui_builder_document_load_failed", failure); }
        }

        private bool Matches(VmUIBuilderDocumentState state) =>
            state.Asset == RequestedAsset && string.Equals(NormalizeAssetPath(state.Path, ""),
                NormalizeAssetPath((string)Receipt["requestedAssetPath"], ""), StringComparison.OrdinalIgnoreCase);

        internal bool MatchesCurrent(UnityEngine.Object document, string path)
        {
            return Window != null && document == Document && RequestedAsset != null &&
                Binding.Asset(document) == RequestedAsset &&
                string.Equals(NormalizeAssetPath(path, ""), NormalizeAssetPath(
                    (string)Receipt["requestedAssetPath"], ""), StringComparison.OrdinalIgnoreCase);
        }

        internal Dictionary<string, object> Observe(string phase)
        {
            try
            {
                var current = Binding.Read(Window);
                Receipt["after"] = current.Product();
                if (current.Document != Document || !Matches(current))
                    return Invalidate(phase, "ui_builder_document_changed", "The bound UI Builder document changed after native adoption. No replacement window or document is selected.");
                return null;
            }
            catch (Exception failure)
            {
                Receipt["failureType"] = failure.GetType().FullName;
                return Invalidate(phase, "ui_builder_document_changed", failure.Message);
            }
        }

        internal Dictionary<string, object> RetireCreatedWindow(bool requested)
        {
            var result = new Dictionary<string, object>
            {
                { "windowInstanceId", Receipt["windowInstanceId"] },
                { "state", "not-requested" }, { "error", "" },
            };
            if (!requested) return result;
            if (!(bool)Receipt["createdWindow"])
            {
                result["state"] = Window == null ? "no-owned-window" : "existing-preserved";
                return result;
            }
            if (Window == null) { result["state"] = "already-closed"; return result; }
            try
            {
                var current = Binding.Read(Window);
                if (current.HasUnsavedChanges || current.WindowHasUnsavedChanges)
                {
                    result["state"] = "unsaved-preserved";
                    result["error"] = "The request-created UI Builder has unsaved changes and remains open. No source is saved or discarded.";
                    return result;
                }
                Window.Close();
                result["state"] = Window == null ? "created-closed" : "close-failed";
                if (Window != null) result["error"] = "The native Close call returned without retiring the request-created UI Builder.";
            }
            catch (Exception failure)
            {
                result["state"] = "close-failed";
                result["error"] = failure.ToString();
            }
            return result;
        }

        internal Dictionary<string, object> Invalidate(string phase, string code, string message)
        {
            Adopted = false;
            return Fail(phase, code, message);
        }

        internal Dictionary<string, object> Fail(string phase, string code, string message)
        {
            Receipt["adopted"] = Adopted;
            Receipt["state"] = Adopted ? "adopted" : "rejected";
            Receipt["failurePhase"] = phase;
            return VmAutomationResponse.Error(message, code, false,
                new Dictionary<string, object>
                {
                    { "uxmlPath", Receipt["requestedAssetPath"] }, { "opened", Adopted },
                    { "windowFound", Window != null },
                    { "window", Window == null ? null : VmAutomationUIToolkitElementUtility.BuildWindowInfo(Window) },
                    { "documentAdoption", Receipt }, { "visualValid", false },
                });
        }

        internal Dictionary<string, object> Fail(string phase, string code, Exception failure)
        {
            Receipt["failureType"] = failure.GetType().FullName;
            return Fail(phase, code, failure.Message);
        }
    }
}
