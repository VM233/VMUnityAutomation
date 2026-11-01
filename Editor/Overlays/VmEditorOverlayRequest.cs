using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorOverlayRequest
    {
        [VmRequired, VmJsonProperty("windowInstanceId")]
        [Description("Exact existing EditorWindow instanceId from uitoolkit/windows. Never creates or focuses a window.")]
        public string WindowInstanceId { get; set; }

        [VmRequired, VmJsonProperty("overlayId")]
        [Description("Exact registered native Overlay id, for example a MainToolbarElement path. No substring matching or overlay creation.")]
        public string OverlayId { get; set; }

        [VmJsonProperty("displayed")]
        [Description("Omit to observe. An explicit Boolean sets native Overlay.displayed and returns its previous value. Unity owns persistence in the user's Editor layout; restore the prior value after temporary validation.")]
        public bool? Displayed { get; set; }
    }
}
