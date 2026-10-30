# Live RenderTexture snapshot

`texture/snapshot-render-texture` reads one exact native RenderTexture identity
through the normal typed project-tool catalog. It does not render a camera or
advance a frame. Use it to distinguish producer pixels from UI presentation.

The source must already be created, 2D, RGBA/BGRA8 UNorm or sRGB, at most1024
pixels on either axis. MSAA is explicitly resolved into a same-format,
single-sample temporary target. PNG stores the resulting RGBA8 bytes without an
extra gamma conversion. The report retains format, sRGB flag, source frame,
alpha coverage/bounds and PNG SHA256. Coverage alone does not establish visual
correctness. Readback synchronizes the GPU and should be used for evidence,
not every frame.

Output is create-new in an existing directory beneath the current project's
Temp or Tools, with no reparse-point ancestors. Assets and external paths are
rejected. Failure removes only the file created by this invocation. The prior
active target and all temporary native objects are restored/released in finally.

## Static Cost Ledger

- One admitted texture, axes1..1024, at most1048576 pixels.
- One GPU resolve, one synchronized ReadPixels, one native PNG encoding and one
  linear alpha pass; at most1048576 managed iterations and no scene/asset scan.
- At most64 checked path ancestors. No recursive filesystem traversal.
- RGBA CPU texture4MiB, Color32 readback4MiB, GPU target4MiB, PNG admission8MiB:
  peak20MiB excluding native driver/encoder internals. Encode output above8MiB
  rejects before publication. No per-pixel allocations or retained cache.
- Main-thread synchronous GPU readback/encode; bounded work, no wall-time
  completion guarantee. This explicit evidence operation does not run in the
  gameplay render loop. Pass for a single admitted1024-square LDR snapshot.

Native fixtures cover transparent/opaque and MSAA outputs, exact alpha bounds,
identity/type/lifecycle/format/size/path/overwrite rejection and active-target
restoration. The public executor must additionally replay the requested live
project texture after adoption.
