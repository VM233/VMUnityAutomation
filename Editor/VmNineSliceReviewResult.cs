namespace VMUnityAutomation.Editor
{
    public sealed class VmNineSliceReviewResult
    {
        [VmRequired, VmJsonProperty("plans")] public VmNineSliceCompactPlan[] Plans { get; set; }
    }
}
