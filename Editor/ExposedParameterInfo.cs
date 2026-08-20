using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal sealed class ExposedParameterInfo
    {
        internal object RawGuid;
        internal string GuidText;
        internal string Name;
        internal string Path;

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "guid", GuidText ?? "" },
                { "name", Name ?? "" },
                { "path", Path ?? "" },
            };
        }
    }

}
