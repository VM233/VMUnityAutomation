using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public enum VmAutomationExecutionMode
    {
        Auto,
        Immediate,
        Batched,
    }

    public sealed class VmAutomationExecutionOptions
    {
        public VmAutomationExecutionMode Mode { get; private set; } = VmAutomationExecutionMode.Auto;
        public int OperationsPerFrame { get; private set; } = 25;
        public int FrameBudgetMs { get; private set; } = 8;
        public int TimeoutMs { get; private set; } = 90000;
        public bool ContinueOnError { get; private set; }

        public static bool TryParse(Dictionary<string, object> args, out VmAutomationExecutionOptions options,
            out string error)
        {
            options = new VmAutomationExecutionOptions();
            error = "";
            if (args == null || !args.TryGetValue("execution", out object rawExecution) || rawExecution == null)
                return true;
            if (!(rawExecution is Dictionary<string, object> execution))
            {
                error = "execution must be an object";
                return false;
            }

            string mode = GetString(execution, "mode", "auto").Trim().ToLowerInvariant();
            switch (mode)
            {
                case "auto":
                    options.Mode = VmAutomationExecutionMode.Auto;
                    break;
                case "immediate":
                    options.Mode = VmAutomationExecutionMode.Immediate;
                    break;
                case "batched":
                    options.Mode = VmAutomationExecutionMode.Batched;
                    break;
                default:
                    error = "execution.mode must be auto, immediate, or batched";
                    return false;
            }

            if (!TryGetPositiveInt(execution, "operationsPerFrame", 25, out int operationsPerFrame, out error) ||
                !TryGetPositiveInt(execution, "frameBudgetMs", 8, out int frameBudgetMs, out error) ||
                !TryGetPositiveInt(execution, "timeoutMs", 90000, out int timeoutMs, out error))
                return false;

            options.OperationsPerFrame = operationsPerFrame;
            options.FrameBudgetMs = frameBudgetMs;
            options.TimeoutMs = timeoutMs;
            options.ContinueOnError = GetBool(execution, "continueOnError", false);
            return true;
        }

        public VmAutomationExecutionMode ResolveMode(int operationCount)
        {
            if (Mode == VmAutomationExecutionMode.Auto)
                return operationCount > 1 ? VmAutomationExecutionMode.Batched : VmAutomationExecutionMode.Immediate;
            return Mode;
        }

        public Dictionary<string, object> ToResult(int operationCount)
        {
            return new Dictionary<string, object>
            {
                { "requestedMode", Mode.ToString().ToLowerInvariant() },
                { "resolvedMode", ResolveMode(operationCount).ToString().ToLowerInvariant() },
                { "operationCount", operationCount },
                { "operationsPerFrame", OperationsPerFrame },
                { "frameBudgetMs", FrameBudgetMs },
                { "timeoutMs", TimeoutMs },
                { "continueOnError", ContinueOnError },
            };
        }

        private static bool TryGetPositiveInt(Dictionary<string, object> values, string key, int defaultValue,
            out int result, out string error)
        {
            result = defaultValue;
            error = "";
            if (!values.TryGetValue(key, out object rawValue) || rawValue == null)
                return true;
            try
            {
                result = Convert.ToInt32(rawValue);
            }
            catch
            {
                error = $"execution.{key} must be an integer";
                return false;
            }

            if (result > 0)
                return true;
            error = $"execution.{key} must be greater than zero";
            return false;
        }

        private static string GetString(Dictionary<string, object> values, string key, string defaultValue)
        {
            return values.TryGetValue(key, out object value) && value != null ? value.ToString() : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key, bool defaultValue)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
                return defaultValue;
            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
