using System.Collections.Generic;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Runtime
{
    public interface IVmUiAuditSource
    {
        IReadOnlyList<VmUiAuditExpectation> CaptureUiExpectations(VisualElement documentRoot);
    }
}
