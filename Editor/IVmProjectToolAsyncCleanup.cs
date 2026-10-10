using System.Threading.Tasks;

namespace VMUnityAutomation.Editor
{
    public interface IVmProjectToolAsyncCleanup : IVmPersistentProjectTool
    {
        Task<VmProjectToolCleanupReceipt> CleanupJobAsync(string actualJobID, string actualCleanupToken);
    }
}
