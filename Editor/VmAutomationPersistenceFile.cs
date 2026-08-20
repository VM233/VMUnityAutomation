using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Owns publication and adoption of Automation-authored file snapshots. Callers provide complete
    /// immutable products; this type serializes same-process access by target path and publishes
    /// through a flushed private file so readers never adopt an in-place partial write.
    /// </summary>
    internal static class VmAutomationPersistenceFile
    {
        private const int MaxTransientIoAttempts = 8;
        private const int Win32ErrorUnableToRemoveReplaced = 1175;
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly StringComparer PathComparer =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        private static readonly ConcurrentDictionary<string, object> PathLocks =
            new ConcurrentDictionary<string, object>(PathComparer);

        internal static bool TryReadAllText(string path, out string contents,
            Encoding encoding = null)
        {
            string targetPath = NormalizePath(path);
            lock (GetPathLock(targetPath))
            {
                try
                {
                    contents = RetryTransientIo(() =>
                        File.ReadAllText(targetPath, encoding ?? Utf8WithoutBom));
                    return true;
                }
                catch (FileNotFoundException)
                {
                    contents = "";
                    return false;
                }
                catch (DirectoryNotFoundException)
                {
                    contents = "";
                    return false;
                }
            }
        }

        internal static string ReadAllText(string path, Encoding encoding = null)
        {
            string targetPath = NormalizePath(path);
            lock (GetPathLock(targetPath))
            {
                return RetryTransientIo(() =>
                    File.ReadAllText(targetPath, encoding ?? Utf8WithoutBom));
            }
        }

        internal static byte[] ReadAllBytes(string path)
        {
            string targetPath = NormalizePath(path);
            lock (GetPathLock(targetPath))
            {
                return RetryTransientIo(() => File.ReadAllBytes(targetPath));
            }
        }

        internal static void WriteAllText(string path, string contents,
            Encoding encoding = null, string backupPath = null)
        {
            Encoding selectedEncoding = encoding ?? Utf8WithoutBom;
            byte[] preamble = selectedEncoding.GetPreamble();
            byte[] body = selectedEncoding.GetBytes(contents ?? "");
            var snapshot = new byte[preamble.Length + body.Length];
            if (preamble.Length > 0)
                Buffer.BlockCopy(preamble, 0, snapshot, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, snapshot, preamble.Length, body.Length);
            WriteAllBytes(path, snapshot, backupPath);
        }

        internal static void WriteAllBytes(string path, byte[] contents, string backupPath = null)
        {
            string targetPath = NormalizePath(path);
            string normalizedBackupPath = string.IsNullOrWhiteSpace(backupPath)
                ? null
                : NormalizePath(backupPath);
            if (normalizedBackupPath != null &&
                PathComparer.Equals(targetPath, normalizedBackupPath))
            {
                throw new ArgumentException("The persistence backup path must differ from the target path.",
                    nameof(backupPath));
            }

            byte[] immutableSnapshot = contents == null
                ? Array.Empty<byte>()
                : (byte[])contents.Clone();

            WithPathLocks(targetPath, normalizedBackupPath, () =>
            {
                string directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string temporaryPath = targetPath + ".vm-unity-automation-" +
                                       Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    WritePrivateSnapshot(temporaryPath, immutableSnapshot);
                    RetryTransientIo(() =>
                    {
                        PublishPrivateSnapshot(temporaryPath, targetPath, normalizedBackupPath);
                        return true;
                    });
                }
                finally
                {
                    TryDeletePrivateSnapshot(temporaryPath);
                }
            });
        }

        internal static bool DeleteIfExists(string path)
        {
            string targetPath = NormalizePath(path);
            lock (GetPathLock(targetPath))
            {
                try
                {
                    return RetryTransientIo(() =>
                    {
                        if (!File.Exists(targetPath))
                            return false;
                        File.Delete(targetPath);
                        return true;
                    });
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (DirectoryNotFoundException)
                {
                    return false;
                }
            }
        }

        private static object GetPathLock(string normalizedPath)
        {
            return PathLocks.GetOrAdd(normalizedPath, _ => new object());
        }

        private static void WithPathLocks(string targetPath, string backupPath, Action action)
        {
            object targetLock = GetPathLock(targetPath);
            if (backupPath == null)
            {
                lock (targetLock)
                    action();
                return;
            }

            object backupLock = GetPathLock(backupPath);
            object firstLock = PathComparer.Compare(targetPath, backupPath) <= 0
                ? targetLock
                : backupLock;
            object secondLock = ReferenceEquals(firstLock, targetLock)
                ? backupLock
                : targetLock;
            lock (firstLock)
            {
                lock (secondLock)
                    action();
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A persistence file path is required.", nameof(path));
            return Path.GetFullPath(path);
        }

        private static void WritePrivateSnapshot(string path, byte[] snapshot)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(snapshot, 0, snapshot.Length);
                stream.Flush(true);
            }
        }

        private static void PublishPrivateSnapshot(string temporaryPath, string targetPath,
            string backupPath)
        {
            if (!File.Exists(targetPath))
            {
                if (backupPath != null && File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(temporaryPath, targetPath);
                return;
            }

            if (backupPath != null && File.Exists(backupPath))
                File.Delete(backupPath);

            File.Replace(temporaryPath, targetPath, backupPath, true);
        }

        private static T RetryTransientIo<T>(Func<T> operation)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (Exception exception) when (
                    attempt < MaxTransientIoAttempts && IsTransientIo(exception))
                {
                    Thread.Sleep(Math.Min(250, 10 << Math.Min(attempt - 1, 4)));
                }
            }
        }

        internal static bool IsTransientIo(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (!(current is IOException ioException))
                    continue;

                int nativeCode = ioException.HResult & 0xFFFF;
                // ReplaceFile guarantees that error 1175 preserves both original names.
                // Errors 1176 and 1177 can leave a partial rename and must fail closed.
                if (nativeCode == 32 || nativeCode == 33 ||
                    nativeCode == Win32ErrorUnableToRemoveReplaced || nativeCode == 1224)
                    return true;
            }
            return false;
        }

        private static void TryDeletePrivateSnapshot(string path)
        {
            if (!File.Exists(path))
                return;
            try
            {
                RetryTransientIo(() =>
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    return true;
                });
            }
            catch
            {
                // Preserve the publication exception. Unique private names prevent a later writer
                // from adopting or deleting this unpublished file.
            }
        }
    }
}
