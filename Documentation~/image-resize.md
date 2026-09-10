# Resize images before import

`image/resize` is a typed package tool exposed as `vm_pt_image_resize` through
the existing Unity CLI Pipeline catalog. It resizes one local 8-bit PNG and
writes one explicitly named PNG. No report, receipt, backup, or staging file is
created. The result is returned in the command response.

Discover `image/resize` with `vm_catalog_list`, read `vm_pt_image_resize` with
`vm_catalog_get`, and invoke it with `vm_automation_call` and the exact
`expected_project_path`. Its argument object is:

```json
{
  "sourcePath": "C:/Art/Original.png",
  "outputPath": "C:/Art/Resized.png",
  "width": 350,
  "height": 350,
  "filter": "Bilinear",
  "overwrite": false,
  "dryRun": false
}
```

Specify `width`, `height`, or both. One dimension determines the other from the
source aspect ratio. Two dimensions define a bounding box, without cropping or
padding. Dimensions are rounded to the nearest whole pixel, with a minimum of
one pixel. `Nearest` preserves pixel-art samples. The default `Bilinear` uses
pixel-center sampling with premultiplied-alpha interpolation, avoiding color
bleed from transparent pixels. Resizing to the original dimensions copies the
original PNG bytes unchanged.

Paths must be absolute and the output directory must already exist. The source
is always retained. Source and output must be different files. An existing output
requires `overwrite: true`. Output beneath this Unity project's `Assets` or
`Packages`, or through a symbolic link/junction, is rejected before writing.
Use the existing `asset/import` command to adopt the resulting PNG. That importer
continues to own Sprite settings, Full Rect, identity, and project PPU validation.
This tool never changes PPU or an importer.

`dryRun` validates paths, PNG header, dimensions, output collisions, and limits
without decoding pixels or creating files. It does not claim a successful decode.
An executed result includes source/output dimensions, SHA-256 hashes, output byte
count, and a `verified` flag set only after reading the written file back.

File publication is one synchronous write, without a multi-file transaction or
rollback. An I/O failure after publication starts can leave an incomplete output,
and is reported with the output path. No success receipt is returned on failure.

## Ownership and validation

The Pipeline executor owns project binding, request identity, and dispatch.
`VmImageResizeTool` owns admission, decoding/encoding, output publication, and
readback. `VmImageResampler` owns only RGBA pixel sampling. Immutable results are
consumed by the CLI caller and then `asset/import`. Texture objects are destroyed
in the same invocation. There is no cache, retained job, or background writer.

Focused tests cover transparent-edge interpolation, nearest-neighbor samples,
aspect ratio and rounding, no-op byte identity, dry-run, overwrite admission,
source preservation, invalid dimensions/PNGs, protected paths, bounded inputs,
and typed catalog discovery. Real CLI acceptance uses the same public tool.

## Static Cost Ledger

- One source and one output per invocation. No folder or asset-database scan.
- Each side is at most 4096 pixels and each image at most 4,194,304 pixels.
  Source bytes are limited to 32 MiB before allocation/decoding.
- Output loops visit at most 4,194,304 pixels. Nearest samples one source pixel
  per output, bilinear four, for at most 16,777,216 source sample contributions.
- One decode, one encode, one output write, and one readback. Dry-run performs
  neither decode nor encode. PNG dimensions come from the bounded input header.
- Explicit RGBA arrays total at most 32 MiB. Source, encoded output, and readback
  buffers are bounded by 32 MiB each. Two RGBA texture CPU/GPU copies add at most
  64 MiB, for a 192 MiB owned-buffer budget. Unity codec-internal working memory
  is separate and bounded by the admitted image dimensions.
- Output ancestry checks visit at most 128 directory entries. The invocation
  runs synchronously on the Editor thread, with no frame loop or reload lifecycle.
- Admission rejects larger work before decoding. Static work/owned-buffer budget:
  pass. Throughput and Unity codec allocations are runtime observations, not
  substitutes for these input and iteration bounds.
