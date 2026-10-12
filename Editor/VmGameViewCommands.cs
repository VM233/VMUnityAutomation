using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmGameViewCommands
    {
        internal static object Presets(Dictionary<string, object> args) => Invoke(() => new VmGameViewNative().Inventory());
        internal static object ReloadPresets(Dictionary<string, object> args) => Invoke(() => VmGameViewPreferencesSource.instance.Reload());

        internal static object SetResolution(Dictionary<string, object> args) => Invoke(() =>
        {
            RequireNoSession();
            var request = VmAutomationInvocationArguments.Bind<VmGameViewResolutionRequest>(args);
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            int index = native.FindResolution(request.Width, request.Height);
            if (index < 0)
                throw new VmProjectToolException("game_view_resolution_unavailable",
                    "No existing fixed-resolution preset matches these dimensions. Use gameview/resolution-session Begin, then End in finally, for temporary validation.");
            native.Select(window, index);
            return new VmGameViewResolutionResult { View = native.Read(window), Inventory = native.Inventory() };
        });

        internal static object Session(Dictionary<string, object> args) => Invoke(() =>
            VmGameViewResolutionSession.instance.Execute(VmAutomationInvocationArguments.Bind<VmGameViewSessionRequest>(args)));

        internal static object RemovePresets(Dictionary<string, object> args) => Invoke(() =>
        {
            RequireNoSession();
            var request = VmAutomationInvocationArguments.Bind<VmGameViewRemovePresetsRequest>(args);
            var native = new VmGameViewNative();
            var inventory = native.Inventory();
            if (request.ExpectedFingerprint != inventory.Fingerprint)
                throw new VmProjectToolException("game_view_presets_changed", "The complete preset table changed. Read gameview/presets and review the exact targets again.");
            if (!inventory.PreferenceSnapshotLoaded || request.ExpectedPreferenceFileHash != inventory.PreferenceFileHash)
                throw new VmProjectToolException("game_view_preference_snapshot_required", "Reload the latest saved global preferences through gameview/reload-presets before reviewing removal targets. The native table and file revision must still match that source.");
            if (request.Presets.Length > 128)
                throw new VmProjectToolException("invalid_arguments", "At most 128 exact custom records may be removed.");
            var targets = new HashSet<int>();
            foreach (var preset in request.Presets)
            {
                if (preset.Index < 0 || preset.Index >= inventory.Presets.Length || !targets.Add(preset.Index))
                    throw new VmProjectToolException("invalid_arguments", "Preset targets must be unique existing indices.");
                var actual = inventory.Presets[preset.Index];
                if (actual.Builtin)
                    throw new VmProjectToolException("game_view_builtin_preset", "Builtin Game View presets cannot be removed.");
                if (actual.BaseText != preset.BaseText || actual.SizeType != preset.SizeType || actual.Width != preset.Width || actual.Height != preset.Height || actual.Builtin != preset.Builtin)
                    throw new VmProjectToolException("game_view_presets_changed", "A removal target no longer matches its complete native record.");
            }
            var views = native.Windows();
            var selections = new object[views.Length];
            var zooms = new Vector2[views.Length];
            for (int i = 0; i < views.Length; i++)
            {
                if (targets.Contains(native.SelectedIndex(views[i])))
                    throw new VmProjectToolException("game_view_selected_preset", "A removal target is selected by an existing Game View. Explicitly select an existing replacement first.");
                selections[i] = native.SelectedSize(views[i]);
                zooms[i] = native.Zoom(views[i]);
            }
            foreach (int index in targets.OrderByDescending(value => value)) native.Remove(index);
            var remaining = native.Indices();
            for (int i = 0; i < views.Length; i++)
            {
                native.Select(views[i], remaining[selections[i]]);
                native.RestoreZoom(views[i], zooms[i]);
            }
            native.SavePreferences();
            return new VmGameViewRemovePresetsResult { RemovedCount = targets.Count, GlobalPreferencesSaved = true, Inventory = native.Inventory() };
        });

        internal static bool TryOutputSchema(string route, out Dictionary<string, object> schema)
        {
            Type type;
            switch (route)
            {
                case "gameview/presets": type = typeof(VmGameViewPresetInventory); break;
                case "gameview/reload-presets": type = typeof(VmGameViewPresetInventory); break;
                case "gameview/set-resolution": type = typeof(VmGameViewResolutionResult); break;
                case "gameview/resolution-session": type = typeof(VmGameViewSessionResult); break;
                case "gameview/remove-presets": type = typeof(VmGameViewRemovePresetsResult); break;
                default: schema = null; return false;
            }
            schema = VmJsonContract.CreateSchema(type);
            return true;
        }

        private static void RequireNoSession()
        {
            if (VmGameViewResolutionSession.instance.Active)
                throw new VmProjectToolException("game_view_session_active", "End the active resolution session before changing presets or selecting an unscoped resolution.");
        }

        private static object Invoke(Func<object> action)
        {
            try { return VmJsonContract.ToTransportValue(action()); }
            catch (VmProjectToolException error) { return VmAutomationResponse.Error(error.Message, error.ErrorCode); }
        }
    }
}
