using System;
using System.IO;
using static VMUnityAutomation.Editor.VmPngResizePreparation;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("image/resize",
        Description = "Resize one local 8-bit PNG before Unity import. Preserve aspect ratio and alpha, write only the explicit output PNG, and return dimensions and verified hashes. Never modify PPU or import settings.",
        MutatesProjectFiles = true,
        SideEffects = VmProjectToolSideEffect.PerformsExternalIO |
                      VmProjectToolSideEffect.CreatesTemporaryObjects,
        ErrorCodes = new[] { "invalid_resize_arguments", "image_source_not_found",
            "image_output_exists", "image_output_is_unity_asset", "image_size_limit",
            "image_format_unsupported", "image_read_failed", "image_decode_failed",
            "image_encode_failed", "image_write_failed", "image_verification_failed" },
        Preconditions = new[] { "absolute-source-and-output-paths", "existing-output-directory",
            "distinct-source-and-output", "output-outside-assets-and-packages" },
        CompletionEvidence = "Executed results include exact source/output dimensions, SHA-256 and byte count. verified=true requires a matching on-disk readback. dryRun validates headers only and reports verified=false.")]
    public sealed class VmImageResizeTool : IVmProjectTool<VmImageResizeRequest, VmImageResizeResult>
    {
        public VmImageResizeResult Execute(VmImageResizeRequest request)
        {
            if (request == null)
                throw Failure("invalid_resize_arguments", "A resize request is required.");
            string sourcePath = AbsolutePngPath(request.SourcePath, "sourcePath");
            string outputPath = AbsolutePngPath(request.OutputPath, "outputPath");
            if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
                throw Failure("invalid_resize_arguments", "Source and output must be different files.");
            ValidateDimensions(request);
            ValidateOutput(outputPath, request.Overwrite);
            PreparedImage prepared = Prepare(request, !request.DryRun,
                2L * MaxPixels, 2L * MaxFileBytes);
            if (request.DryRun)
                return new VmImageResizeResult(sourcePath, outputPath, prepared.SourceWidth,
                    prepared.SourceHeight, prepared.Width, prepared.Height, prepared.Filter,
                    prepared.SourceHash, "", 0, true);
            byte[] outputBytes = prepared.Bytes;
            try
            {
                using (var stream = new FileStream(outputPath,
                           request.Overwrite ? FileMode.Create : FileMode.CreateNew,
                           FileAccess.Write, FileShare.None))
                {
                    stream.Write(outputBytes, 0, outputBytes.Length);
                    stream.Flush();
                }
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                throw Failure("image_write_failed",
                    $"Could not write '{outputPath}'. The output may be incomplete: {exception.Message}");
            }

            prepared.VerifyFile(outputPath);
            return new VmImageResizeResult(sourcePath, outputPath, prepared.SourceWidth,
                prepared.SourceHeight, prepared.Width, prepared.Height, prepared.Filter,
                prepared.SourceHash, prepared.OutputHash, outputBytes.Length, false);
        }

        private static void ValidateOutput(string path, bool overwrite)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (string protectedName in new[] { "Assets", "Packages" })
            {
                string root = Path.Combine(projectRoot, protectedName) + Path.DirectorySeparatorChar;
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw Failure("image_output_is_unity_asset",
                        $"Output '{path}' is a Unity asset. Use asset/import with resize for direct Unity adoption.");
            }
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                throw Failure("invalid_resize_arguments", $"Output directory does not exist for '{path}'.");
            if (File.Exists(path) && !overwrite)
                throw Failure("image_output_exists", $"Output '{path}' exists. Set overwrite=true to replace it.");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw Failure("invalid_resize_arguments", $"Output '{path}' is a symbolic link.");
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(path));
            for (int depth = 0; directory != null; depth++, directory = directory.Parent)
            {
                if (depth >= 128)
                    throw Failure("invalid_resize_arguments", "Output directory ancestry exceeds 128 entries.");
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw Failure("invalid_resize_arguments", $"Output directory '{directory.FullName}' is a symbolic link or junction.");
            }
        }

    }
}
