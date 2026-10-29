using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace VMUnityAutomation.Editor.Localization
{
    public static class VmLocalizationInitializationStateTool
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public |
                                                   BindingFlags.NonPublic;
        private const string OperationSchema = @"{
            ""type"":[""object"",""null""],""additionalProperties"":false,
            ""properties"":{
                ""handleState"":{""type"":""string"",""enum"":[""None"",""Invalid"",""Valid""]},
                ""operationType"":{""type"":[""string"",""null""]},
                ""status"":{""type"":[""string"",""null""]},
                ""isDone"":{""type"":[""boolean"",""null""]},
                ""exceptionType"":{""type"":[""string"",""null""]}
            },
            ""required"":[""handleState"",""operationType"",""status"",""isDone"",""exceptionType""]
        }";
        private const string OutputSchema = @"{
            ""type"":""object"",""additionalProperties"":false,
            ""properties"":{
                ""hasActiveSettings"":{""type"":""boolean""},
                ""settingsAssetPath"":{""type"":[""string"",""null""]},
                ""isPlaying"":{""type"":""boolean""},""frame"":{""type"":""integer""},
                ""stageInspectionAvailable"":{""type"":""boolean""},
                ""root"":" + OperationSchema + @",
                ""current"":" + OperationSchema + @",
                ""dependency"":" + OperationSchema + @",
                ""bundleUnloads"":{
                    ""type"":""object"",""additionalProperties"":false,
                    ""properties"":{
                        ""available"":{""type"":""boolean""},
                        ""registered"":{""type"":[""integer"",""null""]},
                        ""pending"":{""type"":[""integer"",""null""]},
                        ""completed"":{""type"":[""integer"",""null""]}
                    },""required"":[""available"",""registered"",""pending"",""completed""]
                }
            },
            ""required"":[""hasActiveSettings"",""settingsAssetPath"",""isPlaying"",""frame"",
                ""stageInspectionAvailable"",""root"",""current"",""dependency"",""bundleUnloads""]
        }";

        [VmProjectTool("localization/initialization-state",
            Description = "Observe the cached Localization initialization handle, its current stage and Addressables dependency without starting, waiting for or resetting initialization. Also reads bounded native bundle-unload counts when that installed API exists.",
            InputSchemaJson = @"{""type"":""object"",""additionalProperties"":false,""properties"":{}}",
            OutputSchemaJson = OutputSchema,
            ReadOnly = true,
            SideEffects = VmProjectToolSideEffect.ReadsProjectState,
            ErrorCodes = new[] { "localization_diagnostic_contract_unavailable", "localization_diagnostic_limit" },
            CompletionEvidence = "A bounded snapshot distinguishes absent, default, invalid and valid cached handles. No initialization, handle acquisition/release, wait, progress getter or callback is requested.")]
        public static Dictionary<string, object> GetState(Dictionary<string, object> arguments)
        {
            return Capture(LocalizationEditorSettings.ActiveLocalizationSettings);
        }

        private static Dictionary<string, object> Capture(LocalizationSettings settings)
        {
            var result = new Dictionary<string, object>
            {
                { "hasActiveSettings", settings != null },
                { "settingsAssetPath", settings == null ? null : Bounded(AssetDatabase.GetAssetPath(settings), 4096) },
                { "isPlaying", EditorApplication.isPlaying },
                { "frame", Time.frameCount },
                { "stageInspectionAvailable", false },
                { "root", null }, { "current", null }, { "dependency", null },
                { "bundleUnloads", ReadBundleUnloads() }
            };
            if (settings == null)
                return result;

            var field = typeof(LocalizationSettings).GetField("m_InitializingOperationHandle", InstanceFlags);
            if (field == null || field.FieldType != typeof(AsyncOperationHandle<LocalizationSettings>))
                throw ContractUnavailable("The installed Localization cached-handle field is unavailable.");
            var handle = (AsyncOperationHandle<LocalizationSettings>)field.GetValue(settings);
            AsyncOperationHandle untyped = handle;
            result["root"] = Snapshot(untyped);
            if (!handle.IsValid())
                return result;

            object operation = InternalOperation(untyped);
            Type operationType = operation.GetType();
            if (operationType.Assembly != typeof(LocalizationSettings).Assembly)
                return result;
            var current = operationType.GetProperty("CurrentOperation", InstanceFlags);
            var dependency = operationType.GetProperty("Dependency", InstanceFlags);
            if (current?.PropertyType != typeof(AsyncOperationHandle) ||
                dependency?.PropertyType != typeof(AsyncOperationHandle))
                throw ContractUnavailable("The installed Localization stage-handle contract is unavailable.");
            result["stageInspectionAvailable"] = true;
            result["current"] = Snapshot((AsyncOperationHandle)current.GetValue(operation));
            result["dependency"] = Snapshot((AsyncOperationHandle)dependency.GetValue(operation));
            return result;
        }

        private static Dictionary<string, object> Snapshot(AsyncOperationHandle handle)
        {
            bool valid = handle.IsValid();
            return new Dictionary<string, object>
            {
                { "handleState", valid ? "Valid" : handle.Equals(default(AsyncOperationHandle)) ? "None" : "Invalid" },
                { "operationType", valid ? Bounded(InternalOperation(handle).GetType().FullName, 512) : null },
                { "status", valid ? handle.Status.ToString() : null },
                { "isDone", valid ? (object)handle.IsDone : null },
                { "exceptionType", valid && handle.OperationException != null
                    ? Bounded(handle.OperationException.GetType().FullName, 512) : null }
            };
        }

        private static object InternalOperation(AsyncOperationHandle handle)
        {
            var field = typeof(AsyncOperationHandle).GetField("m_InternalOp", InstanceFlags);
            if (field == null)
                throw ContractUnavailable("The installed ResourceManager operation field is unavailable.");
            return field.GetValue(handle);
        }

        private static Dictionary<string, object> ReadBundleUnloads()
        {
            var property = typeof(AssetBundleProvider).GetProperty("UnloadingBundles",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var result = new Dictionary<string, object>
            {
                { "available", property != null }, { "registered", null },
                { "pending", null }, { "completed", null }
            };
            if (property == null)
                return result;
            if (!(property.GetValue(null) is IDictionary entries))
                throw ContractUnavailable("The installed native bundle-unload registry is not a dictionary.");
            if (entries.Count > 128)
                throw new VmProjectToolException("localization_diagnostic_limit",
                    "The native bundle-unload registry exceeds the128-entry observation contract.");

            int pending = 0;
            foreach (DictionaryEntry entry in entries)
            {
                if (!(entry.Value is AsyncOperation operation))
                    throw ContractUnavailable("The native bundle-unload registry contains an unsupported value.");
                if (!operation.isDone)
                    pending++;
            }
            result["registered"] = entries.Count;
            result["pending"] = pending;
            result["completed"] = entries.Count - pending;
            return result;
        }

        private static string Bounded(string value, int limit)
        {
            if (value != null && value.Length > limit)
                throw new VmProjectToolException("localization_diagnostic_limit",
                    "Localization observation metadata exceeds its declared character bound.");
            return value;
        }

        private static VmProjectToolException ContractUnavailable(string message)
        {
            return new VmProjectToolException("localization_diagnostic_contract_unavailable", message);
        }
    }
}
