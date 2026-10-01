using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabBatchEditor;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;
using static VMUnityAutomation.Editor.VmAutomationPrefabYamlUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationPrefabTransactionCommands
    {
    private static object ApplyTransactionImmediate(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        var operations = GetDictionaryList(args, "operations");
        if (operations.Count == 0)
            return new { error = "operations must contain at least one operation" };

        if (!VmAutomationPrefabComponentTypeAdmission.TryValidate(
                assetPath, CollectBatchEditComponentTypes(args), out object admissionError))
            return admissionError;

        var beforeSnapshot = CaptureAssetText(assetPath);
        GameObject root;
        try
        {
            root = PrefabUtility.LoadPrefabContents(assetPath);
        }
        catch (Exception ex)
        {
            return new { error = $"Failed to load prefab at '{assetPath}': {ex.Message}" };
        }

        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                var summaries = new List<Dictionary<string, object>>();
                for (int i = 0; i < operations.Count; i++)
                {
                    if (!TryApplyBatchOperation(root, operations[i], i, out var summary,
                            out string error))
                    {
                        return new Dictionary<string, object>
                        {
                            { "error", error },
                            { "success", false },
                            { "saved", false },
                            { "failedOperationIndex", i },
                            { "failedOperation", operations[i] },
                            { "appliedOperationCount", summaries.Count },
                            { "operationSummaries", summaries },
                        };
                    }

                    summaries.Add(summary);
                }

                string prefabName = root.name;
                if (session.SaveAndClose(CollectExplicitYamlPropertyRoots(operations)) == null)
                {
                    throw new InvalidOperationException(
                        $"SaveAsPrefabAsset returned null for '{assetPath}'.");
                }

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "saved", true },
                    { "prefab", prefabName },
                    { "assetPath", assetPath },
                    { "operationCount", operations.Count },
                    { "operationSummaries", summaries },
                };
                AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return VmAutomationResponse.Error(
                    $"Failed to edit prefab transaction: {ex.Message}",
                    "transaction_edit_failed");
            }
        }
    }

    public static object TransactionEdit(Dictionary<string, object> args)
    {
        args = PrepareTransactionEditArguments(args);
        if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
            return new { success = false, error = executionError };
        if (execution.ContinueOnError)
            return new { success = false, error = "execution.continueOnError is not supported by atomic prefab transactions" };
        var result = AddTransactionEditMetadata(ApplyTransactionImmediate(args), args);
        AddExecutionMetadata(result, execution, GetDictionaryList(args, "operations").Count);
        return result;
    }

    public static object ConfigureComponent(Dictionary<string, object> args)
    {
        return TransactionEdit(BuildConfigureComponentTransactionArguments(args));
    }

    public static void ConfigureComponentDeferred(Dictionary<string, object> args, Action<object> resolve,
        Action<object> progress)
    {
        TransactionEditDeferred(BuildConfigureComponentTransactionArguments(args), resolve, progress);
    }

    private static Dictionary<string, object> BuildConfigureComponentTransactionArguments(
        Dictionary<string, object> args)
    {
        args = args ?? new Dictionary<string, object>();
        var transactionArgs = new Dictionary<string, object>(args);
        var operation = new Dictionary<string, object>
        {
            { "type", "configureComponent" },
            { "prefabPath", GetString(args, "prefabPath") },
            { "componentType", GetString(args, "componentType") },
            { "componentIndex", GetInt(args, "componentIndex", 0) },
            { "addIfMissing", GetBool(args, "addIfMissing", true) },
            { "createPathIfMissing", GetBool(args, "createPathIfMissing", false) },
        };

        var properties = GetDictionary(args, "properties");
        if (properties != null)
            operation["properties"] = properties;

        var references = GetDictionaryList(args, "references");
        if (references.Count > 0)
            operation["references"] = references.Cast<object>().ToList();

        transactionArgs["operations"] = new List<object> { operation };
        return transactionArgs;
    }

    public static void TransactionEditDeferred(Dictionary<string, object> args, Action<object> resolve,
        Action<object> progress)
    {
        args = PrepareTransactionEditArguments(args);
        if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
        {
            resolve(new { success = false, error = executionError });
            return;
        }
        if (execution.ContinueOnError)
        {
            resolve(new { success = false, error = "execution.continueOnError is not supported by atomic prefab transactions" });
            return;
        }
        BatchEditDeferred(args, execution, result =>
        {
            result = AddTransactionEditMetadata(result, args);
            AddExecutionMetadata(result, execution, GetDictionaryList(args, "operations").Count);
            resolve(result);
        }, progress);
    }

    private static void AddExecutionMetadata(object result, VmAutomationExecutionOptions execution, int operationCount)
    {
        if (result is Dictionary<string, object> dictionary)
            dictionary["execution"] = execution.ToResult(operationCount);
    }

    private static Dictionary<string, object> PrepareTransactionEditArguments(Dictionary<string, object> args)
    {
        if (args == null)
            args = new Dictionary<string, object>();

        if (args.ContainsKey("includePrefabFileDiff") == false)
        {
            args["includePrefabFileDiff"] =
                VmAutomationSettings.IncludePrefabFileDiffByDefault;
        }
        if (args.ContainsKey("prefabFileDiffMode") == false)
            args["prefabFileDiffMode"] = "summary";
        if (args.ContainsKey("prefabFileDiffContextLines") == false)
            args["prefabFileDiffContextLines"] = 0;

        return args;
    }

    private static object AddTransactionEditMetadata(object batchResult, Dictionary<string, object> args)
    {
        var result = batchResult as Dictionary<string, object>;
        if (result == null)
            return batchResult;

        result["transaction"] = new Dictionary<string, object>
        {
            { "assetPath", GetString(args, "assetPath") },
            { "operationCount", GetDictionaryList(args, "operations").Count },
            { "diffMode", GetString(args, "prefabFileDiffMode") },
            { "saved", result.TryGetValue("saved", out var saved) && saved is bool savedValue && savedValue },
        };

        return result;
    }

    private static void BatchEditDeferred(Dictionary<string, object> args, VmAutomationExecutionOptions execution,
        Action<object> resolve,
        Action<object> progress)
    {
        if (!VmAutomationPrefabComponentTypeAdmission.TryValidate(
                GetString(args, "assetPath"), CollectBatchEditComponentTypes(args),
                out object admissionError))
        {
            resolve(admissionError);
            return;
        }
        StartBatchEditDeferred(args, execution, resolve, progress);
    }

    private sealed class BatchEditDeferredState
    {
        public string AssetPath;
        public List<Dictionary<string, object>> Operations;
        public PrefabMutationSession Session;
        public readonly List<Dictionary<string, object>> Summaries = new List<Dictionary<string, object>>();
        public int NextOperationIndex;
        public double StartedAt;
        public int TimeoutMs;
        public int OperationsPerFrame;
        public int FrameBudgetMs;
        public VmAutomationExecutionOptions Execution;

        public AssetTextSnapshot BeforeSnapshot => Session?.BeforeSnapshot;
        public GameObject Root => Session?.Root;
        public bool SaveAttempted => Session != null && Session.SaveAttempted;
        public bool Saved => Session != null && Session.Committed;
    }

    private static void StartBatchEditDeferred(Dictionary<string, object> args, VmAutomationExecutionOptions execution,
        Action<object> resolve,
        Action<object> progress)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
        {
            resolve(new Dictionary<string, object>
            {
                { "error", "assetPath is required" },
                { "success", false },
                { "saved", false },
                { "saveAttempted", false },
                { "partialPersisted", false },
                { "partialPersistedKnown", true },
                { "persistedState", "none" },
            });
            return;
        }

        var operations = GetDictionaryList(args, "operations");
        if (operations.Count == 0)
        {
            resolve(new Dictionary<string, object>
            {
                { "error", "operations must contain at least one operation" },
                { "success", false },
                { "saved", false },
                { "saveAttempted", false },
                { "partialPersisted", false },
                { "partialPersistedKnown", true },
                { "persistedState", "none" },
            });
            return;
        }

        bool runBatched = execution.ResolveMode(operations.Count) == VmAutomationExecutionMode.Batched;
        var beforeSnapshot = CaptureAssetText(assetPath);
        var state = new BatchEditDeferredState
        {
            AssetPath = assetPath,
            Operations = operations,
            StartedAt = EditorApplication.timeSinceStartup,
            TimeoutMs = execution.TimeoutMs,
            OperationsPerFrame = runBatched ? execution.OperationsPerFrame : int.MaxValue,
            FrameBudgetMs = runBatched ? execution.FrameBudgetMs : int.MaxValue,
            Execution = execution,
        };

        try
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root != null)
                state.Session = new PrefabMutationSession(assetPath, beforeSnapshot, root);
        }
        catch (Exception ex)
        {
            resolve(new Dictionary<string, object>
            {
                { "error", $"Failed to load prefab at '{assetPath}': {ex.Message}" },
                { "success", false },
                { "saved", false },
                { "saveAttempted", false },
                { "partialPersisted", false },
                { "partialPersistedKnown", true },
                { "persistedState", "none" },
            });
            return;
        }

        if (state.Root == null)
        {
            resolve(new Dictionary<string, object>
            {
                { "error", $"Failed to load prefab at '{assetPath}'" },
                { "success", false },
                { "saved", false },
                { "saveAttempted", false },
                { "partialPersisted", false },
                { "partialPersistedKnown", true },
                { "persistedState", "none" },
            });
            return;
        }

        EditorApplication.CallbackFunction tick = null;
        Action<object> complete = result =>
        {
            if (tick != null)
                EditorApplication.update -= tick;

            resolve(FinalizeBatchEditResult(state, result));
        };

        tick = () =>
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                int elapsedMs = (int)((now - state.StartedAt) * 1000d);
                if (elapsedMs >= state.TimeoutMs)
                {
                    complete(BuildBatchEditFailure(state, args,
                        $"Prefab transaction timed out after {state.TimeoutMs} ms before saving.",
                        "transaction_edit_timeout", true, elapsedMs, state.NextOperationIndex));
                    return;
                }

                double frameStartedAt = EditorApplication.timeSinceStartup;
                int processedThisFrame = 0;
                while (state.NextOperationIndex < state.Operations.Count)
                {
                    int operationIndex = state.NextOperationIndex;
                    if (!TryApplyBatchOperation(state.Root, state.Operations[operationIndex], operationIndex,
                            out var summary, out string error))
                    {
                        complete(BuildBatchEditFailure(state, args, error, "batch_operation_failed",
                            false, elapsedMs, operationIndex, state.Operations[operationIndex]));
                        return;
                    }

                    state.Summaries.Add(summary);
                    state.NextOperationIndex++;
                    processedThisFrame++;

                    progress?.Invoke(BuildBatchEditProgress(state, elapsedMs, "applying"));

                    double frameElapsedMs = (EditorApplication.timeSinceStartup - frameStartedAt) * 1000d;
                    if (processedThisFrame >= state.OperationsPerFrame ||
                        frameElapsedMs >= state.FrameBudgetMs)
                    {
                        return;
                    }

                    elapsedMs = (int)((EditorApplication.timeSinceStartup - state.StartedAt) * 1000d);
                    if (elapsedMs >= state.TimeoutMs)
                        break;
                }

                if (state.NextOperationIndex < state.Operations.Count)
                    return;

                progress?.Invoke(BuildBatchEditProgress(state, elapsedMs, "saving"));
                string prefabName = state.Root.name;
                var savedRoot = state.Session.SaveAndClose(
                    CollectExplicitYamlPropertyRoots(state.Operations));
                if (savedRoot == null)
                    throw new InvalidOperationException($"SaveAsPrefabAsset returned null for '{state.AssetPath}'.");

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "saved", true },
                    { "saveAttempted", true },
                    { "partialPersisted", false },
                    { "partialPersistedKnown", true },
                    { "persistedState", "complete" },
                    { "prefab", prefabName },
                    { "assetPath", state.AssetPath },
                    { "operationCount", state.Operations.Count },
                    { "appliedOperationCount", state.Summaries.Count },
                    { "operationSummaries", state.Summaries },
                    { "elapsedMs", (int)((EditorApplication.timeSinceStartup - state.StartedAt) * 1000d) },
                    { "execution", state.Execution.ToResult(state.Operations.Count) },
                };
                AddPrefabFileDiff(result, state.BeforeSnapshot, state.AssetPath, args);
                state.Session.Commit();
                complete(result);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                int elapsedMs = (int)((EditorApplication.timeSinceStartup - state.StartedAt) * 1000d);
                complete(BuildBatchEditFailure(state, args,
                    $"Failed to edit prefab transaction: {ex.Message}", "transaction_edit_exception",
                    false, elapsedMs, state.NextOperationIndex));
            }
        };

        progress?.Invoke(BuildBatchEditProgress(state, 0, "started"));
        EditorApplication.update += tick;
        tick();
    }

    private static object FinalizeBatchEditResult(BatchEditDeferredState state, object result)
    {
        Exception sessionCompletionException = null;
        try
        {
            state.Session?.Dispose();
        }
        catch (Exception ex)
        {
            sessionCompletionException = ex;
            Debug.LogException(ex);
        }

        var dictionary = result as Dictionary<string, object>;
        if (dictionary == null && sessionCompletionException == null)
            return result;
        dictionary ??= new Dictionary<string, object>();

        if (sessionCompletionException != null)
        {
            string existingError = dictionary.TryGetValue("error", out object errorValue)
                ? Convert.ToString(errorValue, CultureInfo.InvariantCulture)
                : "Prefab transaction could not complete its authoring session.";
            dictionary["error"] = existingError + " Authoring-session cleanup failed: " +
                                  sessionCompletionException.GetBaseException().Message;
            dictionary["message"] = dictionary["error"];
            dictionary["errorCode"] = "prefab_authoring_session_cleanup_failed";
            dictionary["success"] = false;
            dictionary["retryable"] = false;
        }

        bool succeeded = dictionary.TryGetValue("success", out object successValue) &&
                         successValue is bool success && success;
        if (!succeeded)
            SetBatchEditFailurePersistence(dictionary, state);

        return dictionary;
    }

    private static void SetBatchEditFailurePersistence(Dictionary<string, object> result,
        BatchEditDeferredState state)
    {
        bool saveAttempted = state.SaveAttempted;
        bool committed = state.Saved;
        bool rolledBack = state.Session != null && state.Session.RollbackSucceeded;

        result["saved"] = committed;
        result["saveAttempted"] = saveAttempted;
        result["partialPersisted"] = committed ? (object)false :
            !saveAttempted || rolledBack ? false : null;
        result["partialPersistedKnown"] = committed || !saveAttempted || rolledBack;
        result["persistedState"] = committed ? "complete" :
            !saveAttempted || rolledBack ? "none" : "unknown";
        if (saveAttempted && !committed)
            result["rolledBack"] = rolledBack;
    }

    private static Dictionary<string, object> BuildBatchEditProgress(BatchEditDeferredState state,
        int elapsedMs, string phase)
    {
        var saveAttemptedWithoutSuccess = state.SaveAttempted && state.Saved == false;
        return new Dictionary<string, object>
        {
            { "phase", phase },
            { "assetPath", state.AssetPath },
            { "operationCount", state.Operations.Count },
            { "appliedOperationCount", state.Summaries.Count },
            { "nextOperationIndex", state.NextOperationIndex },
            { "elapsedMs", elapsedMs },
            { "timeoutMs", state.TimeoutMs },
            { "saveAttempted", state.SaveAttempted },
            { "saved", state.Saved },
            { "partialPersisted", saveAttemptedWithoutSuccess ? (object)null : false },
            { "partialPersistedKnown", saveAttemptedWithoutSuccess == false },
            { "persistedState", state.Saved ? "complete" : saveAttemptedWithoutSuccess ? "unknown" : "none" },
            { "execution", state.Execution.ToResult(state.Operations.Count) },
        };
    }

    private static Dictionary<string, object> BuildBatchEditFailure(BatchEditDeferredState state,
        Dictionary<string, object> args, string error, string errorCode, bool retryable, int elapsedMs,
        int failedOperationIndex, Dictionary<string, object> failedOperation = null)
    {
        var saveAttemptedWithoutSuccess = state.SaveAttempted && state.Saved == false;
        var result = new Dictionary<string, object>
        {
            { "error", error },
            { "message", error },
            { "errorCode", errorCode },
            { "retryable", retryable },
            { "success", false },
            { "timedOut", errorCode == "transaction_edit_timeout" },
            { "saved", state.Saved },
            { "saveAttempted", state.SaveAttempted },
            { "partialPersisted", saveAttemptedWithoutSuccess ? (object)null : false },
            { "partialPersistedKnown", saveAttemptedWithoutSuccess == false },
            { "persistedState", state.Saved ? "complete" : saveAttemptedWithoutSuccess ? "unknown" : "none" },
            { "assetPath", state.AssetPath },
            { "operationCount", state.Operations.Count },
            { "appliedOperationCount", state.Summaries.Count },
            { "nextOperationIndex", state.NextOperationIndex },
            { "failedOperationIndex", failedOperationIndex },
            { "operationSummaries", state.Summaries },
            { "elapsedMs", elapsedMs },
            { "timeoutMs", state.TimeoutMs },
            { "execution", state.Execution.ToResult(state.Operations.Count) },
        };

        if (failedOperation != null)
            result["failedOperation"] = failedOperation;

        return result;
    }

    }
}
