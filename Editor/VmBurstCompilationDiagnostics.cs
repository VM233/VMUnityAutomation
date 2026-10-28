using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace VMUnityAutomation.Editor
{
    // The workspace job owns this window, rather than the shorter managed compilation cycle.
    internal sealed class VmBurstCompilationDiagnostics
    {
        internal const long MaximumLogBytes = 64L * 1024 * 1024;
        internal const int MaximumLineBytes = 64 * 1024;
        internal const int MaximumLines = 262144;
        internal const int MaximumEntries = 1000;
        internal const int MaximumDiagnosticCharacters = 1024 * 1024;
        private const int BoundaryBytes = 256;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal string Revision { get; private set; }
        internal string LogPath { get; private set; }
        internal string FileCreationUtc { get; private set; }
        internal string BoundaryHash { get; private set; }
        internal DateTime StartedAt { get; private set; }
        internal long StartOffset { get; private set; }
        internal long? EndOffset { get; private set; }
        internal bool CaptureComplete { get; private set; }
        internal string CaptureIssue { get; private set; } = "";
        internal int ErrorCount { get; private set; }
        internal int WarningCount { get; private set; }
        internal List<Dictionary<string, object>> Entries { get; private set; } = new();

        internal static VmBurstCompilationDiagnostics Begin(string path, DateTime now)
        {
            var window = new VmBurstCompilationDiagnostics
            {
                Revision = Guid.NewGuid().ToString("N"), LogPath = path ?? "", StartedAt = now,
            };
            try
            {
                using var stream = Open(path);
                window.FileCreationUtc = File.GetCreationTimeUtc(path).ToString("O");
                window.StartOffset = stream.Length;
                if (window.StartOffset > 0)
                {
                    stream.Position = window.StartOffset - 1;
                    if (stream.ReadByte() != '\n')
                        throw new InvalidDataException("The Editor log ends inside a line at the diagnostic window start.");
                }
                window.BoundaryHash = ReadBoundaryHash(stream, window.StartOffset);
            }
            catch (Exception exception) when (IsCaptureException(exception))
            {
                window.CaptureIssue = "Cannot establish the native diagnostic window: " + exception.Message;
            }
            return window;
        }

        internal void Complete(string currentLogPath)
        {
            if (EndOffset.HasValue)
                throw new InvalidOperationException("A native diagnostic window may be completed only once.");
            if (!string.IsNullOrEmpty(CaptureIssue)) return;
            try
            {
                if (!string.Equals(LogPath, currentLogPath, StringComparison.Ordinal))
                    throw new InvalidDataException("Unity changed the Editor log path during native compilation.");
                using var stream = Open(LogPath);
                EndOffset = stream.Length;
                if (File.GetCreationTimeUtc(LogPath).ToString("O") != FileCreationUtc ||
                    EndOffset.Value < StartOffset ||
                    ReadBoundaryHash(stream, StartOffset) != BoundaryHash)
                    throw new InvalidDataException("The Editor log was replaced, truncated or rewritten during compilation.");
                long length = EndOffset.Value - StartOffset;
                if (length > MaximumLogBytes)
                    throw new InvalidDataException($"The native diagnostic window exceeds {MaximumLogBytes} bytes ({length}).");
                stream.Position = StartOffset;
                ReadWindow(stream, length);
                CaptureComplete = true;
            }
            catch (Exception exception) when (IsCaptureException(exception))
            {
                CaptureIssue = "Native diagnostic capture is incomplete: " + exception.Message;
            }
        }

        internal Dictionary<string, object> BuildFailure()
        {
            if (!CaptureComplete)
                return VmAutomationResponse.Error(
                    string.IsNullOrEmpty(CaptureIssue) ? "The native diagnostic window has not completed." : CaptureIssue,
                    "burst_compilation_diagnostics_incomplete", false,
                    new Dictionary<string, object> { ["burstDiagnostics"] = ToDictionary() });
            return ErrorCount == 0 ? null : VmAutomationResponse.Error(
                $"Native Burst compilation emitted {ErrorCount} error(s) in the workspace diagnostic window.",
                "burst_compilation_failed", false,
                new Dictionary<string, object> { ["burstDiagnostics"] = ToDictionary() });
        }

        private void ReadWindow(FileStream stream, long remaining)
        {
            var block = new byte[MaximumLineBytes];
            var line = new byte[MaximumLineBytes];
            int lineLength = 0;
            int retainedCharacters = 0;
            int lineCount = 0;
            long lineOffset = StartOffset;
            var followingLog = new StringBuilder();
            try
            {
                while (remaining > 0)
                {
                    int count = stream.Read(block, 0, (int)Math.Min(block.Length, remaining));
                    if (count == 0) throw new EndOfStreamException("The Editor log ended before the frozen window boundary.");
                    remaining -= count;
                    int cursor = 0;
                    while (cursor < count)
                    {
                        int newline = Array.IndexOf(block, (byte)'\n', cursor, count - cursor);
                        int end = newline < 0 ? count : newline;
                        int bytes = end - cursor;
                        if (lineLength + bytes > line.Length)
                            throw new InvalidDataException($"An Editor log line exceeds {MaximumLineBytes} bytes.");
                        Buffer.BlockCopy(block, cursor, line, lineLength, bytes);
                        lineLength += bytes;
                        cursor = newline < 0 ? count : newline + 1;
                        if (newline < 0) continue;
                        if (++lineCount > MaximumLines)
                            throw new InvalidDataException($"The native diagnostic window exceeds {MaximumLines} log lines.");
                        RecordLine(line, lineLength, lineOffset, ref retainedCharacters, followingLog);
                        lineOffset += lineLength + 1;
                        lineLength = 0;
                    }
                }
                // An incomplete line could contain a native diagnostic that has not been fully logged.
                if (lineLength != 0)
                    throw new InvalidDataException("The frozen Editor log window ends inside an unterminated line.");
            }
            finally
            {
                PublishFollowingLog(followingLog);
            }
        }

        private void RecordLine(byte[] bytes, int length, long logOffset, ref int retainedCharacters,
            StringBuilder followingLog)
        {
            string source = Utf8.GetString(bytes, 0, length);
            string raw = source.TrimEnd('\r');
            int error = raw.IndexOf("error BC", StringComparison.OrdinalIgnoreCase);
            int warning = raw.IndexOf("warning BC", StringComparison.OrdinalIgnoreCase);
            int start = error < 0 ? warning : warning < 0 ? error : Math.Min(error, warning);
            if (start < 0)
            {
                if (raw.IndexOf("Burst error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    raw.IndexOf("Burst warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    raw.IndexOf("Burst internal compiler error", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new InvalidDataException("An unrecognized native compiler diagnostic was emitted: " + raw);
                if (Entries.Count > 0)
                {
                    if (retainedCharacters + source.Length + 1 > MaximumDiagnosticCharacters)
                        throw new InvalidDataException("The native diagnostic text retention budget was exceeded.");
                    followingLog.Append(source).Append('\n');
                    retainedCharacters += source.Length + 1;
                }
                return;
            }
            if (Entries.Count == MaximumEntries || retainedCharacters + raw.Length > MaximumDiagnosticCharacters)
                throw new InvalidDataException("The native diagnostic record or text retention budget was exceeded.");
            string severity = start == error ? "error" : "warning";
            int codeStart = start + severity.Length + 1;
            int codeEnd = codeStart + 2;
            while (codeEnd < raw.Length && raw[codeEnd] >= '0' && raw[codeEnd] <= '9') codeEnd++;
            if (codeEnd == codeStart + 2 || codeEnd == raw.Length || raw[codeEnd] != ':')
                throw new InvalidDataException("An unrecognized native compiler diagnostic was emitted: " + raw);
            ReadLocation(raw.Substring(0, start), out string file, out int line, out int column);
            PublishFollowingLog(followingLog);
            followingLog.Clear();
            Entries.Add(new Dictionary<string, object>
            {
                ["file"] = file, ["line"] = line, ["column"] = column,
                ["severity"] = severity, ["code"] = raw.Substring(codeStart, codeEnd - codeStart).ToUpperInvariant(),
                ["message"] = raw, ["logByteOffset"] = logOffset, ["followingLogText"] = "",
            });
            retainedCharacters += raw.Length;
            if (severity == "error") ErrorCount++; else WarningCount++;
        }

        private void PublishFollowingLog(StringBuilder followingLog)
        {
            if (Entries.Count > 0)
                Entries[Entries.Count - 1]["followingLogText"] = followingLog.ToString();
        }

        private static void ReadLocation(string prefix, out string file, out int line, out int column)
        {
            file = "";
            line = column = 0;
            prefix = prefix.TrimEnd();
            if (prefix.EndsWith("Burst", StringComparison.OrdinalIgnoreCase))
                prefix = prefix.Substring(0, prefix.Length - "Burst".Length).TrimEnd();
            if (!prefix.EndsWith("):", StringComparison.Ordinal)) return;
            int open = prefix.LastIndexOf('(');
            int comma = prefix.LastIndexOf(',');
            if (open < 0 || comma <= open) return;
            line = int.Parse(prefix.Substring(open + 1, comma - open - 1), CultureInfo.InvariantCulture);
            column = int.Parse(prefix.Substring(comma + 1, prefix.Length - comma - 3), CultureInfo.InvariantCulture);
            file = prefix.Substring(0, open);
        }

        private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        private static string ReadBoundaryHash(FileStream stream, long offset)
        {
            int count = (int)Math.Min(BoundaryBytes, offset);
            stream.Position = offset - count;
            var bytes = new byte[count];
            int read = 0;
            while (read < count)
            {
                int next = stream.Read(bytes, read, count - read);
                if (next == 0) throw new EndOfStreamException("The diagnostic window boundary is missing.");
                read += next;
            }
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(bytes));
        }

        private static bool IsCaptureException(Exception exception) =>
            exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
            exception is ArgumentException || exception is NotSupportedException ||
            exception is SecurityException ||
            exception is FormatException || exception is OverflowException;

        internal Dictionary<string, object> ToDictionary() => new()
        {
            ["revision"] = Revision, ["logPath"] = LogPath, ["fileCreationUtc"] = FileCreationUtc,
            ["boundaryHash"] = BoundaryHash, ["startedAt"] = StartedAt.ToString("O"),
            ["startOffset"] = StartOffset, ["endOffset"] = EndOffset,
            ["captureComplete"] = CaptureComplete, ["captureIssue"] = CaptureIssue,
            ["errorCount"] = ErrorCount, ["warningCount"] = WarningCount, ["entries"] = Entries,
        };

        internal static VmBurstCompilationDiagnostics FromDictionary(Dictionary<string, object> value)
        {
            var entries = new List<Dictionary<string, object>>();
            foreach (object entry in (System.Collections.IEnumerable)value["entries"])
                entries.Add((Dictionary<string, object>)entry);
            return new VmBurstCompilationDiagnostics
            {
                Revision = (string)value["revision"], LogPath = (string)value["logPath"],
                FileCreationUtc = (string)value["fileCreationUtc"], BoundaryHash = (string)value["boundaryHash"],
                StartedAt = DateTime.Parse((string)value["startedAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                StartOffset = Convert.ToInt64(value["startOffset"], CultureInfo.InvariantCulture),
                EndOffset = value["endOffset"] == null ? null : Convert.ToInt64(value["endOffset"], CultureInfo.InvariantCulture),
                CaptureComplete = (bool)value["captureComplete"], CaptureIssue = (string)value["captureIssue"],
                ErrorCount = Convert.ToInt32(value["errorCount"], CultureInfo.InvariantCulture),
                WarningCount = Convert.ToInt32(value["warningCount"], CultureInfo.InvariantCulture), Entries = entries,
            };
        }
    }
}
