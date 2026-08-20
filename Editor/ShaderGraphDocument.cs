using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal sealed class ShaderGraphDocument
    {
        public readonly List<string> Blocks = new List<string>();
        public readonly Dictionary<string, Dictionary<string, object>> ObjectsById =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        public Dictionary<string, object> GraphData;
    }


}
