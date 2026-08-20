using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class VmProjectToolPackageAttribute : Attribute
    {
        public string PackageId { get; }

        public VmProjectToolPackageAttribute(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("Package id cannot be empty.", nameof(packageId));

            PackageId = packageId.Trim();
        }
    }
}
