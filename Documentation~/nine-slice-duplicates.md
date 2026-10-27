# Exact nine-slice duplicate artwork

`texture/review-nine-slice` reviews explicit PNG Sprite paths and returns compact
dimensions, source SHA256, fixed borders and exact reconstruction evidence.
`texture/compact-nine-slice` applies one current plan by path and expected SHA256.
It preserves native Sprite identities, pivot and PPU, requires point filtering,
uncompressed non-mipmapped input and one Sprite covering the complete source,
and uses Full Rect. Multi-frame atlases are reported as ineligible. Source PNG
and importer rollback data are held only in memory for the operation.

An already authored border is the stretch contract: only an entirely identical
center band can collapse to one pixel on that axis, and fixed borders remain
unchanged. A textured authored center stays intact. On an unconfigured axis,
compaction selects the longest identical interior column/row band. Other bands,
decorations, edges and alpha are preserved. Ties choose the earliest source band;
a uniform unconfigured axis collapses to one pixel with zero borders. This
minimizes source area within the existing or newly selected one-band-per-axis
nine-slice contract.
Every source pixel is reconstructed exactly (RGB under alpha zero is immaterial).
Consumer review is required: a fixed icon, animation, SpriteRenderer in Simple
mode, intrinsic Image size, or USS slice override may not consume that plan
correctly. Author explicit original display dimensions and plan borders as needed
before applying. The tool does not mutate consumers or consolidate references.

## Compact-source work admission (Static Cost Ledger)

The compact-source review admits 1..256 explicit Assets PNG paths, processes them
sequentially, and shares the duplicate owner's 64 Mi decoded-pixel request budget.
Each image is additionally limited by VmPngImage to 4096 per side, 4 Mi pixels and
32 MiB encoded bytes. No global AssetDatabase scan, pair product, render, static
cache, background work or per-pixel Unity API call is introduced. Run discovery
and the reduced fingerprint reuse VmNineSliceFingerprint. Selecting the longest
interior run is O(width+height); mapping, compaction and exact point reconstruction
are at most three further full-image passes. At most 7*64 Mi pixel visits and
256 source decodes are admitted per review; one source decode and one output
encode per apply. Source and output each have at most 4 Mi Color32 entries;
textures are destroyed before encoding. With source/encoded buffers, metadata
(limited to 1 MiB), run/maps and decoder/encoder buffers, peak managed/native
working admission is below 192 MiB per image. Plans retain only scalar receipts.
Apply accepts one source hash, revalidates it before writing, uses one native
reimport, and reads back GUID, Sprite ID/local file ID, rectangle, border, PPU and
filter. Rollback retains original PNG/meta in memory only and reimports once.
Rollback verifies both original byte hashes and native Sprite identity, rectangle
and border; it adds at most two source/meta reads and hashes on the failure path.
Tests use at most 10 small fixtures, each at most 64x64. Main-thread synchronous
work is explicitly bounded; no soft timeout is used as admission. Result: pass.

Decoded-pixel duplicate detection also compares reversible nine-slice artwork by
default. `includeNineSlice: false` requests dimension-sensitive RGBA equality
only. File-byte comparison is unchanged. The same owner serves the audit and
asset-import preflight, including prepared crop/resize/center products and prior
sources in the same batch.

The proof compresses identical adjacent complete columns and complete rows, then
compares their run lengths. At most one interior run may differ on each axis;
all other run lengths and every reduced visible RGBA pixel must match exactly. RGB
under zero alpha is ignored only for this rendering proof; the ordinary RGBA
hash remains byte exact. A uniform
image may stretch its single run. This preserves corners, border thickness,
decorations, colors and transparent pixels. Changing multiple separate bands,
resampling artwork or changing a perimeter is not a match. No perceptual threshold
or approximate image comparison is used.

Nine-slice results are pairwise certificates, not transitive similarity groups.
They publish each image's dimensions and the exact proposed border in source
pixels, plus the axes proven interchangeable. These are artwork-replacement
instructions: an existing importer's border, PPU, mesh, filtering and consumers
still require authoring review before references are migrated. The tool never
rewrites existing files, importers or references during duplicate detection.

Import retains the source's dimension-sensitive `contentHash`; `duplicateMatch`
identifies the comparison and the two reconstruction borders. Existing assets
are preferred to earlier sources, and the smallest exact reconstructing asset
is the deterministic canonical choice. An overwrite excludes its own destination
from duplicate candidates. Skipped sources resolve to their actual existing or
planned canonical destination, rather than creating chains to skipped files.

## Static Cost Ledger (before implementation)

Frozen command limits: 50,000 audit candidates; 500 import sources; one index per
distinct mode/search root (at most 500). The shared per-request budget, rather
than their Cartesian product, admits at most 64 Mi decoded pixels, 256 MiB encoded
input, 4 Mi retained row/column run entries, 200,000 candidate comparisons and
16 Mi compared run entries. A single image is at most 16 Mi pixels, 16,384 pixels
per side and 64 MiB encoded. Headers are checked before native decoding. Exceeding
any budget fails visibly before imports; audit cannot report complete success.

Each decoded image performs at most two RGBA hash traversals and two neighboring
pixel traversals (columns and rows), each O(width * height). Column/row run
construction uses O(width + height) storage; the reduced hash streams through a
64 KiB buffer and never allocates a reduced image or rendered comparison canvas.
Pair matching compares at most the two run arrays once; no per-pair decode,
AssetDatabase scan or rendering occurs. Each search root invokes FindAssets once.
Exact hashes use dictionary lookup; nine-slice comparisons occur only inside
identical reduced-pixel buckets. Budget checks precede each comparison.

Returned group dictionaries are retained only up to maxGroups (at most 2,000),
while total pair and unique duplicate-asset counts still cover the entire scan.
Peak retained run data is 16 MiB plus bounded candidate records/hashes. The largest
native RGBA image and managed pixels use 128 MiB together, encoded input at most
64 MiB; no decoded image survives its fingerprint call. There is no static cache,
timestamp-based reuse, runtime loop, worker or subscription. Execution is on the
Editor main thread, with immediate hard work admission rather than a claimed
soft frame deadline. Existing batched import execution retains its lifecycle.

The consumer checkpoint has 1,098 source images and 36,679,508 pixels; its largest
source is 9,437,184 pixels. The task's 17 Figma sources total fewer than 5,000
pixels. A single project index plus these sources fits the 64 Mi pixel admission.
Focused tests use at most eight assets below 64 x 64 per case, and two sources per
import transaction (three for the canonical-chain case). PASS for these finite axes; expanding any limit requires
updating this ledger and its rejection tests.
