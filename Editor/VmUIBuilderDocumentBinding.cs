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
}
