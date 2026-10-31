using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Runtime
{
    public static class VmUiNativeText
    {
#if UNITY_6000_3_OR_NEWER
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Computed = typeof(VisualElement).GetField("m_Style", Members);
        private static readonly PropertyInfo AutoSize = Computed?.FieldType.GetProperty("unityTextAutoSize", Members);
        private static readonly PropertyInfo AutoMode = AutoSize?.PropertyType.GetProperty("mode", Members);
        private static readonly FieldInfo AutoModeField = AutoSize?.PropertyType.GetField("mode", Members);
        private static readonly PropertyInfo Handle = typeof(TextElement).GetProperty("uitkTextHandle", Members);
        private static readonly PropertyInfo Advanced = Handle?.PropertyType.GetProperty("useAdvancedText", Members);
        private static readonly PropertyInfo Measured = Handle?.PropertyType.GetProperty("MeasuredWidth", Members);
        private static readonly PropertyInfo Rounded = Handle?.PropertyType.GetProperty("RoundedWidth", Members);
        private static readonly PropertyInfo LastScale = Handle?.PropertyType.GetProperty("LastPixelPerPoint", Members);
        private static readonly PropertyInfo Scale = typeof(VisualElement).GetProperty("scaledPixelsPerPoint", Members);
        private static readonly MethodInfo Compute = Handle?.PropertyType.GetMethod("ComputeTextSize", Members, null,
            new[] { typeof(string), typeof(float), typeof(VisualElement.MeasureMode), typeof(float),
                typeof(VisualElement.MeasureMode), typeof(float?) }, null);
#endif
        public static Vector2 Measure(TextElement text, Rect content, float tolerance)
        {
#if UNITY_6000_3_OR_NEWER
            if (Computed == null || AutoSize == null || AutoMode == null && AutoModeField == null)
                throw new NotSupportedException("Native computed text-auto-size metadata is unavailable.");
            object auto = AutoSize.GetValue(Computed.GetValue(text));
            object mode = AutoMode != null ? AutoMode.GetValue(auto) : AutoModeField.GetValue(auto);
            if (mode.ToString() == "BestFit")
            {
                if (Handle == null || Compute == null)
                    throw new NotSupportedException("Native unclamped best-fit text measurement is unavailable.");
                // Public MeasureTextSize clamps AtMost results. The raw native result must still reject
                // overflow when the minimum permitted font cannot fit; never treat the constraint as proof.
                return (Vector2)Compute.Invoke(Handle.GetValue(text), new object[] { text.text,
                    content.width + tolerance, VisualElement.MeasureMode.AtMost,
                    content.height + tolerance, VisualElement.MeasureMode.AtMost, null });
            }
#endif
            bool wrap = text.resolvedStyle.whiteSpace == WhiteSpace.Normal;
            return text.MeasureTextSize(text.text, wrap ? RenderWidth(text, content.width) + tolerance : 0,
                wrap ? VisualElement.MeasureMode.AtMost : VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
        }

        private static float RenderWidth(TextElement text, float contentWidth)
        {
#if UNITY_6000_3_OR_NEWER
            var native = ReadLayout(text, contentWidth);
            return native.Width;
#else
            return contentWidth;
#endif
        }

        public static Dictionary<string, object> Describe(TextElement text)
        {
            var content = text.contentRect;
            var result = new Dictionary<string, object>
            {
                { "contentWidth", content.width }, { "contentHeight", content.height },
                { "fontSize", text.resolvedStyle.fontSize }, { "whiteSpace", text.resolvedStyle.whiteSpace.ToString() },
            };
#if UNITY_6000_3_OR_NEWER
            var native = ReadLayout(text, content.width);
            result["renderWidth"] = native.Width; result["pixelsPerPoint"] = native.Scale;
            result["measuredWidth"] = native.Measured; result["roundedWidth"] = native.Rounded;
            result["lastPixelsPerPoint"] = native.LastScale;
            result["usesCachedWidth"] = native.UsesCache; result["advancedText"] = native.Advanced;
#endif
            return result;
        }

#if UNITY_6000_3_OR_NEWER
        private static NativeLayout ReadLayout(TextElement text, float width)
        {
            if (Handle == null || Advanced == null || Measured == null || Rounded == null || LastScale == null || Scale == null)
                throw new NotSupportedException("Native rendered text-width metadata is unavailable.");
            object handle = Handle.GetValue(text);
            bool advanced = (bool)Advanced.GetValue(handle);
            float? measured = (float?)Measured.GetValue(handle);
            float rounded = (float)Rounded.GetValue(handle);
            float lastScale = (float)LastScale.GetValue(handle);
            float scale = (float)Scale.GetValue(text);
            // Match Unity's SDF render-width admission, including its native0.01 cache identity check.
            bool usesCache = !advanced && measured.HasValue && Mathf.Abs(width - rounded) < .01f && lastScale == scale;
            return new NativeLayout(usesCache ? measured.Value : width, scale, measured, rounded, lastScale, usesCache, advanced);
        }

        private readonly struct NativeLayout
        {
            public readonly float Width, Scale, Rounded, LastScale;
            public readonly float? Measured;
            public readonly bool UsesCache, Advanced;
            public NativeLayout(float width, float scale, float? measured, float rounded, float lastScale, bool usesCache, bool advanced)
            { Width = width; Scale = scale; Measured = measured; Rounded = rounded; LastScale = lastScale; UsesCache = usesCache; Advanced = advanced; }
        }
#endif
    }
}
