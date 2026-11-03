using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPrefabOverrideCleanupResult
    {
        public sealed class RemovedOverride
        {
            public string target;
            public string targetType;
            public string propertyPath;
            public string value;
            public string reason;
        }

        public string assetPath;
        public string prefabPath;
        public bool dryRun;
        public int beforeCount;
        public int keptCount;
        public int removedCount;
        public List<RemovedOverride> removed = new();
    }
}
