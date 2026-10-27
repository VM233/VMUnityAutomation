namespace VMUnityAutomation.Editor
{
    [VmProjectTool("texture/compact-nine-slice", MutatesAssets = true,
        Description = "Apply one reviewed PNG Sprite compaction after consumer authoring review. Reject a stale SHA256, atlases, trimming, non-point filtering, compression and mipmaps. Preserve GUID, Sprite ID/local file ID, pivot and PPU; verify source reconstruction and persisted native metadata. Retain rollback data in memory only.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesAssets,
        Preconditions = new[] { "editor-connected", "stable-edit-mode" },
        ErrorCodes = new[] { "nine_slice_invalid_input", "nine_slice_ineligible", "nine_slice_stale_source", "nine_slice_readback_mismatch" },
        TransactionScope = "single-texture-and-importer", TransactionAtomicity = "rollback-on-failure",
        TransactionIsolation = "editor-main-thread", TransactionDurability = "asset-database",
        TransactionRollbackKind = "in-memory-source-and-meta",
        CompletionEvidence = "Native GUID, Sprite/local-file identity, dimensions, border, pivot and PPU match the exact reconstruction plan after import.")]
    public sealed class VmNineSliceCompactTool : IVmProjectTool<VmNineSliceCompactRequest, VmNineSliceCompactPlan>
    {
        public VmNineSliceCompactPlan Execute(VmNineSliceCompactRequest request) =>
            VmNineSliceCompaction.Apply(request);
    }
}
