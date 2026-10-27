using System;
using System.IO;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    public static class VmEditorWindowRecording
    {
        private static VmEditorWindowRecordingSession current;

        static VmEditorWindowRecording()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Interrupt;
            EditorApplication.quitting += Interrupt;
        }

        public static VmEditorWindowRecordingResult Execute(VmEditorWindowRecordingRequest request)
        {
            if (request.Action == "Begin") return Begin(request);
            if (current == null || current.Result.SessionId != request.SessionId)
                throw new VmProjectToolException("recording_session_identity", "The recording session is not owned by this Editor lifetime.");
            if (request.Action == "End") current.End();
            else if (request.Action != "Inspect") Invalid("Action must be Begin, Inspect or End.");
            return current.Result;
        }

        public static VmEditorWindowRecordingResult Begin(VmEditorWindowRecordingRequest request,
            Action<double> beforeCapture = null, Action afterCapture = null)
        {
            if (current != null && !current.Result.ResourcesReleased)
                throw new VmProjectToolException("recording_already_active", "Finish the current recording before starting another.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying))
                Invalid("Recording admission requires a stable Editor state.");
            if (double.IsNaN(request.DurationSeconds) || double.IsInfinity(request.DurationSeconds) || request.DurationSeconds <= 0 || request.DurationSeconds > 30 ||
                request.FramesPerSecond < 1 || request.FramesPerSecond > 15 || request.MaxDimension < 256 || request.MaxDimension > 4096)
                Invalid("Duration must be in (0,30], fps in [1,15], and maxDimension in [256,4096].");
            EditorWindow window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null) Invalid("windowInstanceId must identify an existing EditorWindow.");
            if (!Path.IsPathRooted(request.OutputPath ?? "") || !request.OutputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                request.OutputPath.Contains('"') || !Path.IsPathRooted(request.EncoderPath ?? "") || !File.Exists(request.EncoderPath))
                Invalid("Provide an absolute new .mp4 path and an existing absolute FFmpeg executable path.");
            string output = Path.GetFullPath(request.OutputPath);
            string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            if (output.StartsWith(assets, StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output + ".frames"))
                Invalid("Output must be new and outside Assets, with no existing .frames directory.");
            current = new VmEditorWindowRecordingSession(request, window, beforeCapture, afterCapture);
            current.Start();
            return current.Result;
        }

        private static void Interrupt() => current?.Interrupt();

        private static void Invalid(string message) =>
            throw new VmProjectToolException("recording_invalid_arguments", message);
    }
}
