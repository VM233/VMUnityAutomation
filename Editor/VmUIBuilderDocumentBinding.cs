using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUIBuilderDocumentBinding
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public;
        internal readonly Type WindowType;
        private readonly MethodInfo showWindow;
        private readonly MethodInfo loadDocument;
        private readonly PropertyInfo document;
        private readonly PropertyInfo asset;
        private readonly PropertyInfo path;
        private readonly PropertyInfo unsaved;

        internal static Type NativeWindowType =>
            Type.GetType("Unity.UI.Builder.Builder, UnityEditor.UIBuilderModule", false)
            ?? throw new TypeLoadException("Unity.UI.Builder.Builder in UnityEditor.UIBuilderModule is unavailable.");

        internal VmUIBuilderDocumentBinding()
        {
            // Each supported Editor compiles one exact native return ABI; the assembly identity is unchanged.
            WindowType = NativeWindowType;
            if (!typeof(EditorWindow).IsAssignableFrom(WindowType))
                throw new TypeLoadException("The native Builder type is not an EditorWindow.");
            showWindow = WindowType.GetMethod("ShowWindow", BindingFlags.Static | BindingFlags.Public,
                null, Type.EmptyTypes, null) ?? throw new MissingMethodException(WindowType.FullName, "ShowWindow()");
            loadDocument = WindowType.GetMethod("LoadDocument", Instance, null,
                new[] { typeof(VisualTreeAsset), typeof(bool) }, null)
                ?? throw new MissingMethodException(WindowType.FullName, "LoadDocument(VisualTreeAsset, bool)");
#if UNITY_2022_1_OR_NEWER
            var loadReturnType = typeof(bool);
#else
            var loadReturnType = typeof(void);
#endif
            if (!WindowType.IsAssignableFrom(showWindow.ReturnType) || loadDocument.ReturnType != loadReturnType)
                throw new MissingMethodException("The native Builder document methods have an unexpected return contract.");
            document = RequireProperty(WindowType, "document", typeof(UnityEngine.Object), true);
            asset = RequireProperty(document.PropertyType, "visualTreeAsset", typeof(VisualTreeAsset), false);
            path = RequireProperty(document.PropertyType, "uxmlPath", typeof(string), false);
            unsaved = RequireProperty(document.PropertyType, "hasUnsavedChanges", typeof(bool), false);
        }

        private static PropertyInfo RequireProperty(Type type, string name, Type valueType, bool assignable)
        {
            var property = type.GetProperty(name, Instance);
            if (property == null || property.GetMethod == null ||
                (assignable ? !valueType.IsAssignableFrom(property.PropertyType) : property.PropertyType != valueType))
                throw new MissingMemberException(type.FullName, name);
            return property;
        }

        internal EditorWindow Show() => (EditorWindow)Invoke(showWindow, null, null);
        internal bool? Load(EditorWindow window, VisualTreeAsset requested)
        {
#if UNITY_2022_1_OR_NEWER
            return (bool)Invoke(loadDocument, window, new object[] { requested, true });
#else
            Invoke(loadDocument, window, new object[] { requested, true });
            return null;
#endif
        }

        internal VmUIBuilderDocumentState Read(EditorWindow window)
        {
            if (window == null || !WindowType.IsInstanceOfType(window))
                throw new InvalidOperationException("The bound native UI Builder window is no longer available.");
            var owner = (UnityEngine.Object)document.GetValue(window);
            if (owner == null) throw new InvalidOperationException("The bound UI Builder document is not initialized.");
            return new VmUIBuilderDocumentState(owner, (VisualTreeAsset)asset.GetValue(owner),
                (string)path.GetValue(owner), (bool)unsaved.GetValue(owner), window.hasUnsavedChanges);
        }

        internal UnityEngine.Object Document(EditorWindow window) => (UnityEngine.Object)document.GetValue(window);
        internal VisualTreeAsset Asset(UnityEngine.Object owner) => (VisualTreeAsset)asset.GetValue(owner);

        private static object Invoke(MethodInfo method, object target, object[] args)
        {
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException failure) when (failure.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }
    }

    // The native toolbar owns theme publication. Its initialization mode changes
    // the bound canvas without writing the user's project/theme override.
    internal sealed class VmUIBuilderThemeScope : IDisposable
    {
        private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
        private readonly UnityEngine.Object document;
        private readonly object toolbar;
        private readonly PropertyInfo mode;
        private readonly PropertyInfo sheet;
        private readonly PropertyInfo defaultSheet;
        private readonly MethodInfo change;
        private readonly object previousMode;
        private readonly ThemeStyleSheet previousSheet;
        private readonly ThemeStyleSheet requested;
        private readonly bool previousDefaultFlag;
        private bool attempted;
        internal readonly VisualElement Root;
        internal readonly System.Collections.Generic.Dictionary<string, object> Receipt;

        internal VmUIBuilderThemeScope(VmUIBuilderDocumentBinding binding, EditorWindow window,
            ThemeStyleSheet requested)
        {
            this.requested = requested;
            document = binding.Document(window);
            mode = document.GetType().GetProperty("currentCanvasTheme", Public)
                ?? throw new MissingMemberException(document.GetType().FullName, "currentCanvasTheme");
            sheet = document.GetType().GetProperty("currentCanvasThemeStyleSheet", Public)
                ?? throw new MissingMemberException(document.GetType().FullName, "currentCanvasThemeStyleSheet");
            if (!mode.PropertyType.IsEnum || sheet.PropertyType != typeof(ThemeStyleSheet))
                throw new MissingMemberException("Native UI Builder theme properties have an unexpected contract.");
            toolbar = binding.WindowType.GetProperty("toolbar", Public)?.GetValue(window)
                ?? throw new MissingMemberException(binding.WindowType.FullName, "toolbar");
            Root = binding.WindowType.GetProperty("documentRootElement", Public)?.GetValue(window) as VisualElement
                ?? throw new MissingMemberException(binding.WindowType.FullName, "documentRootElement");
            change = toolbar.GetType().GetMethod("ChangeCanvasTheme", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { mode.PropertyType, typeof(ThemeStyleSheet), typeof(bool) }, null)
                ?? throw new MissingMethodException(toolbar.GetType().FullName, "ChangeCanvasTheme");
            if (change.ReturnType != typeof(void)) throw new MissingMethodException("Unexpected native theme return contract.");
            defaultSheet = typeof(StyleSheet).GetProperty("isDefaultStyleSheet", Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(typeof(StyleSheet).FullName, "isDefaultStyleSheet");
            previousMode = mode.GetValue(document);
            previousSheet = (ThemeStyleSheet)sheet.GetValue(document);
            previousDefaultFlag = (bool)defaultSheet.GetValue(requested);
            Receipt = new System.Collections.Generic.Dictionary<string, object>
            {
                { "requestedAssetPath", AssetDatabase.GetAssetPath(requested) },
                { "previousAssetPath", AssetDatabase.GetAssetPath(previousSheet) },
                { "previousMode", previousMode.ToString() }, { "observedMode", "" },
                { "observedAssetPath", "" }, { "rootContainsTheme", false },
                { "applied", false }, { "restored", false }, { "restoreError", "" }
            };
        }

        internal void Apply()
        {
            attempted = true;
            // The enum's declaring type changed between supported Unity versions.
            // Bind its exact type from the native document rather than guessing an ABI.
            change.Invoke(toolbar, new[] { Enum.Parse(mode.PropertyType, "Custom"), requested, (object)true });
            Verify();
            Receipt["applied"] = true;
        }

        internal void Verify()
        {
            var actualMode = mode.GetValue(document);
            var actualSheet = (ThemeStyleSheet)sheet.GetValue(document);
            bool contains = Root.styleSheets.Contains(requested);
            Receipt["observedMode"] = actualMode.ToString();
            Receipt["observedAssetPath"] = AssetDatabase.GetAssetPath(actualSheet);
            Receipt["rootContainsTheme"] = contains;
            if (actualMode.ToString() != "Custom" || actualSheet != requested || !contains)
                throw new InvalidOperationException("The native Builder canvas did not retain the requested theme.");
        }

        public void Dispose()
        {
            if (!attempted) return;
            attempted = false;
            try
            {
                change.Invoke(toolbar, new object[] { previousMode, previousSheet, true });
                if (!Equals(mode.GetValue(document), previousMode) || (ThemeStyleSheet)sheet.GetValue(document) != previousSheet)
                    throw new InvalidOperationException("The native Builder theme was not restored.");
                Receipt["restored"] = true;
            }
            finally { defaultSheet.SetValue(requested, previousDefaultFlag); }
        }
    }
}
