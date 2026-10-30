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
    }
}
