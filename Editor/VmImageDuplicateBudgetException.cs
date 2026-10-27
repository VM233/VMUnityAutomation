using System;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmImageDuplicateBudgetException : Exception
    {
        internal VmImageDuplicateBudgetException(string message) : base(message) { }
    }
}
