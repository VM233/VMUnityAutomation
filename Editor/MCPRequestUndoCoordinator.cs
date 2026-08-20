using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Isolates synchronous MCP mutations into request-owned Undo groups. A group is targetable
    /// only while its in-memory ownership token is current; reloads and intervening edits revoke it.
    /// </summary>
    [InitializeOnLoad]
    internal static class MCPRequestUndoCoordinator
    {
        private static readonly Dictionary<long, Ownership> ByRequestId = new();
        private static readonly Dictionary<long, Ownership> ByActionId = new();
        private static Ownership activeOwnership;
        private static Ownership redoCandidate;
        private static bool applyingOwnedUndo;

        static MCPRequestUndoCoordinator()
        {
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.willFlushUndoRecord -= OnWillFlushUndoRecord;
            Undo.willFlushUndoRecord += OnWillFlushUndoRecord;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        internal static bool IsControlRoute(string route)
        {
            return !string.IsNullOrEmpty(route) &&
                   route.StartsWith("undo/", StringComparison.Ordinal);
        }

        internal static Ownership Begin(long requestId, string actionName, bool eligible)
        {
            if (!eligible) return null;
            InvalidateAvailable("superseded_by_later_request");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            string groupName = $"VM Unity Automation request {requestId}: {actionName}";
            Undo.SetCurrentGroupName(groupName);
            activeOwnership = new Ownership
            {
                RequestId = requestId,
                ActionName = actionName ?? "",
                UndoGroup = group,
                UndoGroupName = groupName,
                Status = "recording",
            };
            return activeOwnership;
        }

        internal static void Complete(Ownership ownership, bool committed)
        {
            if (ownership == null) return;
            bool boundaryCreated = false;
            try
            {
                Undo.FlushUndoRecordObjects();
                if (!committed || !ownership.ObservedUndoModification)
                {
                    ownership.Status = "unavailable";
                    ownership.UnavailableReason = committed
                        ? "no_unity_undo_record"
                        : "request_not_completed";
                    return;
                }

                Undo.CollapseUndoOperations(ownership.UndoGroup);
                Undo.IncrementCurrentGroup();
                boundaryCreated = true;
                ownership.BoundaryGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"VM Unity Automation boundary {ownership.RequestId}");
                ownership.Status = "available";
                redoCandidate = null;
            }
            finally
            {
                if (ReferenceEquals(activeOwnership, ownership))
                    activeOwnership = null;
                if (!boundaryCreated)
                    Undo.IncrementCurrentGroup();
            }
        }

        internal static void RegisterAction(MCPActionRecord record, Ownership ownership)
        {
            if (record == null || ownership == null) return;
            record.RequestId = ownership.RequestId;
            record.UndoGroup = ownership.UndoGroup;
            record.UndoGroupName = ownership.UndoGroupName;
            record.UndoStatus = ownership.Status;
            record.UndoUnavailableReason = ownership.UnavailableReason;
            ownership.ActionId = record.Id;
            ownership.Record = record;
            if (ownership.Status == "available")
            {
                ByRequestId[ownership.RequestId] = ownership;
                ByActionId[record.Id] = ownership;
            }
        }

        internal static object PerformUndo(Dictionary<string, object> args)
        {
            if (!TryResolveOwnership(args, out Ownership ownership, out object error))
                return error;
            if (ownership.Status != "available")
            {
                return MCPResponse.Error(
                    $"Request {ownership.RequestId} is not directionally undoable: " +
                    $"{ownership.UnavailableReason ?? ownership.Status}.",
                    "undo_request_not_available", false,
                    Describe(ownership));
            }
            if (Undo.GetCurrentGroup() != ownership.BoundaryGroup)
            {
                Invalidate(ownership, "unity_undo_group_advanced");
                return MCPResponse.Error(
                    "The Unity Undo group advanced after this MCP request; targeted undo would affect unrelated work.",
                    "undo_request_not_latest", false, Describe(ownership));
            }

            applyingOwnedUndo = true;
            try
            {
                Undo.RevertAllDownToGroup(ownership.UndoGroup);
            }
            finally
            {
                applyingOwnedUndo = false;
            }
            ownership.Status = "undone";
            if (ownership.Record != null)
                ownership.Record.UndoStatus = ownership.Status;
            redoCandidate = ownership;
            MCPActionHistory.NotifyRecordUpdated();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "requestId", ownership.RequestId },
                { "actionId", ownership.ActionId },
                { "undoGroup", ownership.UndoGroup },
                { "undoStatus", ownership.Status },
            };
        }

        internal static object PerformRedo(Dictionary<string, object> args)
        {
            if (!TryResolveOwnership(args, out Ownership ownership, out object error))
                return error;
            if (!ReferenceEquals(ownership, redoCandidate) || ownership.Status != "undone")
            {
                return MCPResponse.Error(
                    "Redo is available only for the exact request most recently undone through this API.",
                    "redo_request_not_available", false, Describe(ownership));
            }

            applyingOwnedUndo = true;
            try
            {
                Undo.PerformRedo();
            }
            finally
            {
                applyingOwnedUndo = false;
            }
            ownership.Status = "redone";
            if (ownership.Record != null)
                ownership.Record.UndoStatus = ownership.Status;
            redoCandidate = null;
            MCPActionHistory.NotifyRecordUpdated();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "requestId", ownership.RequestId },
                { "actionId", ownership.ActionId },
                { "undoStatus", ownership.Status },
            };
        }

        internal static void InvalidateAll(string reason)
        {
            foreach (Ownership ownership in ByRequestId.Values.ToList())
                Invalidate(ownership, reason);
            ByRequestId.Clear();
            ByActionId.Clear();
            redoCandidate = null;
        }

        private static UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            if (applyingOwnedUndo)
                return modifications;
            if (activeOwnership != null)
                activeOwnership.ObservedUndoModification = true;
            else
                InvalidateAvailable("intervening_unowned_undo_modification");
            return modifications;
        }

        private static void OnWillFlushUndoRecord()
        {
            if (applyingOwnedUndo) return;
            if (activeOwnership != null)
                activeOwnership.ObservedUndoModification = true;
            else
                InvalidateAvailable("intervening_unowned_undo_record");
        }

        private static void OnUndoRedoPerformed()
        {
            if (!applyingOwnedUndo)
                InvalidateAvailable("intervening_unowned_undo_or_redo");
        }

        private static void InvalidateAvailable(string reason)
        {
            foreach (Ownership ownership in ByRequestId.Values
                         .Where(item => item.Status == "available" || item.Status == "undone")
                         .ToList())
                Invalidate(ownership, reason);
        }

        private static void Invalidate(Ownership ownership, string reason)
        {
            if (ownership == null || ownership.Status == "unavailable") return;
            ownership.Status = "unavailable";
            ownership.UnavailableReason = reason;
            if (ownership.Record != null)
            {
                ownership.Record.UndoStatus = ownership.Status;
                ownership.Record.UndoUnavailableReason = reason;
            }
            ByRequestId.Remove(ownership.RequestId);
            if (ownership.ActionId > 0)
                ByActionId.Remove(ownership.ActionId);
            if (ReferenceEquals(redoCandidate, ownership))
                redoCandidate = null;
            MCPActionHistory.NotifyRecordUpdated();
        }

        private static bool TryResolveOwnership(Dictionary<string, object> args,
            out Ownership ownership, out object error)
        {
            ownership = null;
            error = null;
            if (TryGetLong(args, "actionId", out long actionId))
                ByActionId.TryGetValue(actionId, out ownership);
            else if (TryGetLong(args, "requestId", out long requestId))
                ByRequestId.TryGetValue(requestId, out ownership);
            else
            {
                error = MCPResponse.Error(
                    "actionId or requestId is required; global latest-operation undo is not supported.",
                    "invalid_arguments");
                return false;
            }

            if (ownership != null) return true;
            error = MCPResponse.Error(
                "The requested MCP Undo identity is unavailable in this Unity domain.",
                "undo_request_not_available", false);
            return false;
        }

        private static Dictionary<string, object> Describe(Ownership ownership)
        {
            return new Dictionary<string, object>
            {
                { "requestId", ownership.RequestId },
                { "actionId", ownership.ActionId },
                { "actionName", ownership.ActionName ?? "" },
                { "undoGroup", ownership.UndoGroup },
                { "undoStatus", ownership.Status ?? "unavailable" },
                { "undoUnavailableReason", ownership.UnavailableReason ?? "" },
            };
        }

        private static bool TryGetLong(Dictionary<string, object> values, string key,
            out long result)
        {
            result = 0;
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null && long.TryParse(value.ToString(), out result);
        }

        internal sealed class Ownership
        {
            internal long RequestId;
            internal long ActionId;
            internal string ActionName;
            internal int UndoGroup;
            internal int BoundaryGroup;
            internal string UndoGroupName;
            internal string Status;
            internal string UnavailableReason;
            internal bool ObservedUndoModification;
            internal MCPActionRecord Record;
        }
    }
}
