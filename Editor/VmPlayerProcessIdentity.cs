using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VMUnityAutomation.Editor
{
    internal static class VmPlayerProcessIdentity
    {
        internal static void Verify(Process process, string executablePath, DateTime startedAt)
        {
            string actualPath = Path.GetFullPath(process.MainModule.FileName);
            DateTime actualStart = process.StartTime.ToUniversalTime();
            if (!string.Equals(actualPath, executablePath, StringComparison.OrdinalIgnoreCase) ||
                actualStart != startedAt)
                throw new VmProjectToolException("player_identity_mismatch",
                    "The live process path or OS creation time differs from the requested Player identity.",
                    false, new Dictionary<string, object>
                    {
                        { "processId", process.Id }, { "executablePath", actualPath },
                        { "startedAt", actualStart.ToString("O") }
                    });
        }
    }
}
