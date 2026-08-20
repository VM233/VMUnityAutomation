using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class MCPToolInputSchemaCatalog
    {
        internal static Dictionary<string, object> Get(string route)
        {
            switch (route)
            {
                case "_meta/tools":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("compact", "boolean", "Return compact descriptors. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includeSchema", "boolean", "Include input schemas. Defaults to false."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Tool offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum tools returned. Built-in default is 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("category", "string", "Optional exact category filter."),
                        MCPToolSchemaFactory.Prop("includeMetadataIssues", "boolean", "Include metadata audit diagnostics in detailed mode. Defaults to false.")
                    ));
                case "asset/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("folder", "string", "Folder to search. Defaults to Assets."),
                        MCPToolSchemaFactory.Prop("type", "string", "Optional Unity asset type filter."),
                        MCPToolSchemaFactory.Prop("search", "string", "Optional AssetDatabase search expression."),
                        MCPToolSchemaFactory.Prop("recursive", "boolean", "Include descendants. Defaults to true."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum assets. Defaults to 100; capped at 500.")));
                case "asset/import-settings/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone.")
                    ), "assetPath");
                case "asset/import-settings/set":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        AssetImportSettingsProp("settings", "Semantic importer fields. Unsupported keys are rejected with the allowed field list."),
                        MCPToolSchemaFactory.Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone."),
                        AssetPlatformSettingsProp("platformSettings", "Optional semantic TextureImporter or AudioImporter override settings for platform."),
                        MCPToolSchemaFactory.Prop("reimport", "boolean", "Save and reimport the asset after updating settings. Defaults to true."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return before/requested settings without modifying the importer.")
                    ), "assetPath", "settings");
                case "scene/workspace":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("action", "string", "Workspace action: list, open, close, or set-active. Defaults to list."),
                        MCPToolSchemaFactory.Prop("path", "string", "Scene asset path for open, close, or set-active."),
                        MCPToolSchemaFactory.Prop("name", "string", "Loaded scene name for close or set-active when path is omitted."),
                        MCPToolSchemaFactory.Prop("mode", "string", "Open mode: additive (default) or single."),
                        MCPToolSchemaFactory.Prop("saveModified", "boolean", "For single open, save every dirty loaded scene before replacement."),
                        MCPToolSchemaFactory.Prop("discardModified", "boolean", "For single open, explicitly allow replacement of dirty loaded scenes without saving."),
                        MCPToolSchemaFactory.Prop("save", "boolean", "For close, save a dirty scene before closing."),
                        MCPToolSchemaFactory.Prop("discardChanges", "boolean", "For close, explicitly discard dirty scene changes."),
                        MCPToolSchemaFactory.Prop("removeScene", "boolean", "For close, remove the scene from the workspace. Defaults to true.")
                    ));
                case "material/properties/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Material asset path below Assets/."),
                        MCPToolSchemaFactory.ArrayProp("propertyNames", "string", "Optional shader property names. Omit to page through declared shader properties."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Shader property offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum shader properties returned. Defaults to 100; capped at 500.")
                    ), "assetPath");
                case "material/properties/set":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Material asset path below Assets/."),
                        MaterialPropertyMapProp("properties", "Shader property values keyed by declared shader property name. Texture values accept assetPath plus optional scale and offset."),
                        MaterialKeywordsProp("keywords", "Keyword changes with enable and disable string arrays."),
                        MCPToolSchemaFactory.Prop("shader", "string", "Optional replacement shader name."),
                        MCPToolSchemaFactory.Prop("renderQueue", "number", "Optional Material render queue."),
                        MCPToolSchemaFactory.Prop("enableInstancing", "boolean", "Optional GPU instancing flag."),
                        MCPToolSchemaFactory.Prop("doubleSidedGI", "boolean", "Optional double-sided global illumination flag."),
                        MCPToolSchemaFactory.Prop("globalIlluminationFlags", "string", "Optional MaterialGlobalIlluminationFlags value."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return requested changes without modifying the Material.")
                    ), "assetPath");
                case "shadergraph/info":
                case "shadergraph/get-nodes":
                case "shadergraph/get-edges":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Shader Graph asset path below Assets/.")
                    ), "path");
                case "shadergraph/get-properties":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional Shader or Shader Graph asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("shaderName", "string", "Optional loaded shader name when path is omitted.")
                    ));
                case "shadergraph/set-node-property":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Shader Graph asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("objectId", "string", "Serialized graph object ID returned by shadergraph/get-properties or shadergraph/get-nodes."),
                        MCPToolSchemaFactory.Prop("nodeId", "string", "Legacy alias for objectId."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Existing top-level scalar field on the target graph object."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Replacement scalar value. Its JSON type must match the existing field.")
                    ), "path", "propertyName", "value");
                case "physics/raycast":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to Project Settings > VM Unity Automation > Tool Defaults (3D initially)."),
                        MCPToolSchemaFactory.Vector3Prop("origin", "Ray origin with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Vector3Prop("direction", "Ray direction with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Prop("maxDistance", "number", "Maximum ray distance. Defaults to infinity."),
                        MCPToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        MCPToolSchemaFactory.Prop("all", "boolean", "Return multiple hits rather than only the closest hit."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum hits returned when all is true. Defaults to 100; capped at 500.")
                    ), "origin", "direction");
                case "physics/overlap-sphere":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the VM Unity Automation project setting (3D initially). In 2D this performs an overlap circle."),
                        MCPToolSchemaFactory.Vector3Prop("center", "Query center with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Prop("radius", "number", "Sphere or circle radius. Defaults to 1."),
                        MCPToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center");
                case "physics/overlap-box":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the VM Unity Automation project setting (3D initially)."),
                        MCPToolSchemaFactory.Vector3Prop("center", "Query center with x/y/z. z is ignored for 2D."),
                        MCPToolSchemaFactory.Vector3Prop("halfExtents", "Half extents with x/y/z. In 2D, x/y are doubled into box size."),
                        MCPToolSchemaFactory.Prop("angle", "number", "2D box rotation in degrees. Ignored for 3D."),
                        MCPToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center", "halfExtents");
                case "search/scene":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Optional case-insensitive GameObject name substring or regular expression."),
                        MCPToolSchemaFactory.Prop("regex", "boolean", "Interpret name as a regular expression with a bounded match timeout. Defaults to false."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional Component type name or full name that must exist on the GameObject."),
                        MCPToolSchemaFactory.Prop("tag", "string", "Optional exact Unity Tag."),
                        MCPToolSchemaFactory.Prop("layer", "string", "Optional Unity Layer name or numeric index."),
                        MCPToolSchemaFactory.Prop("shader", "string", "Optional case-insensitive shader-name substring used by a Renderer on the GameObject."),
                        MCPToolSchemaFactory.Prop("includeInactive", "boolean", "Include inactive GameObjects. Defaults to true."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Stable result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 200; capped at 500.")));
                case "_meta/capabilities":
                case "queue/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "queue/status":
                case "queue/cancel":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("ticketId", "number", "Owned queue ticket identifier.")), "ticketId");
                case "asset/create-folder":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Folder path below Assets/."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and report without creating folders.")), "path");
                case "asset/copy":
                {
                    var copyProperties = MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Source asset path."),
                        MCPToolSchemaFactory.Prop("targetPath", "string", "Target asset path."));
                    var properties = MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Source asset path for a single copy."),
                        MCPToolSchemaFactory.Prop("targetPath", "string", "Target asset path for a single copy."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Replace existing targets with rollback snapshots. Defaults to false."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Preflight without copying. Defaults to false."));
                    properties["copies"] = new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "description", "Batch of sourcePath/targetPath copy requests." },
                        { "minItems", 1 },
                        { "items", MCPToolSchemaFactory.Schema(copyProperties, "sourcePath", "targetPath") },
                    };
                    var schema = MCPToolSchemaFactory.Schema(properties);
                    schema["oneOf"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "required", new List<object> { "sourcePath", "targetPath" } },
                        },
                        new Dictionary<string, object>
                        {
                            { "required", new List<object> { "copies" } },
                        },
                    };
                    return schema;
                }
                case "asset/dependencies":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Asset whose references should be inspected."),
                        MCPToolSchemaFactory.Prop("direction", "string", "outgoing, incoming, or both. Defaults to both."),
                        MCPToolSchemaFactory.Prop("recursive", "boolean", "Use recursive dependency resolution. Defaults to true."),
                        MCPToolSchemaFactory.ArrayProp("searchRoots", "string", "Folders scanned for incoming references. Defaults to Assets."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 100; capped at 500.")), "path");
                case "asset/transaction":
                    return MCPToolSchemaFactory.AssetTransactionSchema();
                case "uitoolkit/edit-uxml":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "UXML asset path below Assets/."),
                        UxmlOperationArrayProp("operations", "Ordered structural UXML edit operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/edit-uss":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "USS asset path below Assets/."),
                        UssOperationArrayProp("operations", "Ordered selector/declaration edit operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/authoring-transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        UIAuthoringEditArrayProp("edits", "Ordered edit objects with kind, assetPath, and operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate all edits without writing.")), "edits");
                case "packages/add":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("identifier", "string", "Registry package name, Git URL, local path, or tarball identifier.")),
                        "identifier");
                case "packages/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum packages. Defaults to 100; capped at 200.")));
                case "packages/remove":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Installed package name to remove.")), "name");
                case "packages/search":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("query", "string", "Registry search query."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum returned packages. Defaults to 50; capped at 200.")),
                        "query");
                case "localization/status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "localization/locales":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includePseudo", "boolean", "Include PseudoLocale assets. Defaults to true.")
                    ));
                case "localization/create-locale":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("code", "string", "Locale code, for example en-US or zh-CN."),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Locale asset path under Assets ending in .asset."),
                        MCPToolSchemaFactory.Prop("name", "string", "Optional Locale display name."),
                        MCPToolSchemaFactory.Prop("addToProject", "boolean", "Register the Locale with Localization Settings. Defaults to true.")
                    ), "code", "assetPath");
                case "localization/set-selected-locale":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("locale", "string", "Registered Locale code to select.")
                    ), "locale");
                case "localization/collections":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("type", "string", "Optional collection type filter: string or asset."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive collection name filter.")
                    ));
                case "localization/create-collection":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Table Collection name."),
                        MCPToolSchemaFactory.Prop("type", "string", "Collection type: string or asset."),
                        MCPToolSchemaFactory.Prop("assetDirectory", "string", "Existing or new directory under Assets."),
                        MCPToolSchemaFactory.ArrayProp("locales", "string", "Optional Locale codes. Defaults to every registered Locale."),
                        MCPToolSchemaFactory.Prop("group", "string", "Optional Localization window group."),
                        MCPToolSchemaFactory.Prop("preload", "boolean", "Optional preload flag for all created tables.")
                    ), "name", "type", "assetDirectory");
                case "localization/entries":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("collection", "string", "Table Collection name or GUID."),
                        MCPToolSchemaFactory.Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        MCPToolSchemaFactory.Prop("locale", "string", "Optional Locale code filter."),
                        MCPToolSchemaFactory.Prop("keyContains", "string", "Optional case-insensitive key filter."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Filtered key offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum keys returned. Defaults to 100; capped at 500.")
                    ), "collection");
                case "localization/upsert-entry":
                    return MCPToolSchemaFactory.LocalizationUpsertEntriesSchema();
                case "localization/remove-entry":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("collection", "string", "Table Collection name or GUID."),
                        MCPToolSchemaFactory.Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        MCPToolSchemaFactory.Prop("key", "string", "Localization key to remove."),
                        MCPToolSchemaFactory.Prop("locale", "string", "Optional Locale code. Omit to remove the shared key from every table.")
                    ), "collection", "key");
                case "localization/validate":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("collection", "string", "Optional Table Collection name or GUID."),
                        MCPToolSchemaFactory.Prop("type", "string", "Optional collection type filter: string or asset."),
                        MCPToolSchemaFactory.Prop("includeEmpty", "boolean", "Report empty values as well as missing entries. Defaults to true."),
                        MCPToolSchemaFactory.Prop("maxIssues", "number", "Maximum issues returned. Defaults to 200; capped at 2000.")
                    ));
                case "localization/settings":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("initializeSynchronously", "boolean", "Optional Localization initialization mode."),
                        MCPToolSchemaFactory.Prop("projectLocale", "string", "Optional registered project Locale code."),
                        MCPToolSchemaFactory.Prop("selectedLocale", "string", "Optional registered selected Locale code.")
                    ));
                case "localization/variables":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("group", "string", "Optional case-insensitive persistent variable group filter."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive variable name filter.")
                    ));
                case "localization/upsert-variable":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("group", "string", "Persistent variable group name."),
                        MCPToolSchemaFactory.Prop("name", "string", "Variable name inside the group."),
                        MCPToolSchemaFactory.Prop("type", "string", "Variable type: bool, int, long, float, double, string, or object."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Variable value. Object variables accept an Assets path."),
                        MCPToolSchemaFactory.Prop("groupAssetPath", "string", "Required asset path when creating a missing VariablesGroupAsset.")
                    ), "group", "name", "type", "value");
                case "localization/remove-variable":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("group", "string", "Persistent variable group name."),
                        MCPToolSchemaFactory.Prop("name", "string", "Variable name to remove.")
                    ), "group", "name");
                case "packages/update-git":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Package name, e.g. com.example.package"),
                        MCPToolSchemaFactory.Prop("gitUrl", "string", "Optional Git URL. Defaults to the current manifest Git URL."),
                        MCPToolSchemaFactory.Prop("revision", "string", "Required full 40-character Git commit SHA."),
                        MCPToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity. Reusing it with identical arguments returns the same durable job; different arguments are rejected.")
                    ), "name", "revision");
                case "packages/resolve":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        GitPackageExpectationArrayProp(),
                        MCPToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity. Reusing it with identical arguments returns the same durable job; different arguments are rejected.")
                    ), "expectedPackages");
                case "packages/status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Optional package name. If omitted, returns all Git dependencies from the manifest."),
                        MCPToolSchemaFactory.Prop("includeResolved", "boolean", "Include Package Manager resolved package data when available. Defaults to false.")
                    ));
                case "packages/lint-metas":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "Installed package name to lint."),
                        MCPToolSchemaFactory.Prop("path", "string", "Absolute or project-relative package path to lint."),
                        MCPToolSchemaFactory.Prop("all", "boolean", "Lint all resolved package roots."),
                        MCPToolSchemaFactory.Prop("checkDirectories", "boolean", "Also require directory .meta files. Defaults to true."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum missing entries returned per package.")
                    ));
                case "wait/editor-idle":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 30000."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Number of consecutive idle editor frames required. Defaults to 3."),
                        MCPToolSchemaFactory.Prop("stableMs", "number", "Minimum continuous idle time in milliseconds. Defaults to 500.")
                    ));
                case "mcp/health":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeRecentActions", "boolean", "Include recent and slow action details. Defaults to false so health checks remain compact."),
                        MCPToolSchemaFactory.Prop("recentCount", "number", "Number of recent MCP actions to return when includeRecentActions is true. Defaults to 20."),
                        MCPToolSchemaFactory.Prop("slowThresholdMs", "number", "Recent actions at or above this duration are listed as slow. Defaults to 1000.")
                    ));
                case "mcp/set-autostart":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("enabled", "boolean", "Whether this Unity Editor instance should auto-start the MCP bridge after reload.")
                    ), "enabled");
                case "jobs/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobType", "string", "Optional job type filter."),
                        MCPToolSchemaFactory.Prop("status", "string", "Optional status filter."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum jobs. Defaults to 50; capped at 200.")));
                case "jobs/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Job identifier. Supply this or requestId."),
                        MCPToolSchemaFactory.Prop("requestId", "string", "Original transport request identity for recovering the same workspace job when its start response was lost."),
                        MCPToolSchemaFactory.Prop("jobType", "string", "Optional job type disambiguator."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating MCP agent disconnects.")));
                case "jobs/cancel":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Persistent job identifier returned by its start route."),
                        MCPToolSchemaFactory.Prop("jobType", "string", "Optional job type disambiguator."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating MCP agent disconnects.")
                    ), "jobId");
                case "jobs/cleanup":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Terminal persistent job identifier whose explicit cleanup contract should run."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when the persistent job started.")
                    ), "jobId");
                case "vfxgraph/catalog":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.EnumProp("kind", "Optional VFX catalog kind.", "asset-kind", "template", "context", "block", "operator", "parameter", "property-binder", "event-binder", "output-event-handler", "spawner-callback"),
                        MCPToolSchemaFactory.Prop("query", "string", "Case-insensitive search across names, categories, types, synonyms and variant settings."),
                        MCPToolSchemaFactory.Prop("category", "string", "Exact category filter."),
                        MCPToolSchemaFactory.Prop("includeExperimental", "boolean", "Include experimental contexts, blocks and operators."),
                        MCPToolSchemaFactory.Prop("contextCatalogId", "string", "When listing blocks, restrict results to blocks accepted by this exact context catalog item."),
                        MCPToolSchemaFactory.Prop("catalogId", "string", "Optional exact catalog item ID. Use includeDetails to inspect its settings and slots."),
                        MCPToolSchemaFactory.Prop("includeDetails", "boolean", "Instantiate and include settings and recursive slot definitions only for the returned page. Defaults to false."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Zero-based result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 100; capped at 500, or 100 when includeDetails is true."),
                        MCPToolSchemaFactory.Prop("settingOffset", "number", "Per-item setting offset when includeDetails is true."),
                        MCPToolSchemaFactory.Prop("maxSettingsPerItem", "number", "Per-item setting page size. Defaults to 64; capped at 128."),
                        MCPToolSchemaFactory.Prop("inputSlotOffset", "number", "Per-item flattened recursive input-slot offset."),
                        MCPToolSchemaFactory.Prop("outputSlotOffset", "number", "Per-item flattened recursive output-slot offset."),
                        MCPToolSchemaFactory.Prop("maxSlotsPerItem", "number", "Per-direction flattened recursive slot page size. Defaults to 64; capped at 256.")));
                case "vfxgraph/create":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "New VFX asset path below Assets/. Extension must match assetKind."),
                        MCPToolSchemaFactory.EnumProp("assetKind", "VFX asset kind.", "graph", "block-subgraph", "operator-subgraph"),
                        MCPToolSchemaFactory.Prop("templateId", "string", "Optional exact template catalog ID for graph assets."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Replace existing asset contents while preserving its meta GUID.")), "assetPath", "assetKind");
                case "vfxgraph/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "VisualEffectAsset or VFX subgraph path below Assets/ or Packages/."),
                        MCPToolSchemaFactory.Prop("nodeOffset", "number", "Semantic node offset."),
                        MCPToolSchemaFactory.Prop("maxObjects", "number", "Maximum semantic nodes returned. Defaults to 250; capped at 1000."),
                        MCPToolSchemaFactory.Prop("parameterOffset", "number", "Parameter definition offset."),
                        MCPToolSchemaFactory.Prop("maxParameters", "number", "Maximum parameter definitions returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("connectionOffset", "number", "Data and flow connection offset."),
                        MCPToolSchemaFactory.Prop("maxConnections", "number", "Maximum data and flow connections returned. Defaults to 500; capped at 5000."),
                        MCPToolSchemaFactory.Prop("uiOffset", "number", "Group and sticky-note offset."),
                        MCPToolSchemaFactory.Prop("maxUIItems", "number", "Maximum groups and sticky notes returned."),
                        MCPToolSchemaFactory.Prop("dataOffset", "number", "VFX data-object offset."),
                        MCPToolSchemaFactory.Prop("maxDataObjects", "number", "Maximum VFX data objects returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("categoryOffset", "number", "Blackboard category offset."),
                        MCPToolSchemaFactory.Prop("maxCategories", "number", "Maximum blackboard categories returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("customAttributeOffset", "number", "Custom attribute offset."),
                        MCPToolSchemaFactory.Prop("maxCustomAttributes", "number", "Maximum custom attributes returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("settingOffset", "number", "Per-model setting offset."),
                        MCPToolSchemaFactory.Prop("maxSettingsPerNode", "number", "Per-model setting page size. Defaults to 64; capped at 128."),
                        MCPToolSchemaFactory.Prop("occurrenceOffset", "number", "Per-parameter visual occurrence offset."),
                        MCPToolSchemaFactory.Prop("maxOccurrencesPerParameter", "number", "Per-parameter occurrence page size. Defaults to 100; capped at 256."),
                        MCPToolSchemaFactory.Prop("inputSlotOffset", "number", "Per-model flattened recursive input-slot offset."),
                        MCPToolSchemaFactory.Prop("outputSlotOffset", "number", "Per-model flattened recursive output-slot offset."),
                        MCPToolSchemaFactory.Prop("eventOffset", "number", "Declared event-name offset."),
                        MCPToolSchemaFactory.Prop("maxEvents", "number", "Maximum declared event names returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("dependencyOffset", "number", "Source dependency offset."),
                        MCPToolSchemaFactory.Prop("maxDependencies", "number", "Maximum source dependencies returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("diagnosticOffset", "number", "Diagnostic offset."),
                        MCPToolSchemaFactory.Prop("maxDiagnostics", "number", "Maximum current model diagnostics returned."),
                        MCPToolSchemaFactory.Prop("maxSlotsPerNode", "number", "Maximum recursive slots returned per direction and node when includeSlots is true. Defaults to 50; capped at 256."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum visible serialized properties per graph object when includeSerialized is true. Defaults to 40; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeSlots", "boolean", "Include recursive typed input/output slot values. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includeDiagnostics", "boolean", "Generate and include current model/compile diagnostics."),
                        MCPToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized graph diagnostic. Defaults to false.")
                    ), "assetPath");
                case "vfxgraph/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "VFX Graph or subgraph asset path below Assets/."),
                        VFXGraphOperationArrayProp(),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Apply all semantic validation against a transient graph, restore the graph, and do not publish the asset.")), "assetPath", "operations");
                case "vfxgraph/validate":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "VFX Graph or subgraph asset path."),
                        MCPToolSchemaFactory.EnumProp("mode", "Validation mode. inspect is read-only; reimport and compile mutate importer/compiled state.", "inspect", "reimport", "compile"),
                        MCPToolSchemaFactory.Prop("diagnosticOffset", "number", "Diagnostic offset."),
                        MCPToolSchemaFactory.Prop("maxDiagnostics", "number", "Maximum diagnostics returned."),
                        MCPToolSchemaFactory.Prop("includeShaders", "boolean", "Include generated shader manifests."),
                        MCPToolSchemaFactory.Prop("includeShaderSource", "boolean", "Include bounded shader source text."),
                        MCPToolSchemaFactory.Prop("shaderOffset", "number", "Generated shader offset."),
                        MCPToolSchemaFactory.Prop("maxShaders", "number", "Maximum generated shaders returned. Defaults to 64; capped at 256."),
                        MCPToolSchemaFactory.Prop("shaderSourceOffset", "number", "Per-shader source character offset."),
                        MCPToolSchemaFactory.Prop("maxShaderSourceChars", "number", "Maximum source characters returned per shader. Defaults to and is capped at 4096."),
                        MCPToolSchemaFactory.Prop("systemOffset", "number", "Compiled system-name offset."),
                        MCPToolSchemaFactory.Prop("maxSystems", "number", "Maximum system names returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("eventOffset", "number", "Event-name offset."),
                        MCPToolSchemaFactory.Prop("maxEvents", "number", "Maximum event names returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("exposedPropertyOffset", "number", "Exposed-property manifest offset."),
                        MCPToolSchemaFactory.Prop("maxExposedProperties", "number", "Maximum exposed properties returned. Defaults to 100; capped at 1000."),
                        MCPToolSchemaFactory.Prop("dependencyOffset", "number", "Asset dependency offset."),
                        MCPToolSchemaFactory.Prop("maxDependencies", "number", "Maximum asset dependencies returned. Defaults to 100; capped at 1000.")), "assetPath");
                case "vfxgraph/component-info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Optional prefab asset path; omit for loaded scenes."),
                        MCPToolSchemaFactory.Prop("scenePath", "string", "Optional exact loaded scene path."),
                        MCPToolSchemaFactory.Prop("hierarchyPath", "string", "Exact GameObject hierarchy path for one component."),
                        MCPToolSchemaFactory.Prop("hierarchyIndexPath", "string", "Exact slash-separated sibling-index path returned by this route; use it when names are duplicated."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Zero-based VisualEffect component index."),
                        MCPToolSchemaFactory.Prop("gameObjectInstanceId", "string", "Loaded GameObject instance ID."),
                        MCPToolSchemaFactory.Prop("componentInstanceId", "string", "Loaded VisualEffect component instance ID."),
                        MCPToolSchemaFactory.Prop("includeOverrides", "boolean", "Include exposed-property values and override state."),
                        MCPToolSchemaFactory.Prop("overrideOffset", "number", "Per-component exposed-property override offset."),
                        MCPToolSchemaFactory.Prop("maxOverridesPerComponent", "number", "Per-component override page size. Defaults to 100; capped at 256."),
                        MCPToolSchemaFactory.Prop("includeRuntimeState", "boolean", "In Play Mode, include paged per-system particle/spawner state and output-event names for loaded scene components."),
                        MCPToolSchemaFactory.Prop("systemOffset", "number", "Per-component runtime system offset."),
                        MCPToolSchemaFactory.Prop("maxSystemsPerComponent", "number", "Per-component runtime system page size. Defaults to 100; capped at 256."),
                        MCPToolSchemaFactory.Prop("outputEventOffset", "number", "Per-component runtime output-event offset."),
                        MCPToolSchemaFactory.Prop("maxOutputEventsPerComponent", "number", "Per-component runtime output-event page size. Defaults to 100; capped at 256."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Component result offset."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum components returned.")));
                case "vfxgraph/component-transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Optional prefab asset path; omit for a loaded-scene component."),
                        MCPToolSchemaFactory.Prop("scenePath", "string", "Exact loaded scene path."),
                        MCPToolSchemaFactory.Prop("hierarchyPath", "string", "Exact GameObject hierarchy path."),
                        MCPToolSchemaFactory.Prop("hierarchyIndexPath", "string", "Exact slash-separated sibling-index path returned by component-info."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Zero-based VisualEffect component index."),
                        MCPToolSchemaFactory.Prop("gameObjectInstanceId", "string", "Loaded GameObject instance ID."),
                        MCPToolSchemaFactory.Prop("componentInstanceId", "string", "Loaded VisualEffect component instance ID."),
                        VFXComponentOperationArrayProp(),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate the ordered component transaction and restore the original component.")), "operations");
                case "vfxgraph/component-control":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("scenePath", "string", "Exact loaded scene path."),
                        MCPToolSchemaFactory.Prop("hierarchyPath", "string", "Exact GameObject hierarchy path."),
                        MCPToolSchemaFactory.Prop("hierarchyIndexPath", "string", "Exact slash-separated sibling-index path returned by component-info."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Zero-based VisualEffect component index."),
                        MCPToolSchemaFactory.Prop("gameObjectInstanceId", "string", "Loaded GameObject instance ID."),
                        MCPToolSchemaFactory.Prop("componentInstanceId", "string", "Loaded VisualEffect component instance ID."),
                        MCPToolSchemaFactory.EnumProp("action", "Play Mode action.", "play", "stop", "pause", "resume", "reinit", "advance-one-frame", "simulate", "send-event", "set-override", "reset-override"),
                        MCPToolSchemaFactory.Prop("eventName", "string", "Event name for send-event."),
                        VFXEventAttributeArrayProp(),
                        MCPToolSchemaFactory.Prop("deltaTime", "number", "Simulation step duration in (0, 10]. Defaults to 1/60 second."),
                        MCPToolSchemaFactory.Prop("stepCount", "number", "Simulation step count in [1, 1024]. deltaTime multiplied by stepCount must not exceed 60 seconds."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Exposed property for set/reset-override."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Typed exposed-property value.")), "action");
                case "vfxgraph/settings-info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.EnumProp("scope", "Optional settings scope.", "project", "user")));
                case "vfxgraph/settings-transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        VFXSettingsOperationArrayProp(),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate settings and restore both ProjectSettings and EditorPrefs.")), "operations");
                case "vfxgraph/bake":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.EnumProp("kind", "Bake pipeline.", "sdf", "point-cache-mesh", "point-cache-texture"),
                        MCPToolSchemaFactory.Prop("meshPath", "string", "Source Mesh asset path for SDF or mesh point cache."),
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Source Texture2D asset path for texture point cache."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output .asset (SDF) or .pcache path below Assets/."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Overwrite contents while preserving existing meta identity."),
                        MCPToolSchemaFactory.Vector3Prop("boxSize", "SDF bake box size."),
                        MCPToolSchemaFactory.Vector3Prop("boxCenter", "SDF bake box center."),
                        MCPToolSchemaFactory.Prop("maxResolution", "number", "SDF resolution along the largest dimension."),
                        MCPToolSchemaFactory.Prop("signPassCount", "number", "SDF sign refinement passes, 1 to 20."),
                        MCPToolSchemaFactory.Prop("threshold", "number", "SDF inside/outside or texture-decimation threshold."),
                        MCPToolSchemaFactory.Prop("offset", "number", "SDF surface offset."),
                        MCPToolSchemaFactory.EnumProp("meshBakeMode", "Mesh point sampling primitive.", "vertex", "triangle"),
                        MCPToolSchemaFactory.EnumProp("distribution", "Mesh point distribution.", "sequential", "random", "random-uniform-area"),
                        MCPToolSchemaFactory.Prop("pointCount", "number", "Mesh point-cache sample count."),
                        MCPToolSchemaFactory.Prop("seed", "number", "Random sampling seed."),
                        MCPToolSchemaFactory.Prop("exportNormals", "boolean", "Export mesh normals."),
                        MCPToolSchemaFactory.Prop("exportColors", "boolean", "Export colors."),
                        MCPToolSchemaFactory.Prop("exportUV", "boolean", "Export first mesh UV channel."),
                        MCPToolSchemaFactory.EnumProp("format", "Point cache output encoding.", "ascii", "binary"),
                        MCPToolSchemaFactory.EnumProp("thresholdMode", "Texture point-cache threshold channel.", "none", "alpha", "luminance", "r", "g", "b"),
                        MCPToolSchemaFactory.Prop("randomize", "boolean", "Randomize accepted texture pixel order.")), "kind", "outputPath");
                case "audio-mixer/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        MCPToolSchemaFactory.Prop("maxGroups", "number", "Maximum groups returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxSnapshots", "number", "Maximum snapshots returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxEffects", "number", "Maximum detailed effects returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxChildrenPerGroup", "number", "Maximum child groups listed per group. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxEffectsPerGroup", "number", "Maximum effect references listed per group. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxParametersPerEffect", "number", "Maximum parameter definitions returned per effect. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("maxExposedParameters", "number", "Maximum exposed parameters returned. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxObjects", "number", "Maximum mixer subassets in the optional serialized diagnostic. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum visible serialized properties per object when includeSerialized is true. Defaults to 40; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized mixer diagnostic. Defaults to false.")
                    ), "assetPath");
                case "audio-mixer/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        AudioMixerOperationArrayProp("operations", "Ordered semantic group, snapshot, effect, exposed-parameter, snapshot-value, rename, or set-property operations. Runtime exposed-parameter overrides must use a separate transaction."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and describe the transaction without changing the mixer.")
                    ), "assetPath", "operations");
                case "build/profile":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.EnumProp("action", "Build Profile action. Defaults to info.", "info", "transaction"),
                        BuildProfileOperationArrayProp("operations", "For transaction, ordered set-active, set-scenes, set-scripting-defines, set-global-scenes, or set-property operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return current profiles plus requested operations without mutation."),
                        MCPToolSchemaFactory.Prop("includeAfter", "boolean", "Include a paginated post-transaction Build Profile snapshot. Defaults to false; operation results are returned regardless."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Build Profile offset for info or includeAfter. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum Build Profiles for info or includeAfter. Defaults to 50; capped at 200.")
                    ));
                case "addressables/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("offset", "number", "Addressable entry offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum entries returned. Defaults to 100; capped at 500.")
                    ));
                case "addressables/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        AddressablesOperationArrayProp("operations", "Ordered create/remove/default-group, add/remove/rename-label, create-or-move-entry, set-address, set-label, or remove-entry operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and describe the Addressables transaction without modifying settings.")
                    ), "operations");
                case "addressables/build":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "timeline/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        MCPToolSchemaFactory.Prop("maxTracks", "number", "Maximum tracks returned across the semantic hierarchy. Defaults to 250; capped at 1000."),
                        MCPToolSchemaFactory.Prop("maxClipsPerTrack", "number", "Maximum clips returned per track. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxMarkersPerTrack", "number", "Maximum markers returned per track. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxObjects", "number", "Maximum Timeline subassets returned. Defaults to 250; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum serialized properties per Timeline object when includeSerialized is true. Defaults to 60; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized Timeline diagnostic. Defaults to false.")
                    ), "assetPath");
                case "timeline/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        TimelineOperationArrayProp("operations", "Ordered create-track, delete-track, rename-track, set-track-property, create-clip, delete-clip, or set-clip operations."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return the current Timeline plus requested operations without mutation."),
                        MCPToolSchemaFactory.Prop("includeAfter", "boolean", "Include a bounded post-transaction Timeline snapshot. Defaults to false; operation results are returned regardless."),
                        MCPToolSchemaFactory.Prop("maxTracks", "number", "Maximum tracks in includeAfter. Defaults to 250; capped at 1000."),
                        MCPToolSchemaFactory.Prop("maxClipsPerTrack", "number", "Maximum clips per track in includeAfter. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("maxMarkersPerTrack", "number", "Maximum markers per track in includeAfter. Defaults to 100; capped at 500.")
                    ), "assetPath", "operations");
                case "cinemachine/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Optional prefab asset path. Omit to inspect loaded scenes."),
                        MCPToolSchemaFactory.Prop("includeProperties", "boolean", "Include bounded serialized properties for every Cinemachine component. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum serialized properties per component. Defaults to 60; capped at 200."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Cinemachine component offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Maximum Cinemachine components returned. Defaults to 100; capped at 500.")
                    ));
                case "cinemachine/transaction":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Optional prefab asset path. Omit to edit loaded scene objects."),
                        CinemachineOperationArrayProp("operations", "Ordered set-property, set-object-reference, or set-enabled operations. Select scene objects by scenePath plus GameObject path, and components or target components by type plus zero-based index."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Resolve and describe every operation without modifying scene or prefab data.")
                    ), "operations");
                case "instance/current":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "instance/list":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeStale", "boolean", "Include registry entries whose editor process may no longer be running. Defaults to false.")
                    ));
                case "instance/resolve":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("projectPath", "string", "Unity project root path to resolve. Exact normalized path match."),
                        MCPToolSchemaFactory.Prop("port", "number", "MCP bridge port to resolve.")
                    ));
                case "instance/assert-project":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("expectedProjectPath", "string", "Expected Unity project root path.")
                    ));
                case "asset/export-unitypackage":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Unity asset paths to export, e.g. Assets/MyFolder or Assets/MyPrefab.prefab."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Absolute path or project-root-relative path for the .unitypackage output."),
                        MCPToolSchemaFactory.Prop("includeDependencies", "boolean", "Include asset dependencies. Defaults to true."),
                        MCPToolSchemaFactory.Prop("recurse", "boolean", "Recursively export folder contents. Defaults to true."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Replace an existing output file. Defaults to false."),
                        MCPToolSchemaFactory.Prop("interactive", "boolean", "Show Unity's export package UI. Defaults to false.")
                    ), "outputPath");
                case "asset/import-unitypackage":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("packagePath", "string", "Absolute path or project-root-relative path to a .unitypackage file. Import is always non-interactive.")
                    ), "packagePath");
                case "editor/play-mode":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("action", "string", "Target action: play, pause, resume, step, or stop. Defaults to play. Pause is idempotent; step advances one frame and remains paused."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for the confirmed target state. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive Editor updates that must confirm the target state. Defaults to 2.")
                    ));
                case "editor/execute-code":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("code", "string", "C# method body to execute. Return a value to serialize it."),
                        MCPToolSchemaFactory.ArrayProp("usings", "string", "Additional namespace imports for this call. Recurring imports can be configured in Project Settings > VM Unity Automation > Execute Code. UnityEngine.UIElements is included by default."),
                        MCPToolSchemaFactory.Prop("maxResultItems", "number", "Maximum serialized collection/object entries across the result. Defaults to 200; capped at 2000."),
                        MCPToolSchemaFactory.Prop("maxResultDepth", "number", "Maximum serialized result depth. Defaults to 8; capped at 16."),
                        MCPToolSchemaFactory.Prop("maxResultStringLength", "number", "Maximum characters per returned string. Defaults to 20000; capped at 200000."),
                        MCPToolSchemaFactory.EnumProp("unityStructFormat", "Unity value structs in the result: compact strings or structured typed objects. Defaults to compact.", "compact", "structured"),
                        MCPToolSchemaFactory.Prop("includeStackTrace", "boolean", "Include a full managed stack trace when executed code throws. Defaults to false."),
                        MCPToolSchemaFactory.Prop("idempotencyKey", "string", "Optional project-scoped key. Repeating the same key returns the existing persistent job instead of executing code again."),
                        MCPToolSchemaFactory.Prop("cleanupCode", "string", "Optional C# method body used only by jobs/cleanup to reverse temporary state created by this job.")
                    ), "code");
                case "profiler/enable":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("enabled", "boolean", "Enable or disable Profiler recording. Defaults to true."),
                        MCPToolSchemaFactory.Prop("deepProfiling", "boolean", "Optional deep profiling state.")
                    ));
                case "profiler/stats":
                case "profiler/memory":
                case "profiler/analyze":
                case "profiler/memory-status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "profiler/frame-data":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("frameIndex", "number", "Recorded Profiler frame index. Defaults to the latest frame."),
                        MCPToolSchemaFactory.Prop("threadIndex", "number", "Profiler thread index. Defaults to 0 for Main Thread."),
                        MCPToolSchemaFactory.Prop("maxItems", "number", "Maximum timing entries. Defaults to 30."),
                        MCPToolSchemaFactory.Prop("minTimeMs", "number", "Exclude nested timing entries below this total time.")
                    ));
                case "profiler/memory-breakdown":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeDetails", "boolean", "Include the largest assets in each category."),
                        MCPToolSchemaFactory.Prop("maxPerCategory", "number", "Maximum detailed assets per category. Defaults to 5.")
                    ));
                case "profiler/memory-top-assets":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("count", "number", "Maximum assets to return. Defaults to 20."),
                        MCPToolSchemaFactory.Prop("type", "string", "Optional asset type filter such as texture, mesh, audio, material, shader, animation, or font.")
                    ));
                case "profiler/memory-snapshot":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional output directory. Defaults to Unity's temporary cache MemorySnapshots folder."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for snapshot completion. Defaults to 120000.")
                    ));
                case "profiler/memory-snapshot-status":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional snapshot job ID. Defaults to the current job in this Editor session.")
                    ));
                case "scene/hierarchy":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000."),
                        MCPToolSchemaFactory.Prop("parentPath", "string", "Optional GameObject path used as the search root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type name or full name. When set, returns compact flat matches instead of the full hierarchy."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive GameObject name filter used with componentType."),
                        MCPToolSchemaFactory.Prop("pathContains", "string", "Optional case-insensitive hierarchy path filter used with componentType."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Component-filtered result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum component-filtered matches. Defaults to min(maxNodes, 50); capped at 200.")
                    ));
                case "testing/list-tests":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        MCPToolSchemaFactory.Prop("nameFilter", "string", "Optional case-insensitive test full-name filter."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Test result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum tests to return. Defaults to 100; capped at 500.")
                    ));
                case "testing/run-tests":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        MCPToolSchemaFactory.ArrayProp("testNames", "string", "Optional exact test full names."),
                        MCPToolSchemaFactory.ArrayProp("categories", "string", "Optional test categories. VM VM Unity Automation defaults to VMUnityAutomation.PackageSmoke when testNames, categories, and groupNames are all omitted; pass VMUnityAutomation.FullRegression for the full suite."),
                        MCPToolSchemaFactory.ArrayProp("assemblies", "string", "Optional test assembly names."),
                        MCPToolSchemaFactory.ArrayProp("groupNames", "string", "Optional Unity Test Runner group names."),
                        MCPToolSchemaFactory.Prop("clearStuck", "boolean", "Force-clear a previously stuck job before starting. Defaults to false.")
                    ));
                case "testing/get-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional job ID. Defaults to the current or latest job."),
                        MCPToolSchemaFactory.Prop("includeDetails", "boolean", "Include paginated individual test results. Defaults to false."),
                        MCPToolSchemaFactory.Prop("includeFailedOnly", "boolean", "Include only failed or inconclusive test results."),
                        MCPToolSchemaFactory.Prop("includeStackTrace", "boolean", "Include test stack traces. Defaults to false."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Individual test result offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("limit", "number", "Individual test result limit. Defaults to 100; capped at 500."),
                        MCPToolSchemaFactory.Prop("failureLimit", "number", "Maximum failures included in progress. Defaults to 20; capped at 100.")
                    ));
                case "testing/run-package-tests":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("packageName", "string", "Git package name. Defaults to com.vm233.unity-automation."),
                        MCPToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        MCPToolSchemaFactory.ArrayProp("assemblies", "string", "Test assembly names. Defaults to the VM Unity Automation regression assembly for the VM Unity Automation package."),
                        MCPToolSchemaFactory.ArrayProp("testNames", "string", "Optional exact test full names."),
                        MCPToolSchemaFactory.ArrayProp("categories", "string", "Optional test categories."),
                        MCPToolSchemaFactory.ArrayProp("groupNames", "string", "Optional Unity Test Runner group names.")
                    ));
                case "testing/get-package-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional package-test job ID. Defaults to the active or latest workflow."),
                        MCPToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when the package-test job started. Required after the originating MCP agent disconnects."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Delete terminal workflow state after returning it. Defaults to false.")
                    ));
                case "scene/instantiate-prefab":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Prefab asset path to instantiate into the currently open scene."),
                        MCPToolSchemaFactory.Prop("name", "string", "Optional name for the created scene instance."),
                        MCPToolSchemaFactory.Prop("parent", "string", "Optional scene GameObject name used as the parent."),
                        MCPToolSchemaFactory.Vector3Prop("position", "Optional world position object with x/y/z."),
                        MCPToolSchemaFactory.Vector3Prop("rotation", "Optional world Euler rotation object with x/y/z.")
                    ), "prefabPath");
                case "prefab-asset/hierarchy":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to inspect."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Optional GameObject path used as the hierarchy root."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000.")
                    ), "assetPath");
                case "prefab-asset/get-properties":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to inspect."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name.")
                    ), "assetPath", "componentType");
                case "prefab-asset/set-property":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Serialized property name or property path to set."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized value to assign. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType", "propertyName", "value");
                case "prefab-asset/set-reference":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name. Optional when propertyName can identify the component."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "ObjectReference serialized property name or property path."),
                        MCPToolSchemaFactory.Prop("referenceAssetPath", "string", "Project asset path to assign. Ambiguous compatible objects require an exact subasset selector."),
                        MCPToolSchemaFactory.Prop("referenceSubAssetName", "string", "Optional exact object name within referenceAssetPath."),
                        MCPToolSchemaFactory.Prop("referenceSubAssetLocalId", "string", "Optional exact local file ID within referenceAssetPath, encoded as a decimal string."),
                        MCPToolSchemaFactory.Prop("referencePrefabPath", "string", "Path of a GameObject inside the same prefab to assign."),
                        MCPToolSchemaFactory.Prop("referenceComponentType", "string", "When using referencePrefabPath, assign this component instead of the GameObject."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Clear the ObjectReference."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "propertyName");
                case "prefab-asset/instantiate-child-prefab":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Target prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("sourcePrefabPath", "string", "Prefab asset path to instantiate into the target prefab."),
                        MCPToolSchemaFactory.Prop("parentPrefabPath", "string", "Parent path inside the target prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("name", "string", "Optional name override for the created GameObject."),
                        MCPToolSchemaFactory.Prop("siblingIndex", "number", "Optional sibling index under the parent."),
                        MCPToolSchemaFactory.Vector3Prop("position", "Optional local position object with x/y/z."),
                        MCPToolSchemaFactory.Vector3Prop("rotation", "Optional local Euler rotation object with x/y/z."),
                        MCPToolSchemaFactory.Vector3Prop("scale", "Optional local scale object with x/y/z.")
                    ), "assetPath", "sourcePrefabPath");
                case "prefab-asset/add-gameobject":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("parentPrefabPath", "string", "Parent path inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("name", "string", "Name of the new child GameObject."),
                        MCPToolSchemaFactory.Prop("primitiveType", "string", "Optional Unity PrimitiveType to create, e.g. Cube or Sphere."),
                        MCPToolSchemaFactory.Prop("layer", "string", "Optional Unity layer name or numeric index. Defaults to the parent GameObject's layer."),
                        MCPToolSchemaFactory.Vector3Prop("position", "Optional local position object with x/y/z."),
                        MCPToolSchemaFactory.Vector3Prop("rotation", "Optional local Euler rotation object with x/y/z."),
                        MCPToolSchemaFactory.Vector3Prop("scale", "Optional local scale object with x/y/z."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "name");
                case "prefab-asset/add-component":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.JsonValueMapProp("properties", "Optional serialized property names/paths mapped to initial JSON values. Values are applied before the new component is saved."),
                        MCPToolSchemaFactory.Prop("waitForType", "boolean", "Wait for compilation/import until the component type is available. Defaults to true."),
                        MCPToolSchemaFactory.Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                        MCPToolSchemaFactory.Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                        MCPToolSchemaFactory.Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh once before waiting. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."),
                        MCPToolSchemaFactory.ArrayProp("prefabFileDiffIgnoreContains", "string", "Optional substrings used to hide noisy diff lines."),
                        MCPToolSchemaFactory.ArrayProp("prefabFileDiffIgnoreYamlProperties", "string", "Optional YAML property names used to hide noisy diff lines.")
                    ), "assetPath", "componentType");
                case "prefab-asset/configure-component":
                    return MCPToolSchemaFactory.PrefabAssetConfigureComponentSchema();
                case "prefab-asset/remove-component":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType");
                case "prefab-asset/move-component":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("sourcePrefabPath", "string", "Path of the source GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("targetPrefabPath", "string", "Path of the target GameObject inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index on the source GameObject. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "sourcePrefabPath", "targetPrefabPath", "componentType");
                case "prefab-asset/move-gameobject":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject to move inside the prefab."),
                        MCPToolSchemaFactory.Prop("newParentPrefabPath", "string", "New parent path inside the prefab. Empty means root."),
                        MCPToolSchemaFactory.Prop("siblingIndex", "number", "Optional sibling index under the new parent."),
                        MCPToolSchemaFactory.Prop("worldPositionStays", "boolean", "Preserve world transform while reparenting. Defaults to false.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/remove-gameobject":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        MCPToolSchemaFactory.Prop("prefabPath", "string", "Path of the child GameObject to remove. Cannot be root."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/find":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to search."),
                        MCPToolSchemaFactory.Prop("name", "string", "Exact GameObject name filter."),
                        MCPToolSchemaFactory.Prop("nameContains", "string", "Case-insensitive GameObject name contains filter."),
                        MCPToolSchemaFactory.Prop("pathContains", "string", "Case-insensitive prefab path contains filter."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type name or full name filter."),
                        MCPToolSchemaFactory.Prop("propertyName", "string", "Optional serialized property name/path to require on the component."),
                        MCPToolSchemaFactory.Prop("propertyValue", "string", "Optional serialized property value to match."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum returned matches. Defaults to 50.")
                    ), "assetPath");
                case "prefab-asset/transaction-edit":
                    return MCPToolSchemaFactory.PrefabAssetTransactionEditSchema();
                case "prefab-asset/cleanup-missing-overrides":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Prefab Variant asset path to clean."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Report removable overrides without saving. Defaults to false."),
                        MCPToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        MCPToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath");
                case "component/set-reference":
                    return MCPToolSchemaFactory.ComponentSetReferenceSchema();
                case "component/set-property":
                    return MCPRouteSchemaFactory.RequireAnyOf(
                        MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                            MCPToolSchemaFactory.Prop("instanceId", "string", "Target scene GameObject instance id."),
                            MCPToolSchemaFactory.Prop("path", "string", "Target scene GameObject hierarchy path when instanceId is omitted."),
                            MCPToolSchemaFactory.Prop("componentType", "string", "Component type name or full name."),
                            MCPToolSchemaFactory.Prop("propertyName", "string", "Serialized property name, or inherited Behaviour property name such as enabled."),
                            MCPToolSchemaFactory.AnyJsonValueProp("value", "Property value. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object.")
                        ), "componentType", "propertyName", "value"),
                        "path", "instanceId");
                case "serialized-object/get":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("instanceId", "number", "Target Unity object instance id."),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        MCPToolSchemaFactory.Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        MCPToolSchemaFactory.Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        MCPToolSchemaFactory.Prop("propertyPath", "string", "Optional serialized property path to read."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Visible property offset. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxProperties", "number", "Maximum properties to return when propertyPath is omitted. Defaults to 50; capped at 500."),
                        MCPToolSchemaFactory.Prop("includeChildren", "boolean", "Walk child properties. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum nested serialized value depth. Defaults to 3; capped at 8."),
                        MCPToolSchemaFactory.Prop("maxArrayElements", "number", "Maximum elements returned per serialized array. Defaults to 50; capped at 500.")
                    ));
                case "serialized-object/set":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("instanceId", "number", "Target Unity object instance id."),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        MCPToolSchemaFactory.Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        MCPToolSchemaFactory.Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        MCPToolSchemaFactory.Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        MCPToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        MCPToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path to write."),
                        MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized value. A primitive scalar may be wrapped as {value: ...} when the MCP client exposes this field as an object. ObjectReference supports assetPath, instanceId, or gameObject. SerializeReference objects may include '$managedReferenceType' as 'AssemblyName::Namespace.TypeName'.")
                    ), "propertyPath", "value");
                case "asset/rename":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Current asset path, e.g. Assets/Art/Old Name.png."),
                        MCPToolSchemaFactory.Prop("newName", "string", "New file or folder name. Do not include a directory path."),
                        MCPToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return expected paths without renaming.")
                    ));
                case "asset/import":
                    return MCPToolSchemaFactory.AssetImportSchema();
                case "asset/refresh":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Optional Unity asset paths to import. When supplied, only these paths are imported, with known dependencies before dependents. Omit to run a full synchronous AssetDatabase refresh and reconcile all external changes."),
                        MCPToolSchemaFactory.Prop("forceUpdate", "boolean", "Use ImportAssetOptions.ForceUpdate for full refreshes and non-compilation targeted assets. Compilation assets are always imported without ForceUpdate to avoid broad dependency reimports. Defaults to false."),
                        MCPToolSchemaFactory.Prop("saveAssets", "boolean", "Call AssetDatabase.SaveAssets after refresh/import. Defaults to false."),
                        MCPToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity. Reusing it with identical arguments returns the same durable job; different arguments are rejected.")
                    ));
                case "asset/move":
                    return MCPToolSchemaFactory.AssetMoveSchema();
                case "console/query":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("count", "number", "Maximum returned entries. Defaults to 50; capped at 200."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Filtered entry offset, counting from the newest match. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("type", "string", "Filter by all, error, warning, info, exception, or assert. Defaults to all."),
                        MCPToolSchemaFactory.Prop("messageContains", "string", "Case-insensitive message substring filter."),
                        MCPToolSchemaFactory.Prop("sourceContains", "string", "Case-insensitive source stack frame/path substring filter."),
                        MCPToolSchemaFactory.Prop("stackContains", "string", "Case-insensitive full stack substring filter."),
                        MCPToolSchemaFactory.Prop("since", "string", "Start time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        MCPToolSchemaFactory.Prop("until", "string", "End time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        MCPToolSchemaFactory.Prop("sinceSecondsAgo", "number", "Start time filter relative to now."),
                        MCPToolSchemaFactory.Prop("sinceLastPlay", "boolean", "Only include entries recorded after the latest Play transition."),
                        MCPToolSchemaFactory.Prop("includeStack", "boolean", "Include full stack traces. Defaults to false."),
                        MCPToolSchemaFactory.Prop("newestFirst", "boolean", "Return newest entries first. Defaults to false.")
                    ));
                case "debug/attach-unity":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("openWindow", "boolean", "Open Unity's Managed Debugger window. Defaults to false."),
                        MCPToolSchemaFactory.Prop("waitForAttach", "boolean", "Wait briefly for an external managed debugger to attach. Defaults to false."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Attach wait timeout in milliseconds when waitForAttach is true. Defaults to 0.")
                    ));
                case "debug/set-breakpoint":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("file", "string", "Source file path for the requested breakpoint."),
                        MCPToolSchemaFactory.Prop("line", "number", "1-based source line for the requested breakpoint.")
                    ), "file", "line");
                case "debug/stack-trace":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("skipFrames", "number", "Number of MCP call frames to skip. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxFrames", "number", "Maximum stack frames to return. Defaults to 50.")
                    ));
                case "debug/variables":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("frameId", "number", "Paused debugger frame id.")
                    ), "frameId");
                case "debug/evaluate":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("expression", "string", "C# expression to evaluate in Unity Editor context. Wrapped as return <expression>; when code is omitted."),
                        MCPToolSchemaFactory.Prop("code", "string", "Full C# method body for editor-context evaluation.")
                    ));
                case "animation/transition-info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("sourceState", "string", "Optional source state name filter."),
                        MCPToolSchemaFactory.Prop("destinationState", "string", "Optional destination state, state machine, or Exit filter."),
                        MCPToolSchemaFactory.Prop("fromAnyState", "boolean", "When true, only inspect Any State transitions. When false, only inspect state transitions."),
                        MCPToolSchemaFactory.Prop("transitionIndex", "number", "Optional transition index under the source.")
                    ), "controllerPath");
                case "animation/update-state":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("stateName", "string", "State name to modify."),
                        MCPToolSchemaFactory.Prop("newStateName", "string", "Optional new state name."),
                        MCPToolSchemaFactory.Prop("motionPath", "string", "AnimationClip or Motion asset path to assign."),
                        MCPToolSchemaFactory.Prop("clearMotion", "boolean", "Clear the state's motion."),
                        MCPToolSchemaFactory.Prop("speed", "number", "State speed."),
                        MCPToolSchemaFactory.Prop("tag", "string", "State tag."),
                        MCPToolSchemaFactory.Vector2Prop("position", "State graph position object with x/y."),
                        MCPToolSchemaFactory.Prop("isDefault", "boolean", "Set this state as the layer default state."),
                        MCPToolSchemaFactory.Prop("writeDefaultValues", "boolean", "State write default values flag."),
                        MCPToolSchemaFactory.Prop("mirror", "boolean", "State mirror flag."),
                        MCPToolSchemaFactory.Prop("iKOnFeet", "boolean", "State IK on feet flag."),
                        MCPToolSchemaFactory.Prop("cycleOffset", "number", "State cycle offset.")
                    ), "controllerPath", "stateName");
                case "animation/update-transition":
                    return MCPToolSchemaFactory.AnimationUpdateTransitionSchema();
                case "animation/connect-states":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.ArrayProp("stateNames", "string", "State names to connect pairwise."),
                        MCPToolSchemaFactory.Prop("skipExisting", "boolean", "Skip existing transitions. Defaults to true."),
                        MCPToolSchemaFactory.Prop("replaceExisting", "boolean", "Remove existing matching transitions before creating new ones."),
                        MCPToolSchemaFactory.Prop("hasExitTime", "boolean", "Transition has exit time applied to created transitions."),
                        MCPToolSchemaFactory.Prop("exitTime", "number", "Transition exit time applied to created transitions."),
                        MCPToolSchemaFactory.Prop("duration", "number", "Transition duration applied to created transitions."),
                        MCPToolSchemaFactory.Prop("offset", "number", "Transition offset applied to created transitions."),
                        MCPToolSchemaFactory.Prop("hasFixedDuration", "boolean", "Fixed duration flag applied to created transitions."),
                        AnimatorConditionArrayProp("conditions", "Conditions applied to every created transition.")
                    ), "controllerPath", "stateNames");
                case "animation/validate-controller":
                {
                    Dictionary<string, object> parameter = MCPToolSchemaFactory.ObjectSchema(
                        MCPToolSchemaFactory.Props(
                            MCPToolSchemaFactory.Prop("name", "string", "Exact Animator parameter name."),
                            MCPToolSchemaFactory.Prop("type", "string", "Optional expected AnimatorControllerParameterType.")),
                        "name");
                    Dictionary<string, object> transition = MCPToolSchemaFactory.ObjectSchema(
                        MCPToolSchemaFactory.Props(
                            MCPToolSchemaFactory.Prop("source", "string", "Exact source state name."),
                            MCPToolSchemaFactory.Prop("destination", "string", "Exact destination state name."),
                            MCPToolSchemaFactory.Prop("conditionParameter", "string", "Optional required transition-condition parameter.")),
                        "source", "destination");
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        MCPToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        MCPToolSchemaFactory.ArrayProp("requiredParameters", new Dictionary<string, object>
                        {
                            { "anyOf", new List<object>
                                {
                                    new Dictionary<string, object> { { "type", "string" } },
                                    parameter,
                                }
                            },
                        }, "Strings or objects with name and optional type."),
                        MCPToolSchemaFactory.ArrayProp("requiredStates", "string", "State names that must exist."),
                        MCPToolSchemaFactory.Prop("requireMotion", "boolean", "Require every state in the layer to have a motion."),
                        MCPToolSchemaFactory.ArrayProp("requiredTransitions", transition, "Objects with source, destination, and optional conditionParameter."),
                        MCPToolSchemaFactory.Prop("requireFullMesh", "boolean", "Require all stateNames to have pairwise transitions."),
                        MCPToolSchemaFactory.ArrayProp("stateNames", "string", "States used by full mesh validation. Defaults to all layer states.")
                    ), "controllerPath");
                }
                case "uitoolkit/audit-uss-styles":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("paths", "string", "Optional Assets-relative USS files. Omit to audit every USS file in the effective roots."),
                        MCPToolSchemaFactory.ArrayProp("roots", "string", "Assets-relative roots used to index USS and UXML files. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for UI Toolkit runtime class API references. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        MCPToolSchemaFactory.Prop("useProjectSettings", "boolean", "Use ProjectSettings/VMUnityAutomationUIToolkitAudit.json as the default scope. Defaults to true."),
                        MCPToolSchemaFactory.Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        MCPToolSchemaFactory.Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        MCPToolSchemaFactory.Prop("includeSuppressed", "boolean", "Include findings with a reasoned uss-audit suppression comment. Defaults to false."),
                        MCPToolSchemaFactory.Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        MCPToolSchemaFactory.Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/audit-uxml-layout":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.ArrayProp("paths", "string", "Optional Assets-relative UXML files. Omit to audit every UXML file in the effective roots."),
                        MCPToolSchemaFactory.ArrayProp("roots", "string", "Assets-relative roots used to index UXML and USS files. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for runtime UI element-name references. Defaults to the project audit settings, then Assets."),
                        MCPToolSchemaFactory.ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        MCPToolSchemaFactory.Prop("useProjectSettings", "boolean", "Use ProjectSettings/VMUnityAutomationUIToolkitAudit.json as the default scope. Defaults to true."),
                        MCPToolSchemaFactory.Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        MCPToolSchemaFactory.Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        MCPToolSchemaFactory.Prop("includeSuppressed", "boolean", "Include findings with a reasoned uxml-layout-audit suppression comment. Defaults to false."),
                        MCPToolSchemaFactory.Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        MCPToolSchemaFactory.Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        MCPToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/windows":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "uitoolkit/tree":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/query":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/style":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Element path from uitoolkit/tree or uitoolkit/query."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/repaint":
                    return MCPToolSchemaFactory.EditorWindowSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional element path from uitoolkit/tree or uitoolkit/query.")
                    ));
                case "uitoolkit/asset-inspect":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("uxmlPath", "string", "UXML asset path, e.g. Assets/UI/HUD.uxml."),
                        MCPToolSchemaFactory.Prop("ussPath", "string", "Optional USS asset path. UXML Style src entries are also auto-resolved."),
                        MCPToolSchemaFactory.ArrayProp("ussPaths", "string", "Optional USS asset paths. UXML Style src entries are also auto-resolved."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        MCPToolSchemaFactory.ArrayProp("names", "string", "VisualElement.name values to validate."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class exact match."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "Expected or filtered VisualElement type name."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Total result budget for elements and name matches. Defaults to 100."),
                        MCPToolSchemaFactory.Prop("includeUss", "boolean", "Parse USS files, keeping unconditional class defaults separate from contextual and pseudo-state rules. Defaults to true."),
                        MCPToolSchemaFactory.Prop("includeElements", "boolean", "Return the general elements collection. Defaults to false for names queries and true otherwise."),
                        MCPToolSchemaFactory.Prop("includeAllUssClasses", "boolean", "Return every parsed USS class. Targeted queries default to only classes used by returned elements.")
                    ));
                case "uitoolkit/runtime-documents":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("includeInactive", "boolean", "Include inactive scene UIDocuments. Defaults to true.")
                    ));
                case "uitoolkit/runtime-tree":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        MCPToolSchemaFactory.Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-query":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names, e.g. MainMap/RightControls."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match."),
                        MCPToolSchemaFactory.Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        MCPToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-style":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/diagnose-runtime":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        UIToolkitQueryArrayProp("queries", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, and pixelScale."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path if queries is omitted."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array if queries is omitted."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale used for pixel diagnostics. Defaults to 1.")
                    ));
                case "uitoolkit/visual-check":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        UIToolkitVisualCheckArrayProp("checks", "Visual checks. Supported type values: pixel-grid, background-scale, size."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path if checks is omitted."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if checks is omitted."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale. Defaults to 1."),
                        MCPToolSchemaFactory.Prop("expectedScale", "number", "Expected background image scale for background-scale checks."),
                        MCPToolSchemaFactory.Prop("width", "number", "Expected element width for size checks."),
                        MCPToolSchemaFactory.Prop("height", "number", "Expected element height for size checks."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01.")
                    ));
                case "uitoolkit/locate-element":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Locate a runtime UIDocument element when true; otherwise locate an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title. Runtime defaults to Game when capture uses it later."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        MCPToolSchemaFactory.Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/capture-element":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game, editor defaults to the focused/matched window."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output PNG path for the cropped element screenshot."),
                        MCPToolSchemaFactory.Prop("windowOutputPath", "string", "Output PNG path for the full containing window screenshot."),
                        MCPToolSchemaFactory.Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        MCPToolSchemaFactory.Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/compare-element":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Reference PNG path."),
                        MCPToolSchemaFactory.Prop("actualPath", "string", "Output path for captured current element PNG."),
                        MCPToolSchemaFactory.Prop("diffOutputPath", "string", "Optional output path for diff PNG."),
                        MCPToolSchemaFactory.RectProp("referenceRect", "Optional comparison rect in reference image."),
                        MCPToolSchemaFactory.RectProp("actualRect", "Optional comparison rect in captured image."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed per-channel pixel delta. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("padding", "number", "Extra capture padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/generated-children":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Inspect a runtime UIDocument element when true; otherwise inspect an EditorWindow UI Toolkit element. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title for editor inspection."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        MCPToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Descendant depth to inspect. Defaults to 4."),
                        MCPToolSchemaFactory.Prop("includeAll", "boolean", "Return all descendants, not only generated-looking children. Defaults to false."),
                        MCPToolSchemaFactory.ArrayProp("forbiddenClassContains", "string", "Class substrings that should produce warnings when found."),
                        MCPToolSchemaFactory.ArrayProp("forbiddenTypeContains", "string", "Type-name substrings that should produce warnings when found.")
                    ));
                case "uitoolkit/resource-audit":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("runtime", "boolean", "Audit runtime UIDocument elements when true; otherwise audit EditorWindow UI Toolkit elements. Defaults to false."),
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type/title for editor audits."),
                        UIToolkitResourceQueryArrayProp("queries", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, expectedBackgroundContains, forbiddenBackgroundContains, requireBackground."),
                        MCPToolSchemaFactory.Prop("path", "string", "Element tree path if queries is omitted."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        MCPToolSchemaFactory.Prop("expectedBackgroundContains", "string", "Expected substring in resolved background asset path or name."),
                        MCPToolSchemaFactory.ArrayProp("forbiddenBackgroundContains", "string", "Substrings that must not appear in the resolved background asset path or name."),
                        MCPToolSchemaFactory.Prop("requireBackground", "boolean", "Warn if the target has no resolved background image."),
                        MCPToolSchemaFactory.Prop("warnHighlighted", "boolean", "Warn when a target appears to use a highlighted asset. Defaults to true."),
                        MCPToolSchemaFactory.Prop("maxDepth", "number", "Descendant depth to scan for background resources. Defaults to 3.")
                    ));
                case "uitoolkit/runtime-repaint":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Optional element tree path from runtime-tree."),
                        MCPToolSchemaFactory.Prop("visualElementPath", "string", "Optional slash-separated VisualElementPath names."),
                        MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "Optional VisualElementPath names array.")
                    ));
                case "uitoolkit/refresh":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh before repainting. Defaults to true."),
                        MCPToolSchemaFactory.Prop("forceSynchronousImport", "boolean", "Use ForceSynchronousImport. Defaults to true."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive idle repaint frames required. Defaults to 2.")
                    ));
                case "uitoolkit/builder-preview":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("uxmlPath", "string", "UXML asset path to open in UI Builder."),
                        MCPToolSchemaFactory.Prop("waitFrames", "number", "Editor frames to wait before capturing. Defaults to 8."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive ready UI Builder frames required. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for the requested document and canvas. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("capture", "boolean", "Capture the UI Builder window after opening. Defaults to true."),
                        MCPToolSchemaFactory.Prop("autoMatchGameView", "boolean", "Enable UI Builder Match Game View when visible document content overflows the configured canvas. Defaults to true."),
                        MCPToolSchemaFactory.Prop("requireContentFit", "boolean", "Fail the preview result when visible document content remains clipped by the canvas. Defaults to true."),
                        MCPToolSchemaFactory.Prop("screenshotPath", "string", "PNG path for the UI Builder screenshot. Defaults to the VM Unity Automation project screenshot directory."),
                        MCPToolSchemaFactory.Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192."),
                        MCPToolSchemaFactory.Prop("zoom", "number", "Requested zoom, recorded for diagnostics. UI Builder has no stable public zoom API.")
                    ));
                case "uitoolkit/assert-layout":
                    return MCPToolSchemaFactory.RuntimeUIDocumentSchema(MCPToolSchemaFactory.Props(
                        UIToolkitLayoutAssertionArrayProp("assertions", "Layout assertions. Supported types: edge-touch, same-edge, same-center, inside, size.")
                    ), "assertions");
                case "screenshot/game":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Output PNG path. Defaults to the VM Unity Automation project screenshot directory (Assets/Screenshots initially)."),
                        MCPToolSchemaFactory.Prop("superSize", "number", "Resolution multiplier. Defaults to 1."),
                        MCPToolSchemaFactory.Prop("waitFrames", "number", "Frames to wait before requesting a running capture. Ignored while paused. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("stableFrames", "number", "Consecutive stable file-size frames required for a running capture. Ignored while paused. Defaults to 2."),
                        MCPToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for a complete decodable PNG. Defaults to 10000."),
                        MCPToolSchemaFactory.Prop("editorOverlays", "string", "Game View Gizmos and Stats policy: suppress or preserve. Defaults to suppress; use preserve only when editor overlays are the evidence subject.")
                    ));
                case "screenshot/crop":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Image path to crop."),
                        MCPToolSchemaFactory.RectProp("rect", "Crop rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output PNG path. Defaults next to source with _crop suffix."),
                        MCPToolSchemaFactory.Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true.")
                    ));
                case "screenshot/scene":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Output PNG path for file or both transport. Defaults to the VM Unity Automation project screenshot directory (Assets/Screenshots initially)."),
                        MCPToolSchemaFactory.Prop("width", "number", "Capture width in pixels. Defaults to 1920."),
                        MCPToolSchemaFactory.Prop("height", "number", "Capture height in pixels. Defaults to 1080."),
                        MCPToolSchemaFactory.Prop("transport", "string", "Output transport: file, base64, or both. Defaults to file.")
                    ));
                case "screenshot/editor-window":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("window", "string", "EditorWindow type full name, simple type name, or exact tab title."),
                        MCPToolSchemaFactory.Prop("typeOrTitle", "string", "Legacy alias for window."),
                        MCPToolSchemaFactory.Prop("path", "string", "Output PNG path. Defaults to the VM Unity Automation project screenshot directory (Assets/Screenshots initially)."),
                        MCPToolSchemaFactory.Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192.")
                    ));
                case "graphics/asset-preview":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Asset path to preview, including prefab, material, mesh, or texture assets."),
                        MCPToolSchemaFactory.Prop("width", "number", "Requested preview width in pixels. Defaults to 256."),
                        MCPToolSchemaFactory.Prop("height", "number", "Requested preview height in pixels. Defaults to 256.")
                    ), "assetPath");
                case "gameview/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props());
                case "gameview/set-resolution":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("width", "number", "Game View custom resolution width in pixels."),
                        MCPToolSchemaFactory.Prop("height", "number", "Game View custom resolution height in pixels."),
                        MCPToolSchemaFactory.Prop("label", "string", "Optional custom size label shown in the Game View size menu.")
                    ), "width", "height");
                case "gameview/set-scale":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("mode", "string", "Scale source: value or minimum. Defaults to value."),
                        MCPToolSchemaFactory.Prop("scale", "number", "Game View zoom scale when mode is value, e.g. 0.76 or 1."),
                        MCPToolSchemaFactory.Prop("fallbackScale", "number", "Fallback minimum scale used if Unity internals do not expose a valid one. Defaults to 0.76.")
                    ));
                case "graphics/image-alpha-bounds":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture2D asset path."),
                        MCPToolSchemaFactory.Prop("filePath", "string", "Absolute or project-relative PNG path if assetPath is omitted."),
                        MCPToolSchemaFactory.Prop("alphaThreshold", "number", "Alpha threshold. 0-1 or 0-255. Defaults to 0.01.")
                    ));
                case "graphics/rect-gap":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.RectProp("firstRect", "First rect with x, y, width, height."),
                        MCPToolSchemaFactory.RectProp("secondRect", "Second rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("axis", "string", "x or y. Defaults to x."),
                        MCPToolSchemaFactory.Prop("firstEdge", "string", "First rect edge. Defaults to right for x, bottom for y."),
                        MCPToolSchemaFactory.Prop("secondEdge", "string", "Second rect edge. Defaults to left for x, top for y."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Touch tolerance in pixels. Defaults to 0.5.")
                    ), "firstRect", "secondRect");
                case "graphics/annotate-rects":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Image path to annotate."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Output PNG path. Defaults next to source with _annotated suffix."),
                        AnnotationRectArrayProp("rects", "Rectangles to draw. Each has x, y, width, height, optional color and thickness."),
                        MCPToolSchemaFactory.Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true."),
                        MCPToolSchemaFactory.Prop("color", "string", "Default HTML color, e.g. #ff00ffff."),
                        MCPToolSchemaFactory.Prop("thickness", "number", "Default border thickness in pixels. Defaults to 2.")
                    ), "rects");
                case "graphics/compare-images":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("expectedPath", "string", "Reference image path."),
                        MCPToolSchemaFactory.Prop("actualPath", "string", "Current image path."),
                        MCPToolSchemaFactory.RectProp("expectedRect", "Optional reference crop rect with x, y, width, height."),
                        MCPToolSchemaFactory.RectProp("actualRect", "Optional current crop rect with x, y, width, height."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Per-channel pixel tolerance, 0-255. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("maxSamples", "number", "Maximum differing pixel samples returned. Defaults to 20."),
                        MCPToolSchemaFactory.Prop("diffOutputPath", "string", "Optional PNG path to write a red-highlight diff image.")
                    ));
                case "sprite/sheet-info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path.")
                    ));
                case "sprite/pixel-check":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture/Sprite asset path."),
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture/Sprite asset paths."),
                        MCPToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        MCPToolSchemaFactory.Prop("dimensionsMultipleOf", "number", "Optional divisor required for texture width/height."),
                        MCPToolSchemaFactory.Prop("expectedScale", "number", "Optional UI scale used to check source dimensions after scaling."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01."),
                        MCPToolSchemaFactory.Prop("requirePointFilter", "boolean", "Warn if FilterMode is not Point. Defaults to true."),
                        MCPToolSchemaFactory.Prop("requireNoCompression", "boolean", "Warn if default platform format is compressed. Defaults to true."),
                        MCPToolSchemaFactory.Prop("requireNoMipMaps", "boolean", "Warn if mip maps are enabled. Defaults to true.")
                    ));
                case "sprite/replace-and-slice":
                case "sprite/slice-sheet":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "External image file to copy over texturePath. Required for replace-and-slice."),
                        MCPToolSchemaFactory.Prop("frameWidth", "number", "Frame width in pixels."),
                        MCPToolSchemaFactory.Prop("frameHeight", "number", "Frame height in pixels."),
                        MCPToolSchemaFactory.Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        MCPToolSchemaFactory.Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        MCPToolSchemaFactory.Prop("columns", "number", "Grid column count. Defaults to textureWidth / frameWidth."),
                        MCPToolSchemaFactory.Prop("startX", "number", "Grid start x in pixels. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("startY", "number", "Grid start y in top-left pixels. Defaults to 0."),
                        MCPToolSchemaFactory.Prop("pivotX", "number", "Optional normalized pivot x."),
                        MCPToolSchemaFactory.Prop("pivotY", "number", "Optional normalized pivot y."),
                        MCPToolSchemaFactory.Prop("preserveSpriteIDs", "boolean", "Preserve existing sprite IDs by generated name. Defaults to true.")
                    ), "texturePath", "frameWidth", "frameHeight");
                case "sprite/update-animation-clip":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("clipPath", "string", "AnimationClip asset path."),
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        MCPToolSchemaFactory.Prop("bindingPath", "string", "Animation binding path to SpriteRenderer. Empty means the animated object itself."),
                        MCPToolSchemaFactory.Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        MCPToolSchemaFactory.ArrayProp("spriteNames", "string", "Optional exact sprite names to use."),
                        MCPToolSchemaFactory.Prop("loopTime", "boolean", "Whether the clip loops. Defaults to the current clip setting.")
                    ), "clipPath", "texturePath");
                case "sprite/replace-slice-update-clip":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "External image file to copy over texturePath."),
                        MCPToolSchemaFactory.Prop("clipPath", "string", "Optional AnimationClip asset path to update after slicing."),
                        MCPToolSchemaFactory.Prop("frameWidth", "number", "Frame width in pixels."),
                        MCPToolSchemaFactory.Prop("frameHeight", "number", "Frame height in pixels."),
                        MCPToolSchemaFactory.Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        MCPToolSchemaFactory.Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        MCPToolSchemaFactory.Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        MCPToolSchemaFactory.Prop("bindingPath", "string", "Animation binding path to SpriteRenderer.")
                    ), "texturePath", "sourcePath", "frameWidth", "frameHeight");
                case "textcore/sprite-asset/upsert-images":
                {
                    Dictionary<string, object> sprite = MCPToolSchemaFactory.ObjectSchema(
                        MCPToolSchemaFactory.Props(
                            MCPToolSchemaFactory.Prop("name", "string",
                                "Exact Sprite and SpriteCharacter name used by <sprite name=\"...\">."),
                            MCPToolSchemaFactory.Prop("imagePath", "string",
                                "Assets-relative PNG source path. The source asset is retained and need not be importer-readable."),
                            MCPToolSchemaFactory.Prop("glyphScale", "number",
                                "SpriteGlyph scale. Defaults to 1."),
                            MCPToolSchemaFactory.Prop("bearingX", "number",
                                "Horizontal glyph bearing X in atlas pixels. Defaults to 0."),
                            MCPToolSchemaFactory.Prop("bearingY", "number",
                                "Horizontal glyph bearing Y in atlas pixels. Defaults to spriteHeight."),
                            MCPToolSchemaFactory.Prop("advance", "number",
                                "Horizontal glyph advance in atlas pixels. Defaults to spriteWidth."),
                            MCPToolSchemaFactory.Prop("characterScale", "number",
                                "SpriteCharacter scale. Defaults to 1.")),
                        "name", "imagePath");
                    Dictionary<string, object> sprites =
                        (Dictionary<string, object>)MCPToolSchemaFactory.ArrayProp(
                            "sprites", sprite,
                            "One to sixteen uniquely named PNG images to upsert atomically.").Value;
                    sprites["minItems"] = 1;
                    sprites["maxItems"] = 16;

                    Dictionary<string, object> spriteWidth =
                        (Dictionary<string, object>)MCPToolSchemaFactory.Prop(
                            "spriteWidth", "integer",
                            "Raster width assigned to every requested Sprite.").Value;
                    spriteWidth["minimum"] = 1;
                    spriteWidth["maximum"] = 512;
                    Dictionary<string, object> spriteHeight =
                        (Dictionary<string, object>)MCPToolSchemaFactory.Prop(
                            "spriteHeight", "integer",
                            "Raster height assigned to every requested Sprite.").Value;
                    spriteHeight["minimum"] = 1;
                    spriteHeight["maximum"] = 512;
                    Dictionary<string, object> packingPadding =
                        (Dictionary<string, object>)MCPToolSchemaFactory.Prop(
                            "packingPadding", "integer",
                            "Transparent pixels around newly appended Sprite cells. Defaults to 0.").Value;
                    packingPadding["minimum"] = 0;
                    packingPadding["maximum"] = 64;

                    return MCPToolSchemaFactory.StrictSchema(
                        new Dictionary<string, object>
                        {
                            ["spriteAssetPath"] = MCPToolSchemaFactory.Prop(
                                "spriteAssetPath", "string",
                                "Assets-relative path to one existing TextCore SpriteAsset backed by an external Multiple-Sprite PNG atlas.").Value,
                            ["sprites"] = sprites,
                            ["spriteWidth"] = spriteWidth,
                            ["spriteHeight"] = spriteHeight,
                            ["packingPadding"] = packingPadding,
                        },
                        "spriteAssetPath", "sprites", "spriteWidth", "spriteHeight");
                }
                case "textmeshpro/font-asset/upsert-bitmap-glyphs":
                {
                    Dictionary<string, object> glyph = MCPToolSchemaFactory.ObjectSchema(
                        MCPToolSchemaFactory.Props(
                            MCPToolSchemaFactory.Prop("unicode", "integer",
                                "BMP private-use Unicode code point from U+E000 through U+F8FF."),
                            MCPToolSchemaFactory.Prop("imagePath", "string",
                                "Assets-relative PNG source path. The PNG is decoded from file bytes and need not be importer-readable.")),
                        "unicode", "imagePath");
                    Dictionary<string, object> glyphs =
                        (Dictionary<string, object>)MCPToolSchemaFactory.ArrayProp(
                            "glyphs", glyph,
                            "One to sixteen unique private-use bitmap glyphs to upsert atomically.").Value;
                    glyphs["minItems"] = 1;
                    glyphs["maxItems"] = 16;

                    Dictionary<string, object> glyphPixelHeight =
                        (Dictionary<string, object>)MCPToolSchemaFactory.Prop(
                            "glyphPixelHeight", "integer",
                            "Atlas raster height per glyph. Defaults to 40; the font face ascent/descent owns layout size.").Value;
                    glyphPixelHeight["minimum"] = 8;
                    glyphPixelHeight["maximum"] = 256;
                    Dictionary<string, object> packingPadding =
                        (Dictionary<string, object>)MCPToolSchemaFactory.Prop(
                            "packingPadding", "integer",
                            "Empty atlas pixels reserved around placed glyph rectangles. Defaults to 1.").Value;
                    packingPadding["minimum"] = 0;
                    packingPadding["maximum"] = 16;

                    return MCPToolSchemaFactory.StrictSchema(
                        new Dictionary<string, object>
                        {
                            ["fontAssetPath"] = MCPToolSchemaFactory.Prop(
                                "fontAssetPath", "string",
                                "Assets-relative path to one existing static TMP font asset with an embedded Alpha8 SDFAA atlas.").Value,
                            ["glyphs"] = glyphs,
                            ["glyphPixelHeight"] = glyphPixelHeight,
                            ["packingPadding"] = packingPadding,
                        },
                        "fontAssetPath", "glyphs");
                }
                case "texture/apply-sprite-preset":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Texture asset path."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are copied first."),
                        MCPToolSchemaFactory.Prop("preset", "string", "High-level preset. Supported: pixel-sprite. Preserves the current Single/Multiple mode."),
                        MCPToolSchemaFactory.Prop("pixelsPerUnit", "number", "Sprite pixels per unit."),
                        MCPToolSchemaFactory.Prop("filterMode", "string", "Texture FilterMode, e.g. Point."),
                        MCPToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                        MCPToolSchemaFactory.Prop("defaultPlatformFormat", "string", "Default platform TextureImporterFormat, e.g. RGBA32."),
                        MCPToolSchemaFactory.Prop("defaultPlatformCompression", "string", "Default platform TextureImporterCompression."),
                        MCPToolSchemaFactory.Prop("readable", "boolean", "Texture is readable."),
                        MCPToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        MCPToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Alpha is transparency."),
                        MCPToolSchemaFactory.Vector2Prop("pivot", "Sprite pivot with x/y."),
                        SpriteBorderProp("border", "Sprite border. Accepts number, [left,bottom,right,top], or object with left/bottom/right/top.")
                    ), "path");
                case "texture/info":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Texture asset path under Assets/.")
                    ), "path");
                case "texture/set-import":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("path", "string", "Texture asset path under Assets/."),
                        MCPToolSchemaFactory.Prop("textureType", "string", "TextureImporterType, such as Default, Sprite, or NormalMap."),
                        MCPToolSchemaFactory.Prop("spriteMode", "string", "SpriteImportMode, such as Single or Multiple."),
                        MCPToolSchemaFactory.Prop("spritePixelsPerUnit", "number", "Sprite pixels per unit."),
                        MCPToolSchemaFactory.Prop("sRGB", "boolean", "Import as sRGB texture."),
                        MCPToolSchemaFactory.Prop("readable", "boolean", "Enable CPU read/write access."),
                        MCPToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                        MCPToolSchemaFactory.Prop("filterMode", "string", "Texture FilterMode."),
                        MCPToolSchemaFactory.Prop("wrapMode", "string", "TextureWrapMode."),
                        MCPToolSchemaFactory.Prop("maxTextureSize", "number", "Maximum imported texture size."),
                        MCPToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                        MCPToolSchemaFactory.Prop("anisoLevel", "number", "Anisotropic filtering level."),
                        MCPToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                        MCPToolSchemaFactory.Prop("npotScale", "string", "TextureImporterNPOTScale value.")
                    ), "path");
                case "texture/find-duplicates":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("folder", "string", "Single search folder under Assets/. Defaults to Assets."),
                        MCPToolSchemaFactory.ArrayProp("folders", "string", "Additional search folders under Assets/. Results are de-duplicated across folders."),
                        MCPToolSchemaFactory.Prop("mode", "string", "Comparison mode: decodedPixels (default) or fileBytes."),
                        MCPToolSchemaFactory.ArrayProp("extensions", "string", "Optional file extensions such as png, jpg, or jpeg. decodedPixels supports PNG/JPEG."),
                        MCPToolSchemaFactory.Prop("maxAssets", "number", "Maximum assets to fingerprint. Defaults to 10000; capped at 50000."),
                        MCPToolSchemaFactory.Prop("maxGroups", "number", "Maximum duplicate groups returned. Defaults to 100; capped at 2000.")
                    ));
                case "texture/import-image":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("sourcePath", "string", "Local image file path."),
                        MCPToolSchemaFactory.Prop("sourceUrl", "string", "Remote image URL."),
                        MCPToolSchemaFactory.Prop("targetPath", "string", "Target asset path inside Assets."),
                        MCPToolSchemaFactory.Prop("targetFolder", "string", "Target folder used with assetName."),
                        MCPToolSchemaFactory.Prop("assetName", "string", "Target file name used with targetFolder."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Overwrite targetPath if content differs. Defaults to false."),
                        MCPToolSchemaFactory.Prop("dedupeByHash", "boolean", "Skip if the target folder already contains identical image bytes. Defaults to true."),
                        MCPToolSchemaFactory.Prop("applySpritePreset", "boolean", "Apply sprite import settings after import. Defaults to true."),
                        MCPToolSchemaFactory.Prop("preset", "string", "Preset passed to texture/apply-sprite-preset. Defaults to pixel-sprite.")
                    ));
                case "texture/check-import-settings":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture asset path to check."),
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        MCPToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        MCPToolSchemaFactory.Prop("preset", "string", "Optional high-level preset to check. Supported: pixel-sprite."),
                        MCPToolSchemaFactory.Prop("requirePixelSprite", "boolean", "Shortcut for preset=pixel-sprite. Defaults to true when referencePath is omitted."),
                        MCPToolSchemaFactory.Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false.")
                    ));
                case "texture/check-ui-import-settings":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("assetPath", "string", "Texture asset path to check."),
                        MCPToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture asset paths to check."),
                        MCPToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        MCPToolSchemaFactory.Prop("referencePath", "string", "Optional texture asset whose importer settings are treated as expected."),
                        MCPToolSchemaFactory.Prop("includeMatching", "boolean", "Include passing comparisons in the returned comparisons list. Defaults to false."),
                        MCPToolSchemaFactory.Prop("expectedWidth", "number", "Optional exact texture width check."),
                        MCPToolSchemaFactory.Prop("expectedHeight", "number", "Optional exact texture height check."),
                        SpriteBorderObjectProp("expectedBorder", "Optional sprite border check. Accepts object with left/bottom/right/top or x/y/z/w."),
                        MCPToolSchemaFactory.Prop("maxTextureSize", "number", "Optional exact TextureImporter maxTextureSize check."),
                        MCPToolSchemaFactory.Prop("tolerance", "number", "Float tolerance for border/PPU checks. Defaults to 0.001.")
                    ));
                case "build/start":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("target", "string", "BuildTarget. Defaults to StandaloneWindows64."),
                        MCPToolSchemaFactory.Prop("outputPath", "string", "Player output executable path."),
                        MCPToolSchemaFactory.Prop("developmentBuild", "boolean", "Build with Development flag."),
                        MCPToolSchemaFactory.ArrayProp("scenes", "string", "Optional scene paths. Defaults to enabled Build Settings scenes."),
                        MCPToolSchemaFactory.Prop("overwrite", "boolean", "Delete existing exe and Data folder before build. Defaults to true."),
                        MCPToolSchemaFactory.Prop("run", "boolean", "Launch the built executable after a successful build. Defaults to true."),
                        MCPToolSchemaFactory.Prop("runSeconds", "number", "Seconds to let the executable run before sampling/termination. Defaults to 5."),
                        MCPToolSchemaFactory.Prop("terminateAfter", "boolean", "Kill the process after sampling. Defaults to true."),
                        MCPToolSchemaFactory.Prop("captureWindow", "boolean", "Capture the built player's main window on Windows. Defaults to false."),
                        MCPToolSchemaFactory.Prop("screenshotPath", "string", "PNG path for captureWindow output."),
                        MCPToolSchemaFactory.Prop("windowWaitMs", "number", "Milliseconds to wait for the main window. Defaults to 5000."),
                        MCPToolSchemaFactory.Prop("logTailLines", "number", "Player.log tail lines to return. Defaults to 120."),
                        MCPToolSchemaFactory.Prop("clearStuck", "boolean", "Replace a non-terminal build job left behind by an interrupted editor session. Defaults to false.")
                    ), "outputPath");
                case "undo/perform":
                case "undo/redo":
                {
                    Dictionary<string, object> schema = MCPToolSchemaFactory.StrictSchema(
                        MCPToolSchemaFactory.Props(
                            MCPToolSchemaFactory.Prop("actionId", "number",
                                "Exact MCP action-history identity."),
                            MCPToolSchemaFactory.Prop("requestId", "number",
                                "Exact MCP queue request identity.")));
                    schema["anyOf"] = new List<object>
                    {
                        new Dictionary<string, object> { { "required", new List<object> { "actionId" } } },
                        new Dictionary<string, object> { { "required", new List<object> { "requestId" } } },
                    };
                    return schema;
                }
                case "undo/history":
                    return MCPToolSchemaFactory.StrictSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("limit", "number",
                            "Maximum recent MCP request records. Defaults to 50; capped at 200.")));
                case "undo/clear":
                    return MCPToolSchemaFactory.StrictSchema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("confirm", "boolean",
                            "Required explicit acknowledgement of irreversible Undo-history deletion."),
                        MCPToolSchemaFactory.Prop("objectPath", "string",
                            "Optional scene GameObject path. Omit to clear global Unity Undo history.")),
                        "confirm");
                case "build/get-job":
                    return MCPToolSchemaFactory.Schema(MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("jobId", "string", "Optional build job ID. Defaults to the current or latest job."),
                        MCPToolSchemaFactory.Prop("clear", "boolean", "Clear the persisted job after a terminal result is read. Defaults to false.")
                    ));
                default:
                    if (MCPGeneratedRouteContracts.TryGetInput(route, out var generated))
                        return generated;
                    throw new System.InvalidOperationException(
                        $"Registered route '{route}' does not declare an input contract.");
            }
        }

        private static KeyValuePair<string, object> SpriteBorderProp(
            string name, string description)
        {
            Dictionary<string, object> array = new Dictionary<string, object>
            {
                { "type", "array" },
                { "items", new Dictionary<string, object> { { "type", "number" } } },
                { "minItems", 4 },
                { "maxItems", 4 },
            };
            return MCPToolSchemaFactory.OneOfProp(name, description,
                new Dictionary<string, object> { { "type", "number" } },
                array,
                SpriteBorderObjectSchema("left", "bottom", "right", "top"));
        }

        private static KeyValuePair<string, object> SpriteBorderObjectProp(
            string name, string description)
        {
            return MCPToolSchemaFactory.OneOfProp(name, description,
                SpriteBorderObjectSchema("left", "bottom", "right", "top"),
                SpriteBorderObjectSchema("x", "y", "z", "w"));
        }

        private static Dictionary<string, object> SpriteBorderObjectSchema(
            params string[] fields)
        {
            var properties = new Dictionary<string, object>();
            foreach (string field in fields)
            {
                properties[field] = new Dictionary<string, object>
                {
                    { "type", "number" },
                    { "description", $"Sprite border {field} component." },
                };
            }
            return MCPToolSchemaFactory.ObjectSchema(properties, fields);
        }

        private static KeyValuePair<string, object> AssetImportSettingsProp(
            string name, string description)
        {
            Dictionary<string, object> sampleSettings = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("loadType", "string", "AudioClipLoadType value."),
                    MCPToolSchemaFactory.Prop("compressionFormat", "string", "AudioCompressionFormat value."),
                    MCPToolSchemaFactory.Prop("quality", "number", "Audio compression quality."),
                    MCPToolSchemaFactory.Prop("sampleRateSetting", "string", "AudioSampleRateSetting value."),
                    MCPToolSchemaFactory.Prop("sampleRateOverride", "number", "Explicit sample rate override."),
                    MCPToolSchemaFactory.Prop("preloadAudioData", "boolean", "Preload decoded audio data.")));
            sampleSettings["description"] =
                "Default audio sample settings applied by the importer.";
            Dictionary<string, object> properties = MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop("userData", "string", "Importer user data."),
                MCPToolSchemaFactory.Prop("assetBundleName", "string", "AssetBundle name."),
                MCPToolSchemaFactory.Prop("assetBundleVariant", "string", "AssetBundle variant."),
                MCPToolSchemaFactory.Prop("textureType", "string", "TextureImporterType value."),
                MCPToolSchemaFactory.Prop("textureShape", "string", "TextureImporterShape value."),
                MCPToolSchemaFactory.Prop("spriteImportMode", "string", "SpriteImportMode value."),
                MCPToolSchemaFactory.Prop("spritePixelsPerUnit", "number", "Sprite pixels per unit."),
                MCPToolSchemaFactory.Prop("sRGBTexture", "boolean", "Import as sRGB."),
                MCPToolSchemaFactory.Prop("alphaSource", "string", "TextureImporterAlphaSource value."),
                MCPToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                MCPToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
                MCPToolSchemaFactory.Prop("isReadable", "boolean", "Enable CPU read access."),
                MCPToolSchemaFactory.Prop("streamingMipmaps", "boolean", "Enable mipmap streaming."),
                MCPToolSchemaFactory.Prop("filterMode", "string", "FilterMode value."),
                MCPToolSchemaFactory.Prop("anisoLevel", "number", "Anisotropic filtering level."),
                MCPToolSchemaFactory.Prop("wrapMode", "string", "TextureWrapMode value."),
                MCPToolSchemaFactory.Prop("wrapModeU", "string", "U-axis TextureWrapMode."),
                MCPToolSchemaFactory.Prop("wrapModeV", "string", "V-axis TextureWrapMode."),
                MCPToolSchemaFactory.Prop("wrapModeW", "string", "W-axis TextureWrapMode."),
                MCPToolSchemaFactory.Prop("maxTextureSize", "number", "Maximum imported texture size."),
                MCPToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
                MCPToolSchemaFactory.Prop("compressionQuality", "number", "Texture compression quality."),
                MCPToolSchemaFactory.Prop("crunchedCompression", "boolean", "Enable crunch compression."),
                MCPToolSchemaFactory.Prop("npotScale", "string", "TextureImporterNPOTScale value."),
                MCPToolSchemaFactory.Prop("globalScale", "number", "Model global scale."),
                MCPToolSchemaFactory.Prop("useFileScale", "boolean", "Use model file scale."),
                MCPToolSchemaFactory.Prop("importBlendShapes", "boolean", "Import model blend shapes."),
                MCPToolSchemaFactory.Prop("importCameras", "boolean", "Import model cameras."),
                MCPToolSchemaFactory.Prop("importLights", "boolean", "Import model lights."),
                MCPToolSchemaFactory.Prop("importAnimation", "boolean", "Import model animation."),
                MCPToolSchemaFactory.Prop("animationType", "string", "ModelImporterAnimationType value."),
                MCPToolSchemaFactory.Prop("meshCompression", "string", "ModelImporterMeshCompression value."),
                MCPToolSchemaFactory.Prop("addCollider", "boolean", "Generate model colliders."),
                MCPToolSchemaFactory.Prop("keepQuads", "boolean", "Preserve model quads."),
                MCPToolSchemaFactory.Prop("weldVertices", "boolean", "Weld model vertices."),
                MCPToolSchemaFactory.Prop("indexFormat", "string", "Model index format."),
                MCPToolSchemaFactory.Prop("importNormals", "string", "ModelImporterNormals value."),
                MCPToolSchemaFactory.Prop("importTangents", "string", "ModelImporterTangents value."),
                MCPToolSchemaFactory.Prop("forceToMono", "boolean", "Force audio to mono."),
                MCPToolSchemaFactory.Prop("normalize", "boolean", "Normalize audio after forcing mono."),
                MCPToolSchemaFactory.Prop("loadInBackground", "boolean", "Load audio in the background."),
                MCPToolSchemaFactory.Prop("ambisonic", "boolean", "Import audio as ambisonic."),
                MCPToolSchemaFactory.Prop("preloadAudioData", "boolean", "Preload audio data."));
            properties["defaultSampleSettings"] = sampleSettings;
            return MCPToolSchemaFactory.ObjectProp(name, description, properties);
        }

        private static KeyValuePair<string, object> AssetPlatformSettingsProp(
            string name, string description)
        {
            return MCPToolSchemaFactory.ObjectProp(name, description,
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("overridden", "boolean", "Override platform texture settings."),
                    MCPToolSchemaFactory.Prop("maxTextureSize", "number", "Platform maximum texture size."),
                    MCPToolSchemaFactory.Prop("format", "string", "Platform texture format."),
                    MCPToolSchemaFactory.Prop("compressionQuality", "number", "Platform compression quality."),
                    MCPToolSchemaFactory.Prop("allowsAlphaSplitting", "boolean", "Allow platform alpha splitting."),
                    MCPToolSchemaFactory.Prop("loadType", "string", "Platform AudioClipLoadType value."),
                    MCPToolSchemaFactory.Prop("compressionFormat", "string", "Platform AudioCompressionFormat value."),
                    MCPToolSchemaFactory.Prop("quality", "number", "Platform audio quality."),
                    MCPToolSchemaFactory.Prop("sampleRateSetting", "string", "Platform AudioSampleRateSetting value."),
                    MCPToolSchemaFactory.Prop("sampleRateOverride", "number", "Platform sample rate override."),
                    MCPToolSchemaFactory.Prop("preloadAudioData", "boolean", "Preload platform audio data.")));
        }

        private static KeyValuePair<string, object> MaterialPropertyMapProp(
            string name, string description)
        {
            Dictionary<string, object> number = new Dictionary<string, object>
                { { "type", "number" } };
            Dictionary<string, object> scalarWrapper = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("value", "number", "Wrapped numeric shader value.")),
                "value");
            Dictionary<string, object> color = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("r", "number", "Red component."),
                    MCPToolSchemaFactory.Prop("g", "number", "Green component."),
                    MCPToolSchemaFactory.Prop("b", "number", "Blue component."),
                    MCPToolSchemaFactory.Prop("a", "number", "Alpha component.")),
                "r", "g", "b");
            Dictionary<string, object> vector = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("x", "number", "X component."),
                    MCPToolSchemaFactory.Prop("y", "number", "Y component."),
                    MCPToolSchemaFactory.Prop("z", "number", "Z component."),
                    MCPToolSchemaFactory.Prop("w", "number", "W component.")),
                "x", "y", "z", "w");
            Dictionary<string, object> texture = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("assetPath", "string", "Texture asset path; empty clears the texture."),
                    MCPToolSchemaFactory.Vector2Prop("scale", "Texture scale."),
                    MCPToolSchemaFactory.Vector2Prop("offset", "Texture offset.")));
            texture["anyOf"] = new List<object>
            {
                new Dictionary<string, object> { { "required", new List<object> { "assetPath" } } },
                new Dictionary<string, object> { { "required", new List<object> { "scale" } } },
                new Dictionary<string, object> { { "required", new List<object> { "offset" } } },
            };
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", description },
                { "additionalProperties", new Dictionary<string, object>
                    {
                        { "oneOf", new List<object>
                            {
                                new Dictionary<string, object> { { "type", "null" } },
                                number, scalarWrapper, color, vector, texture,
                            }
                        },
                    }
                },
            });
        }

        private static KeyValuePair<string, object> MaterialKeywordsProp(
            string name, string description)
        {
            return MCPToolSchemaFactory.ObjectProp(name, description,
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.ArrayProp("enable", "string", "Keywords to enable."),
                    MCPToolSchemaFactory.ArrayProp("disable", "string", "Keywords to disable.")));
        }

        private static Dictionary<string, object> DiscriminatedOperation(
            string type, Dictionary<string, object> properties,
            params string[] required)
        {
            properties["type"] = MCPToolSchemaFactory.EnumProp("type",
                "Operation discriminator.", type).Value;
            return MCPToolSchemaFactory.ObjectSchema(properties,
                new[] { "type" }.Concat(required ?? new string[0]).ToArray());
        }

        private static Dictionary<string, object> DiscriminatedAction(
            string action, Dictionary<string, object> properties,
            params string[] required)
        {
            properties["action"] = MCPToolSchemaFactory.EnumProp("action",
                "Operation action discriminator.", action).Value;
            return MCPToolSchemaFactory.ObjectSchema(properties,
                new[] { "action" }.Concat(required ?? new string[0]).ToArray());
        }

        private static Dictionary<string, object> OneOfOperations(
            params Dictionary<string, object>[] variants)
        {
            return new Dictionary<string, object>
            {
                { "oneOf", variants.Cast<object>().ToList() },
            };
        }

        private static Dictionary<string, object> RequiredAlternative(
            params string[] propertyNames)
        {
            return new Dictionary<string, object>
            {
                { "anyOf", propertyNames.Select(name => (object)
                    new Dictionary<string, object>
                    {
                        { "required", new List<object> { name } },
                    }).ToList()
                },
            };
        }

        private static KeyValuePair<string, object> AudioMixerOperationArrayProp(
            string name, string description)
        {
            Dictionary<string, object> setGroupState = DiscriminatedAction(
                "set-group-state", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("groupLocalId", "string", "Target group local file ID."),
                    MCPToolSchemaFactory.Prop("mute", "boolean", "Set the group mute state."),
                    MCPToolSchemaFactory.Prop("solo", "boolean", "Set the group solo state."),
                    MCPToolSchemaFactory.Prop("bypassEffects", "boolean", "Set whether the group bypasses effects.")),
                "groupLocalId");
            setGroupState["anyOf"] = RequiredAlternative(
                "mute", "solo", "bypassEffects")["anyOf"];

            Dictionary<string, object> unexpose = DiscriminatedAction(
                "unexpose-parameter", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("guid", "string", "Exposed parameter GUID."),
                    MCPToolSchemaFactory.Prop("exposedName", "string", "Exposed parameter name.")));
            unexpose["anyOf"] = RequiredAlternative(
                "guid", "exposedName")["anyOf"];

            Dictionary<string, object> item = OneOfOperations(
                DiscriminatedAction("set-exposed-parameter",
                    MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("parameter", "string", "Exposed parameter name."),
                        MCPToolSchemaFactory.Prop("value", "number", "Runtime exposed parameter value.")),
                    "parameter", "value"),
                DiscriminatedAction("clear-exposed-parameter",
                    MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.Prop("parameter", "string", "Exposed parameter name.")),
                    "parameter"),
                DiscriminatedAction("rename", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("targetLocalId", "string", "Mixer group, snapshot, or effect local file ID."),
                    MCPToolSchemaFactory.Prop("name", "string", "Replacement object name.")),
                    "targetLocalId", "name"),
                DiscriminatedAction("create-group", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("name", "string", "New group name."),
                    MCPToolSchemaFactory.Prop("parentGroupLocalId", "string", "Optional parent group local file ID; defaults to the master group.")),
                    "name"),
                DiscriminatedAction("remove-group", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("groupLocalId", "string", "Group local file ID.")),
                    "groupLocalId"),
                setGroupState,
                DiscriminatedAction("create-snapshot", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("name", "string", "New snapshot name.")),
                    "name"),
                DiscriminatedAction("remove-snapshot", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("snapshotLocalId", "string", "Snapshot local file ID.")),
                    "snapshotLocalId"),
                DiscriminatedAction("set-target-snapshot", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("snapshotLocalId", "string", "Snapshot local file ID.")),
                    "snapshotLocalId"),
                DiscriminatedAction("add-effect", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("groupLocalId", "string", "Target group local file ID."),
                    MCPToolSchemaFactory.Prop("effectName", "string", "Unity AudioMixer effect name."),
                    MCPToolSchemaFactory.Prop("index", "number", "Optional insertion index.")),
                    "groupLocalId", "effectName"),
                DiscriminatedAction("remove-effect", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID.")),
                    "effectLocalId"),
                DiscriminatedAction("set-effect-bypass", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID."),
                    MCPToolSchemaFactory.Prop("bypass", "boolean", "Requested effect bypass state.")),
                    "effectLocalId", "bypass"),
                DiscriminatedAction("expose-effect-parameter", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID."),
                    MCPToolSchemaFactory.Prop("parameter", "string", "Effect parameter name."),
                    MCPToolSchemaFactory.Prop("exposedName", "string", "Optional exposed parameter name.")),
                    "effectLocalId", "parameter"),
                unexpose,
                DiscriminatedAction("set-snapshot-parameter", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID."),
                    MCPToolSchemaFactory.Prop("parameter", "string", "Effect parameter name."),
                    MCPToolSchemaFactory.Prop("snapshotLocalId", "string", "Optional snapshot local file ID; defaults to the target snapshot."),
                    MCPToolSchemaFactory.Prop("value", "number", "Snapshot parameter value.")),
                    "effectLocalId", "parameter", "value"),
                DiscriminatedAction("set-property", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("targetLocalId", "string", "Mixer group, snapshot, or effect local file ID."),
                    MCPToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path."),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized JSON value to assign.")),
                    "targetLocalId", "propertyPath", "value"));
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static Dictionary<string, object> BuildProfileSceneItemSchema()
        {
            return new Dictionary<string, object>
            {
                { "oneOf", new List<object>
                    {
                        new Dictionary<string, object> { { "type", "string" } },
                        MCPToolSchemaFactory.ObjectSchema(
                            MCPToolSchemaFactory.Props(
                                MCPToolSchemaFactory.Prop("path", "string", "Scene asset path."),
                                MCPToolSchemaFactory.Prop("enabled", "boolean", "Whether the scene is enabled in the build.")),
                            "path"),
                    }
                },
            };
        }

        private static KeyValuePair<string, object> BuildProfileOperationArrayProp(
            string name, string description)
        {
            KeyValuePair<string, object> Scenes(string field) =>
                MCPToolSchemaFactory.ArrayProp(field, BuildProfileSceneItemSchema(),
                    "Ordered scene asset paths or path/enabled objects.");
            Dictionary<string, object> item = OneOfOperations(
                DiscriminatedAction("set-active", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path.")),
                    "assetPath"),
                DiscriminatedAction("set-scenes", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path."),
                    Scenes("scenes"),
                    MCPToolSchemaFactory.Prop("overrideGlobalScenes", "boolean", "Whether this profile overrides global scenes. Defaults to true.")),
                    "assetPath", "scenes"),
                DiscriminatedAction("set-scripting-defines", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path."),
                    MCPToolSchemaFactory.ArrayProp("defines", "string", "Complete scripting define list.")),
                    "assetPath", "defines"),
                DiscriminatedAction("set-global-scenes", MCPToolSchemaFactory.Props(
                    Scenes("scenes")), "scenes"),
                DiscriminatedAction("set-property", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path."),
                    MCPToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path."),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized JSON value to assign.")),
                    "assetPath", "propertyPath", "value"));
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static Dictionary<string, object> AddressablesEntryProperties()
        {
            return MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop("guid", "string", "Asset GUID."),
                MCPToolSchemaFactory.Prop("assetPath", "string", "Asset path resolved to a GUID."));
        }

        private static Dictionary<string, object> RequireAddressablesEntrySelector(
            Dictionary<string, object> schema)
        {
            schema["anyOf"] = RequiredAlternative("guid", "assetPath")["anyOf"];
            return schema;
        }

        private static KeyValuePair<string, object> AddressablesOperationArrayProp(
            string name, string description)
        {
            Dictionary<string, object> EntryOperation(string action,
                Dictionary<string, object> additional, params string[] required)
            {
                Dictionary<string, object> properties = AddressablesEntryProperties();
                foreach (KeyValuePair<string, object> property in additional)
                    properties[property.Key] = property.Value;
                return RequireAddressablesEntrySelector(DiscriminatedAction(
                    action, properties, required));
            }

            Dictionary<string, object> item = OneOfOperations(
                DiscriminatedAction("create-group", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("group", "string", "New group name."),
                    MCPToolSchemaFactory.Prop("setAsDefault", "boolean", "Set the new group as default."),
                    MCPToolSchemaFactory.Prop("copySchemas", "boolean", "Copy schemas to the new group. Defaults to true."),
                    MCPToolSchemaFactory.Prop("copySchemasFromGroup", "string", "Optional schema source group.")),
                    "group"),
                DiscriminatedAction("remove-group", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("group", "string", "Existing group name.")),
                    "group"),
                DiscriminatedAction("set-default-group", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("group", "string", "Existing group name.")),
                    "group"),
                DiscriminatedAction("add-label", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("label", "string", "Label to add.")),
                    "label"),
                DiscriminatedAction("remove-label", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("label", "string", "Label to remove.")),
                    "label"),
                DiscriminatedAction("rename-label", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("oldLabel", "string", "Existing label."),
                    MCPToolSchemaFactory.Prop("newLabel", "string", "Replacement label.")),
                    "oldLabel", "newLabel"),
                EntryOperation("create-or-move-entry", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("group", "string", "Target group; defaults to the default group."),
                    MCPToolSchemaFactory.Prop("address", "string", "Optional address override."))),
                EntryOperation("set-address", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("address", "string", "Replacement address.")),
                    "address"),
                EntryOperation("set-label", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("label", "string", "Existing label."),
                    MCPToolSchemaFactory.Prop("enabled", "boolean", "Whether the label is assigned. Defaults to true.")),
                    "label"),
                EntryOperation("remove-entry", MCPToolSchemaFactory.Props()));
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static Dictionary<string, object> TimelineClipProperties()
        {
            return MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop("displayName", "string", "Timeline clip display name."),
                MCPToolSchemaFactory.Prop("start", "number", "Clip start time."),
                MCPToolSchemaFactory.Prop("duration", "number", "Clip duration."),
                MCPToolSchemaFactory.Prop("clipIn", "number", "Clip source offset."),
                MCPToolSchemaFactory.Prop("timeScale", "number", "Clip playback time scale."),
                MCPToolSchemaFactory.Prop("easeInDuration", "number", "Clip ease-in duration."),
                MCPToolSchemaFactory.Prop("easeOutDuration", "number", "Clip ease-out duration."));
        }

        private static KeyValuePair<string, object> TimelineOperationArrayProp(
            string name, string description)
        {
            Dictionary<string, object> ClipOperationProperties(
                bool includeAssetType, bool includeIndex)
            {
                Dictionary<string, object> properties = MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."));
                if (includeAssetType)
                    properties["clipAssetType"] = MCPToolSchemaFactory.Prop(
                        "clipAssetType", "string", "PlayableAsset type name or full name.").Value;
                if (includeIndex)
                    properties["clipIndex"] = MCPToolSchemaFactory.Prop(
                        "clipIndex", "number", "Zero-based clip index.").Value;
                foreach (KeyValuePair<string, object> property in TimelineClipProperties())
                    properties[property.Key] = property.Value;
                return properties;
            }

            Dictionary<string, object> item = OneOfOperations(
                DiscriminatedAction("create-track", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("trackType", "string", "TrackAsset type name or full name."),
                    MCPToolSchemaFactory.Prop("name", "string", "Optional track name."),
                    MCPToolSchemaFactory.Prop("parentTrackLocalId", "string", "Optional parent track local file ID.")),
                    "trackType"),
                DiscriminatedAction("delete-track", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID.")),
                    "trackLocalId"),
                DiscriminatedAction("rename-track", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."),
                    MCPToolSchemaFactory.Prop("name", "string", "Replacement track name.")),
                    "trackLocalId", "name"),
                DiscriminatedAction("set-track-property", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."),
                    MCPToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path."),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Serialized JSON value to assign.")),
                    "trackLocalId", "propertyPath", "value"),
                DiscriminatedAction("create-clip",
                    ClipOperationProperties(includeAssetType: true, includeIndex: false),
                    "trackLocalId", "clipAssetType"),
                DiscriminatedAction("delete-clip", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."),
                    MCPToolSchemaFactory.Prop("clipIndex", "number", "Zero-based clip index.")),
                    "trackLocalId", "clipIndex"),
                DiscriminatedAction("set-clip",
                    ClipOperationProperties(includeAssetType: false, includeIndex: true),
                    "trackLocalId", "clipIndex"));
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static Dictionary<string, object> CinemachineSelectorProperties()
        {
            return MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop("path", "string", "GameObject path or exact name."),
                MCPToolSchemaFactory.Prop("gameObjectPath", "string", "Legacy GameObject path alias."),
                MCPToolSchemaFactory.Prop("scenePath", "string", "Optional loaded scene asset path."),
                MCPToolSchemaFactory.Prop("instanceId", "number", "Loaded scene GameObject instance ID."));
        }

        private static Dictionary<string, object> RequireCinemachineSelector(
            Dictionary<string, object> schema)
        {
            schema["anyOf"] = RequiredAlternative(
                "path", "gameObjectPath", "instanceId")["anyOf"];
            return schema;
        }

        private static KeyValuePair<string, object> CinemachineOperationArrayProp(
            string name, string description)
        {
            Dictionary<string, object> Common()
            {
                Dictionary<string, object> properties = CinemachineSelectorProperties();
                properties["componentType"] = MCPToolSchemaFactory.Prop(
                    "componentType", "string", "Cinemachine component type name or full name; optional when exactly one matches.").Value;
                properties["componentIndex"] = MCPToolSchemaFactory.Prop(
                    "componentIndex", "number", "Zero-based component index. Defaults to 0.").Value;
                return properties;
            }

            Dictionary<string, object> setEnabled = RequireCinemachineSelector(
                DiscriminatedAction("set-enabled", Common(), "enabled"));
            RequireProperties(setEnabled)["enabled"] = MCPToolSchemaFactory.Prop(
                "enabled", "boolean", "Requested Behaviour enabled state.").Value;

            Dictionary<string, object> setPropertyProperties = Common();
            setPropertyProperties["propertyPath"] = MCPToolSchemaFactory.Prop(
                "propertyPath", "string", "Serialized property path.").Value;
            setPropertyProperties["value"] = MCPToolSchemaFactory.AnyJsonValueProp(
                "value", "Serialized JSON value to assign.").Value;
            Dictionary<string, object> setProperty = RequireCinemachineSelector(
                DiscriminatedAction("set-property", setPropertyProperties,
                    "propertyPath", "value"));

            Dictionary<string, object> target = RequireCinemachineSelector(
                MCPToolSchemaFactory.ObjectSchema(CinemachineSelectorProperties()));
            target["description"] = "Target GameObject selector for an object-reference assignment.";

            Dictionary<string, object> ReferenceProperties()
            {
                Dictionary<string, object> properties = Common();
                properties["propertyPath"] = MCPToolSchemaFactory.Prop(
                    "propertyPath", "string", "ObjectReference serialized property path.").Value;
                return properties;
            }

            Dictionary<string, object> clearReferenceProperties = ReferenceProperties();
            clearReferenceProperties["clear"] = new Dictionary<string, object>
            {
                { "type", "boolean" },
                { "const", true },
                { "description", "Clear the object reference." },
            };
            Dictionary<string, object> clearReference = RequireCinemachineSelector(
                DiscriminatedAction("set-object-reference", clearReferenceProperties,
                    "propertyPath", "clear"));

            Dictionary<string, object> TargetReference(string targetKind,
                bool requireTargetKind, bool includeComponentSelector)
            {
                Dictionary<string, object> properties = ReferenceProperties();
                properties["target"] = target;
                properties["targetKind"] = MCPToolSchemaFactory.EnumProp(
                    "targetKind", targetKind == "transform"
                        ? "Assigned target kind. Omit to use the default transform target."
                        : "Assigned target kind.", targetKind).Value;
                var required = new List<string> { "propertyPath", "target" };
                if (requireTargetKind)
                    required.Add("targetKind");
                if (includeComponentSelector)
                {
                    properties["targetComponentType"] = MCPToolSchemaFactory.Prop(
                        "targetComponentType", "string", "Target component type name or full name.").Value;
                    properties["targetComponentIndex"] = MCPToolSchemaFactory.Prop(
                        "targetComponentIndex", "number", "Zero-based target component index. Defaults to 0.").Value;
                    required.Add("targetComponentType");
                }
                return RequireCinemachineSelector(DiscriminatedAction(
                    "set-object-reference", properties, required.ToArray()));
            }

            return MCPToolSchemaFactory.ArrayProp(name,
                OneOfOperations(
                    setProperty,
                    clearReference,
                    TargetReference("transform", requireTargetKind: false,
                        includeComponentSelector: false),
                    TargetReference("gameobject", requireTargetKind: true,
                        includeComponentSelector: false),
                    TargetReference("component", requireTargetKind: true,
                        includeComponentSelector: true),
                    setEnabled),
                description);
        }

        private static Dictionary<string, object> RequireProperties(
            Dictionary<string, object> schema)
        {
            return (Dictionary<string, object>)schema["properties"];
        }

        private static KeyValuePair<string, object> VFXGraphOperationArrayProp()
        {
            var definitions = MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop("catalogId", "string", "Exact catalog ID."),
                MCPToolSchemaFactory.Prop("kind", "string", "Model kind."),
                MCPToolSchemaFactory.Prop("nodeId", "string", "Exact model local ID or earlier alias."),
                MCPToolSchemaFactory.Prop("parameterId", "string", "Exact parameter definition ID or earlier alias."),
                MCPToolSchemaFactory.Prop("parameterNodeId", "string", "Exact <parameterId>:<nodeId> occurrence ID or earlier occurrence alias."),
                MCPToolSchemaFactory.Prop("parentContextId", "string", "Parent VFX context ID or alias."),
                MCPToolSchemaFactory.Prop("fromNodeId", "string", "Source node or parameter occurrence ID."),
                MCPToolSchemaFactory.Prop("toNodeId", "string", "Destination node or parameter occurrence ID."),
                MCPToolSchemaFactory.Prop("fromContextId", "string", "Source context ID."),
                MCPToolSchemaFactory.Prop("toContextId", "string", "Destination context ID."),
                MCPToolSchemaFactory.Prop("fromSlot", "string", "Exact output slot selector returned by catalog or info."),
                MCPToolSchemaFactory.Prop("toSlot", "string", "Exact input slot selector returned by catalog or info."),
                MCPToolSchemaFactory.Prop("direction", "string", "Slot direction: input or output."),
                MCPToolSchemaFactory.Prop("slotPath", "string", "Exact slot selector returned by catalog or info."),
                MCPToolSchemaFactory.Prop("fromIndex", "number", "Source flow slot index."),
                MCPToolSchemaFactory.Prop("toIndex", "number", "Destination flow slot index."),
                MCPToolSchemaFactory.Prop("index", "number", "Insertion or ordering index."),
                MCPToolSchemaFactory.Prop("alias", "string", "Request-local alias for the created model or occurrence."),
                MCPToolSchemaFactory.Prop("name", "string", "Semantic name."),
                MCPToolSchemaFactory.Prop("category", "string", "Parameter category."),
                MCPToolSchemaFactory.Prop("categoryName", "string", "Exact category name selector."),
                MCPToolSchemaFactory.Prop("categoryIndex", "number", "Exact category index selector."),
                MCPToolSchemaFactory.Prop("attributeName", "string", "Exact custom attribute name."),
                MCPToolSchemaFactory.Prop("valueType", "string", "VFX value type or parameter type."),
                MCPToolSchemaFactory.Prop("description", "string", "Description text."),
                MCPToolSchemaFactory.Prop("tooltip", "string", "Parameter tooltip."),
                MCPToolSchemaFactory.Prop("order", "number", "Parameter order."),
                MCPToolSchemaFactory.Prop("exposed", "boolean", "Expose the parameter."),
                MCPToolSchemaFactory.Prop("isOutput", "boolean", "Use an output parameter."),
                MCPToolSchemaFactory.Prop("collapsed", "boolean", "Collapsed state."),
                MCPToolSchemaFactory.Prop("superCollapsed", "boolean", "Super-collapsed state."),
                MCPToolSchemaFactory.Prop("expanded", "boolean", "Parameter occurrence expanded state."),
                MCPToolSchemaFactory.Prop("enabled", "boolean", "Block enabled state."),
                MCPToolSchemaFactory.Prop("removeUsages", "boolean", "Explicitly remove models using a custom attribute."),
                MCPToolSchemaFactory.Prop("parameterDisposition", "string", "Category parameter disposition: uncategorize or delete."),
                MCPToolSchemaFactory.Prop("valueFilter", "string", "Parameter value filter: Default, Range, or Enum."),
                MCPToolSchemaFactory.ArrayProp("enumValues", "string", "Parameter enum labels."),
                MCPToolSchemaFactory.ArrayProp("contents", "string", "Group model IDs or sticky:<index> selectors."),
                MCPToolSchemaFactory.Prop("title", "string", "Group or sticky-note title."),
                MCPToolSchemaFactory.Prop("theme", "string", "Sticky-note theme."),
                MCPToolSchemaFactory.Prop("textSize", "string", "Sticky-note text size."),
                MCPToolSchemaFactory.Prop("colorTheme", "number", "Sticky-note color theme index."),
                MCPToolSchemaFactory.Prop("groupIndex", "number", "Exact group index."),
                MCPToolSchemaFactory.Prop("stickyNoteIndex", "number", "Exact sticky-note index."),
                MCPToolSchemaFactory.Prop("settingName", "string", "Graph setting name."),
                MCPToolSchemaFactory.Vector2Prop("position", "Graph position."),
                MCPToolSchemaFactory.RectProp("bounds", "Graph UI bounds."),
                MCPToolSchemaFactory.JsonValueMapProp("settings", "Typed VFX model settings."),
                MCPToolSchemaFactory.JsonValueMapProp("slots", "Initial input slot values by exact path."),
                MCPToolSchemaFactory.AnyJsonValueProp("value", "Typed value."),
                MCPToolSchemaFactory.AnyJsonValueProp("space", "VFX coordinate space enum value."),
                MCPToolSchemaFactory.AnyJsonValueProp("min", "Parameter range minimum."),
                MCPToolSchemaFactory.AnyJsonValueProp("max", "Parameter range maximum."));

            Dictionary<string, object> Operation(string op, string[] fields,
                params string[] required)
            {
                var properties = new Dictionary<string, object>
                {
                    ["op"] = MCPToolSchemaFactory.EnumProp("op",
                        "VFX graph operation discriminator.", op).Value,
                };
                foreach (string field in fields)
                    properties[field] = definitions[field];
                return MCPToolSchemaFactory.ObjectSchema(properties,
                    new[] { "op" }.Concat(required).ToArray());
            }

            var variants = new List<object>
            {
                Operation("add-node", new[] { "catalogId", "kind", "parentContextId", "index", "position", "collapsed", "superCollapsed", "enabled", "settings", "slots", "alias" }, "catalogId", "kind"),
                Operation("remove-node", new[] { "nodeId", "parameterNodeId" }),
                Operation("set-node", new[] { "nodeId", "position", "collapsed", "superCollapsed", "enabled", "name", "settings" }, "nodeId"),
                Operation("set-slot", new[] { "nodeId", "direction", "slotPath", "value", "space", "collapsed" }, "nodeId", "direction", "slotPath"),
                Operation("connect-data", new[] { "fromNodeId", "fromSlot", "toNodeId", "toSlot" }, "fromNodeId", "fromSlot", "toNodeId", "toSlot"),
                Operation("disconnect-data", new[] { "fromNodeId", "fromSlot", "toNodeId", "toSlot" }, "fromNodeId", "fromSlot", "toNodeId", "toSlot"),
                Operation("connect-flow", new[] { "fromContextId", "fromIndex", "toContextId", "toIndex" }, "fromContextId", "toContextId"),
                Operation("disconnect-flow", new[] { "fromContextId", "fromIndex", "toContextId", "toIndex" }, "fromContextId", "toContextId"),
                Operation("move-block", new[] { "nodeId", "parentContextId", "index" }, "nodeId", "parentContextId", "index"),
                Operation("add-parameter", new[] { "catalogId", "name", "value", "exposed", "isOutput", "category", "order", "tooltip", "valueFilter", "min", "max", "enumValues", "position", "collapsed", "superCollapsed", "alias" }, "catalogId", "name"),
                Operation("set-parameter", new[] { "parameterId", "name", "value", "exposed", "isOutput", "category", "order", "tooltip", "valueFilter", "min", "max", "enumValues", "position", "collapsed", "superCollapsed" }, "parameterId"),
                Operation("add-parameter-node", new[] { "parameterId", "position", "expanded", "superCollapsed", "alias" }, "parameterId", "position"),
                Operation("remove-parameter-node", new[] { "parameterNodeId" }, "parameterNodeId"),
                Operation("add-category", new[] { "name", "collapsed", "index" }, "name"),
                Operation("set-category", new[] { "categoryName", "categoryIndex", "name", "collapsed" }),
                Operation("remove-category", new[] { "categoryName", "categoryIndex", "parameterDisposition" }, "parameterDisposition"),
                Operation("move-category", new[] { "categoryName", "categoryIndex", "index" }, "index"),
                Operation("add-custom-attribute", new[] { "name", "valueType", "description", "expanded", "index" }, "name", "valueType"),
                Operation("set-custom-attribute", new[] { "attributeName", "name", "valueType", "description", "expanded", "index", "removeUsages" }, "attributeName"),
                Operation("remove-custom-attribute", new[] { "attributeName", "removeUsages" }, "attributeName"),
                Operation("move-custom-attribute", new[] { "attributeName", "index" }, "attributeName", "index"),
                Operation("add-group", new[] { "title", "position", "contents", "index" }, "title", "position"),
                Operation("set-group", new[] { "groupIndex", "title", "position", "contents" }, "groupIndex"),
                Operation("remove-group", new[] { "groupIndex" }, "groupIndex"),
                Operation("add-sticky-note", new[] { "title", "position", "contents", "theme", "textSize", "colorTheme", "index" }, "title", "position"),
                Operation("set-sticky-note", new[] { "stickyNoteIndex", "title", "position", "contents", "theme", "textSize", "colorTheme" }, "stickyNoteIndex"),
                Operation("remove-sticky-note", new[] { "stickyNoteIndex" }, "stickyNoteIndex"),
                Operation("set-ui-bounds", new[] { "bounds" }, "bounds"),
                Operation("set-graph-setting", new[] { "name", "value" }, "name", "value"),
                Operation("set-asset-setting", new[] { "name", "value" }, "name", "value"),
            };
            return MCPToolSchemaFactory.ArrayProp("operations",
                new Dictionary<string, object> { { "oneOf", variants } },
                "Atomic ordered semantic VFX graph operations.");
        }

        private static KeyValuePair<string, object> VFXComponentOperationArrayProp()
        {
            Dictionary<string, object> Operation(string op,
                Dictionary<string, object> properties, params string[] required)
            {
                properties["op"] = MCPToolSchemaFactory.EnumProp("op",
                    "VFX component operation discriminator.", op).Value;
                return MCPToolSchemaFactory.ObjectSchema(properties,
                    new[] { "op" }.Concat(required).ToArray());
            }
            var variants = new List<object>
            {
                Operation("set-asset", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("assetPath", "string", "VisualEffectAsset path."),
                    MCPToolSchemaFactory.Prop("clear", "boolean", "Clear the assigned asset."))),
                Operation("set-enabled", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("value", "boolean", "Enabled state.")), "value"),
                Operation("set-seed", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("startSeed", "number", "Start seed."),
                    MCPToolSchemaFactory.Prop("resetSeedOnPlay", "boolean", "Reset seed when playing."))),
                Operation("set-initial-event", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("name", "string", "Initial event name.")), "name"),
                Operation("set-rendering", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("propertyName", "string", "Documented persistent rendering property."),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Typed property value.")), "propertyName", "value"),
                Operation("set-override", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("propertyName", "string", "Exact exposed property name."),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Typed exposed value.")), "propertyName", "value"),
                Operation("reset-override", MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("propertyName", "string", "Exact exposed property name.")), "propertyName"),
            };
            return MCPToolSchemaFactory.ArrayProp("operations",
                new Dictionary<string, object> { { "oneOf", variants } },
                "Ordered persistent VisualEffect component operations.");
        }

        private static KeyValuePair<string, object> VFXEventAttributeArrayProp()
        {
            Dictionary<string, object> item = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("name", "string", "Event attribute name."),
                    MCPToolSchemaFactory.EnumProp("type", "Event attribute value type.", "bool", "int", "uint", "float", "vector2", "vector3", "vector4", "matrix4x4"),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Typed event attribute value.")),
                "name", "type", "value");
            return MCPToolSchemaFactory.ArrayProp("eventAttributes", item,
                "Typed attributes attached to send-event.");
        }

        private static KeyValuePair<string, object> VFXSettingsOperationArrayProp()
        {
            Dictionary<string, object> item = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.EnumProp("scope", "Settings owner.", "project", "user"),
                    MCPToolSchemaFactory.Prop("name", "string", "Documented VFX setting name."),
                    MCPToolSchemaFactory.AnyJsonValueProp("value", "Typed setting value."),
                    MCPToolSchemaFactory.EnumProp("reimport", "Explicit graph recompilation policy.", "none", "all")),
                "scope", "name", "value");
            return MCPToolSchemaFactory.ArrayProp("operations", item,
                "Ordered project and per-user VFX settings changes.");
        }

        private static Dictionary<string, object> UxmlOperationItemSchema()
        {
            Dictionary<string, object> TargetProperties()
            {
                return MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("path", "string", "Element tree path."),
                    MCPToolSchemaFactory.Prop("name", "string", "Exact UXML name attribute."));
            }

            var variants = new List<object>();
            variants.Add(DiscriminatedOperation("add-element",
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("parentPath", "string", "Parent element tree path."),
                    MCPToolSchemaFactory.Prop("parentName", "string", "Exact parent UXML name attribute."),
                    MCPToolSchemaFactory.Prop("elementType", "string", "UXML element type. Defaults to VisualElement."),
                    MCPToolSchemaFactory.StringMapProp("attributes", "Initial UXML attributes."),
                    MCPToolSchemaFactory.Prop("index", "number", "Optional child insertion index."))));
            foreach (string type in new[] { "remove-element", "remove-attribute", "add-class", "remove-class", "set-text" })
            {
                Dictionary<string, object> properties = TargetProperties();
                if (type == "remove-attribute")
                    properties["attribute"] = MCPToolSchemaFactory.Prop("attribute", "string", "Attribute name to remove.").Value;
                else if (type == "add-class" || type == "remove-class")
                    properties["className"] = MCPToolSchemaFactory.Prop("className", "string", "USS class name.").Value;
                else if (type == "set-text")
                    properties["text"] = MCPToolSchemaFactory.Prop("text", "string", "Replacement text attribute.").Value;
                string required = type == "remove-attribute" ? "attribute" :
                    type == "add-class" || type == "remove-class" ? "className" : null;
                variants.Add(DiscriminatedOperation(type, properties,
                    required == null ? new string[0] : new[] { required }));
            }
            Dictionary<string, object> move = TargetProperties();
            move["parentPath"] = MCPToolSchemaFactory.Prop("parentPath", "string", "New parent tree path.").Value;
            move["parentName"] = MCPToolSchemaFactory.Prop("parentName", "string", "Exact new parent UXML name attribute.").Value;
            move["index"] = MCPToolSchemaFactory.Prop("index", "number", "Optional child insertion index.").Value;
            variants.Add(DiscriminatedOperation("move-element", move));
            Dictionary<string, object> setAttribute = TargetProperties();
            setAttribute["attribute"] = MCPToolSchemaFactory.Prop("attribute", "string", "Attribute name to set.").Value;
            setAttribute["value"] = MCPToolSchemaFactory.Prop("value", "string", "Attribute value.").Value;
            variants.Add(DiscriminatedOperation("set-attribute", setAttribute, "attribute"));
            return new Dictionary<string, object> { { "oneOf", variants } };
        }

        private static Dictionary<string, object> UssOperationItemSchema()
        {
            Dictionary<string, object> Common()
            {
                return MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("selector", "string", "Exact USS selector."));
            }

            var variants = new List<object>();
            variants.Add(DiscriminatedOperation("remove-selector", Common(), "selector"));
            Dictionary<string, object> upsert = Common();
            upsert["declarations"] = MCPToolSchemaFactory.StringMapProp(
                "declarations", "Complete selector declarations.").Value;
            variants.Add(DiscriminatedOperation("upsert-selector", upsert,
                "selector", "declarations"));
            foreach (string type in new[] { "set-declaration", "remove-declaration" })
            {
                Dictionary<string, object> properties = Common();
                properties["property"] = MCPToolSchemaFactory.Prop("property", "string", "USS property name.").Value;
                if (type == "set-declaration")
                    properties["value"] = MCPToolSchemaFactory.Prop("value", "string", "USS property value.").Value;
                variants.Add(DiscriminatedOperation(type, properties, "selector", "property"));
            }
            return new Dictionary<string, object> { { "oneOf", variants } };
        }

        private static KeyValuePair<string, object> UxmlOperationArrayProp(
            string name, string description)
        {
            return MCPToolSchemaFactory.ArrayProp(name, UxmlOperationItemSchema(), description);
        }

        private static KeyValuePair<string, object> UssOperationArrayProp(
            string name, string description)
        {
            return MCPToolSchemaFactory.ArrayProp(name, UssOperationItemSchema(), description);
        }

        private static KeyValuePair<string, object> UIAuthoringEditArrayProp(
            string name, string description)
        {
            Dictionary<string, object> Edit(string kind,
                Dictionary<string, object> operationItem)
            {
                return MCPToolSchemaFactory.ObjectSchema(
                    MCPToolSchemaFactory.Props(
                        MCPToolSchemaFactory.EnumProp("kind", "Authoring edit kind.", kind),
                        MCPToolSchemaFactory.Prop("assetPath", "string", "UXML or USS asset path."),
                        MCPToolSchemaFactory.ArrayProp("operations", operationItem,
                            "Ordered edit operations.")),
                    "kind", "assetPath", "operations");
            }
            var item = new Dictionary<string, object>
            {
                { "oneOf", new List<object>
                    {
                        Edit("uxml", UxmlOperationItemSchema()),
                        Edit("uss", UssOperationItemSchema()),
                    }
                },
            };
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static KeyValuePair<string, object> AnimatorConditionArrayProp(
            string name, string description)
        {
            Dictionary<string, object> item = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("parameter", "string", "Animator parameter name."),
                    MCPToolSchemaFactory.Prop("mode", "string", "AnimatorConditionMode value."),
                    MCPToolSchemaFactory.Prop("threshold", "number", "Condition threshold.")));
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static Dictionary<string, object> UIToolkitLocatorProperties(
            string prefix = "")
        {
            string Prefix(string suffix) => string.IsNullOrEmpty(prefix)
                ? char.ToLowerInvariant(suffix[0]) + suffix.Substring(1)
                : prefix + suffix;
            return MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop(Prefix("Path"), "string", "Element tree path."),
                MCPToolSchemaFactory.Prop(Prefix("ElementPath"), "string", "Legacy element tree path alias."),
                MCPToolSchemaFactory.Prop(Prefix("VisualElementPath"), "string", "Slash-separated VisualElementPath names."),
                MCPToolSchemaFactory.ArrayProp(Prefix("VisualElementNames"), "string", "VisualElementPath names."),
                MCPToolSchemaFactory.Prop(Prefix("Name"), "string", "VisualElement.name exact match."),
                MCPToolSchemaFactory.Prop(Prefix("ClassName"), "string", "USS class exact match."),
                MCPToolSchemaFactory.Prop(Prefix("TypeName"), "string", "VisualElement type-name match."),
                MCPToolSchemaFactory.Prop(Prefix("Text"), "string", "TextElement text match."));
        }

        private static KeyValuePair<string, object> UIToolkitQueryArrayProp(
            string name, string description)
        {
            Dictionary<string, object> properties = UIToolkitLocatorProperties();
            properties["pixelScale"] = MCPToolSchemaFactory.Prop("pixelScale", "number",
                "Pixel grid scale for diagnostics.").Value;
            return MCPToolSchemaFactory.ArrayProp(name,
                MCPToolSchemaFactory.ObjectSchema(properties), description);
        }

        private static KeyValuePair<string, object> UIToolkitVisualCheckArrayProp(
            string name, string description)
        {
            Dictionary<string, object> properties = UIToolkitLocatorProperties();
            foreach (KeyValuePair<string, object> property in MCPToolSchemaFactory.Props(
                         MCPToolSchemaFactory.Prop("type", "string", "Visual check type."),
                         MCPToolSchemaFactory.Prop("kind", "string", "Legacy visual check type alias."),
                         MCPToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale."),
                         MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed numeric delta."),
                         MCPToolSchemaFactory.Prop("expectedScale", "number", "Expected background scale."),
                         MCPToolSchemaFactory.Prop("scale", "number", "Legacy expected background scale alias."),
                         MCPToolSchemaFactory.Prop("width", "number", "Expected element width."),
                         MCPToolSchemaFactory.Prop("height", "number", "Expected element height."),
                         MCPToolSchemaFactory.Prop("expectedWidth", "number", "Expected element width alias."),
                         MCPToolSchemaFactory.Prop("expectedHeight", "number", "Expected element height alias.")))
                properties[property.Key] = property.Value;
            return MCPToolSchemaFactory.ArrayProp(name,
                MCPToolSchemaFactory.ObjectSchema(properties), description);
        }

        private static KeyValuePair<string, object> UIToolkitResourceQueryArrayProp(
            string name, string description)
        {
            Dictionary<string, object> properties = UIToolkitLocatorProperties();
            foreach (KeyValuePair<string, object> property in MCPToolSchemaFactory.Props(
                         MCPToolSchemaFactory.Prop("expectedBackgroundContains", "string", "Required background reference substring."),
                         MCPToolSchemaFactory.ArrayProp("forbiddenBackgroundContains", "string", "Forbidden background reference substrings."),
                         MCPToolSchemaFactory.Prop("requireBackground", "boolean", "Require a resolved background image.")))
                properties[property.Key] = property.Value;
            return MCPToolSchemaFactory.ArrayProp(name,
                MCPToolSchemaFactory.ObjectSchema(properties), description);
        }

        private static KeyValuePair<string, object> UIToolkitLayoutAssertionArrayProp(
            string name, string description)
        {
            Dictionary<string, object> properties = UIToolkitAssertionLocatorProperties();
            foreach (string prefix in new[] { "first", "second", "inner", "outer" })
            foreach (KeyValuePair<string, object> property in
                     UIToolkitAssertionLocatorProperties(prefix))
                properties[property.Key] = property.Value;
            foreach (KeyValuePair<string, object> property in MCPToolSchemaFactory.Props(
                         MCPToolSchemaFactory.Prop("type", "string", "Layout assertion type."),
                         MCPToolSchemaFactory.Prop("kind", "string", "Legacy layout assertion type alias."),
                         MCPToolSchemaFactory.Prop("axis", "string", "Comparison axis: x or y."),
                         MCPToolSchemaFactory.Prop("edge", "string", "Shared edge for alignment."),
                         MCPToolSchemaFactory.Prop("firstEdge", "string", "First element edge."),
                         MCPToolSchemaFactory.Prop("secondEdge", "string", "Second element edge."),
                         MCPToolSchemaFactory.Prop("tolerance", "number", "Allowed layout delta."),
                         MCPToolSchemaFactory.Prop("width", "number", "Expected width."),
                         MCPToolSchemaFactory.Prop("height", "number", "Expected height."),
                         MCPToolSchemaFactory.Prop("expectedWidth", "number", "Expected width alias."),
                         MCPToolSchemaFactory.Prop("expectedHeight", "number", "Expected height alias.")))
                properties[property.Key] = property.Value;
            return MCPToolSchemaFactory.ArrayProp(name,
                MCPToolSchemaFactory.ObjectSchema(properties), description);
        }

        private static Dictionary<string, object> UIToolkitAssertionLocatorProperties(
            string prefix = "")
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("path", "string", "Element tree path."),
                    MCPToolSchemaFactory.Prop("elementPath", "string", "Legacy element tree path alias."),
                    MCPToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                    MCPToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names."),
                    MCPToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."));
            }

            return MCPToolSchemaFactory.Props(
                MCPToolSchemaFactory.Prop(prefix + "Path", "string", "Prefixed element tree path."),
                MCPToolSchemaFactory.Prop(prefix + "VisualElementPath", "string", "Prefixed slash-separated VisualElementPath names."),
                MCPToolSchemaFactory.ArrayProp(prefix + "Names", "string", "Prefixed VisualElementPath names."),
                MCPToolSchemaFactory.Prop(prefix + "Name", "string", "Prefixed VisualElement.name exact match."));
        }

        private static KeyValuePair<string, object> AnnotationRectArrayProp(
            string name, string description)
        {
            Dictionary<string, object> item = MCPToolSchemaFactory.ObjectSchema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("x", "number", "Rectangle x coordinate."),
                    MCPToolSchemaFactory.Prop("y", "number", "Rectangle y coordinate."),
                    MCPToolSchemaFactory.Prop("width", "number", "Rectangle width."),
                    MCPToolSchemaFactory.Prop("height", "number", "Rectangle height."),
                    MCPToolSchemaFactory.Prop("color", "string", "Optional HTML border color."),
                    MCPToolSchemaFactory.Prop("thickness", "number", "Optional border thickness.")),
                "x", "y", "width", "height");
            return MCPToolSchemaFactory.ArrayProp(name, item, description);
        }

        private static KeyValuePair<string, object> GitPackageExpectationArrayProp()
        {
            Dictionary<string, object> item = MCPToolSchemaFactory.Schema(
                MCPToolSchemaFactory.Props(
                    MCPToolSchemaFactory.Prop("name", "string", "Git package name."),
                    MCPToolSchemaFactory.Prop("gitUrl", "string", "Optional Git URL. Defaults to the current manifest Git URL."),
                    MCPToolSchemaFactory.Prop("revision", "string", "Required full 40-character Git commit SHA.")),
                "name", "revision");
            return MCPToolSchemaFactory.ArrayProp(
                "expectedPackages", item,
                "Exact Git package targets that must match manifest, lockfile, and Unity's registered package state after resolution.");
        }
    }
}
