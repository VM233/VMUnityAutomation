using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmRenderDocCaptureRequest
    {
        private static VmRenderDocCaptureRequest current;
        private readonly EditorWindow window;
        private readonly string path, previousTemplate;
        private readonly Action<object> resolve;
        private readonly VmRenderDocNativeApi api;
        private readonly uint previousCount;
        private readonly int startedFrame;
        private readonly double deadline;
        private bool capturing, completed;
        internal static bool Active => current != null;

        private VmRenderDocCaptureRequest(EditorWindow view, string target, Action<object> completion)
        {
            window = view; path = target; resolve = completion;
            api = new VmRenderDocNativeApi(); previousTemplate = api.PathTemplate;
            previousCount = api.Count; startedFrame = Time.frameCount;
            deadline = EditorApplication.timeSinceStartup + 30;
        }
        internal static void Start(EditorWindow window, string path, Action<object> resolve)
        {
            var request = new VmRenderDocCaptureRequest(window, path, resolve);
            current = request;
            try
            {
                request.api.PathTemplate = Path.ChangeExtension(path, null);
                EditorApplication.update += request.Tick;
                AssemblyReloadEvents.beforeAssemblyReload += request.Interrupt;
                EditorApplication.quitting += request.Interrupt;
                RenderDoc.BeginCaptureRenderDoc(window);
                request.capturing = true;
                window.Repaint();
            }
            catch (Exception exception) { request.Fail(exception); }
        }
        internal static bool Cancel()
        {
            if (current == null) return false;
            current.Interrupt(); return true;
        }
        private void Tick()
        {
            try
            {
                if (window == null || !EditorApplication.isPlaying || EditorApplication.isPaused)
                    throw new InvalidOperationException("Native capture lost its existing window or active Play Mode.");
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Native RenderDoc capture did not publish within30 seconds.");
                if (capturing && Time.frameCount > startedFrame)
                {
                    RenderDoc.EndCaptureRenderDoc(window); capturing = false;
                }
                if (capturing || api.Count <= previousCount) return;
                string source = Path.GetFullPath(api.CapturePath(previousCount));
                string folder = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
                if (!source.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(source) != ".rdc")
                    throw new InvalidOperationException("RenderDoc published outside the requested output folder.");
                var file = new FileInfo(source);
                if (!file.Exists || file.Length == 0) return;
                if (!string.Equals(source, path, StringComparison.OrdinalIgnoreCase)) File.Move(source, path);
                Finish(new Dictionary<string, object>
                {
                    { "path", path }, { "bytes", new FileInfo(path).Length },
                    { "startFrame", startedFrame }, { "endFrame", Time.frameCount },
                    { "windowInstanceId", VmObjectId.Get(window) }, { "capturePathRestored", true },
                });
            }
            catch (Exception exception) { Fail(exception); }
        }
        private void Interrupt() => Fail(new OperationCanceledException("Native capture was cancelled or interrupted by reload/shutdown."));
        private void Fail(Exception exception) => Finish(VmAutomationResponse.UnexpectedError(exception, "renderdoc_capture_failed"));
        private void Finish(object result)
        {
            if (completed) return;
            completed = true;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Interrupt;
            EditorApplication.quitting -= Interrupt;
            try
            {
                try { if (capturing && window != null) RenderDoc.EndCaptureRenderDoc(window); }
                finally { api.PathTemplate = previousTemplate; }
                if (api.PathTemplate != previousTemplate) throw new InvalidOperationException("Native capture path restoration failed.");
            }
            catch (Exception exception) { result = VmAutomationResponse.UnexpectedError(exception, "renderdoc_restore_failed"); }
            finally { current = null; }
            resolve(result);
        }
    }
}
