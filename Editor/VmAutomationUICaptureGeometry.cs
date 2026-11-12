using System;
using System.Collections.Generic;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUICaptureGeometry
    {
        internal static RectInt ReadCaptureContentRect(Dictionary<string, object> capture, int width, int height)
        {
            var content = capture != null && capture.TryGetValue("contentRect", out object rawContent)
                ? AsDictionary(rawContent) : new Dictionary<string, object>();
            var rect = new RectInt(GetInt(content, "x", 0), GetInt(content, "y", 0),
                GetInt(content, "width", width), GetInt(content, "height", height));
            rect = ClampRectToImage(rect, width, height);
            return rect.width > 0 && rect.height > 0 ? rect : new RectInt(0, 0, width, height);
        }

        internal static RectInt MapWorldRectToCapture(Rect worldRect, Rect rootWorldRect,
            RectInt contentRect, int width, int height)
        {
            if (rootWorldRect.width <= 0 || rootWorldRect.height <= 0 || contentRect.width <= 0 || contentRect.height <= 0)
                return new RectInt();
            float scaleX = contentRect.width / rootWorldRect.width;
            float scaleY = contentRect.height / rootWorldRect.height;
            return ClampRectToImage(new RectInt(
                Mathf.FloorToInt(contentRect.x + (worldRect.x - rootWorldRect.x) * scaleX),
                Mathf.FloorToInt(contentRect.y + (worldRect.y - rootWorldRect.y) * scaleY),
                Mathf.Max(1, Mathf.CeilToInt(worldRect.width * scaleX)),
                Mathf.Max(1, Mathf.CeilToInt(worldRect.height * scaleY))), width, height);
        }

        internal static RectInt ClampRectToImage(RectInt rect, int width, int height)
        {
            int xMin = Mathf.Clamp(rect.xMin, 0, Math.Max(0, width));
            int yMin = Mathf.Clamp(rect.yMin, 0, Math.Max(0, height));
            int xMax = Mathf.Clamp(rect.xMax, xMin, Math.Max(0, width));
            int yMax = Mathf.Clamp(rect.yMax, yMin, Math.Max(0, height));
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
