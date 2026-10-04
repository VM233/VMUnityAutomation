using System.ComponentModel;

namespace VMUnityAutomation.Editor.InputSystem
{
    public sealed class VmInputRuntimeStateRequest
    {
        [VmRequired, VmJsonProperty("assetPath")]
        [Description("Imported InputActionAsset to observe without changing its enable state.")]
        public string AssetPath { get; set; }

        [VmRequired, VmMinItems(1), VmJsonProperty("actionNames")]
        [Description("One through sixteen exact action paths or IDs in the selected asset.")]
        public string[] ActionNames { get; set; }
    }
}
