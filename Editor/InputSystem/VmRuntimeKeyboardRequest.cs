using System.ComponentModel;

namespace VMUnityAutomation.Editor.InputSystem
{
    public sealed class VmRuntimeKeyboardRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("actionName")]
        [Description("Exact map/action path or action GUID in the enabled project-wide InputActionAsset.")]
        public string ActionName { get; set; }

        [VmRequired, VmMinItems(1), VmJsonProperty("keys")]
        [Description("One through four unique Input System Key enum names. All are pressed together and released before return.")]
        public string[] Keys { get; set; }
    }
}
