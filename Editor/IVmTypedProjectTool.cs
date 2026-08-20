namespace VMUnityAutomation.Editor
{
    public interface IVmProjectTool<in TRequest, out TResult>
    {
        TResult Execute(TRequest request);
    }
}
