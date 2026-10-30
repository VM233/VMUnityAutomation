using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Runtime
{
    internal static class VmUiNativeClip
    {
        private static readonly PropertyInfo WorldClip = typeof(VisualElement).GetProperty("worldClip",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static Rect Read(VisualElement element)
        {
            if (WorldClip == null || WorldClip.PropertyType != typeof(Rect))
                throw new NotSupportedException("This Unity version does not expose the qualified native VisualElement.worldClip contract.");
            return (Rect)WorldClip.GetValue(element);
        }
    }
}
