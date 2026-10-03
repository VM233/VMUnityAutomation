using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationAssetPreviewRequest
    {
        private readonly UnityEngine.Object asset;
        private readonly string assetPath;
        private readonly int width;
        private readonly int height;
        private readonly Action<object> resolve;
        private readonly double deadline;

        private VmAutomationAssetPreviewRequest(UnityEngine.Object asset,
            string assetPath, int width, int height, Action<object> resolve)
        {
            this.asset = asset;
            this.assetPath = assetPath;
            this.width = width;
            this.height = height;
            this.resolve = resolve;
            deadline = EditorApplication.timeSinceStartup + 5d;
        }

        internal static void Capture(Dictionary<string, object> args,
            Action<object> resolve, Action<object> progress)
        {
            string path = args["assetPath"].ToString();
            var target = AssetDatabase.LoadMainAssetAtPath(path);
            if (target == null)
            {
                resolve(VmAutomationResponse.Error(
                    $"Asset not found at '{path}'.", "asset_not_found"));
                return;
            }
            int width = args.TryGetValue("width", out object requestedWidth)
                ? Convert.ToInt32(requestedWidth) : 256;
            int height = args.TryGetValue("height", out object requestedHeight)
                ? Convert.ToInt32(requestedHeight) : 256;
            Start(target, path, width, height, resolve);
        }

        internal static void Start(UnityEngine.Object asset, string path,
            int width, int height, Action<object> resolve)
        {
            var request = new VmAutomationAssetPreviewRequest(
                asset, path, width, height, resolve);
            EditorApplication.update += request.Tick;
            AssemblyReloadEvents.beforeAssemblyReload += request.Interrupt;
            EditorApplication.quitting += request.Interrupt;
            request.Tick();
        }

        private void Tick()
        {
            object result;
            try
            {
                Texture2D preview = AssetPreview.GetAssetPreview(asset);
                if (preview != null)
                    result = Encode(preview);
                else if (EditorApplication.timeSinceStartup >= deadline)
                    result = VmAutomationResponse.Error(
                        $"Native asset preview for '{assetPath}' was not available " +
                        "within 5 seconds. No thumbnail substitute was returned.",
                        "asset_preview_timeout");
                else
                    return;
            }
            catch (Exception exception)
            {
                result = VmAutomationResponse.Error(
                    $"Asset preview for '{assetPath}' failed: {exception.Message}",
                    "asset_preview_failed", false,
                    new Dictionary<string, object>
                    {
                        { "assetPath", assetPath },
                        { "exceptionType", exception.GetType().FullName },
                        { "stackTrace", exception.StackTrace ?? "" },
                    });
            }
            Complete(result);
        }

        private void Interrupt()
        {
            Complete(VmAutomationResponse.Error(
                $"Asset preview for '{assetPath}' was interrupted by Editor shutdown or reload.",
                "asset_preview_interrupted"));
        }

        private void Complete(object result)
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Interrupt;
            EditorApplication.quitting -= Interrupt;
            resolve(result);
        }

        private Dictionary<string, object> Encode(Texture2D preview)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0);
            Texture2D readable = null;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                float scale = Mathf.Min((float)width / preview.width,
                    (float)height / preview.height);
                var rect = new Rect((width - preview.width * scale) * 0.5f,
                    (height - preview.height * scale) * 0.5f,
                    preview.width * scale, preview.height * scale);
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, width, height, 0);
                    Graphics.DrawTexture(rect, preview);
                }
                finally
                {
                    GL.PopMatrix();
                }
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply();
                return new Dictionary<string, object>
                {
                    { "base64", Convert.ToBase64String(readable.EncodeToPNG()) },
                    { "width", width },
                    { "height", height },
                    { "assetPath", assetPath },
                    { "assetType", asset.GetType().Name },
                };
            }
            finally
            {
                RenderTexture.active = previous;
                if (readable != null)
                    UnityEngine.Object.DestroyImmediate(readable);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
