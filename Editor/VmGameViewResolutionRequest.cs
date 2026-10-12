using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewResolutionRequest
    {
        [VmRequired, VmRange(10, 8192), VmJsonProperty("width")]
        public int Width { get; set; }

        [VmRequired, VmRange(10, 8192), VmJsonProperty("height")]
        public int Height { get; set; }
    }
}
