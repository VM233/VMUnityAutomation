using System;

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
        public string CallerSymbol { get; set; }
        public string Target { get; set; }
        public string Expression { get; set; }
        public string Kind { get; set; }
        public string Symbol { get; set; }
        public string CandidateReason { get; set; }
        public string[] CandidateSymbols { get; set; } = Array.Empty<string>();
        public bool Registered { get; set; }
        public string RegistrationIssue { get; set; }
        public string DriverPhaseMember { get; set; }
        public string DriverRetirementMember { get; set; }
    }
}
