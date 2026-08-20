#if UNITY_EDITOR
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal readonly struct UssUsageLocation
    {
        public readonly string Path;
        public readonly int Line;
        public readonly int Column;

        public UssUsageLocation(string path, int line, int column)
        {
            Path = path;
            Line = line;
            Column = column;
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "path", Path },
                { "line", Line },
                { "column", Column }
            };
        }
    }
}
#endif
