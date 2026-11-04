using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor
{
    internal static class VmRenderDocCaptureCommands
    {
        internal static object Status(Dictionary<string, object> args) => new Dictionary<string, object>
        {
            { "installed", RenderDoc.IsInstalled() }, { "loaded", RenderDoc.IsLoaded() },
            { "supported", RenderDoc.IsLoaded() && RenderDoc.IsSupported() },
            { "active", VmRenderDocCaptureRequest.Active },
            { "capturePathTemplate", RenderDoc.IsLoaded() ? new VmRenderDocNativeApi().PathTemplate : "" },
        };
        internal static object Load(Dictionary<string, object> args)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return VmAutomationResponse.Error("RenderDoc loading requires idle Edit Mode.", "editor_not_idle");
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    return VmAutomationResponse.Error("Save the loaded scenes before loading a graphics debugger.", "dirty_scene");
            }
            if (!RenderDoc.IsInstalled())
                return VmAutomationResponse.Error("Unity cannot locate an installed RenderDoc.", "renderdoc_not_installed");
            bool recreated = false;
            if (!RenderDoc.IsLoaded())
            {
                // Loading the DLL alone leaves the existing D3D device outside RenderDoc's hooks.
                // Recreate only after loading, while all scene/Editor admission guards still hold.
                var recreate = typeof(ShaderUtil).GetMethod("RecreateGfxDevice", BindingFlags.Static | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (recreate == null || recreate.ReturnType != typeof(void))
                    return VmAutomationResponse.Error("This Editor has no native graphics-device recreation entry point.", "capability_unavailable");
                RenderDoc.Load();
                if (!RenderDoc.IsLoaded())
                    return VmAutomationResponse.Error("Unity did not load the installed RenderDoc library.", "renderdoc_load_failed");
                recreate.Invoke(null, null);
                recreated = true;
            }
            return new Dictionary<string, object>
            {
                { "loaded", RenderDoc.IsLoaded() }, { "supported", RenderDoc.IsLoaded() && RenderDoc.IsSupported() },
                { "graphicsDeviceRecreated", recreated },
            };
        }
        internal static object Cancel(Dictionary<string, object> args)
        {
            bool cancelled = VmRenderDocCaptureRequest.Cancel();
            return new Dictionary<string, object> { { "cancelled", cancelled }, { "active", VmRenderDocCaptureRequest.Active } };
        }
        internal static void Capture(Dictionary<string, object> args, Action<object> resolve, Action<object> progress)
        {
            try
            {
                if (VmRenderDocCaptureRequest.Active) throw new InvalidOperationException("Another native capture is active.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                    throw new InvalidOperationException("Native capture requires active unpaused Play Mode.");
                if (!RenderDoc.IsLoaded() || !RenderDoc.IsSupported())
                    throw new InvalidOperationException("Load a supported RenderDoc device before capture.");
                var window = VmObjectId.ToObject(args["windowInstanceId"]) as EditorWindow;
                if (window == null || window.GetType().FullName != "UnityEditor.GameView")
                    throw new ArgumentException("windowInstanceId must identify an existing Game View.");
                string path = ValidatePath(args["path"].ToString());
                VmRenderDocCaptureRequest.Start(window, path, resolve);
            }
            catch (Exception exception) { resolve(VmAutomationResponse.UnexpectedError(exception, "renderdoc_capture_failed")); }
        }
        internal static string ValidatePath(string value)
        {
            string path = Path.GetFullPath(value);
            string logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(logs, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".rdc")
                throw new ArgumentException("Capture path must be a .rdc file under this project's Logs directory.");
            if (File.Exists(path) || !Directory.Exists(Path.GetDirectoryName(path)))
                throw new ArgumentException("Capture needs an existing output directory and a new file path.");
            return path;
        }
    }
}
