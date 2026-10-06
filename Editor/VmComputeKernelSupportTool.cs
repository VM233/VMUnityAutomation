using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("shader/compute-kernel-support",
        Description = "Request one imported ComputeShader's exact native kernel program and report ComputeShader.IsSupported for the current graphics device. A supported program also reports native thread-group dimensions. This may compile the device-specific program and emit or populate native compiler messages; it does not dispatch, reimport, clear diagnostics or write Assets. Use shader/diagnostics separately to read those messages.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState,
        Preconditions = new[] { "editor-connected" },
        ErrorCodes = new[] { "compute_asset_not_found", "compute_kernel_not_found" },
        CompletionEvidence = "Exact imported compute asset, authored kernel identity, current graphics device, native support state and supported program dimensions; unsupported dimensions are absent.")]
    public sealed class VmComputeKernelSupportTool :
        IVmProjectTool<VmComputeKernelSupportRequest, VmComputeKernelSupportResult>
    {
        public VmComputeKernelSupportResult Execute(VmComputeKernelSupportRequest request)
        {
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(request.AssetPath);
            if (compute == null)
                throw new VmProjectToolException("compute_asset_not_found",
                    $"No imported ComputeShader exists at '{request.AssetPath}'.");
            if (!compute.HasKernel(request.KernelName))
                throw new VmProjectToolException("compute_kernel_not_found",
                    $"ComputeShader '{request.AssetPath}' has no kernel '{request.KernelName}'.");
            int kernel = compute.FindKernel(request.KernelName);
            bool supported = compute.IsSupported(kernel);
            var result = new VmComputeKernelSupportResult
            {
                AssetPath = AssetDatabase.GetAssetPath(compute), ShaderName = compute.name,
                KernelName = request.KernelName, KernelIndex = kernel,
                GraphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(), IsSupported = supported
            };
            if (supported)
            {
                compute.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);
                result.ThreadsX = x; result.ThreadsY = y; result.ThreadsZ = z;
            }
            return result;
        }
    }
}
