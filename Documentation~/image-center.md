# Centered PNG subjects during asset import

`asset/import` accepts `center` on each import or shared defaults. Supply the
output `width` and `height`; optional `sourceRect: {x,y,width,height}` selects
a complete subject region using Unity's bottom-left pixel origin. The default
region is the complete source PNG. Separate sheet cells use separate import
entries and destination PNGs. Centering owns Sprite/Single/FullRect and requires
explicit project-standard pixelsPerUnit. It is exclusive with crop, resize,
spriteGrid and spriteSlice. There is no staging image or resampling.

The alpha bounding rectangle is translated by integer pixels onto a transparent
canvas. Disconnected parts inside the selected region remain one complete icon.
Default `backgroundAlphaMax=0` preserves every nontransparent source pixel.
An explicitly supplied threshold may discard only faint pixels outside the
complete subject bounds; every RGBA pixel inside those bounds is copied exactly.
Empty subjects and subjects larger than the output are rejected before any
publication. The receipt records the region, source and target rectangles,
center error, discarded background count and verified source/output hashes.

For a sheet, create one import entry per complete region. For example:

```json
{
  "defaults": { "pixelsPerUnit": 100, "dedupeMode": "none" },
  "imports": [
    {
      "sourcePath": "C:/Art/Medical.png",
      "destinationPath": "Assets/Icons/Saline.png",
      "center": {
        "width": 512,
        "height": 512,
        "sourceRect": { "x": 0, "y": 444, "width": 443, "height": 443 }
      }
    }
  ]
}
```

Read the exact `vm_auto_asset_import` contract through bounded catalog discovery
and bind the calling project's absolute path. `dryRun=true` performs complete
image and destination admission without publishing an asset. The default alpha
threshold should change only when the source's faint background is established.

## Static cost ledger

Frozen production bounds: at most 500 import entries, 4,194,304 source pixels and
4,194,304 output pixels per entry, side length at most 4096, and at most 32 MiB
per encoded PNG. Before decoding, the existing batch owner admits at most
67,108,864 combined source/output pixels and 256 MiB combined encoded bytes.
Repeated sources count per entry. The selected rectangle is scanned once to
derive exact alpha bounds, once to count retained nontransparent pixels, and
copied in at most 4096 row copies. All scan/copy pixels are bounded by twice
the source plus the output, at most 134,217,728 across a batch. No additional
AssetDatabase scan or persistent cache is introduced. The same subject product
is consumed by spriteGrid, which retains its existing partition/cell budgets.

Source/output CPU arrays and native textures together require at most 64 MiB
per sequential entry. Retained encoded preparation products stay within the
existing 256 MiB aggregate admission budget. Processing is on the Editor thread
during preflight; publication uses the import transaction's existing bounded
immediate/batched execution. Pass for these admitted finite axes.

Frozen medical-sheet witness: eight entries read the same 1774 x 887 PNG and
produce eight 512 x 512 Single Sprites: 14,685,456 admitted source/output pixels,
8,137,624 source bytes, and at most eight output byte products. At most 444 x 444
pixels are scanned per region; no subject rescaling occurs. Peak array/texture
memory is below 16 MiB per entry. Pass within the public batch budgets.

Focused fixture bounds: source at most 32 x 32, two output assets per request,
output side at most 32, below 1 MiB transient memory. Cases cover asymmetric
RGBA, disconnected subject parts, odd pixel centering, explicit faint-background
classification, native Single Sprite publication, admission failures, operation
exclusivity and dryRun. Fixture-owned sources and assets are retired by its
lifecycle. Existing crop and grid fixtures cover shared-owner regression.
