# Centered Sprite grid import

`asset/import` accepts `spriteGrid` on an item or shared defaults. Supply the
original PNG, the intended column/row counts and `cellSize`. Preparation finds
empty artwork lines near nominal divisions, extracts complete subjects and
centers them by integer translation into square cells. It never resamples or
redraws artwork. The same request publishes the PNG, imports Sprite/Multiple with
Full Rect, performs the fixed-grid slicing and preserves existing Sprite IDs by
name. Existing project-standard `pixelsPerUnit` must be supplied explicitly.

`backgroundAlphaMax` defaults to 0: all nontransparent pixels belong to artwork.
Use an explicit value only for a source with known faint background traces; 1
admits the 1/255 alpha traces in the supplied hand-drawn sheets. Pixels inside
each complete subject rectangle retain exact RGBA values. Pixels outside those
rectangles at or below the declared background threshold are removed. Artwork
crossing a nominal divider is retained by choosing the nearest empty line within
one quarter of the nominal cell; an inseparable grid is rejected. Empty cells
and subjects larger than `cellSize` are rejected before any asset is changed.

`spriteGrid` owns the derived fixed slice dimensions, count, columns, center
pivot and Sprite/Multiple/Full Rect settings. Do not also supply `spriteSlice` or
`center`, `crop` or `resize`. An optional `baseName` supports an existing custom Sprite name prefix;
otherwise the destination filename supplies the prefix. Original source files
are retained. Preparation, duplicate detection, dry-run, import and rollback
share the existing asset-import transaction and prepared-byte product.

```json
{
  "defaults": {
    "overwrite": true,
    "pixelsPerUnit": 100,
    "filterMode": "Bilinear",
    "compression": "uncompressed",
    "alphaIsTransparency": true,
    "mipmapEnabled": false,
    "dedupeMode": "none",
    "spriteGrid": {
      "columns": 4,
      "rows": 4,
      "cellSize": 512,
      "backgroundAlphaMax": 1
    }
  },
  "imports": [
    {
      "sourcePath": "C:/Art/Medical.png",
      "destinationPath": "Assets/Icons/Medical.png"
    },
    {
      "sourcePath": "C:/Art/Weapons.png",
      "destinationPath": "Assets/Icons/Weapons.png",
      "spriteGrid": {
        "columns": 4,
        "rows": 2,
        "cellSize": 512,
        "backgroundAlphaMax": 1
      }
    }
  ]
}
```

Use bounded discovery and the current `vm_auto_asset_import` contract; supply the
calling project's absolute binding. Set `dryRun=true` for full pixel admission
without publication. Each import receipt reports `spriteGrid` geometry, source
and output hashes, complete subject rectangles, removed background count and
maximum center error. `verified=true` requires actual import and file readback.
Odd-sized subjects have at most 0.5 pixel center error per axis.

## Static cost ledger

PNG headers admit at most 4096 per side, 4 Mi pixels and 32 MiB encoded bytes per
image. An import batch admits at most 64 Mi combined input/output pixels, 256 MiB
combined encoded bytes and 4096 grid cells. Each grid dimension is at most 64.
Rows/columns projections, complete bounds and copies are linear in source/output
pixels; seam searches inspect only projection entries. The shared subject owner
scans each partition once for bounds, once for retained alpha count and copies
each subject once. Including projection construction, at most four source-pixel
visits plus output allocation occur per image, within 268,435,456 pixel visits
across the admitted batch. No Cartesian image/subject
search occurs. Frame metadata is bounded by 4096 cells. Native temporary textures
and pixel buffers are released after each preparation; retained encoded products
are bounded by the batch byte budget. Peak transform storage is at most 192 MiB
above retained products, under a 512 MiB preparation budget. Source reads and PNG
codec execution occur on the Unity owner thread. Import uses the existing batched
execution contract; a single decode/encode is atomic and cannot claim an 8 ms
hard limit. Status: pass for these enforced axes and memory bounds.

The production witness consists of ten originals, 152 cells, 20,969,758 input
pixels and 39,845,888 output pixels. It stays within every admitted bound and
compares every prepared subject against the original RGBA crop independently.
