using System;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerLaunchArgumentsTests
    {
#if UNITY_EDITOR_WIN
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr pointer);

        [Test]
        public void WindowsNativeParserPreservesEveryArgumentAndOwnedLogPath()
        {
            string executable = @"C:\Player Folder\Game.exe";
            string logPath = @"C:\Logs Folder\Leak Trace.log";
            string[] arguments = { "", "plain", "two words", "中文", "a\"b", @"C:\tail\", "line\ttab", @"two\\\" };
            string encoded = VmPlayerLaunchArguments.Encode(executable, arguments, logPath);
            IntPtr native = CommandLineToArgvW("\"" + executable + "\" " + encoded, out int count);
            Assert.That(native, Is.Not.EqualTo(IntPtr.Zero));
            try
            {
                var actual = new string[count];
                for (int index = 0; index < count; index++)
                    actual[index] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(native, index * IntPtr.Size));
                Assert.That(actual, Is.EqualTo(new[] { executable }.Concat(arguments).Concat(new[] { "-logFile", logPath })));
            }
            finally { LocalFree(native); }
        }
#endif

        [TestCase("-logFile")]
        [TestCase("-LOGFILE=other.log")]
        public void InvalidEntriesAreRejectedBeforeLaunch(string value) =>
            Assert.Throws<VmProjectToolException>(() => VmPlayerLaunchArguments.Encode(
                @"C:\Game.exe", new[] { value }, @"C:\Game.log"));

        [Test]
        public void EmbeddedNulIsRejectedBeforeLaunch() =>
            Assert.Throws<VmProjectToolException>(() => VmPlayerLaunchArguments.Encode(
                @"C:\Game.exe", new[] { "a\0b" }, @"C:\Game.log"));

        [Test]
        public void ArgumentCountAndEncodedLengthHaveFiniteAdmission()
        {
            Assert.Throws<VmProjectToolException>(() => VmPlayerLaunchArguments.Encode(
                @"C:\Game.exe", new string[65], @"C:\Game.log"));
            Assert.Throws<VmProjectToolException>(() => VmPlayerLaunchArguments.Encode(
                @"C:\Game.exe", Enumerable.Repeat(new string('\\', 4096), 8).ToArray(), @"C:\Game.log"));
        }

        [Test]
        public void LaunchIsAnExactTypedMutationContract()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("player/launch", true, out var contract), Is.True);
            string route = (string)contract["route"];
            Assert.That(VmAutomationCatalog.IsRouteReadOnly(route), Is.False);
            Assert.That(VmAutomationCatalog.RouteRequiresTargetBinding(route), Is.True);
            Assert.That(contract["inputSchema"], Is.Not.Null);
            Assert.That(contract["outputSchema"], Is.Not.Null);
        }
    }
}
