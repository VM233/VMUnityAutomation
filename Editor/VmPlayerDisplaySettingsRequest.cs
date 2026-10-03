using System.ComponentModel;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerDisplaySettingsRequest
    {
        [VmRequired, VmJsonProperty("action")]
        [Description("State reads native settings; Configure applies supplied fields outside Play Mode.")]
        public VmPlayerDisplaySettingsAction Action { get; set; }

        [VmJsonProperty("defaultInterfaceOrientation")]
        public UIOrientation? DefaultInterfaceOrientation { get; set; }

        [VmRange(1, int.MaxValue), VmJsonProperty("defaultScreenWidth")]
        public int? DefaultScreenWidth { get; set; }

        [VmRange(1, int.MaxValue), VmJsonProperty("defaultScreenHeight")]
        public int? DefaultScreenHeight { get; set; }

        [VmJsonProperty("allowedAutorotateToPortrait")]
        public bool? AllowedAutorotateToPortrait { get; set; }

        [VmJsonProperty("allowedAutorotateToPortraitUpsideDown")]
        public bool? AllowedAutorotateToPortraitUpsideDown { get; set; }

        [VmJsonProperty("allowedAutorotateToLandscapeLeft")]
        public bool? AllowedAutorotateToLandscapeLeft { get; set; }

        [VmJsonProperty("allowedAutorotateToLandscapeRight")]
        public bool? AllowedAutorotateToLandscapeRight { get; set; }

        internal bool HasChanges => DefaultInterfaceOrientation.HasValue ||
            DefaultScreenWidth.HasValue || DefaultScreenHeight.HasValue ||
            AllowedAutorotateToPortrait.HasValue || AllowedAutorotateToPortraitUpsideDown.HasValue ||
            AllowedAutorotateToLandscapeLeft.HasValue || AllowedAutorotateToLandscapeRight.HasValue;
    }
}
