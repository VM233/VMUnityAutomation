using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmComputeKernelSupportRequest
    {
        [VmRequired, VmJsonProperty("assetPath"), Description("Exact imported ComputeShader asset path.")]
        public string AssetPath { get; set; }

        [VmRequired, VmJsonProperty("kernelName"), Description("Exact authored kernel name. Requests this native device-specific program without dispatching it.")]
        public string KernelName { get; set; }
    }
}
