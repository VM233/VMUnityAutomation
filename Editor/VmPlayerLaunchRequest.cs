using System;
using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerLaunchRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("executablePath")]
        [Description("Absolute path to an existing Windows Unity Player executable and its adjacent Data directory.")]
        public string ExecutablePath { get; set; }

        [VmJsonProperty("playerArguments")]
        [Description("Argument vector: at most 64 entries, each at most 4096 UTF-16 code units. Do not supply -logFile. The entire quoted command line must fit 32760 code units.")]
        public string[] PlayerArguments { get; set; } = Array.Empty<string>();

        [VmRequired, VmMinLength(1), VmJsonProperty("playerLogPath")]
        [Description("Absolute Player log path in an existing directory. This field exclusively supplies -logFile.")]
        public string PlayerLogPath { get; set; }
    }
}
