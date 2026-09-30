using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationJobHistoryRepair
    {
        internal static object Execute(Dictionary<string, object> args)
        {
            string id = args.TryGetValue("repairId", out object value) ? value?.ToString() : "";
            if (!Guid.TryParseExact(id, "N", out _))
                return VmAutomationResponse.Error("repairId must be a 32-character UUID.", "invalid_arguments");
            string action = args.TryGetValue("action", out value) ? value?.ToString() : "status";
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Library", "VMUnityAutomation", "history-repairs");
            string receiptPath = Path.Combine(directory, id + ".json");
            if (VmAutomationPersistenceFile.TryReadAllText(receiptPath, out string receipt))
                return MiniJson.Deserialize(receipt);
            if (action == "status")
                return VmAutomationResponse.Error("This repair has no completed receipt.", "history_repair_not_found");
            if (action != "repair")
                return VmAutomationResponse.Error("action must be repair or status.", "invalid_arguments");
            if (!VmAutomationRuntimePreconditions.TryRequireEditMode("jobs/repair-history",
                    "history reconstruction must not compete with runtime execution", out var error))
                return error;
            try
            {
                var result = VmAutomationJobHistory.RestoreWorkspaceRecords();
                bool reload = args.TryGetValue("reloadDomain", out value) && value is bool requested && requested;
                result["success"] = true;
                result["repairId"] = id;
                result["status"] = "reconstructed";
                result["reloadRequested"] = reload;
                VmAutomationPersistenceFile.WriteAllText(receiptPath, MiniJson.Serialize(result));
                if (reload) EditorUtility.RequestScriptReload();
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                return VmAutomationResponse.Error(exception.Message, "history_repair_failed", false);
            }
        }
    }
}
