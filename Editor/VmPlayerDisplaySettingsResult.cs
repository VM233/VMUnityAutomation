using UnityEditor;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerDisplaySettingsResult
    {
        public UIOrientation DefaultInterfaceOrientation { get; set; }
        public int DefaultScreenWidth { get; set; }
        public int DefaultScreenHeight { get; set; }
        public bool AllowedAutorotateToPortrait { get; set; }
        public bool AllowedAutorotateToPortraitUpsideDown { get; set; }
        public bool AllowedAutorotateToLandscapeLeft { get; set; }
        public bool AllowedAutorotateToLandscapeRight { get; set; }
    }
}
