namespace VMUnityAutomation.Editor
{
    public sealed class VmUiRebuildResult
    {
        public string RootInstanceId { get; set; }
        public string RootPath { get; set; }
        public int NodeCount { get; set; }
        public int GraphicCount { get; set; }
        public int TextGeneratorsInvalidated { get; set; }
        public int ActiveTextsPrepared { get; set; }
        public int ActiveGraphicsRebuilt { get; set; }
        public bool LayoutRebuilt { get; set; }
    }
}
