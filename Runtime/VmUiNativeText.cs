using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Runtime
{
    internal static class VmUiNativeText
    {
#if UNITY_6000_3_OR_NEWER
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo Computed = typeof(VisualElement).GetField("m_Style", Members);
        private static readonly PropertyInfo AutoSize = Computed?.FieldType.GetProperty("unityTextAutoSize", Members);
        private static readonly PropertyInfo AutoMode = AutoSize?.PropertyType.GetProperty("mode", Members);
        private static readonly FieldInfo AutoModeField = AutoSize?.PropertyType.GetField("mode", Members);
        private static readonly PropertyInfo Handle = typeof(TextElement).GetProperty("uitkTextHandle", Members);
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
            return text.MeasureTextSize(text.text, wrap ? content.width + tolerance : 0,
                wrap ? VisualElement.MeasureMode.AtMost : VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
        }
    }
}
