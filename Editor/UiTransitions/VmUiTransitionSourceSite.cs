namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceSite
    {
        public string ID { get; set; }
        public string Path { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Owner { get; set; }
        public string Member { get; set; }
        public string Target { get; set; }
        public string Expression { get; set; }
        public string Kind { get; set; }
        public string Symbol { get; set; }
        public bool Registered { get; set; }
    }
}
