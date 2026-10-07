# Native asset file publication

Raw asset bytes are published by `asset/import`; transaction and workspace
snapshots restore the same asset and meta files. These owners release Unity's
cached file handles immediately before writing or deleting a touched file.
Unity can otherwise retain a mapped serialized file on Windows, rejecting an
overwrite and its rollback even when the Editor is idle. Each deferred item and
each rollback item releases handles at its own publication boundary because an
earlier import can cache files again. No retry or alternate publication path is
used. Loaded-scene mutation admission and GUID/meta preservation stay unchanged.

## Static Cost Ledger

- Import admits at most 500 items; each publishes once and can roll back once.
  Added work is at most 1,000 native handle-release calls per batch; no new scan,
  allocation axis, cache or retained state. It runs on the Editor main thread.
- Snapshot restoration releases once per existing touched asset and once per
  removed created folder. Their existing admitted operation/path bounds apply;
  no loop, input domain or snapshot buffer is enlarged.
- Focused loaded AnimationClip fixtures publish/restore one clip plus one failure
  control file. One curve has two keys. They verify source bytes, meta bytes,
  GUID, local file ID and native curve readback for immediate/deferred/rollback.
- Real acceptance repeats the two loaded TextCore font overwrites that failed in
  DoomsdayDiary, including the multi-atlas body font. It checks exact snapshot
  SHA-256 and retained GUIDs. This witness complements the small fixture.
- Static added-work budget: pass. Native handle-release internals remain owned
  by Unity; no assumption about throughput is used as a correctness claim.

Supported API: [AssetDatabase.ReleaseCachedFileHandles](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AssetDatabase.ReleaseCachedFileHandles.html).
