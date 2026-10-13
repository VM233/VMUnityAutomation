# Exact PNG crops during asset import

`asset/import` accepts `crop` on each import or shared defaults. Its four integer
fields `x`, `y`, `width`, `height` select a rectangle in source pixels with Unity's
bottom-left origin. Use the actual Sprite rectangle when extracting a sheet.
Every selected RGBA pixel and transparent margin keeps its original position;
there is no redraw, resampling, centering or alpha cleanup. Each output is named
by `destinationPath`. Use explicit Sprite/Single, project-standard pixelsPerUnit
and FullRect settings for independent icons.

Crop is exclusive with resize and spriteGrid. The entire request is admitted
before publication, including source dimensions, integer bounds, destination
collisions and the existing image budgets. `dryRun` performs preparation without
writing files. No staging PNG is created; the existing import transaction owns
publication, exact encoded-byte readback and rollback. Sources and references
are retained: migrate consumers through their native authoring tools before
retiring a sheet. The crop receipt includes source dimensions, rectangle,
source/output SHA256, byte count and verification status. Current exact schemas
and error metadata are published by the catalog.

## Static cost ledger

The finite import axis is at most 500 items. Each 8-bit source has at most
4,194,304 pixels, side length 4096 and 32 MiB encoded bytes. Before each decode,
the existing aggregate budget admits at most 67,108,864 combined source/output
pixels and 256 MiB source/output bytes across the batch; a repeated sheet counts
for each item. Crop copies exactly `width * height` selected pixels in at most
4096 row copies per item. There is no item-by-item asset scan added by crop;
duplicate detection keeps its separately declared existing scope.

Source decode and crop arrays have a simultaneous upper bound of 32 MiB; native
source/output textures add at most 32 MiB, and all retained encoded preparation
products stay within the 256 MiB aggregate byte budget. Processing runs on the
Editor thread during admission; destination publication uses the import owner's
existing immediate/batched execution and timeout. There is no persistent cache.
These axes pass the existing import memory/work admission budgets. Large sheets
must be divided into bounded import requests; source retirement is independent.

The focused fixture freezes a 4 x 3 asymmetric source and at most two 2 x 3
outputs. Its twelve cases cover exact transparent RGBA/orientation, two actual
Single Sprite imports, atomic late admission rejection, exclusive operations,
four illegal coordinates, budget rejection and dryRun. Each case uses one
source, at most two asset publications and less than 1 MiB transient memory;
the fixture owns and removes its source/output paths. Pass for these axes.
