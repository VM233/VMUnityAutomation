using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmUiAuditExpectation
    {
        public string Owner;
        public VisualElement Element;
        public bool? Displayed;
        public bool? Enabled;
        public string Text;
        public string ClassName;
        public bool ClassPresent;
        public float? MinimumOpacity;
        public Object Background;
        public GeometryContract Geometry;

        public enum GeometryKind { Anchors, Insets, PixelScale, Untransformed }

        public sealed class GeometryContract
        {
            public GeometryKind Kind;
            public VisualElement Reference;
            public Vector2 ElementAnchor;
            public Vector2 ReferenceAnchor;
            public Vector2 ElementOffset;
            public Vector2 ReferenceOffset;
            // Left, top, right, bottom, in the reference element's local UI units.
            public Vector4 Insets;
            public float PixelUnits = 1;
            public float TolerancePixels = .25f;
            public bool CheckX = true;
            public bool CheckY = true;
        }
    }
}
