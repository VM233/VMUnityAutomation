using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VMUnityAutomation.Editor
{
    internal static class VmPlayerLogTail
    {
        internal const int MaximumBytes = 1024 * 1024;

        internal static Dictionary<string, object> Read(string path, int maxLines)
        {
            if (maxLines < 1 || maxLines > 2000) throw new ArgumentOutOfRangeException(nameof(maxLines));
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Result("", false, 0);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long length = stream.Length;
            int count = (int)Math.Min(length, MaximumBytes);
            long offset = length - count;
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] bytes = new byte[count];
            int read = 0;
            while (read < count)
            {
                int next = stream.Read(bytes, read, count - read);
                if (next == 0) break;
                read += next;
            }
            string text = Encoding.UTF8.GetString(bytes, 0, read);
            if (offset > 0)
            {
                int firstNewline = text.IndexOf('\n');
                text = firstNewline < 0 ? "" : text.Substring(firstNewline + 1);
            }
            else if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int end = lines.Length;
            if (end > 0 && lines[end - 1].Length == 0) end--;
            int start = Math.Max(0, end - maxLines);
            return Result(string.Join("\n", lines, start, end - start), offset > 0 || start > 0, read);
        }

        private static Dictionary<string, object> Result(string text, bool truncated, int bytesRead) =>
            new Dictionary<string, object>
            {
                { "text", text }, { "truncated", truncated }, { "bytesRead", bytesRead },
            };
    }
}
