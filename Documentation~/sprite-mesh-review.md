# Sprite mesh review

`asset/sprite-mesh-review` is the read-only owner for project Sprite mesh
validation. It reads `TextureImporterSettings.spriteMeshType` from every Sprite
texture below the requested project folders and accepts only `FullRect`.

Discover the typed contract as `vm_pt_asset_sprite_mesh_review`, then invoke the
project-tool route through the Pipeline facade:

```json
{
  "assetRoots": ["Assets"],
  "maxIssues": 200
}
```

`assetRoots` defaults to the complete `Assets` tree. Each root must be an
existing project folder at or below `Assets`. The scan has a hard capacity of
100,000 candidate textures and accepts at most 32 roots. It does not load Sprite
objects or change importers.

A successful review requires both `passed: true` and `totalIssues: 0`.
`spriteCount`, `fullRectCount`, and `tightCount` describe the complete scan.
`issues` is bounded by `maxIssues`; `totalIssues` remains complete and
`truncated` reports whether issue details were capped.

Use `asset/import-settings/set` with `spriteMeshType: "FullRect"` for an
explicit correction, then rerun the review. The semantic importer command reads
back the resulting `spriteMeshType`; the review itself never repairs assets.
