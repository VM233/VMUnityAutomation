using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationManagedMethodCommands
    {
        private const int MaximumNameBytes = 1024;
        private static readonly string DomainLifetime = Guid.NewGuid().ToString("N");

        public static object ReadManagedRuntime(Dictionary<string, object> args)
        {
#if UNITY_EDITOR_WIN
            using var process = Process.GetCurrentProcess();
            DateTime startedAt = process.StartTime.ToUniversalTime();
            int domainId = AppDomain.CurrentDomain.Id;
            string runtimeId = string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}",
                process.Id, startedAt.Ticks, DomainLifetime);
            bool resolveAddresses = args.ContainsKey("methodAddresses");
            if (resolveAddresses && !string.Equals(
                    (string)args["expectedRuntimeId"], runtimeId, StringComparison.Ordinal))
                return VmAutomationResponse.Error(
                    "The capture's process or managed domain is no longer current.",
                    "managed_runtime_changed");

            var methods = new List<object>();
            if (resolveAddresses)
            {
                IntPtr domain = MonoDomainGet();
                foreach (object value in (System.Collections.IEnumerable)args["methodAddresses"])
                {
                    string address = (string)value;
                    ulong instruction = ulong.Parse(address.Substring(2),
                        NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                    IntPtr info = MonoJitInfoTableFind(domain, new IntPtr(unchecked((long)instruction)));
                    if (info == IntPtr.Zero)
                    {
                        methods.Add(new Dictionary<string, object>
                        {
                            { "address", address }, { "resolved", false },
                            { "codeStart", null }, { "codeSize", null },
                            { "namespace", null }, { "className", null },
                            { "methodName", null }, { "imageName", null },
                            { "metadataToken", null },
                        });
                        continue;
                    }

                    IntPtr method = MonoJitInfoGetMethod(info);
                    IntPtr klass = MonoMethodGetClass(method);
                    methods.Add(new Dictionary<string, object>
                    {
                        { "address", address }, { "resolved", true },
                        { "codeStart", "0x" + unchecked((ulong)MonoJitInfoGetCodeStart(info).ToInt64())
                            .ToString("x16", CultureInfo.InvariantCulture) },
                        { "codeSize", MonoJitInfoGetCodeSize(info) },
                        { "namespace", ReadName(MonoClassGetNamespace(klass)) },
                        { "className", ReadName(MonoClassGetName(klass)) },
                        { "methodName", ReadName(MonoMethodGetName(method)) },
                        { "imageName", ReadName(MonoImageGetName(MonoClassGetImage(klass))) },
                        { "metadataToken", "0x" + MonoMethodGetToken(method)
                            .ToString("x8", CultureInfo.InvariantCulture) },
                    });
                }
            }
            return new Dictionary<string, object>
            {
                { "runtimeId", runtimeId }, { "processId", process.Id },
                { "processStartedAt", startedAt.ToString("O", CultureInfo.InvariantCulture) },
                { "managedDomainId", domainId }, { "resolvedMethods", methods },
            };
#else
            return VmAutomationResponse.Error(
                "Live Mono JIT method addresses are available in Windows Editors.",
                "capability_unavailable");
#endif
        }

#if UNITY_EDITOR_WIN
        private static string ReadName(IntPtr name)
        {
            var bytes = new byte[MaximumNameBytes];
            for (int index = 0; index < bytes.Length; index++)
            {
                byte value = Marshal.ReadByte(name, index);
                if (value == 0)
                    return Encoding.UTF8.GetString(bytes, 0, index);
                bytes[index] = value;
            }
            throw new InvalidOperationException("Mono method name exceeds the 1,024-byte query budget.");
        }

        private const string MonoLibrary = "mono-2.0-bdwgc.dll";
        [DllImport(MonoLibrary, EntryPoint = "mono_domain_get", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoDomainGet();
        [DllImport(MonoLibrary, EntryPoint = "mono_jit_info_table_find", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoJitInfoTableFind(IntPtr domain, IntPtr address);
        [DllImport(MonoLibrary, EntryPoint = "mono_jit_info_get_method", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoJitInfoGetMethod(IntPtr info);
        [DllImport(MonoLibrary, EntryPoint = "mono_jit_info_get_code_start", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoJitInfoGetCodeStart(IntPtr info);
        [DllImport(MonoLibrary, EntryPoint = "mono_jit_info_get_code_size", CallingConvention = CallingConvention.Cdecl)]
        private static extern int MonoJitInfoGetCodeSize(IntPtr info);
        [DllImport(MonoLibrary, EntryPoint = "mono_method_get_class", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoMethodGetClass(IntPtr method);
        [DllImport(MonoLibrary, EntryPoint = "mono_method_get_name", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoMethodGetName(IntPtr method);
        [DllImport(MonoLibrary, EntryPoint = "mono_method_get_token", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint MonoMethodGetToken(IntPtr method);
        [DllImport(MonoLibrary, EntryPoint = "mono_class_get_namespace", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoClassGetNamespace(IntPtr klass);
        [DllImport(MonoLibrary, EntryPoint = "mono_class_get_name", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoClassGetName(IntPtr klass);
        [DllImport(MonoLibrary, EntryPoint = "mono_class_get_image", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoClassGetImage(IntPtr klass);
        [DllImport(MonoLibrary, EntryPoint = "mono_image_get_name", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr MonoImageGetName(IntPtr image);
#endif
    }
}
