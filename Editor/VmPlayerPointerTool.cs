using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("player/window-pointer",
        Description = "Inspect the real Windows receiver or send native Move, Click, DoubleClick or complete Drag to an exact running Unity Player. Coordinates are desktop pixels. Press gestures require the cursor already at the start and Win32 to identify the Player as opaque receiver; allow a Player frame after Move. DoubleClick separates press/release phases within the OS interval, with at most150ms total pacing. Drag requires endX/endY and sends 20 motion steps over 400ms with release in finally. Never focuses a window or retains a held button. Observe Player UI effects afterward.",
        MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ChangesRuntimeState | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "invalid_player_pointer_arguments", "player_pointer_platform_unsupported",
            "invalid_player_identity_arguments", "player_process_not_running", "player_identity_mismatch", "player_window_unavailable",
            "player_pointer_receiver_mismatch", "player_pointer_button_held", "player_pointer_native_failed" },
        CompletionEvidence = "Actual Win32 window style, receiver PID/window and cursor; Move and Click also report native inserted event count. Player UI effects require a later runtime observation.")]
    public sealed class VmPlayerPointerTool : IVmProjectTool<VmPlayerPointerRequest, VmPlayerPointerResult>
    {
        public VmPlayerPointerResult Execute(VmPlayerPointerRequest request)
        {
#if !UNITY_EDITOR_WIN
            throw new VmProjectToolException("player_pointer_platform_unsupported", "Native Player pointer requires Windows.");
#else
            if (!Enum.IsDefined(typeof(VmPlayerPointerAction), request.Action))
                throw new VmProjectToolException("invalid_player_pointer_arguments", "Supply a declared pointer action.");
            ValidateGesture(request);
            using (Process process = VmPlayerProcessIdentity.Open(request))
            {
                IntPtr window = VmPlayerProcessIdentity.MainWindow(process);
                uint owner = (uint)process.Id;
                int inserted = 0;
                if (request.Action == VmPlayerPointerAction.Move)
                {
                    var motion = new[] { new MouseInput { Mouse = new MouseData
                    {
                        X = NormalizeAbsoluteCoordinate(request.X, GetSystemMetrics(76), GetSystemMetrics(78)),
                        Y = NormalizeAbsoluteCoordinate(request.Y, GetSystemMetrics(77), GetSystemMetrics(79)),
                        Flags = 0x0001 | 0x4000 | 0x8000
                    } } };
                    inserted = (int)SendInput(1, motion, Marshal.SizeOf(typeof(MouseInput)));
                    if (inserted != 1) throw NativeError();
                }
                if (!GetCursorPos(out Point cursor)) throw NativeError();
                var point = new Point { X = request.X, Y = request.Y };
                IntPtr receiver = WindowFromPoint(point);
                GetWindowThreadProcessId(receiver, out uint receiverOwner);
                int style = GetWindowLongW(window, -20);
                if (request.Action == VmPlayerPointerAction.Click || request.Action == VmPlayerPointerAction.DoubleClick ||
                    request.Action == VmPlayerPointerAction.Drag)
                {
                    if (cursor.X != point.X || cursor.Y != point.Y || receiverOwner != owner || (style & 0x20) != 0)
                        throw new VmProjectToolException("player_pointer_receiver_mismatch", "The cursor and opaque native receiver must already identify the requested Player at this point.");
                    if ((GetAsyncKeyState(1) & 0x8000) != 0)
                        throw new VmProjectToolException("player_pointer_button_held", "The physical left button is already held.");
                    if (request.Action == VmPlayerPointerAction.Drag)
                    {
                        inserted = Drag(request);
                    }
                    else if (request.Action == VmPlayerPointerAction.DoubleClick)
                    {
                        inserted = DoubleClick(pressed => SendOne(new MouseData { Flags = pressed ? 0x0002u : 0x0004u }),
                            System.Threading.Thread.Sleep, GetDoubleClickTime());
                    }
                    else
                    {
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
                }
                if (!GetCursorPos(out cursor)) throw NativeError();
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

        internal static void ValidateGesture(VmPlayerPointerRequest request)
        {
            bool destination = request.EndX.HasValue && request.EndY.HasValue;
            if (request.Action == VmPlayerPointerAction.Drag ? !destination : request.EndX.HasValue || request.EndY.HasValue)
                throw new VmProjectToolException("invalid_player_pointer_arguments", "Only Drag requires both endX and endY; other actions omit them.");
        }

        internal static int DoubleClick(Action<bool> send, Action<int> wait, uint nativeInterval)
        {
            // UI input collectors need distinct press/release phases, rather than four events in one frame.
            int phase = (int)Math.Min(50u, nativeInterval / 4);
            int inserted = 0;
            for (int click = 0; click < 2; click++)
            {
                try
                {
                    send(true);
                    inserted++;
                    wait(phase);
                }
                finally { send(false); inserted++; }
                if (click == 0) wait(phase);
            }
            return inserted;
        }

        private static int Drag(VmPlayerPointerRequest request)
        {
            // Validate the entire path's destination before pressing. Virtual desktop bounds are convex.
            int originX = GetSystemMetrics(76), originY = GetSystemMetrics(77);
            int width = GetSystemMetrics(78), height = GetSystemMetrics(79);
            NormalizeAbsoluteCoordinate(request.EndX.Value, originX, width);
            NormalizeAbsoluteCoordinate(request.EndY.Value, originY, height);
            int inserted = 0;
            try
            {
                inserted += SendOne(new MouseData { Flags = 0x0002 });
                for (int step = 1; step <= 20; step++)
                {
                    System.Threading.Thread.Sleep(20);
                    int x = (int)(request.X + ((long)request.EndX.Value - request.X) * step / 20);
                    int y = (int)(request.Y + ((long)request.EndY.Value - request.Y) * step / 20);
                    inserted += SendOne(new MouseData { X = NormalizeAbsoluteCoordinate(x, originX, width),
                        Y = NormalizeAbsoluteCoordinate(y, originY, height), Flags = 0x0001 | 0x4000 | 0x8000 });
                }
            }
            finally { inserted += SendOne(new MouseData { Flags = 0x0004 }); }
            return inserted;
        }

        private static int SendOne(MouseData data)
        {
            if (SendInput(1, new[] { new MouseInput { Mouse = data } }, Marshal.SizeOf(typeof(MouseInput))) != 1)
                throw NativeError();
            return 1;
        }

        internal static int NormalizeAbsoluteCoordinate(int pixel, int origin, int size)
        {
            long offset = (long)pixel - origin;
            if (size < 1 || size > 65536 || offset < 0 || offset >= size)
                throw new VmProjectToolException("invalid_player_pointer_arguments",
                    "The requested pixel must lie inside a virtual desktop axis of 1..65536 pixels.");
            // Aim at the pixel center so Win32's integer mapping returns that pixel.
            return (int)Math.Min(65535L, (offset * 65536 + 32768) / size);
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

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);
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
        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, MouseInput[] input, int size);
    }
}
