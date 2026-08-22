using System.Collections.Generic;
using System.Linq;
using static VMUnityAutomation.Editor.VmAutomationToolInputSchemaComponents;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationToolInputSchemaCatalog
    {
        internal static Dictionary<string, object> Get(string route)
        {
            Dictionary<string, object> specialized =
                VmAutomationSpecializedToolInputSchemaCatalog.GetOrNull(route);
            if (specialized != null)
                return specialized;

            switch (route)
            {
                case "asset/list":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("folder", "string", "Folder to search. Defaults to Assets."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Optional Unity asset type filter."),
                        VmAutomationToolSchemaFactory.Prop("search", "string", "Optional AssetDatabase search expression."),
                        VmAutomationToolSchemaFactory.Prop("recursive", "boolean", "Include descendants. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum assets. Defaults to 100; capped at 500.")));
                case "asset/import-settings/get":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone.")
                    ), "assetPath");
                case "asset/import-settings/set":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture, model, or audio asset path below Assets/."),
                        AssetImportSettingsProp("settings", "Semantic importer fields. Unsupported keys are rejected with the allowed field list."),
                        VmAutomationToolSchemaFactory.Prop("platform", "string", "Optional Unity platform override name such as Standalone, Android, or iPhone."),
                        AssetPlatformSettingsProp("platformSettings", "Optional semantic TextureImporter or AudioImporter override settings for platform."),
                        VmAutomationToolSchemaFactory.Prop("reimport", "boolean", "Save and reimport the asset after updating settings. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return before/requested settings without modifying the importer.")
                    ), "assetPath", "settings");
                case "scene/workspace":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("action", "string", "Workspace action: list, open, close, or set-active. Defaults to list."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Scene asset path for open, close, or set-active."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Loaded scene name for close or set-active when path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("mode", "string", "Open mode: additive (default) or single."),
                        VmAutomationToolSchemaFactory.Prop("saveModified", "boolean", "For single open, save every dirty loaded scene before replacement."),
                        VmAutomationToolSchemaFactory.Prop("discardModified", "boolean", "For single open, explicitly allow replacement of dirty loaded scenes without saving."),
                        VmAutomationToolSchemaFactory.Prop("save", "boolean", "For close, save a dirty scene before closing."),
                        VmAutomationToolSchemaFactory.Prop("discardChanges", "boolean", "For close, explicitly discard dirty scene changes."),
                        VmAutomationToolSchemaFactory.Prop("removeScene", "boolean", "For close, remove the scene from the workspace. Defaults to true.")
                    ));
                case "material/properties/get":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Material asset path below Assets/."),
                        VmAutomationToolSchemaFactory.ArrayProp("propertyNames", "string", "Optional shader property names. Omit to page through declared shader properties."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Shader property offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum shader properties returned. Defaults to 100; capped at 500.")
                    ), "assetPath");
                case "material/properties/set":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Material asset path below Assets/."),
                        MaterialPropertyMapProp("properties", "Shader property values keyed by declared shader property name. Texture values accept assetPath plus optional scale and offset."),
                        MaterialKeywordsProp("keywords", "Keyword changes with enable and disable string arrays."),
                        VmAutomationToolSchemaFactory.Prop("shader", "string", "Optional replacement shader name."),
                        VmAutomationToolSchemaFactory.Prop("renderQueue", "number", "Optional Material render queue."),
                        VmAutomationToolSchemaFactory.Prop("enableInstancing", "boolean", "Optional GPU instancing flag."),
                        VmAutomationToolSchemaFactory.Prop("doubleSidedGI", "boolean", "Optional double-sided global illumination flag."),
                        VmAutomationToolSchemaFactory.Prop("globalIlluminationFlags", "string", "Optional MaterialGlobalIlluminationFlags value."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return requested changes without modifying the Material.")
                    ), "assetPath");
                case "shadergraph/info":
                case "shadergraph/get-nodes":
                case "shadergraph/get-edges":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Shader Graph asset path below Assets/.")
                    ), "path");
                case "shadergraph/get-properties":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Optional Shader or Shader Graph asset path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("shaderName", "string", "Optional loaded shader name when path is omitted.")
                    ));
                case "shadergraph/set-node-property":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Shader Graph asset path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("objectId", "string", "Serialized graph object ID returned by shadergraph/get-properties or shadergraph/get-nodes."),
                        VmAutomationToolSchemaFactory.Prop("nodeId", "string", "Legacy alias for objectId."),
                        VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Existing top-level scalar field on the target graph object."),
                        VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Replacement scalar value. Its JSON type must match the existing field.")
                    ), "path", "propertyName", "value");
                case "physics/raycast":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to Project Settings > VM Unity Automation > Tool Defaults (3D initially)."),
                        VmAutomationToolSchemaFactory.Vector3Prop("origin", "Ray origin with x/y/z. z is ignored for 2D."),
                        VmAutomationToolSchemaFactory.Vector3Prop("direction", "Ray direction with x/y/z. z is ignored for 2D."),
                        VmAutomationToolSchemaFactory.Prop("maxDistance", "number", "Maximum ray distance. Defaults to infinity."),
                        VmAutomationToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        VmAutomationToolSchemaFactory.Prop("all", "boolean", "Return multiple hits rather than only the closest hit."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum hits returned when all is true. Defaults to 100; capped at 500.")
                    ), "origin", "direction");
                case "physics/overlap-sphere":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the VM Unity Automation project setting (3D initially). In 2D this performs an overlap circle."),
                        VmAutomationToolSchemaFactory.Vector3Prop("center", "Query center with x/y/z. z is ignored for 2D."),
                        VmAutomationToolSchemaFactory.Prop("radius", "number", "Sphere or circle radius. Defaults to 1."),
                        VmAutomationToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center");
                case "physics/overlap-box":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("dimension", "string", "Physics dimension: 2D or 3D. Defaults to the VM Unity Automation project setting (3D initially)."),
                        VmAutomationToolSchemaFactory.Vector3Prop("center", "Query center with x/y/z. z is ignored for 2D."),
                        VmAutomationToolSchemaFactory.Vector3Prop("halfExtents", "Half extents with x/y/z. In 2D, x/y are doubled into box size."),
                        VmAutomationToolSchemaFactory.Prop("angle", "number", "2D box rotation in degrees. Ignored for 3D."),
                        VmAutomationToolSchemaFactory.Prop("layerMask", "number", "Optional Physics or Physics2D layer mask."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum colliders returned. Defaults to 100; capped at 500.")
                    ), "center", "halfExtents");
                case "search/scene":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Optional case-insensitive GameObject name substring or regular expression."),
                        VmAutomationToolSchemaFactory.Prop("regex", "boolean", "Interpret name as a regular expression with a bounded match timeout. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Optional component short, full, or assembly-qualified type name that must exist on the GameObject."),
                        VmAutomationToolSchemaFactory.Prop("tag", "string", "Optional exact Unity Tag."),
                        VmAutomationToolSchemaFactory.Prop("layer", "string", "Optional Unity Layer name or numeric index."),
                        VmAutomationToolSchemaFactory.Prop("shader", "string", "Optional case-insensitive shader-name substring used by a Renderer on the GameObject."),
                        VmAutomationToolSchemaFactory.Prop("includeInactive", "boolean", "Include inactive GameObjects. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Stable result offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 200; capped at 500.")));
                case "queue/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props());
                case "queue/status":
                case "queue/cancel":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("ticketId", "number", "Owned queue ticket identifier.")), "ticketId");
                case "asset/create-folder":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Folder path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and report without creating folders.")), "path");
                case "asset/copy":
                {
                    var copyProperties = VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "Source asset path."),
                        VmAutomationToolSchemaFactory.Prop("targetPath", "string", "Target asset path."));
                    var properties = VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "Source asset path for a single copy."),
                        VmAutomationToolSchemaFactory.Prop("targetPath", "string", "Target asset path for a single copy."),
                        VmAutomationToolSchemaFactory.Prop("overwrite", "boolean", "Replace existing targets with rollback snapshots. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Preflight without copying. Defaults to false."));
                    properties["copies"] = new Dictionary<string, object>
                    {
                        { "type", "array" },
                        { "description", "Batch of sourcePath/targetPath copy requests." },
                        { "minItems", 1 },
                        { "items", VmAutomationToolSchemaFactory.Schema(copyProperties, "sourcePath", "targetPath") },
                    };
                    var schema = VmAutomationToolSchemaFactory.Schema(properties);
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
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Asset whose references should be inspected."),
                        VmAutomationToolSchemaFactory.Prop("direction", "string", "outgoing, incoming, or both. Defaults to both."),
                        VmAutomationToolSchemaFactory.Prop("recursive", "boolean", "Use recursive dependency resolution. Defaults to true."),
                        VmAutomationToolSchemaFactory.ArrayProp("searchRoots", "string", "Folders scanned for incoming references. Defaults to Assets."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 100; capped at 500.")), "path");
                case "asset/transaction":
                    return VmAutomationToolSchemaFactory.AssetTransactionSchema();
                case "uitoolkit/edit-uxml":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "UXML asset path below Assets/."),
                        UxmlOperationArrayProp("operations", "Ordered structural UXML edit operations."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/edit-uss":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "USS asset path below Assets/."),
                        UssOperationArrayProp("operations", "Ordered selector/declaration edit operations."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Return the edit result without writing.")), "assetPath", "operations");
                case "uitoolkit/authoring-transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        UIAuthoringEditArrayProp("edits", "Ordered edit objects with kind, assetPath, and operations."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate all edits without writing.")), "edits");
                case "packages/add":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("identifier", "string", "Registry package name, Git URL, local path, or tarball identifier.")),
                        "identifier");
                case "packages/list":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum packages. Defaults to 100; capped at 200.")));
                case "packages/remove":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Installed package name to remove.")), "name");
                case "packages/search":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("query", "string", "Registry search query."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum returned packages. Defaults to 50; capped at 200.")),
                        "query");
                case "localization/status":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props());
                case "localization/locales":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("includePseudo", "boolean", "Include PseudoLocale assets. Defaults to true.")
                    ));
                case "localization/create-locale":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("code", "string", "Locale code, for example en-US or zh-CN."),
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Locale asset path under Assets ending in .asset."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Optional Locale display name."),
                        VmAutomationToolSchemaFactory.Prop("addToProject", "boolean", "Register the Locale with Localization Settings. Defaults to true.")
                    ), "code", "assetPath");
                case "localization/set-selected-locale":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("locale", "string", "Registered Locale code to select.")
                    ), "locale");
                case "localization/collections":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Optional collection type filter: string or asset."),
                        VmAutomationToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive collection name filter.")
                    ));
                case "localization/create-collection":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Table Collection name."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Collection type: string or asset."),
                        VmAutomationToolSchemaFactory.Prop("assetDirectory", "string", "Existing or new directory under Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("locales", "string", "Optional Locale codes. Defaults to every registered Locale."),
                        VmAutomationToolSchemaFactory.Prop("group", "string", "Optional Localization window group."),
                        VmAutomationToolSchemaFactory.Prop("preload", "boolean", "Optional preload flag for all created tables.")
                    ), "name", "type", "assetDirectory");
                case "localization/entries":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("collection", "string", "Table Collection name or GUID."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        VmAutomationToolSchemaFactory.Prop("locale", "string", "Optional Locale code filter."),
                        VmAutomationToolSchemaFactory.Prop("keyContains", "string", "Optional case-insensitive key filter."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Filtered key offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum keys returned. Defaults to 100; capped at 500.")
                    ), "collection");
                case "localization/upsert-entry":
                    return VmAutomationToolSchemaFactory.LocalizationUpsertEntriesSchema();
                case "localization/remove-entry":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("collection", "string", "Table Collection name or GUID."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                        VmAutomationToolSchemaFactory.Prop("key", "string", "Localization key to remove."),
                        VmAutomationToolSchemaFactory.Prop("locale", "string", "Optional Locale code. Omit to remove the shared key from every table.")
                    ), "collection", "key");
                case "localization/validate":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("collection", "string", "Optional Table Collection name or GUID."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Optional collection type filter: string or asset."),
                        VmAutomationToolSchemaFactory.Prop("includeEmpty", "boolean", "Report empty values as well as missing entries. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("maxIssues", "number", "Maximum issues returned. Defaults to 200; capped at 2000.")
                    ));
                case "localization/settings":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("initializeSynchronously", "boolean", "Optional Localization initialization mode."),
                        VmAutomationToolSchemaFactory.Prop("projectLocale", "string", "Optional registered project Locale code."),
                        VmAutomationToolSchemaFactory.Prop("selectedLocale", "string", "Optional registered selected Locale code.")
                    ));
                case "localization/variables":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("group", "string", "Optional case-insensitive persistent variable group filter."),
                        VmAutomationToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive variable name filter.")
                    ));
                case "localization/upsert-variable":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("group", "string", "Persistent variable group name."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Variable name inside the group."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Variable type: bool, int, long, float, double, string, or object."),
                        VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Variable value. Object variables accept an Assets path."),
                        VmAutomationToolSchemaFactory.Prop("groupAssetPath", "string", "Required asset path when creating a missing VariablesGroupAsset.")
                    ), "group", "name", "type", "value");
                case "localization/remove-variable":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("group", "string", "Persistent variable group name."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Variable name to remove.")
                    ), "group", "name");
                case "packages/update-git":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Package name, e.g. com.example.package"),
                        VmAutomationToolSchemaFactory.Prop("gitUrl", "string", "Optional Git URL. Defaults to the current manifest Git URL."),
                        VmAutomationToolSchemaFactory.Prop("revision", "string", "Required full 40-character Git commit SHA."),
                        VmAutomationToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity. Reusing it with identical arguments returns the same durable job; different arguments are rejected.")
                    ), "name", "revision");
                case "packages/resolve":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        GitPackageExpectationArrayProp(),
                        VmAutomationToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity. Reusing it with identical arguments returns the same durable job; different arguments are rejected.")
                    ), "expectedPackages");
                case "packages/status":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Optional package name. If omitted, returns all Git dependencies from the manifest."),
                        VmAutomationToolSchemaFactory.Prop("includeResolved", "boolean", "Include Package Manager resolved package data when available. Defaults to false.")
                    ));
                case "packages/lint-metas":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Installed package name to lint."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Absolute or project-relative package path to lint."),
                        VmAutomationToolSchemaFactory.Prop("all", "boolean", "Lint all resolved package roots."),
                        VmAutomationToolSchemaFactory.Prop("checkDirectories", "boolean", "Also require directory .meta files. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum missing entries returned per package.")
                    ));
                case "wait/editor-idle":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 30000."),
                        VmAutomationToolSchemaFactory.Prop("stableFrames", "number", "Number of consecutive idle editor frames required. Defaults to 3."),
                        VmAutomationToolSchemaFactory.Prop("stableMs", "number", "Minimum continuous idle time in milliseconds. Defaults to 500.")
                    ));
                case "jobs/list":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobType", "string", "Optional job type filter."),
                        VmAutomationToolSchemaFactory.Prop("status", "string", "Optional status filter."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum jobs. Defaults to 50; capped at 200.")));
                case "jobs/get":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Job identifier. Supply this or requestId."),
                        VmAutomationToolSchemaFactory.Prop("requestId", "string", "Original transport request identity for recovering the same workspace job when its start response was lost."),
                        VmAutomationToolSchemaFactory.Prop("jobType", "string", "Optional job type disambiguator."),
                        VmAutomationToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating Automation agent disconnects.")));
                case "jobs/cancel":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Persistent job identifier returned by its start route."),
                        VmAutomationToolSchemaFactory.Prop("jobType", "string", "Optional job type disambiguator."),
                        VmAutomationToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when a persistent job starts. Required after the originating Automation agent disconnects.")
                    ), "jobId");
                case "jobs/cleanup":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Terminal persistent job identifier whose explicit cleanup contract should run."),
                        VmAutomationToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when the persistent job started.")
                    ), "jobId");
                case "vfxgraph/catalog":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.EnumProp("kind", "Optional VFX catalog kind.", "asset-kind", "template", "context", "block", "operator", "parameter", "property-binder", "event-binder", "output-event-handler", "spawner-callback"),
                        VmAutomationToolSchemaFactory.Prop("query", "string", "Case-insensitive search across names, categories, types, synonyms and variant settings."),
                        VmAutomationToolSchemaFactory.Prop("category", "string", "Exact category filter."),
                        VmAutomationToolSchemaFactory.Prop("includeExperimental", "boolean", "Include experimental contexts, blocks and operators."),
                        VmAutomationToolSchemaFactory.Prop("contextCatalogId", "string", "When listing blocks, restrict results to blocks accepted by this exact context catalog item."),
                        VmAutomationToolSchemaFactory.Prop("catalogId", "string", "Optional exact catalog item ID. Use includeDetails to inspect its settings and slots."),
                        VmAutomationToolSchemaFactory.Prop("includeDetails", "boolean", "Instantiate and include settings and recursive slot definitions only for the returned page. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Zero-based result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum results. Defaults to 100; capped at 500, or 100 when includeDetails is true."),
                        VmAutomationToolSchemaFactory.Prop("settingOffset", "number", "Per-item setting offset when includeDetails is true."),
                        VmAutomationToolSchemaFactory.Prop("maxSettingsPerItem", "number", "Per-item setting page size. Defaults to 64; capped at 128."),
                        VmAutomationToolSchemaFactory.Prop("inputSlotOffset", "number", "Per-item flattened recursive input-slot offset."),
                        VmAutomationToolSchemaFactory.Prop("outputSlotOffset", "number", "Per-item flattened recursive output-slot offset."),
                        VmAutomationToolSchemaFactory.Prop("maxSlotsPerItem", "number", "Per-direction flattened recursive slot page size. Defaults to 64; capped at 256.")));
                case "vfxgraph/create":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "New VFX asset path below Assets/. Extension must match assetKind."),
                        VmAutomationToolSchemaFactory.EnumProp("assetKind", "VFX asset kind.", "graph", "block-subgraph", "operator-subgraph"),
                        VmAutomationToolSchemaFactory.Prop("templateId", "string", "Optional exact template catalog ID for graph assets."),
                        VmAutomationToolSchemaFactory.Prop("overwrite", "boolean", "Replace existing asset contents while preserving its meta GUID.")), "assetPath", "assetKind");
                case "vfxgraph/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "VisualEffectAsset or VFX subgraph path below Assets/ or Packages/."),
                        VmAutomationToolSchemaFactory.Prop("nodeOffset", "number", "Semantic node offset."),
                        VmAutomationToolSchemaFactory.Prop("maxObjects", "number", "Maximum semantic nodes returned. Defaults to 250; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("parameterOffset", "number", "Parameter definition offset."),
                        VmAutomationToolSchemaFactory.Prop("maxParameters", "number", "Maximum parameter definitions returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("connectionOffset", "number", "Data and flow connection offset."),
                        VmAutomationToolSchemaFactory.Prop("maxConnections", "number", "Maximum data and flow connections returned. Defaults to 500; capped at 5000."),
                        VmAutomationToolSchemaFactory.Prop("uiOffset", "number", "Group and sticky-note offset."),
                        VmAutomationToolSchemaFactory.Prop("maxUIItems", "number", "Maximum groups and sticky notes returned."),
                        VmAutomationToolSchemaFactory.Prop("dataOffset", "number", "VFX data-object offset."),
                        VmAutomationToolSchemaFactory.Prop("maxDataObjects", "number", "Maximum VFX data objects returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("categoryOffset", "number", "Blackboard category offset."),
                        VmAutomationToolSchemaFactory.Prop("maxCategories", "number", "Maximum blackboard categories returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("customAttributeOffset", "number", "Custom attribute offset."),
                        VmAutomationToolSchemaFactory.Prop("maxCustomAttributes", "number", "Maximum custom attributes returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("settingOffset", "number", "Per-model setting offset."),
                        VmAutomationToolSchemaFactory.Prop("maxSettingsPerNode", "number", "Per-model setting page size. Defaults to 64; capped at 128."),
                        VmAutomationToolSchemaFactory.Prop("occurrenceOffset", "number", "Per-parameter visual occurrence offset."),
                        VmAutomationToolSchemaFactory.Prop("maxOccurrencesPerParameter", "number", "Per-parameter occurrence page size. Defaults to 100; capped at 256."),
                        VmAutomationToolSchemaFactory.Prop("inputSlotOffset", "number", "Per-model flattened recursive input-slot offset."),
                        VmAutomationToolSchemaFactory.Prop("outputSlotOffset", "number", "Per-model flattened recursive output-slot offset."),
                        VmAutomationToolSchemaFactory.Prop("eventOffset", "number", "Declared event-name offset."),
                        VmAutomationToolSchemaFactory.Prop("maxEvents", "number", "Maximum declared event names returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("dependencyOffset", "number", "Source dependency offset."),
                        VmAutomationToolSchemaFactory.Prop("maxDependencies", "number", "Maximum source dependencies returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("diagnosticOffset", "number", "Diagnostic offset."),
                        VmAutomationToolSchemaFactory.Prop("maxDiagnostics", "number", "Maximum current model diagnostics returned."),
                        VmAutomationToolSchemaFactory.Prop("maxSlotsPerNode", "number", "Maximum recursive slots returned per direction and node when includeSlots is true. Defaults to 50; capped at 256."),
                        VmAutomationToolSchemaFactory.Prop("maxProperties", "number", "Maximum visible serialized properties per graph object when includeSerialized is true. Defaults to 40; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("includeSlots", "boolean", "Include recursive typed input/output slot values. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("includeDiagnostics", "boolean", "Generate and include current model/compile diagnostics."),
                        VmAutomationToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized graph diagnostic. Defaults to false.")
                    ), "assetPath");
                case "vfxgraph/transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "VFX Graph or subgraph asset path below Assets/."),
                        VFXGraphOperationArrayProp(),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Apply all semantic validation against a transient graph, restore the graph, and do not publish the asset.")), "assetPath", "operations");
                case "vfxgraph/validate":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "VFX Graph or subgraph asset path."),
                        VmAutomationToolSchemaFactory.EnumProp("mode", "Validation mode. inspect is read-only; reimport and compile mutate importer/compiled state.", "inspect", "reimport", "compile"),
                        VmAutomationToolSchemaFactory.Prop("diagnosticOffset", "number", "Diagnostic offset."),
                        VmAutomationToolSchemaFactory.Prop("maxDiagnostics", "number", "Maximum diagnostics returned."),
                        VmAutomationToolSchemaFactory.Prop("includeShaders", "boolean", "Include generated shader manifests."),
                        VmAutomationToolSchemaFactory.Prop("includeShaderSource", "boolean", "Include bounded shader source text."),
                        VmAutomationToolSchemaFactory.Prop("shaderOffset", "number", "Generated shader offset."),
                        VmAutomationToolSchemaFactory.Prop("maxShaders", "number", "Maximum generated shaders returned. Defaults to 64; capped at 256."),
                        VmAutomationToolSchemaFactory.Prop("shaderSourceOffset", "number", "Per-shader source character offset."),
                        VmAutomationToolSchemaFactory.Prop("maxShaderSourceChars", "number", "Maximum source characters returned per shader. Defaults to and is capped at 4096."),
                        VmAutomationToolSchemaFactory.Prop("systemOffset", "number", "Compiled system-name offset."),
                        VmAutomationToolSchemaFactory.Prop("maxSystems", "number", "Maximum system names returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("eventOffset", "number", "Event-name offset."),
                        VmAutomationToolSchemaFactory.Prop("maxEvents", "number", "Maximum event names returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("exposedPropertyOffset", "number", "Exposed-property manifest offset."),
                        VmAutomationToolSchemaFactory.Prop("maxExposedProperties", "number", "Maximum exposed properties returned. Defaults to 100; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("dependencyOffset", "number", "Asset dependency offset."),
                        VmAutomationToolSchemaFactory.Prop("maxDependencies", "number", "Maximum asset dependencies returned. Defaults to 100; capped at 1000.")), "assetPath");
                case "vfxgraph/component-info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Optional prefab asset path; omit for loaded scenes."),
                        VmAutomationToolSchemaFactory.Prop("scenePath", "string", "Optional exact loaded scene path."),
                        VmAutomationToolSchemaFactory.Prop("hierarchyPath", "string", "Exact GameObject hierarchy path for one component."),
                        VmAutomationToolSchemaFactory.Prop("hierarchyIndexPath", "string", "Exact slash-separated sibling-index path returned by this route; use it when names are duplicated."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Zero-based VisualEffect component index."),
                        VmAutomationToolSchemaFactory.Prop("gameObjectInstanceId", "string", "Loaded GameObject instance ID."),
                        VmAutomationToolSchemaFactory.Prop("componentInstanceId", "string", "Loaded VisualEffect component instance ID."),
                        VmAutomationToolSchemaFactory.Prop("includeOverrides", "boolean", "Include exposed-property values and override state."),
                        VmAutomationToolSchemaFactory.Prop("overrideOffset", "number", "Per-component exposed-property override offset."),
                        VmAutomationToolSchemaFactory.Prop("maxOverridesPerComponent", "number", "Per-component override page size. Defaults to 100; capped at 256."),
                        VmAutomationToolSchemaFactory.Prop("includeRuntimeState", "boolean", "In Play Mode, include paged per-system particle/spawner state and output-event names for loaded scene components."),
                        VmAutomationToolSchemaFactory.Prop("systemOffset", "number", "Per-component runtime system offset."),
                        VmAutomationToolSchemaFactory.Prop("maxSystemsPerComponent", "number", "Per-component runtime system page size. Defaults to 100; capped at 256."),
                        VmAutomationToolSchemaFactory.Prop("outputEventOffset", "number", "Per-component runtime output-event offset."),
                        VmAutomationToolSchemaFactory.Prop("maxOutputEventsPerComponent", "number", "Per-component runtime output-event page size. Defaults to 100; capped at 256."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Component result offset."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum components returned.")));
                case "vfxgraph/component-transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Optional prefab asset path; omit for a loaded-scene component."),
                        VmAutomationToolSchemaFactory.Prop("scenePath", "string", "Exact loaded scene path."),
                        VmAutomationToolSchemaFactory.Prop("hierarchyPath", "string", "Exact GameObject hierarchy path."),
                        VmAutomationToolSchemaFactory.Prop("hierarchyIndexPath", "string", "Exact slash-separated sibling-index path returned by component-info."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Zero-based VisualEffect component index."),
                        VmAutomationToolSchemaFactory.Prop("gameObjectInstanceId", "string", "Loaded GameObject instance ID."),
                        VmAutomationToolSchemaFactory.Prop("componentInstanceId", "string", "Loaded VisualEffect component instance ID."),
                        VFXComponentOperationArrayProp(),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate the ordered component transaction and restore the original component.")), "operations");
                case "vfxgraph/component-control":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("scenePath", "string", "Exact loaded scene path."),
                        VmAutomationToolSchemaFactory.Prop("hierarchyPath", "string", "Exact GameObject hierarchy path."),
                        VmAutomationToolSchemaFactory.Prop("hierarchyIndexPath", "string", "Exact slash-separated sibling-index path returned by component-info."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Zero-based VisualEffect component index."),
                        VmAutomationToolSchemaFactory.Prop("gameObjectInstanceId", "string", "Loaded GameObject instance ID."),
                        VmAutomationToolSchemaFactory.Prop("componentInstanceId", "string", "Loaded VisualEffect component instance ID."),
                        VmAutomationToolSchemaFactory.EnumProp("action", "Play Mode action.", "play", "stop", "pause", "resume", "reinit", "advance-one-frame", "simulate", "send-event", "set-asset", "set-override", "reset-override"),
                        VmAutomationToolSchemaFactory.Prop("eventName", "string", "Event name for send-event."),
                        VFXEventAttributeArrayProp(),
                        VmAutomationToolSchemaFactory.Prop("deltaTime", "number", "Simulation step duration in (0, 10]. Defaults to 1/60 second."),
                        VmAutomationToolSchemaFactory.Prop("stepCount", "number", "Simulation step count in [1, 1024]. deltaTime multiplied by stepCount must not exceed 60 seconds."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait for the requested VisualEffect update, in [100, 10000]. Defaults to 3000."),
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "VisualEffectAsset path assigned to the loaded component for this Play Mode session."),
                        VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Exposed property for set/reset-override."),
                        VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Typed exposed-property value.")), "action");
                case "vfxgraph/settings-info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.EnumProp("scope", "Optional settings scope.", "project", "user")));
                case "vfxgraph/settings-transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VFXSettingsOperationArrayProp(),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate settings and restore both ProjectSettings and EditorPrefs.")), "operations");
                case "vfxgraph/bake":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.EnumProp("kind", "Bake pipeline.", "sdf", "point-cache-mesh", "point-cache-texture"),
                        VmAutomationToolSchemaFactory.Prop("meshPath", "string", "Source Mesh asset path for SDF or mesh point cache."),
                        VmAutomationToolSchemaFactory.Prop("texturePath", "string", "Source Texture2D asset path for texture point cache."),
                        VmAutomationToolSchemaFactory.Prop("outputPath", "string", "Output .asset (SDF) or .pcache path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("overwrite", "boolean", "Overwrite contents while preserving existing meta identity."),
                        VmAutomationToolSchemaFactory.Vector3Prop("boxSize", "SDF bake box size."),
                        VmAutomationToolSchemaFactory.Vector3Prop("boxCenter", "SDF bake box center."),
                        VmAutomationToolSchemaFactory.Prop("maxResolution", "number", "SDF resolution along the largest dimension."),
                        VmAutomationToolSchemaFactory.Prop("signPassCount", "number", "SDF sign refinement passes, 1 to 20."),
                        VmAutomationToolSchemaFactory.Prop("threshold", "number", "SDF inside/outside or texture-decimation threshold."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "SDF surface offset."),
                        VmAutomationToolSchemaFactory.EnumProp("meshBakeMode", "Mesh point sampling primitive.", "vertex", "triangle"),
                        VmAutomationToolSchemaFactory.EnumProp("distribution", "Mesh point distribution.", "sequential", "random", "random-uniform-area"),
                        VmAutomationToolSchemaFactory.Prop("pointCount", "number", "Mesh point-cache sample count."),
                        VmAutomationToolSchemaFactory.Prop("seed", "number", "Random sampling seed."),
                        VmAutomationToolSchemaFactory.Prop("exportNormals", "boolean", "Export mesh normals."),
                        VmAutomationToolSchemaFactory.Prop("exportColors", "boolean", "Export colors."),
                        VmAutomationToolSchemaFactory.Prop("exportUV", "boolean", "Export first mesh UV channel."),
                        VmAutomationToolSchemaFactory.EnumProp("format", "Point cache output encoding.", "ascii", "binary"),
                        VmAutomationToolSchemaFactory.EnumProp("thresholdMode", "Texture point-cache threshold channel.", "none", "alpha", "luminance", "r", "g", "b"),
                        VmAutomationToolSchemaFactory.Prop("randomize", "boolean", "Randomize accepted texture pixel order.")), "kind", "outputPath");
                case "audio-mixer/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("maxGroups", "number", "Maximum groups returned. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxSnapshots", "number", "Maximum snapshots returned. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxEffects", "number", "Maximum detailed effects returned. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxChildrenPerGroup", "number", "Maximum child groups listed per group. Defaults to 50; capped at 200."),
                        VmAutomationToolSchemaFactory.Prop("maxEffectsPerGroup", "number", "Maximum effect references listed per group. Defaults to 50; capped at 200."),
                        VmAutomationToolSchemaFactory.Prop("maxParametersPerEffect", "number", "Maximum parameter definitions returned per effect. Defaults to 50; capped at 200."),
                        VmAutomationToolSchemaFactory.Prop("maxExposedParameters", "number", "Maximum exposed parameters returned. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxObjects", "number", "Maximum mixer subassets in the optional serialized diagnostic. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxProperties", "number", "Maximum visible serialized properties per object when includeSerialized is true. Defaults to 40; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized mixer diagnostic. Defaults to false.")
                    ), "assetPath");
                case "audio-mixer/transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "AudioMixer asset path below Assets/."),
                        AudioMixerOperationArrayProp("operations", "Ordered semantic group, snapshot, effect, exposed-parameter, snapshot-value, rename, or set-property operations. Runtime exposed-parameter overrides must use a separate transaction."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and describe the transaction without changing the mixer.")
                    ), "assetPath", "operations");
                case "build/profile":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.EnumProp("action", "Build Profile action. Defaults to info.", "info", "transaction"),
                        BuildProfileOperationArrayProp("operations", "For transaction, ordered set-active, set-scenes, set-scripting-defines, set-global-scenes, or set-property operations."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return current profiles plus requested operations without mutation."),
                        VmAutomationToolSchemaFactory.Prop("includeAfter", "boolean", "Include a paginated post-transaction Build Profile snapshot. Defaults to false; operation results are returned regardless."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Build Profile offset for info or includeAfter. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum Build Profiles for info or includeAfter. Defaults to 50; capped at 200.")
                    ));
                case "addressables/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Addressable entry offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum entries returned. Defaults to 100; capped at 500.")
                    ));
                case "addressables/transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        AddressablesOperationArrayProp("operations", "Ordered create/remove/default-group, add/remove/rename-label, create-or-move-entry, set-address, set-label, or remove-entry operations."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and describe the Addressables transaction without modifying settings.")
                    ), "operations");
                case "addressables/build":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props());
                case "timeline/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        VmAutomationToolSchemaFactory.Prop("maxTracks", "number", "Maximum tracks returned across the semantic hierarchy. Defaults to 250; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("maxClipsPerTrack", "number", "Maximum clips returned per track. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxMarkersPerTrack", "number", "Maximum markers returned per track. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxObjects", "number", "Maximum Timeline subassets returned. Defaults to 250; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxProperties", "number", "Maximum serialized properties per Timeline object when includeSerialized is true. Defaults to 60; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("includeSerialized", "boolean", "Include a recursively budgeted serialized Timeline diagnostic. Defaults to false.")
                    ), "assetPath");
                case "timeline/transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "TimelineAsset path below Assets/."),
                        TimelineOperationArrayProp("operations", "Ordered create-track, delete-track, rename-track, set-track-property, create-clip, delete-clip, or set-clip operations."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return the current Timeline plus requested operations without mutation."),
                        VmAutomationToolSchemaFactory.Prop("includeAfter", "boolean", "Include a bounded post-transaction Timeline snapshot. Defaults to false; operation results are returned regardless."),
                        VmAutomationToolSchemaFactory.Prop("maxTracks", "number", "Maximum tracks in includeAfter. Defaults to 250; capped at 1000."),
                        VmAutomationToolSchemaFactory.Prop("maxClipsPerTrack", "number", "Maximum clips per track in includeAfter. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("maxMarkersPerTrack", "number", "Maximum markers per track in includeAfter. Defaults to 100; capped at 500.")
                    ), "assetPath", "operations");
                case "cinemachine/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Optional prefab asset path. Omit to inspect loaded scenes."),
                        VmAutomationToolSchemaFactory.Prop("includeProperties", "boolean", "Include bounded serialized properties for every Cinemachine component. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("maxProperties", "number", "Maximum serialized properties per component. Defaults to 60; capped at 200."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Cinemachine component offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Maximum Cinemachine components returned. Defaults to 100; capped at 500.")
                    ));
                case "cinemachine/transaction":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Optional prefab asset path. Omit to edit loaded scene objects."),
                        CinemachineOperationArrayProp("operations", "Ordered set-property, set-object-reference, or set-enabled operations. Select scene objects by scenePath plus GameObject path, and components or target components by type plus zero-based index."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Resolve and describe every operation without modifying scene or prefab data.")
                    ), "operations");
                case "asset/export-unitypackage":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.ArrayProp("assetPaths", "string", "Unity asset paths to export, e.g. Assets/MyFolder or Assets/MyPrefab.prefab."),
                        VmAutomationToolSchemaFactory.Prop("outputPath", "string", "Absolute path or project-root-relative path for the .unitypackage output."),
                        VmAutomationToolSchemaFactory.Prop("includeDependencies", "boolean", "Include asset dependencies. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("recurse", "boolean", "Recursively export folder contents. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("overwrite", "boolean", "Replace an existing output file. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("interactive", "boolean", "Show Unity's export package UI. Defaults to false.")
                    ), "outputPath");
                case "asset/import-unitypackage":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("packagePath", "string", "Absolute path or project-root-relative path to a .unitypackage file. Import is always non-interactive.")
                    ), "packagePath");
                case "editor/play-mode":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("action", "string", "Target action: play, pause, resume, step, or stop. Defaults to play. Pause is idempotent; step advances one frame and remains paused."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for the confirmed target state. Defaults to 10000."),
                        VmAutomationToolSchemaFactory.Prop("stableFrames", "number", "Consecutive Editor updates that must confirm the target state. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity for durable play and stop transitions. Reusing it with identical arguments returns the same job; different arguments are rejected.")
                    ));
                case "editor/play-mode-options":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("enabled", "boolean", "Whether Enter Play Mode Options are enabled. Setting false normalizes both option flags to None, so Unity performs Domain Reload and Scene Reload."),
                        VmAutomationToolSchemaFactory.Prop("disableDomainReload", "boolean", "Whether Enter Play Mode skips Domain Reload while options are enabled. Requesting true requires enabled=true in the same or current state."),
                        VmAutomationToolSchemaFactory.Prop("disableSceneReload", "boolean", "Whether Enter Play Mode skips Scene Reload while options are enabled. Requesting true requires enabled=true in the same or current state. Omit a field to preserve its current value while enabled; omit all fields to inspect without mutation.")
                    ));
                case "editor/execute-code":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("code", "string", "C# method body to execute. Return a value to serialize it."),
                        VmAutomationToolSchemaFactory.ArrayProp("usings", "string", "Additional namespace imports for this call. Recurring imports can be configured in Project Settings > VM Unity Automation > Execute Code. UnityEngine.UIElements is included by default."),
                        VmAutomationToolSchemaFactory.Prop("maxResultItems", "number", "Maximum serialized collection/object entries across the result. Defaults to 200; capped at 2000."),
                        VmAutomationToolSchemaFactory.Prop("maxResultDepth", "number", "Maximum serialized result depth. Defaults to 8; capped at 16."),
                        VmAutomationToolSchemaFactory.Prop("maxResultStringLength", "number", "Maximum characters per returned string. Defaults to 20000; capped at 200000."),
                        VmAutomationToolSchemaFactory.EnumProp("unityStructFormat", "Unity value structs in the result: compact strings or structured typed objects. Defaults to compact.", "compact", "structured"),
                        VmAutomationToolSchemaFactory.Prop("includeStackTrace", "boolean", "Include a full managed stack trace when executed code throws. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("idempotencyKey", "string", "Optional project-scoped key. Repeating the same key returns the existing persistent job instead of executing code again."),
                        VmAutomationToolSchemaFactory.Prop("cleanupCode", "string", "Optional C# method body used only by jobs/cleanup to reverse temporary state created by this job.")
                    ), "code");
                case "profiler/enable":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("enabled", "boolean", "Enable or disable Profiler recording. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("deepProfiling", "boolean", "Optional deep profiling state.")
                    ));
                case "profiler/stats":
                case "profiler/memory":
                case "profiler/analyze":
                case "profiler/memory-status":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props());
                case "profiler/frame-data":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("frameIndex", "number", "Recorded Profiler frame index. Defaults to the latest frame."),
                        VmAutomationToolSchemaFactory.Prop("threadIndex", "number", "Profiler thread index. Defaults to 0 for Main Thread."),
                        VmAutomationToolSchemaFactory.Prop("maxItems", "number", "Maximum timing entries. Defaults to 30."),
                        VmAutomationToolSchemaFactory.Prop("minTimeMs", "number", "Exclude nested timing entries below this total time.")
                    ));
                case "profiler/memory-breakdown":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("includeDetails", "boolean", "Include the largest assets in each category."),
                        VmAutomationToolSchemaFactory.Prop("maxPerCategory", "number", "Maximum detailed assets per category. Defaults to 5.")
                    ));
                case "profiler/memory-top-assets":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("count", "number", "Maximum assets to return. Defaults to 20."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Optional asset type filter such as texture, mesh, audio, material, shader, animation, or font.")
                    ));
                case "profiler/memory-snapshot":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Optional output directory. Defaults to Unity's temporary cache MemorySnapshots folder."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for snapshot completion. Defaults to 120000.")
                    ));
                case "profiler/memory-snapshot-status":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Optional snapshot job ID. Defaults to the current job in this Editor session.")
                    ));
                case "scene/hierarchy":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        VmAutomationToolSchemaFactory.Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000."),
                        VmAutomationToolSchemaFactory.Prop("parentPath", "string", "Optional GameObject path used as the search root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Optional component short, full, or assembly-qualified type name. When set, returns compact flat matches instead of the full hierarchy."),
                        VmAutomationToolSchemaFactory.Prop("nameContains", "string", "Optional case-insensitive GameObject name filter used with componentType."),
                        VmAutomationToolSchemaFactory.Prop("pathContains", "string", "Optional case-insensitive hierarchy path filter used with componentType."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Component-filtered result offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum component-filtered matches. Defaults to min(maxNodes, 50); capped at 200.")
                    ));
                case "testing/list-tests":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        VmAutomationToolSchemaFactory.Prop("nameFilter", "string", "Optional case-insensitive test full-name filter."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Test result offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum tests to return. Defaults to 100; capped at 500.")
                    ));
                case "testing/run-tests":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        VmAutomationToolSchemaFactory.ArrayProp("testNames", "string", "Optional exact test full names."),
                        VmAutomationToolSchemaFactory.ArrayProp("categories", "string", "Optional test categories."),
                        VmAutomationToolSchemaFactory.ArrayProp("assemblies", "string", "Optional test assembly names."),
                        VmAutomationToolSchemaFactory.ArrayProp("groupNames", "string", "Optional Unity Test Runner group names."),
                        VmAutomationToolSchemaFactory.Prop("clearStuck", "boolean", "Force-clear a previously stuck job before starting. Defaults to false.")
                    ));
                case "testing/get-job":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Optional job ID. Defaults to the current or latest job."),
                        VmAutomationToolSchemaFactory.Prop("includeDetails", "boolean", "Include paginated individual test results. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("includeFailedOnly", "boolean", "Include only failed or inconclusive test results."),
                        VmAutomationToolSchemaFactory.Prop("includeStackTrace", "boolean", "Include test stack traces. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Individual test result offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("limit", "number", "Individual test result limit. Defaults to 100; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("failureLimit", "number", "Maximum failures included in progress. Defaults to 20; capped at 100.")
                    ));
                case "testing/run-package-tests":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("packageName", "string", "Git package name. Defaults to com.vm233.unity-automation."),
                        VmAutomationToolSchemaFactory.Prop("mode", "string", "Test mode: EditMode or PlayMode. Defaults to EditMode."),
                        VmAutomationToolSchemaFactory.ArrayProp("assemblies", "string", "Test assembly names. Defaults to the VM Unity Automation regression assembly for the VM Unity Automation package."),
                        VmAutomationToolSchemaFactory.ArrayProp("testNames", "string", "Optional exact test full names."),
                        VmAutomationToolSchemaFactory.ArrayProp("categories", "string", "Optional test categories. For com.vm233.unity-automation, omit every selection filter to run VMUnityAutomation.PackageSmoke, or pass VMUnityAutomation.FullRegression to run the full package suite."),
                        VmAutomationToolSchemaFactory.ArrayProp("groupNames", "string", "Optional Unity Test Runner group names.")
                    ));
                case "testing/get-package-job":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("jobId", "string", "Optional package-test job ID. Defaults to the active or latest workflow."),
                        VmAutomationToolSchemaFactory.Prop("jobAccessToken", "string", "Capability token returned when the package-test job started. Required after the originating Automation agent disconnects."),
                        VmAutomationToolSchemaFactory.Prop("clear", "boolean", "Delete terminal workflow state after returning it. Defaults to false.")
                    ));
                case "scene/instantiate-prefab":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Prefab asset path to instantiate into the currently open scene."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Optional name for the created scene instance."),
                        VmAutomationToolSchemaFactory.Prop("parent", "string", "Optional scene GameObject name used as the parent."),
                        VmAutomationToolSchemaFactory.Vector3Prop("position", "Optional world position object with x/y/z."),
                        VmAutomationToolSchemaFactory.Vector3Prop("rotation", "Optional world Euler rotation object with x/y/z.")
                    ), "prefabPath");
                case "prefab-asset/hierarchy":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to inspect."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Optional GameObject path used as the hierarchy root."),
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Maximum hierarchy depth to return. Defaults to 10."),
                        VmAutomationToolSchemaFactory.Prop("maxNodes", "number", "Maximum hierarchy nodes to return. Defaults to 250; capped at 2000.")
                    ), "assetPath");
                case "prefab-asset/get-properties":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to inspect."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name.")
                    ), "assetPath", "componentType");
                case "prefab-asset/set-property":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                        VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Serialized property name or property path to set."),
                        VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Serialized value to assign. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the Automation client exposes this field as an object."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType", "propertyName", "value");
                case "prefab-asset/set-reference":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name. Optional when propertyName can identify the component."),
                        VmAutomationToolSchemaFactory.Prop("propertyName", "string", "ObjectReference serialized property name or property path."),
                        VmAutomationToolSchemaFactory.Prop("referenceAssetPath", "string", "Project asset path to assign. Ambiguous compatible objects require an exact subasset selector."),
                        VmAutomationToolSchemaFactory.Prop("referenceSubAssetName", "string", "Optional exact object name within referenceAssetPath."),
                        VmAutomationToolSchemaFactory.Prop("referenceSubAssetLocalId", "string", "Optional exact local file ID within referenceAssetPath, encoded as a decimal string."),
                        VmAutomationToolSchemaFactory.Prop("referencePrefabPath", "string", "Path of a GameObject inside the same prefab to assign."),
                        VmAutomationToolSchemaFactory.Prop("referenceComponentType", "string", "When using referencePrefabPath, assign this component instead of the GameObject."),
                        VmAutomationToolSchemaFactory.Prop("clear", "boolean", "Clear the ObjectReference."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "propertyName");
                case "prefab-asset/instantiate-child-prefab":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Target prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("sourcePrefabPath", "string", "Prefab asset path to instantiate into the target prefab."),
                        VmAutomationToolSchemaFactory.Prop("parentPrefabPath", "string", "Parent path inside the target prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Optional name override for the created GameObject."),
                        VmAutomationToolSchemaFactory.Prop("siblingIndex", "number", "Optional sibling index under the parent."),
                        VmAutomationToolSchemaFactory.Vector3Prop("position", "Optional local position object with x/y/z."),
                        VmAutomationToolSchemaFactory.Vector3Prop("rotation", "Optional local Euler rotation object with x/y/z."),
                        VmAutomationToolSchemaFactory.Vector3Prop("scale", "Optional local scale object with x/y/z.")
                    ), "assetPath", "sourcePrefabPath");
                case "prefab-asset/add-gameobject":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("parentPrefabPath", "string", "Parent path inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Name of the new child GameObject."),
                        VmAutomationToolSchemaFactory.Prop("primitiveType", "string", "Optional Unity PrimitiveType to create, e.g. Cube or Sphere."),
                        VmAutomationToolSchemaFactory.Prop("layer", "string", "Optional Unity layer name or numeric index. Defaults to the parent GameObject's layer."),
                        VmAutomationToolSchemaFactory.Vector3Prop("position", "Optional local position object with x/y/z."),
                        VmAutomationToolSchemaFactory.Vector3Prop("rotation", "Optional local Euler rotation object with x/y/z."),
                        VmAutomationToolSchemaFactory.Vector3Prop("scale", "Optional local scale object with x/y/z."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "name");
                case "prefab-asset/add-component":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                        VmAutomationToolSchemaFactory.JsonValueMapProp("properties", "Optional serialized property names/paths mapped to initial JSON values. Values are applied before the new component is saved."),
                        VmAutomationToolSchemaFactory.Prop("waitForType", "boolean", "Wait for compilation/import until the component type is available. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                        VmAutomationToolSchemaFactory.Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                        VmAutomationToolSchemaFactory.Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh once before waiting. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."),
                        VmAutomationToolSchemaFactory.ArrayProp("prefabFileDiffIgnoreContains", "string", "Optional substrings used to hide noisy diff lines."),
                        VmAutomationToolSchemaFactory.ArrayProp("prefabFileDiffIgnoreYamlProperties", "string", "Optional YAML property names used to hide noisy diff lines.")
                    ), "assetPath", "componentType");
                case "prefab-asset/configure-component":
                    return VmAutomationToolSchemaFactory.PrefabAssetConfigureComponentSchema();
                case "prefab-asset/remove-component":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "componentType");
                case "prefab-asset/move-component":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("sourcePrefabPath", "string", "Path of the source GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("targetPrefabPath", "string", "Path of the target GameObject inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Component index on the source GameObject. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "sourcePrefabPath", "targetPrefabPath", "componentType");
                case "prefab-asset/move-gameobject":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the GameObject to move inside the prefab."),
                        VmAutomationToolSchemaFactory.Prop("newParentPrefabPath", "string", "New parent path inside the prefab. Empty means root."),
                        VmAutomationToolSchemaFactory.Prop("siblingIndex", "number", "Optional sibling index under the new parent."),
                        VmAutomationToolSchemaFactory.Prop("worldPositionStays", "boolean", "Preserve world transform while reparenting. Defaults to false.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/remove-gameobject":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to edit."),
                        VmAutomationToolSchemaFactory.Prop("prefabPath", "string", "Path of the child GameObject to remove. Cannot be root."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath", "prefabPath");
                case "prefab-asset/find":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab asset path to search."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "Exact GameObject name filter."),
                        VmAutomationToolSchemaFactory.Prop("nameContains", "string", "Case-insensitive GameObject name contains filter."),
                        VmAutomationToolSchemaFactory.Prop("pathContains", "string", "Case-insensitive prefab path contains filter."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Optional component short, full, or assembly-qualified type name filter."),
                        VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Optional serialized property name/path to require on the component."),
                        VmAutomationToolSchemaFactory.Prop("propertyValue", "string", "Optional serialized property value to match."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum returned matches. Defaults to 50.")
                    ), "assetPath");
                case "prefab-asset/transaction-edit":
                    return VmAutomationToolSchemaFactory.PrefabAssetTransactionEditSchema();
                case "prefab-asset/cleanup-missing-overrides":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Prefab Variant asset path to clean."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Report removable overrides without saving. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                        VmAutomationToolSchemaFactory.Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary.")
                    ), "assetPath");
                case "component/set-reference":
                    return VmAutomationToolSchemaFactory.ComponentSetReferenceSchema();
                case "component/move":
                    return VmAutomationRouteSchemaFactory.RequireAnyOfEach(
                        VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("sourceInstanceId", "string", "Source scene GameObject instance id."),
                            VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "Source scene GameObject hierarchy path when sourceInstanceId is omitted."),
                            VmAutomationToolSchemaFactory.Prop("targetInstanceId", "string", "Target scene GameObject instance id."),
                            VmAutomationToolSchemaFactory.Prop("targetPath", "string", "Target scene GameObject hierarchy path when targetInstanceId is omitted."),
                            VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                            VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Component index on the source GameObject. Defaults to 0.")),
                            "componentType"),
                        new[] { "sourcePath", "sourceInstanceId" },
                        new[] { "targetPath", "targetInstanceId" });
                case "component/set-property":
                    return VmAutomationRouteSchemaFactory.RequireAnyOf(
                        VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("instanceId", "string", "Target scene GameObject instance id."),
                            VmAutomationToolSchemaFactory.Prop("path", "string", "Target scene GameObject hierarchy path when instanceId is omitted."),
                            VmAutomationToolSchemaFactory.Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                            VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Serialized property name, or inherited Behaviour property name such as enabled."),
                            VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Property value. Accepts primitive values, arrays, and objects. A primitive scalar may be wrapped as {value: ...} when the Automation client exposes this field as an object.")
                        ), "componentType", "propertyName", "value"),
                        "path", "instanceId");
                case "serialized-object/get":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("instanceId", "number", "Target Unity object instance id."),
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        VmAutomationToolSchemaFactory.Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        VmAutomationToolSchemaFactory.Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        VmAutomationToolSchemaFactory.Prop("propertyPath", "string", "Optional serialized property path to read."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Visible property offset. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("maxProperties", "number", "Maximum properties to return when propertyPath is omitted. Defaults to 50; capped at 500."),
                        VmAutomationToolSchemaFactory.Prop("includeChildren", "boolean", "Walk child properties. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Maximum nested serialized value depth. Defaults to 3; capped at 8."),
                        VmAutomationToolSchemaFactory.Prop("maxArrayElements", "number", "Maximum elements returned per serialized array. Defaults to 50; capped at 500.")
                    ));
                case "serialized-object/set":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("instanceId", "number", "Target Unity object instance id."),
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Target asset path if instanceId is omitted."),
                        VmAutomationToolSchemaFactory.Prop("assetType", "string", "Optional asset type name/full name used when loading assetPath."),
                        VmAutomationToolSchemaFactory.Prop("gameObjectPath", "string", "Scene GameObject path if instanceId and assetPath are omitted."),
                        VmAutomationToolSchemaFactory.Prop("componentType", "string", "Optional component type to select from a GameObject target."),
                        VmAutomationToolSchemaFactory.Prop("componentIndex", "number", "Component index when multiple components of the same type exist."),
                        VmAutomationToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path to write."),
                        VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Serialized value. A primitive scalar may be wrapped as {value: ...} when the Automation client exposes this field as an object. ObjectReference supports assetPath, instanceId, or gameObject. SerializeReference objects may include '$managedReferenceType' as 'AssemblyName::Namespace.TypeName'.")
                    ), "propertyPath", "value");
                case "asset/rename":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Current asset path, e.g. Assets/Art/Old Name.png."),
                        VmAutomationToolSchemaFactory.Prop("newName", "string", "New file or folder name. Do not include a directory path."),
                        VmAutomationToolSchemaFactory.Prop("dryRun", "boolean", "Validate and return expected paths without renaming.")
                    ));
                case "asset/import":
                    return VmAutomationToolSchemaFactory.AssetImportSchema();
                case "asset/refresh":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.ArrayProp("assetPaths", "string", "Optional Unity asset paths to import. When supplied, only these paths are imported, with known dependencies before dependents. Omit to run a full synchronous AssetDatabase refresh and reconcile all external changes."),
                        VmAutomationToolSchemaFactory.Prop("forceUpdate", "boolean", "Use ImportAssetOptions.ForceUpdate for full refreshes and non-compilation targeted assets. Compilation assets are always imported without ForceUpdate to avoid broad dependency reimports. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("saveAssets", "boolean", "Call AssetDatabase.SaveAssets after refresh/import. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("idempotencyKey", "string", "Optional caller-stable identity. Reusing it with identical arguments returns the same durable job; different arguments are rejected.")
                    ));
                case "asset/move":
                    return VmAutomationToolSchemaFactory.AssetMoveSchema();
                case "console/query":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("count", "number", "Maximum returned entries. Defaults to 50; capped at 200."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Filtered entry offset, counting from the newest match. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("type", "string", "Filter by all, error, warning, info, exception, or assert. Defaults to all."),
                        VmAutomationToolSchemaFactory.Prop("messageContains", "string", "Case-insensitive message substring filter."),
                        VmAutomationToolSchemaFactory.Prop("sourceContains", "string", "Case-insensitive source stack frame/path substring filter."),
                        VmAutomationToolSchemaFactory.Prop("stackContains", "string", "Case-insensitive full stack substring filter."),
                        VmAutomationToolSchemaFactory.Prop("since", "string", "Start time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        VmAutomationToolSchemaFactory.Prop("until", "string", "End time filter. Accepts ISO/local time, Unix seconds, or Unix milliseconds."),
                        VmAutomationToolSchemaFactory.Prop("sinceSecondsAgo", "number", "Start time filter relative to now."),
                        VmAutomationToolSchemaFactory.Prop("sinceLastPlay", "boolean", "Only include entries recorded after the latest Play transition."),
                        VmAutomationToolSchemaFactory.Prop("includeStack", "boolean", "Include full stack traces. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("newestFirst", "boolean", "Return newest entries first. Defaults to false.")
                    ));
                case "debug/attach-unity":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("openWindow", "boolean", "Open Unity's Managed Debugger window. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("waitForAttach", "boolean", "Wait briefly for an external managed debugger to attach. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Attach wait timeout in milliseconds when waitForAttach is true. Defaults to 0.")
                    ));
                case "debug/set-breakpoint":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("file", "string", "Source file path for the requested breakpoint."),
                        VmAutomationToolSchemaFactory.Prop("line", "number", "1-based source line for the requested breakpoint.")
                    ), "file", "line");
                case "debug/stack-trace":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("skipFrames", "number", "Number of Automation call frames to skip. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("maxFrames", "number", "Maximum stack frames to return. Defaults to 50.")
                    ));
                case "debug/variables":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("frameId", "number", "Paused debugger frame id.")
                    ), "frameId");
                case "debug/evaluate":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("expression", "string", "C# expression to evaluate in Unity Editor context. Wrapped as return <expression>; when code is omitted."),
                        VmAutomationToolSchemaFactory.Prop("code", "string", "Full C# method body for editor-context evaluation.")
                    ));
                case "animation/transition-info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        VmAutomationToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("sourceState", "string", "Optional source state name filter."),
                        VmAutomationToolSchemaFactory.Prop("destinationState", "string", "Optional destination state, state machine, or Exit filter."),
                        VmAutomationToolSchemaFactory.Prop("fromAnyState", "boolean", "When true, only inspect Any State transitions. When false, only inspect state transitions."),
                        VmAutomationToolSchemaFactory.Prop("transitionIndex", "number", "Optional transition index under the source.")
                    ), "controllerPath");
                case "animation/update-state":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        VmAutomationToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("stateName", "string", "State name to modify."),
                        VmAutomationToolSchemaFactory.Prop("newStateName", "string", "Optional new state name."),
                        VmAutomationToolSchemaFactory.Prop("motionPath", "string", "AnimationClip or Motion asset path to assign."),
                        VmAutomationToolSchemaFactory.Prop("clearMotion", "boolean", "Clear the state's motion."),
                        VmAutomationToolSchemaFactory.Prop("speed", "number", "State speed."),
                        VmAutomationToolSchemaFactory.Prop("tag", "string", "State tag."),
                        VmAutomationToolSchemaFactory.Vector2Prop("position", "State graph position object with x/y."),
                        VmAutomationToolSchemaFactory.Prop("isDefault", "boolean", "Set this state as the layer default state."),
                        VmAutomationToolSchemaFactory.Prop("writeDefaultValues", "boolean", "State write default values flag."),
                        VmAutomationToolSchemaFactory.Prop("mirror", "boolean", "State mirror flag."),
                        VmAutomationToolSchemaFactory.Prop("iKOnFeet", "boolean", "State IK on feet flag."),
                        VmAutomationToolSchemaFactory.Prop("cycleOffset", "number", "State cycle offset.")
                    ), "controllerPath", "stateName");
                case "animation/update-transition":
                    return VmAutomationToolSchemaFactory.AnimationUpdateTransitionSchema();
                case "animation/connect-states":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        VmAutomationToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        VmAutomationToolSchemaFactory.ArrayProp("stateNames", "string", "State names to connect pairwise."),
                        VmAutomationToolSchemaFactory.Prop("skipExisting", "boolean", "Skip existing transitions. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("replaceExisting", "boolean", "Remove existing matching transitions before creating new ones."),
                        VmAutomationToolSchemaFactory.Prop("hasExitTime", "boolean", "Transition has exit time applied to created transitions."),
                        VmAutomationToolSchemaFactory.Prop("exitTime", "number", "Transition exit time applied to created transitions."),
                        VmAutomationToolSchemaFactory.Prop("duration", "number", "Transition duration applied to created transitions."),
                        VmAutomationToolSchemaFactory.Prop("offset", "number", "Transition offset applied to created transitions."),
                        VmAutomationToolSchemaFactory.Prop("hasFixedDuration", "boolean", "Fixed duration flag applied to created transitions."),
                        AnimatorConditionArrayProp("conditions", "Conditions applied to every created transition.")
                    ), "controllerPath", "stateNames");
                case "animation/validate-controller":
                {
                    Dictionary<string, object> parameter = VmAutomationToolSchemaFactory.ObjectSchema(
                        VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("name", "string", "Exact Animator parameter name."),
                            VmAutomationToolSchemaFactory.Prop("type", "string", "Optional expected AnimatorControllerParameterType.")),
                        "name");
                    Dictionary<string, object> transition = VmAutomationToolSchemaFactory.ObjectSchema(
                        VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("source", "string", "Exact source state name."),
                            VmAutomationToolSchemaFactory.Prop("destination", "string", "Exact destination state name."),
                            VmAutomationToolSchemaFactory.Prop("conditionParameter", "string", "Optional required transition-condition parameter.")),
                        "source", "destination");
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("controllerPath", "string", "AnimatorController asset path."),
                        VmAutomationToolSchemaFactory.Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                        VmAutomationToolSchemaFactory.ArrayProp("requiredParameters", new Dictionary<string, object>
                        {
                            { "anyOf", new List<object>
                                {
                                    new Dictionary<string, object> { { "type", "string" } },
                                    parameter,
                                }
                            },
                        }, "Strings or objects with name and optional type."),
                        VmAutomationToolSchemaFactory.ArrayProp("requiredStates", "string", "State names that must exist."),
                        VmAutomationToolSchemaFactory.Prop("requireMotion", "boolean", "Require every state in the layer to have a motion."),
                        VmAutomationToolSchemaFactory.ArrayProp("requiredTransitions", transition, "Objects with source, destination, and optional conditionParameter."),
                        VmAutomationToolSchemaFactory.Prop("requireFullMesh", "boolean", "Require all stateNames to have pairwise transitions."),
                        VmAutomationToolSchemaFactory.ArrayProp("stateNames", "string", "States used by full mesh validation. Defaults to all layer states.")
                    ), "controllerPath");
                }
                case "uitoolkit/audit-uss-styles":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.ArrayProp("paths", "string", "Optional Assets-relative USS files. Omit to audit every USS file in the effective roots."),
                        VmAutomationToolSchemaFactory.ArrayProp("roots", "string", "Assets-relative roots used to index USS and UXML files. Defaults to the project audit settings, then Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for UI Toolkit runtime class API references. Defaults to the project audit settings, then Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        VmAutomationToolSchemaFactory.Prop("useProjectSettings", "boolean", "Use ProjectSettings/VMUnityAutomationUIToolkitAudit.json as the default scope. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        VmAutomationToolSchemaFactory.Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        VmAutomationToolSchemaFactory.Prop("includeSuppressed", "boolean", "Include findings with a reasoned uss-audit suppression comment. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/audit-uxml-layout":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.ArrayProp("paths", "string", "Optional Assets-relative UXML files. Omit to audit every UXML file in the effective roots."),
                        VmAutomationToolSchemaFactory.ArrayProp("roots", "string", "Assets-relative roots used to index UXML and USS files. Defaults to the project audit settings, then Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("runtimeSourceRoots", "string", "Assets-relative roots scanned for runtime UI element-name references. Defaults to the project audit settings, then Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("excludePaths", "string", "Assets-relative files or folders excluded from indexing."),
                        VmAutomationToolSchemaFactory.Prop("useProjectSettings", "boolean", "Use ProjectSettings/VMUnityAutomationUIToolkitAudit.json as the default scope. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("pixelGridEnabled", "boolean", "Override the project pixel-grid audit switch. The project/default value is used when omitted."),
                        VmAutomationToolSchemaFactory.Prop("pixelGridStep", "number", "Override the positive pixel-grid step, such as 3 or 4."),
                        VmAutomationToolSchemaFactory.Prop("includeSuppressed", "boolean", "Include findings with a reasoned uxml-layout-audit suppression comment. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("logWarnings", "boolean", "Also write active findings to the Unity Console. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("runSelfTests", "boolean", "Run deterministic in-memory rule tests and return their result. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
                case "uitoolkit/windows":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props());
                case "uitoolkit/tree":
                    return VmAutomationToolSchemaFactory.EditorWindowSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        VmAutomationToolSchemaFactory.Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        VmAutomationToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/query":
                    return VmAutomationToolSchemaFactory.EditorWindowSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match."),
                        VmAutomationToolSchemaFactory.Prop("text", "string", "TextElement text contains match."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        VmAutomationToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline and resolved style summaries.")
                    ));
                case "uitoolkit/style":
                    return VmAutomationToolSchemaFactory.EditorWindowSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element path from uitoolkit/tree or uitoolkit/query."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/repaint":
                    return VmAutomationToolSchemaFactory.EditorWindowSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Optional element path from uitoolkit/tree or uitoolkit/query.")
                    ));
                case "uitoolkit/asset-inspect":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("uxmlPath", "string", "UXML asset path, e.g. Assets/UI/HUD.uxml."),
                        VmAutomationToolSchemaFactory.Prop("ussPath", "string", "Optional USS asset path. UXML Style src entries are also auto-resolved."),
                        VmAutomationToolSchemaFactory.ArrayProp("ussPaths", "string", "Optional USS asset paths. UXML Style src entries are also auto-resolved."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        VmAutomationToolSchemaFactory.ArrayProp("names", "string", "VisualElement.name values to validate."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class exact match."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "Expected or filtered VisualElement type name."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Total result budget for elements and name matches. Defaults to 100."),
                        VmAutomationToolSchemaFactory.Prop("includeUss", "boolean", "Parse USS files, keeping unconditional class defaults separate from contextual and pseudo-state rules. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("includeElements", "boolean", "Return the general elements collection. Defaults to false for names queries and true otherwise."),
                        VmAutomationToolSchemaFactory.Prop("includeAllUssClasses", "boolean", "Return every parsed USS class. Targeted queries default to only classes used by returned elements.")
                    ));
                case "uitoolkit/runtime-documents":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("includeInactive", "boolean", "Include inactive scene UIDocuments. Defaults to true.")
                    ));
                case "uitoolkit/runtime-tree":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Maximum tree depth. Defaults to 8."),
                        VmAutomationToolSchemaFactory.Prop("maxNodes", "number", "Maximum returned nodes. Defaults to 300."),
                        VmAutomationToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-query":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names, e.g. MainMap/RightControls."),
                        VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match."),
                        VmAutomationToolSchemaFactory.Prop("text", "string", "TextElement text contains match."),
                        VmAutomationToolSchemaFactory.Prop("maxResults", "number", "Maximum returned elements. Defaults to 50."),
                        VmAutomationToolSchemaFactory.Prop("includeStyle", "boolean", "Include inline, resolved, and background style summaries.")
                    ));
                case "uitoolkit/runtime-style":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path from runtime-tree, e.g. root/0/1."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted.")
                    ));
                case "uitoolkit/diagnose-runtime":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        UIToolkitQueryArrayProp("queries", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, and pixelScale."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path if queries is omitted."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array if queries is omitted."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        VmAutomationToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale used for pixel diagnostics. Defaults to 1.")
                    ));
                case "uitoolkit/visual-check":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        UIToolkitVisualCheckArrayProp("checks", "Visual checks. Supported type values: pixel-grid, background-scale, size."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path if checks is omitted."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if checks is omitted."),
                        VmAutomationToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale. Defaults to 1."),
                        VmAutomationToolSchemaFactory.Prop("expectedScale", "number", "Expected background image scale for background-scale checks."),
                        VmAutomationToolSchemaFactory.Prop("width", "number", "Expected element width for size checks."),
                        VmAutomationToolSchemaFactory.Prop("height", "number", "Expected element height for size checks."),
                        VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01.")
                    ));
                case "uitoolkit/locate-element":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("runtime", "boolean", "Locate a runtime UIDocument element when true; otherwise locate an EditorWindow UI Toolkit element. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("window", "string", "EditorWindow type/title. Runtime defaults to Game when capture uses it later."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        VmAutomationToolSchemaFactory.Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/capture-element":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game, editor defaults to the focused/matched window."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names array."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("text", "string", "TextElement text contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("outputPath", "string", "Output PNG path for the cropped element screenshot."),
                        VmAutomationToolSchemaFactory.Prop("windowOutputPath", "string", "Output PNG path for the full containing window screenshot."),
                        VmAutomationToolSchemaFactory.Prop("pixelScale", "number", "Scale from UI points to captured pixels. Defaults to EditorGUIUtility.pixelsPerPoint."),
                        VmAutomationToolSchemaFactory.Prop("padding", "number", "Extra crop padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/compare-element":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("runtime", "boolean", "Capture a runtime UIDocument element when true; otherwise capture an EditorWindow UI Toolkit element. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("window", "string", "EditorWindow type/title to capture. Runtime defaults to Game."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("referencePath", "string", "Reference PNG path."),
                        VmAutomationToolSchemaFactory.Prop("actualPath", "string", "Output path for captured current element PNG."),
                        VmAutomationToolSchemaFactory.Prop("diffOutputPath", "string", "Optional output path for diff PNG."),
                        VmAutomationToolSchemaFactory.RectProp("referenceRect", "Optional comparison rect in reference image."),
                        VmAutomationToolSchemaFactory.RectProp("actualRect", "Optional comparison rect in captured image."),
                        VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Allowed per-channel pixel delta. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("padding", "number", "Extra capture padding in pixels. Defaults to 0.")
                    ));
                case "uitoolkit/generated-children":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("runtime", "boolean", "Inspect a runtime UIDocument element when true; otherwise inspect an EditorWindow UI Toolkit element. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("window", "string", "EditorWindow type/title for editor inspection."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path, e.g. root/0/1."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name exact match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("typeName", "string", "VisualElement type name contains match if path is omitted."),
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Descendant depth to inspect. Defaults to 4."),
                        VmAutomationToolSchemaFactory.Prop("includeAll", "boolean", "Return all descendants, not only generated-looking children. Defaults to false."),
                        VmAutomationToolSchemaFactory.ArrayProp("forbiddenClassContains", "string", "Class substrings that should produce warnings when found."),
                        VmAutomationToolSchemaFactory.ArrayProp("forbiddenTypeContains", "string", "Type-name substrings that should produce warnings when found.")
                    ));
                case "uitoolkit/resource-audit":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("runtime", "boolean", "Audit runtime UIDocument elements when true; otherwise audit EditorWindow UI Toolkit elements. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("window", "string", "EditorWindow type/title for editor audits."),
                        UIToolkitResourceQueryArrayProp("queries", "Optional list of element queries. Each accepts path, visualElementPath, name, className, typeName, text, expectedBackgroundContains, forbiddenBackgroundContains, requireBackground."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path if queries is omitted."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names if queries is omitted."),
                        VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match if queries is omitted."),
                        VmAutomationToolSchemaFactory.Prop("expectedBackgroundContains", "string", "Expected substring in resolved background asset path or name."),
                        VmAutomationToolSchemaFactory.ArrayProp("forbiddenBackgroundContains", "string", "Substrings that must not appear in the resolved background asset path or name."),
                        VmAutomationToolSchemaFactory.Prop("requireBackground", "boolean", "Warn if the target has no resolved background image."),
                        VmAutomationToolSchemaFactory.Prop("warnHighlighted", "boolean", "Warn when a target appears to use a highlighted asset. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("maxDepth", "number", "Descendant depth to scan for background resources. Defaults to 3.")
                    ));
                case "uitoolkit/runtime-repaint":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Optional element tree path from runtime-tree."),
                        VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Optional slash-separated VisualElementPath names."),
                        VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "Optional VisualElementPath names array.")
                    ));
                case "uitoolkit/refresh":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("refreshAssets", "boolean", "Call AssetDatabase.Refresh before repainting. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("forceSynchronousImport", "boolean", "Use ForceSynchronousImport. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum wait time in milliseconds. Defaults to 10000."),
                        VmAutomationToolSchemaFactory.Prop("stableFrames", "number", "Consecutive idle repaint frames required. Defaults to 2.")
                    ));
                case "uitoolkit/builder-preview":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("uxmlPath", "string", "UXML asset path to open in UI Builder."),
                        VmAutomationToolSchemaFactory.Prop("waitFrames", "number", "Editor frames to wait before capturing. Defaults to 8."),
                        VmAutomationToolSchemaFactory.Prop("stableFrames", "number", "Consecutive ready UI Builder frames required. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for the requested document and canvas. Defaults to 10000."),
                        VmAutomationToolSchemaFactory.Prop("capture", "boolean", "Capture the UI Builder window after opening. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("autoMatchGameView", "boolean", "Enable UI Builder Match Game View when visible document content overflows the configured canvas. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("requireContentFit", "boolean", "Fail the preview result when visible document content remains clipped by the canvas. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("screenshotPath", "string", "PNG path for the UI Builder screenshot. Defaults to the VM Unity Automation project screenshot directory."),
                        VmAutomationToolSchemaFactory.Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192."),
                        VmAutomationToolSchemaFactory.Prop("zoom", "number", "Requested zoom, recorded for diagnostics. UI Builder has no stable public zoom API.")
                    ));
                case "uitoolkit/assert-layout":
                    return VmAutomationToolSchemaFactory.RuntimeUIDocumentSchema(VmAutomationToolSchemaFactory.Props(
                        UIToolkitLayoutAssertionArrayProp("assertions", "Layout assertions. Supported types: edge-touch, same-edge, same-center, inside, size.")
                    ), "assertions");
                case "screenshot/game":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Output PNG path. Defaults to the VM Unity Automation project screenshot directory (Assets/Screenshots initially)."),
                        VmAutomationToolSchemaFactory.Prop("superSize", "number", "Resolution multiplier. Defaults to 1."),
                        VmAutomationToolSchemaFactory.Prop("waitFrames", "number", "Frames to wait before requesting a running capture. Ignored while paused. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("stableFrames", "number", "Consecutive stable file-size frames required for a running capture. Ignored while paused. Defaults to 2."),
                        VmAutomationToolSchemaFactory.Prop("timeoutMs", "number", "Maximum time to wait for a complete decodable PNG. Defaults to 10000."),
                        VmAutomationToolSchemaFactory.Prop("editorOverlays", "string", "Game View Gizmos and Stats policy: suppress or preserve. Defaults to suppress; use preserve only when editor overlays are the evidence subject.")
                    ));
                case "screenshot/crop":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "Image path to crop."),
                        VmAutomationToolSchemaFactory.RectProp("rect", "Crop rect with x, y, width, height."),
                        VmAutomationToolSchemaFactory.Prop("outputPath", "string", "Output PNG path. Defaults next to source with _crop suffix."),
                        VmAutomationToolSchemaFactory.Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true.")
                    ));
                case "screenshot/scene":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Output PNG path for file or both transport. Defaults to the VM Unity Automation project screenshot directory (Assets/Screenshots initially)."),
                        VmAutomationToolSchemaFactory.Prop("width", "number", "Capture width in pixels. Defaults to 1920."),
                        VmAutomationToolSchemaFactory.Prop("height", "number", "Capture height in pixels. Defaults to 1080."),
                        VmAutomationToolSchemaFactory.Prop("transport", "string", "Output transport: file, base64, or both. Defaults to file.")
                    ));
                case "screenshot/editor-window":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("window", "string", "EditorWindow type full name, simple type name, or exact tab title."),
                        VmAutomationToolSchemaFactory.Prop("typeOrTitle", "string", "Legacy alias for window."),
                        VmAutomationToolSchemaFactory.Prop("path", "string", "Output PNG path. Defaults to the VM Unity Automation project screenshot directory (Assets/Screenshots initially)."),
                        VmAutomationToolSchemaFactory.Prop("maxDimension", "number", "Maximum screenshot dimension. Defaults to 8192.")
                    ));
                case "graphics/asset-preview":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Asset path to preview, including prefab, material, mesh, or texture assets."),
                        VmAutomationToolSchemaFactory.Prop("width", "number", "Requested preview width in pixels. Defaults to 256."),
                        VmAutomationToolSchemaFactory.Prop("height", "number", "Requested preview height in pixels. Defaults to 256.")
                    ), "assetPath");
                case "gameview/info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props());
                case "gameview/set-resolution":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("width", "number", "Game View custom resolution width in pixels."),
                        VmAutomationToolSchemaFactory.Prop("height", "number", "Game View custom resolution height in pixels."),
                        VmAutomationToolSchemaFactory.Prop("label", "string", "Optional custom size label shown in the Game View size menu.")
                    ), "width", "height");
                case "gameview/set-scale":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("mode", "string", "Scale source: value or minimum. Defaults to value."),
                        VmAutomationToolSchemaFactory.Prop("scale", "number", "Game View zoom scale when mode is value, e.g. 0.76 or 1."),
                        VmAutomationToolSchemaFactory.Prop("fallbackScale", "number", "Fallback minimum scale used if Unity internals do not expose a valid one. Defaults to 0.76.")
                    ));
                case "graphics/image-alpha-bounds":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture2D asset path."),
                        VmAutomationToolSchemaFactory.Prop("filePath", "string", "Absolute or project-relative PNG path if assetPath is omitted."),
                        VmAutomationToolSchemaFactory.Prop("alphaThreshold", "number", "Alpha threshold. 0-1 or 0-255. Defaults to 0.01.")
                    ));
                case "graphics/rect-gap":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.RectProp("firstRect", "First rect with x, y, width, height."),
                        VmAutomationToolSchemaFactory.RectProp("secondRect", "Second rect with x, y, width, height."),
                        VmAutomationToolSchemaFactory.Prop("axis", "string", "x or y. Defaults to x."),
                        VmAutomationToolSchemaFactory.Prop("firstEdge", "string", "First rect edge. Defaults to right for x, bottom for y."),
                        VmAutomationToolSchemaFactory.Prop("secondEdge", "string", "Second rect edge. Defaults to left for x, top for y."),
                        VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Touch tolerance in pixels. Defaults to 0.5.")
                    ), "firstRect", "secondRect");
                case "graphics/annotate-rects":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "Image path to annotate."),
                        VmAutomationToolSchemaFactory.Prop("outputPath", "string", "Output PNG path. Defaults next to source with _annotated suffix."),
                        AnnotationRectArrayProp("rects", "Rectangles to draw. Each has x, y, width, height, optional color and thickness."),
                        VmAutomationToolSchemaFactory.Prop("originTopLeft", "boolean", "Treat rect x/y as top-left image coordinates. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("color", "string", "Default HTML color, e.g. #ff00ffff."),
                        VmAutomationToolSchemaFactory.Prop("thickness", "number", "Default border thickness in pixels. Defaults to 2.")
                    ), "rects");
                case "graphics/compare-images":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("expectedPath", "string", "Reference image path."),
                        VmAutomationToolSchemaFactory.Prop("actualPath", "string", "Current image path."),
                        VmAutomationToolSchemaFactory.RectProp("expectedRect", "Optional reference crop rect with x, y, width, height."),
                        VmAutomationToolSchemaFactory.RectProp("actualRect", "Optional current crop rect with x, y, width, height."),
                        VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Per-channel pixel tolerance, 0-255. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("maxSamples", "number", "Maximum differing pixel samples returned. Defaults to 20."),
                        VmAutomationToolSchemaFactory.Prop("diffOutputPath", "string", "Optional PNG path to write a red-highlight diff image.")
                    ));
                case "sprite/sheet-info":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path.")
                    ));
                case "sprite/pixel-check":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture/Sprite asset path."),
                        VmAutomationToolSchemaFactory.ArrayProp("assetPaths", "string", "Texture/Sprite asset paths."),
                        VmAutomationToolSchemaFactory.Prop("folderPath", "string", "Folder to scan recursively for Texture2D assets."),
                        VmAutomationToolSchemaFactory.Prop("dimensionsMultipleOf", "number", "Optional divisor required for texture width/height."),
                        VmAutomationToolSchemaFactory.Prop("expectedScale", "number", "Optional UI scale used to check source dimensions after scaling."),
                        VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Allowed pixel delta. Defaults to 0.01."),
                        VmAutomationToolSchemaFactory.Prop("requirePointFilter", "boolean", "Warn if FilterMode is not Point. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("requireNoCompression", "boolean", "Warn if the default platform resolves to a compressed texture. Automatic is accepted only with Uncompressed platform compression. Defaults to true."),
                        VmAutomationToolSchemaFactory.Prop("requireNoMipMaps", "boolean", "Warn if mip maps are enabled. Defaults to true.")
                    ));
                case "sprite/replace-and-slice":
                case "sprite/slice-sheet":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "External image file to copy over texturePath. Required for replace-and-slice."),
                        VmAutomationToolSchemaFactory.Prop("frameWidth", "number", "Frame width in pixels."),
                        VmAutomationToolSchemaFactory.Prop("frameHeight", "number", "Frame height in pixels."),
                        VmAutomationToolSchemaFactory.Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        VmAutomationToolSchemaFactory.Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        VmAutomationToolSchemaFactory.Prop("columns", "number", "Grid column count. Defaults to textureWidth / frameWidth."),
                        VmAutomationToolSchemaFactory.Prop("startX", "number", "Grid start x in pixels. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("startY", "number", "Grid start y in top-left pixels. Defaults to 0."),
                        VmAutomationToolSchemaFactory.Prop("pivotX", "number", "Optional normalized pivot x."),
                        VmAutomationToolSchemaFactory.Prop("pivotY", "number", "Optional normalized pivot y."),
                        VmAutomationToolSchemaFactory.Prop("preserveSpriteIDs", "boolean", "Preserve existing sprite IDs by generated name. Defaults to true.")
                    ), "texturePath", "frameWidth", "frameHeight");
                case "sprite/update-animation-clip":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("clipPath", "string", "AnimationClip asset path."),
                        VmAutomationToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        VmAutomationToolSchemaFactory.Prop("bindingPath", "string", "Animation binding path to SpriteRenderer. Empty means the animated object itself."),
                        VmAutomationToolSchemaFactory.Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        VmAutomationToolSchemaFactory.ArrayProp("spriteNames", "string", "Optional exact sprite names to use."),
                        VmAutomationToolSchemaFactory.Prop("loopTime", "boolean", "Whether the clip loops. Defaults to the current clip setting.")
                    ), "clipPath", "texturePath");
                case "sprite/replace-slice-update-clip":
                    return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        VmAutomationToolSchemaFactory.Prop("texturePath", "string", "Sprite sheet texture asset path."),
                        VmAutomationToolSchemaFactory.Prop("sourcePath", "string", "External image file to copy over texturePath."),
                        VmAutomationToolSchemaFactory.Prop("clipPath", "string", "Optional AnimationClip asset path to update after slicing."),
                        VmAutomationToolSchemaFactory.Prop("frameWidth", "number", "Frame width in pixels."),
                        VmAutomationToolSchemaFactory.Prop("frameHeight", "number", "Frame height in pixels."),
                        VmAutomationToolSchemaFactory.Prop("frameCount", "number", "Optional frame count. Defaults to the full grid."),
                        VmAutomationToolSchemaFactory.Prop("baseName", "string", "Generated sprite name prefix. Defaults to texture file name."),
                        VmAutomationToolSchemaFactory.Prop("frameRate", "number", "Animation frame rate. Defaults to the clip frame rate or 12."),
                        VmAutomationToolSchemaFactory.Prop("bindingPath", "string", "Animation binding path to SpriteRenderer.")
                    ), "texturePath", "sourcePath", "frameWidth", "frameHeight");
                default:
                    if (VmAutomationGeneratedRouteContracts.TryGetInput(route, out var generated))
                        return generated;
                    throw new System.InvalidOperationException(
                        $"Registered route '{route}' does not declare an input contract.");
            }
        }

    }
}
