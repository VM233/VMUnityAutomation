# Native asset previews

`graphics/asset-preview` observes Unity's asynchronous AssetPreview producer.
The request remains pending while the Editor continues updating. Completion
publishes native preview pixels as PNG; it never substitutes GetMiniThumbnail.
The same request owner supplies the preview embedded in `graphics/material-info`.

Asset preview `width` and `height` are integers from 1 to 2048, defaulting to 256.
The native image fits the output canvas proportionally; remaining pixels are
transparent. The response reports the requested dimensions. Material information
retains its 256-square embedded preview.

Native generation has a five-second observation deadline. Missing assets,
generation timeout, reload/shutdown interruption and capture failure are explicit
catalog errors. Every terminal path retires the Editor callbacks. Capture restores
the preceding active render target and releases temporary GPU/CPU textures.

The Project window and these commands consume the same imported asset and native
preview producer. A source image or generic asset icon is not prefab visual evidence.

## Ownership and regression bounds

The command descriptor is the entry; AssetPreview owns generation and its cache;
VmAutomationAssetPreviewRequest owns pending observation, capture and retirement.
The executor owns project binding, schema validation and publication. There is no
second rendering route, request retry, copied preview cache or material-only fallback.

Each Editor update issues one native preview observation. Capture executes once,
with at most 2048 x 2048 output pixels and one GPU target plus one CPU texture
(32 MiB uncompressed total). Material property enumeration is unchanged.

The focused regression fixture freezes four cases: a cold material image, a warm
portrait-canvas capture, shared material information and a missing asset. At most
one fixture material and two temporary textures exist per case. Each asynchronous
case yields at most 5000 Editor updates, then fails; it does not scan assets or
open production scenes. Fixtures delete only their exact unique assets in finally.
The live acceptance separately checks the originally failing prefab preview.
Static cost ledger: PASS.
