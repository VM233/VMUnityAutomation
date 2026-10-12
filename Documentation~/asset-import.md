# Asset import persistence

`asset/import` publishes the declared source bytes, applies the actual TextureImporter settings and optionally slices Sprite grids. [spriteGrid](sprite-grid.md) also prepares complete centered subjects from an original uneven sheet in the same batch. Each importer persists its own settings. Completion and rollback do not call global SaveAssets or commit unrelated dirty assets; backup retirement owns only the batch's temporary directory.

Replacing a serialized font therefore retains its supplied cache bytes, authored settings and existing asset identity. Importing or restoring files does not authorize saving other loaded objects. Use [asset/reimport](asset-reimport.md) for an existing file that needs native adoption without source replacement.

The regression fixture owns one dirty AnimationClip and one two-pixel PNG. It invokes the same deferred import owner exactly twice, selecting immediate and batched execution once each, and checks source-file persistence, pending dirty state and the imported Sprite. The finite domain is two imports and two one-pixel resize products, below 1 MiB fixture memory and ten seconds; there is no project-wide scan or save. Pass for these frozen axes.
