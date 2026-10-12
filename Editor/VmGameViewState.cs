using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewState
    {
        public string WindowInstanceId { get; set; }
        public string Group { get; set; }
        public int SelectedIndex { get; set; }
        public string DisplayText { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float RenderWidth { get; set; }
        public float RenderHeight { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
    }
}
