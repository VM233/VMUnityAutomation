using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VMUnityAutomation.Editor
{
    internal static class VmPlayerLaunchArguments
    {
        internal const int MaximumArguments = 64;
        internal const int MaximumArgumentLength = 4096;
        internal const int MaximumCommandLineLength = 32760;

        internal static string Encode(string executablePath, IReadOnlyList<string> arguments,
            string logPath)
        {
            ValidateString(executablePath, "executablePath");
            if (logPath != null) ValidateString(logPath, "playerLogPath");
            if (arguments == null || arguments.Count > MaximumArguments)
                throw Invalid("At most 64 playerArguments are admitted.");

            int length = QuotedLength(executablePath) + 1;
            if (logPath != null) length += QuotedLength("-logFile") + QuotedLength(logPath) + 2;
            foreach (string argument in arguments)
            {
                ValidateString(argument, "playerArguments entry", true);
                if (string.Equals(argument, "-logFile", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith("-logFile=", StringComparison.OrdinalIgnoreCase))
                    throw Invalid("Use playerLogPath to select the Player log destination.");
                length += QuotedLength(argument) + 1;
            }
            if (length > MaximumCommandLineLength)
                throw Invalid("The encoded Player command line exceeds 32760 UTF-16 code units.");

            var result = new StringBuilder(length);
            foreach (string argument in arguments) AppendQuoted(result, argument);
            if (logPath != null)
            {
                AppendQuoted(result, "-logFile");
                AppendQuoted(result, logPath);
            }
            return result.ToString();
        }

        internal static string[] ReadVector(object value)
        {
            if (value is not System.Collections.IList values || values.Count > MaximumArguments)
                throw Invalid("playerArguments requires an array of at most 64 strings.");
            var result = new string[values.Count];
            for (int index = 0; index < result.Length; index++)
            {
                if (values[index] is not string argument)
                    throw Invalid("Each playerArguments entry must be a string.");
                result[index] = argument;
            }
            return result;
        }

        internal static string AbsolutePath(string path, string field)
        {
            ValidateString(path, field);
            if (!Path.IsPathRooted(path)) throw Invalid($"{field} requires an absolute path.");
#if UNITY_EDITOR_WIN
            if (Path.GetPathRoot(path).Length < 3)
                throw Invalid($"{field} must include its drive or UNC volume.");
#endif
            return Path.GetFullPath(path);
        }

        private static void ValidateString(string value, string field, bool allowEmpty = false)
        {
            if (value == null || (!allowEmpty && value.Length == 0) ||
                value.Length > MaximumArgumentLength || value.IndexOf('\0') >= 0)
                throw Invalid($"{field} requires a non-null string of at most 4096 code units without NUL.");
        }

        private static int QuotedLength(string value)
        {
            int length = 2, backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { backslashes++; continue; }
                length += character == '"' ? 2 * backslashes + 2 : backslashes + 1;
                backslashes = 0;
            }
            return length + 2 * backslashes;
        }

        private static void AppendQuoted(StringBuilder result, string value)
        {
            if (result.Length > 0) result.Append(' ');
            result.Append('"');
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { backslashes++; continue; }
                result.Append('\\', character == '"' ? 2 * backslashes + 1 : backslashes);
                result.Append(character);
                backslashes = 0;
            }
            result.Append('\\', 2 * backslashes);
            result.Append('"');
        }

        internal static VmProjectToolException Invalid(string message) =>
            new("invalid_player_launch_arguments", message);
    }
}
