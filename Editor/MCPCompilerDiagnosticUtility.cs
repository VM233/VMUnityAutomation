using System;
using System.Text.RegularExpressions;
using UnityEditor.Compilation;

namespace VMUnityAutomation.Editor
{
    internal static class MCPCompilerDiagnosticUtility
    {
        private static readonly Regex ErrorMarker = new(
            @"\b(?:error|exception|failed|failure)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex WarningMarker = new(
            @"\bwarning\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool IsDiagnostic(CompilerMessage message)
        {
            if (message.type != CompilerMessageType.Error &&
                message.type != CompilerMessageType.Warning)
                return false;

            if (!string.IsNullOrWhiteSpace(message.file) || message.line > 0 || message.column > 0)
                return true;

            string text = message.message ?? "";
            return message.type == CompilerMessageType.Error
                ? ErrorMarker.IsMatch(text)
                : WarningMarker.IsMatch(text);
        }
    }
}
