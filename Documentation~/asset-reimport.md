# Existing asset reimport

Discover `asset/reimport` through the bounded catalog, then read the exact `vm_pt_asset_reimport` contract. It imports one existing file below Assets, accepts that file's .meta path, and reports the canonical asset path, native type and before/after GUIDs. The default forces its importer to process the current file. The existing durable project-tool Job owns submission, observation and interruption; preserve its exact job identity and capability.

Use this for current font, UI, texture, model and configuration files. No global SaveAssets, full AssetDatabase.Refresh or clean script compilation is requested. An unrelated native Editor import or code change may still compile; the result does not claim otherwise. Native import itself is synchronous and cannot be interrupted mid-import.

Folders, absent/outside-Assets files, scripts, assembly definition/reference/response files and managed assembly metadata reject before import. Loaded scenes use the shared scene-mutation admission owner. Code or compilation-contract changes use asset/refresh and its mandatory complete compilation/reload evidence. Package adoption keeps its existing resolve/update contract.

The managed owner performs one path normalization, one loaded-scene admission, one native import and two GUID reads. It traverses no project, dependency, asset or directory graph. Output contains six scalar fields; native importer cost belongs to Unity and the selected importer. Tests adopt actual modified on-disk serialized content and preserve its GUID/metadata; separate rejected controls cover compilation files, folders and loaded scenes.
