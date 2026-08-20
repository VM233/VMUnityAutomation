using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Project-tool contract for work that yields between Editor updates. Every value required by a
    /// later step is returned in <see cref="VmProjectToolJobStep.State"/> and persisted by the Job owner.
    /// </summary>
    public interface IVmPersistentProjectTool : IVmProjectTool
    {
        VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
            Dictionary<string, object> state);
    }
}
