# Exact nine-slice duplicate artwork

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
