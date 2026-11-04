using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceEdge
    {
        public string From { get; set; }
        public string To { get; set; }
        public string FromOwner { get; set; }
        public string ToOwner { get; set; }
        public string Kind { get; set; }
        public List<VmUiTransitionSourceLocation> Locations { get; set; } = new();
    }
}
