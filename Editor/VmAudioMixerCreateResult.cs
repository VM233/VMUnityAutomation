namespace VMUnityAutomation.Editor
{
    public sealed class VmAudioMixerCreateResult
    {
        public string AssetPath { get; set; }
        public string Guid { get; set; }
        public string MasterGroup { get; set; }
        public int GroupCount { get; set; }
        public bool SavedAndReadBack { get; set; }
    }
}
