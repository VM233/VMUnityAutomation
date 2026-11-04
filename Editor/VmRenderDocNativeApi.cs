using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VMUnityAutomation.Editor
{
    // The versioned prefix is defined by RenderDoc's renderdoc_app.h, API 1.6.
    // Unity loads the graphics debugger; this adapter never loads or injects it.
    internal sealed class VmRenderDocNativeApi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Api
        {
            public IntPtr Version, SetU32, SetF32, GetU32, GetF32;
            public IntPtr FocusKeys, CaptureKeys, Overlay, MaskOverlay, RemoveHooks, UnloadCrash;
            public IntPtr SetPath, GetPath, Count, Capture;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GetApi(int version, out IntPtr api);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetPath(IntPtr path);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetPath();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint GetCount();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint GetCapture(uint index, IntPtr path, ref uint length, out ulong timestamp);
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        private readonly Api api;
        internal VmRenderDocNativeApi()
        {
#if UNITY_EDITOR_WIN
            var module = GetModuleHandle("renderdoc.dll");
            var entry = module == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(module, "RENDERDOC_GetAPI");
            if (entry == IntPtr.Zero || Marshal.GetDelegateForFunctionPointer<GetApi>(entry)(10600, out var pointer) != 1)
                throw new InvalidOperationException("The already-loaded RenderDoc API 1.6 is unavailable.");
            api = Marshal.PtrToStructure<Api>(pointer);
#else
            throw new PlatformNotSupportedException("This capture adapter requires the Windows RenderDoc integration.");
#endif
        }
        internal uint Count => Marshal.GetDelegateForFunctionPointer<GetCount>(api.Count)();
        internal string PathTemplate
        {
            get => Utf8(Marshal.GetDelegateForFunctionPointer<GetPath>(api.GetPath)());
            set
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
                var pointer = Marshal.AllocHGlobal(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, pointer, bytes.Length);
                    Marshal.GetDelegateForFunctionPointer<SetPath>(api.SetPath)(pointer);
                }
                finally { Marshal.FreeHGlobal(pointer); }
            }
        }
        internal string CapturePath(uint index)
        {
            var get = Marshal.GetDelegateForFunctionPointer<GetCapture>(api.Capture);
            uint length = 0;
            if (get(index, IntPtr.Zero, ref length, out _) != 1 || length == 0 || length > 32768)
                throw new InvalidOperationException("Native RenderDoc capture path was unavailable or exceeded its bound.");
            var pointer = Marshal.AllocHGlobal((int)length);
            try
            {
                if (get(index, pointer, ref length, out _) != 1)
                    throw new InvalidOperationException("Native RenderDoc capture lookup failed.");
                return Utf8(pointer);
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        private static string Utf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return "";
            int length = 0;
            while (length < 32768 && Marshal.ReadByte(pointer, length) != 0) length++;
            if (length == 32768) throw new InvalidOperationException("RenderDoc path exceeds32767 UTF-8 bytes.");
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
