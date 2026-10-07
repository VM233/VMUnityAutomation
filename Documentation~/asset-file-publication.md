# Native asset file publication

Raw asset imports and snapshot restoration publish through the existing atomic
file owner. Source files are copied to a private sibling and adopted with
`File.Replace`, or moved into place for a newly created target. They never truncate
the loaded destination. Byte snapshots use the same private-file publication.
These owners also release Unity's cached file handles before publication.

An active TextCore atlas can keep a mapped serialized file after cached handles
are released. A smaller replacement then rejects an ordinary overwrite on Windows.
The private-file publication changes the file adopted by new readers while the
old mapping retains its complete original bytes. This applies to immediate and
deferred imports, image publication and rollback. Loaded-scene admission,
GUID/meta preservation, and public command contracts stay unchanged.

## Static Cost Ledger

- Import admits at most 500 items; each publishes once and can roll back once.
  Source copy publishes the same source byte count as the original copy, plus
  one constant-size replacement operation. It holds no source byte array and
  retains one private path per current item, removed before the next item.
  There is no new scan, data loop, cache or retained state. Existing backup and
  snapshot byte bounds remain owned by their admitted producer. Byte publication
  uses the existing immutable snapshot buffer contract. Main-thread budget: pass.
- Snapshot restoration releases once per existing touched asset and once per
  removed created folder. Their existing admitted operation/path bounds apply;
  no loop, input domain or snapshot buffer is enlarged.
- Focused loaded AnimationClip fixtures publish/restore one clip plus one failure
  control file. The two-key X position curve has an exact typed binding; Unity
  can generate three position bindings (six keys). They verify source bytes, meta bytes,
  GUID, local file ID and native curve readback for immediate/deferred/rollback.
- Windows fixtures hold an explicit read mapping while importing a smaller native
  clip and publishing a smaller byte snapshot. They prove the exact mapped-file
  rejection of truncation, complete old-view bytes, new file bytes and identity.
  Each uses one clip, one mapping, at most 32 KB of fixture bytes and one publication.
- Real acceptance repeats the two loaded TextCore font overwrites that failed in
  DoomsdayDiary, including the multi-atlas body font. It checks exact snapshot
  SHA-256 and retained GUIDs. This witness complements the small fixture.
- Static added-work budget: pass. Native handle-release internals remain owned
  by Unity; no assumption about throughput is used as a correctness claim.

Supported APIs: [AssetDatabase.ReleaseCachedFileHandles](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetDatabase.ReleaseCachedFileHandles.html)
and [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace).
