namespace VMUnityAutomation.Editor
{
    [VmProjectTool("texture/compact-nine-slice", MutatesAssets = true,
        Description = "Compact one reviewed PNG Sprite only inside existing authored stretch borders after visual and consumer review. Reject zero-border input, stale SHA256, atlases, trimming, non-point filtering, compression and mipmaps. Never invent borders or compact unconfigured axes. Preserve native identity, pivot and PPU; verify exact reconstruction and persisted metadata.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesAssets,
        Preconditions = new[] { "editor-connected", "stable-edit-mode" },
        ErrorCodes = new[] { "nine_slice_invalid_input", "nine_slice_ineligible", "nine_slice_stale_source", "nine_slice_readback_mismatch", "nine_slice_rollback_failed" },
        TransactionScope = "single-texture-and-importer",
        TransactionAtomicity = VmTransactionMechanics.Atomicity.VerifiedSingleAssetRollback,
        TransactionIsolation = VmTransactionMechanics.Isolation.RequestSerialized,
        TransactionDurability = VmTransactionMechanics.Durability.EditorSession,
        TransactionRollbackKind = VmTransactionMechanics.RollbackKind.AtomicByteSnapshot,
        TransactionCommitEvidence = new[] { "native-sprite-identity", "source-pixel-reconstruction", "persisted-source-and-importer" },
        CompletionEvidence = "Native GUID, Sprite/local-file identity, dimensions, border, pivot and PPU match the exact reconstruction plan after import.")]
    public sealed class VmNineSliceCompactTool : IVmProjectTool<VmNineSliceCompactRequest, VmNineSliceCompactPlan>
    {
        public VmNineSliceCompactPlan Execute(VmNineSliceCompactRequest request) =>
            VmNineSliceCompaction.Apply(request);
    }
}
