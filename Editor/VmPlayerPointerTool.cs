using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("player/window-pointer",
        Description = "Inspect the real Windows pointer receiver, move the cursor, or send a complete left click to an exact running Unity Player. Coordinates are desktop pixels. Click requires the cursor already at the point and Win32 to identify that Player as the actual opaque receiver; allow a Player frame after Move. Never clicks another application, focuses a window or retains a held button.",
        MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ChangesRuntimeState | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "invalid_player_pointer_arguments", "player_pointer_platform_unsupported",
            "player_pointer_process_not_running", "player_identity_mismatch", "player_pointer_window_unavailable",
            "player_pointer_receiver_mismatch", "player_pointer_button_held", "player_pointer_native_failed" },
        CompletionEvidence = "Actual Win32 window style, receiver PID/window and cursor; Click also reports native inserted event count. Player UI effects require a later runtime observation.")]
    public sealed class VmPlayerPointerTool : IVmProjectTool<VmPlayerPointerRequest, VmPlayerPointerResult>
    {
        public VmPlayerPointerResult Execute(VmPlayerPointerRequest request)
        {
#if !UNITY_EDITOR_WIN
            throw new VmProjectToolException("player_pointer_platform_unsupported", "Native Player pointer requires Windows.");
#else
            if (!Enum.IsDefined(typeof(VmPlayerPointerAction), request.Action) || request.ProcessId < 1 ||
                string.IsNullOrEmpty(request.ExecutablePath) || !Path.IsPathRooted(request.ExecutablePath) ||
                request.StartedAt == null || request.StartedAt.Length != 28 ||
                !DateTime.TryParseExact(request.StartedAt, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime startedAt) || startedAt.Kind != DateTimeKind.Utc)
                throw new VmProjectToolException("invalid_player_pointer_arguments", "Supply an exact Player launch identity and declared action.");
            string path = Path.GetFullPath(request.ExecutablePath);
            if (!File.Exists(path) || !File.Exists(Path.Combine(Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path) + "_Data", "globalgamemanagers")))
                throw new VmProjectToolException("invalid_player_pointer_arguments", "The executable must name an existing Windows Unity Player.");
            Process process;
            try { process = Process.GetProcessById(request.ProcessId); }
            catch (ArgumentException)
            {
                throw new VmProjectToolException("player_pointer_process_not_running", "The requested Player PID no longer runs.");
            }
            using (process)
            {
                _ = process.Handle;
                if (process.WaitForExit(0))
                    throw new VmProjectToolException("player_pointer_process_not_running", "The requested Player already exited.");
                VmPlayerProcessIdentity.Verify(process, path, startedAt);
                IntPtr window = process.MainWindowHandle;
                GetWindowThreadProcessId(window, out uint owner);
                if (window == IntPtr.Zero || owner != (uint)request.ProcessId)
                    throw new VmProjectToolException("player_pointer_window_unavailable", "The verified Player has no owned main window.");
                if (request.Action == VmPlayerPointerAction.Move && !SetCursorPos(request.X, request.Y))
                    throw NativeError();
                if (!GetCursorPos(out Point cursor)) throw NativeError();
                var point = new Point { X = request.X, Y = request.Y };
                IntPtr receiver = WindowFromPoint(point);
                GetWindowThreadProcessId(receiver, out uint receiverOwner);
                int style = GetWindowLongW(window, -20);
                int inserted = 0;
                if (request.Action == VmPlayerPointerAction.Click)
                {
                    if (cursor.X != point.X || cursor.Y != point.Y || receiverOwner != owner || (style & 0x20) != 0)
                        throw new VmProjectToolException("player_pointer_receiver_mismatch", "The cursor and opaque native receiver must already identify the requested Player at this point.");
                    if ((GetAsyncKeyState(1) & 0x8000) != 0)
                        throw new VmProjectToolException("player_pointer_button_held", "The physical left button is already held.");
                    var input = new[]
                    {
                        new MouseInput { Mouse = new MouseData { Flags = 0x0002 } },
                        new MouseInput { Mouse = new MouseData { Flags = 0x0004 } }
                    };
                    inserted = (int)SendInput(2, input, Marshal.SizeOf(typeof(MouseInput)));
                    if (inserted != 2)
                    {
                        int nativeError = Marshal.GetLastWin32Error();
                        uint released = inserted == 1 ? SendInput(1, new[] { input[1] },
                            Marshal.SizeOf(typeof(MouseInput))) : 0;
                        throw new VmProjectToolException("player_pointer_native_failed",
                            $"SendInput inserted {inserted}/2 events (Win32 {nativeError}); partial-press cleanup inserted {released}/1 release events.");
                    }
                }
                return new VmPlayerPointerResult
                {
                    PlayerWindow = window.ToString(), PlayerExtendedStyle = style,
                    PlayerIsClickThrough = (style & 0x20) != 0, ReceiverWindow = receiver.ToString(),
                    ReceiverProcessId = receiverOwner, ReceiverIsPlayer = receiverOwner == owner,
                    CursorX = cursor.X, CursorY = cursor.Y, InsertedInputEvents = inserted
                };
            }
#endif
        }

        private static VmProjectToolException NativeError() => new("player_pointer_native_failed",
            new Win32Exception(Marshal.GetLastWin32Error()).Message);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseData
        {
            public int X, Y;
            public uint Data, Flags, Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput { public uint Type; public MouseData Mouse; }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int GetWindowLongW(IntPtr window, int index);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, MouseInput[] input, int size);
    }
}
