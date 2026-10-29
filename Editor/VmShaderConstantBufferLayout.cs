namespace VMUnityAutomation.Editor
{
    public sealed class VmShaderConstantBufferLayout
    {
        [VmRequired, VmJsonProperty("name")] public string Name { get; set; }
        [VmRequired, VmJsonProperty("sizeBytes")] public int SizeBytes { get; set; }
        [VmRequired, VmJsonProperty("fieldCount")] public int FieldCount { get; set; }
        [VmRequired, VmJsonProperty("fieldNames")] public string[] FieldNames { get; set; }
        [VmRequired, VmJsonProperty("fieldArraySizes")] public int[] FieldArraySizes { get; set; }
        [VmRequired, VmJsonProperty("fieldsTruncated")] public bool FieldsTruncated { get; set; }
    }
}
