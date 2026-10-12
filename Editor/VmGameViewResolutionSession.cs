using System;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    internal sealed class VmGameViewResolutionSession : ScriptableSingleton<VmGameViewResolutionSession>
    {
        [NonSerialized] private VmGameViewNative native;
        [NonSerialized] private EditorWindow window;
        [NonSerialized] private object originalSize;
        [NonSerialized] private object temporarySize;
        [NonSerialized] private Vector2 originalZoom;
        [NonSerialized] private string sessionId;

        static VmGameViewResolutionSession()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Retire;
            EditorApplication.quitting += Retire;
            EditorUserBuildSettings.activeBuildTargetChanged += Retire;
        }

        internal bool Active => native != null;

        internal VmGameViewSessionResult Execute(VmGameViewSessionRequest request)
        {
            if (request.Action == VmGameViewSessionAction.Begin)
            {
                if (Active)
                    throw new VmProjectToolException("game_view_session_active", "End the active resolution session before beginning another.");
                if (!string.IsNullOrEmpty(request.SessionId))
                    throw new VmProjectToolException("invalid_arguments", "Begin creates its own sessionId.");
                ValidateDimensions(request);
                var source = new VmGameViewNative();
                var target = source.SingleWindow();
                originalSize = source.SelectedSize(target);
                originalZoom = source.Zoom(target);
                native = source;
                window = target;
                sessionId = Guid.NewGuid().ToString("N");
                EditorApplication.update += RetireClosedView;
                try { return Resize(request.Width.Value, request.Height.Value); }
                catch { End(); throw; }
            }
            if (!Active || request.SessionId != sessionId)
                throw new VmProjectToolException("game_view_session_identity", "Resize and End require the active sessionId capability.");
            if (request.Action == VmGameViewSessionAction.End)
            {
                if (request.Width.HasValue || request.Height.HasValue)
                    throw new VmProjectToolException("invalid_arguments", "End does not accept dimensions.");
                return End();
            }
            try
            {
                ValidateDimensions(request);
                return Resize(request.Width.Value, request.Height.Value);
            }
            catch { End(); throw; }
        }

        private static void ValidateDimensions(VmGameViewSessionRequest request)
        {
            if (!request.Width.HasValue || !request.Height.HasValue ||
                request.Width.Value < 10 || request.Width.Value > 8192 ||
                request.Height.Value < 10 || request.Height.Value > 8192)
                throw new VmProjectToolException("invalid_arguments", "Begin and Resize require integer dimensions in 10..8192 pixels.");
        }

        private VmGameViewSessionResult Resize(int width, int height)
        {
            if (new VmGameViewNative().GroupName != native.GroupName)
                throw new VmProjectToolException("game_view_session_group_changed", "The active build-target size group changed during the resolution session.");
            int existing = native.FindResolution(width, height, temporarySize);
            if (existing >= 0) native.Select(window, existing);
            else
            {
                if (temporarySize == null) temporarySize = native.AddTemporary(width, height);
                else native.ResizeTemporary(temporarySize, width, height);
                native.Select(window, native.Indices()[temporarySize]);
            }
            return Result(true);
        }

        internal VmGameViewSessionResult End()
        {
            var source = native;
            string identity = sessionId;
            var views = source.Windows();
            var selections = new object[views.Length];
            var zooms = new Vector2[views.Length];
            for (int i = 0; i < views.Length; i++)
            {
                selections[i] = views[i] == window || ReferenceEquals(source.SelectedSize(views[i]), temporarySize)
                    ? originalSize : source.SelectedSize(views[i]);
                zooms[i] = views[i] == window ? originalZoom : source.Zoom(views[i]);
            }
            if (temporarySize != null)
            {
                source.Remove(source.Indices()[temporarySize]);
                temporarySize = null;
            }
            var indices = source.Indices();
            for (int i = 0; i < views.Length; i++)
            {
                source.Select(views[i], indices[selections[i]]);
                if (source.IsCurrentGroup) source.RestoreZoom(views[i], zooms[i]);
            }
            var result = new VmGameViewSessionResult
            {
                SessionId = identity, Active = false, TemporaryPresetOwned = false,
                View = window != null && source.IsCurrentGroup ? source.Read(window) : null, Inventory = source.Inventory()
            };
            EditorApplication.update -= RetireClosedView;
            native = null; window = null; originalSize = null; temporarySize = null; sessionId = null;
            return result;
        }

        private VmGameViewSessionResult Result(bool active) => new VmGameViewSessionResult
        {
            SessionId = sessionId, Active = active, TemporaryPresetOwned = temporarySize != null,
            View = native.Read(window), Inventory = native.Inventory()
        };

        internal static void Retire()
        {
            if (instance.Active) instance.End();
        }

        private void RetireClosedView()
        {
            if (Active && window == null) End();
        }
    }
}
