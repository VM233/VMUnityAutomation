using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public interface IVmProjectTool
    {
        object Execute(Dictionary<string, object> args);
    }
}
