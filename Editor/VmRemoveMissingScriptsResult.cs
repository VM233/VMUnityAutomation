using Newtonsoft.Json;

namespace VMUnityAutomation.Editor
{
    public sealed class VmRemoveMissingScriptsResult
    {
        [JsonProperty("ok")]
        public bool Ok { get; }
        [JsonProperty("removedCount")]
        public int RemovedCount { get; }
        [JsonProperty("hierarchyPath", NullValueHandling = NullValueHandling.Ignore)]
        public string HierarchyPath { get; }
        [JsonProperty("scenePath", NullValueHandling = NullValueHandling.Ignore)]
        public string ScenePath { get; }
        [JsonProperty("sceneDirty")]
        public bool SceneDirty { get; }
        [JsonProperty("errorCode", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorCode { get; }
        [JsonProperty("errorMessage", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorMessage { get; }

        private VmRemoveMissingScriptsResult(bool ok, int removedCount,
            string hierarchyPath, string scenePath, bool sceneDirty,
            string errorCode = null, string errorMessage = null)
        {
            Ok = ok;
            RemovedCount = removedCount;
            HierarchyPath = hierarchyPath;
            ScenePath = scenePath;
            SceneDirty = sceneDirty;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public static VmRemoveMissingScriptsResult Success(int removedCount,
            string hierarchyPath, string scenePath, bool sceneDirty) =>
            new(true, removedCount, hierarchyPath, scenePath, sceneDirty);

        public static VmRemoveMissingScriptsResult Failure(string code, string message) =>
            new(false, 0, null, null, false, code, message);
    }
}
