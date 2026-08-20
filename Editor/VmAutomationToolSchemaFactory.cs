using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationToolSchemaFactory
    {
        internal const string JsonValueReference = "#/$defs/unityJsonValue";

        internal static Dictionary<string, object> AssetGraphTransactionSchema(string assetKind)
        {
            Dictionary<string, object> Common(string action)
            {
                var properties = Props(
                    EnumProp("action", "Graph operation discriminator.", action),
                    Prop("localId", "string", "Exact graph subasset local file ID."),
                    Prop("type", "string", "Graph subasset type name or full name."),
                    Prop("targetName", "string", "Exact graph subasset name."));
                return properties;
            }

            Dictionary<string, object> rename = Common("rename");
            rename["name"] = Prop("name", "string", "Replacement graph object name.").Value;
            Dictionary<string, object> setProperty = Common("set-property");
            setProperty["propertyPath"] = Prop("propertyPath", "string",
                "Serialized property path to set.").Value;
            setProperty["value"] = AnyJsonValueProp("value",
                "Serialized JSON value to assign.").Value;

            var operation = new Dictionary<string, object>
            {
                { "oneOf", new List<object>
                    {
                        StrictSchema(rename, "action", "name"),
                        StrictSchema(setProperty, "action", "propertyPath", "value"),
                    }
                },
            };
            return Schema(Props(
                Prop("assetPath", "string", $"{assetKind} asset path below Assets/."),
                ArrayProp("operations", operation,
                    "Ordered rename or set-property operations. Target each subasset by localId or by type plus targetName."),
                Prop("dryRun", "boolean",
                    $"Validate and describe the {assetKind} transaction without modifying the asset.")
            ), "assetPath", "operations");
        }

        internal static Dictionary<string, object> ExecutionSchema(bool includeContinueOnError = true)
        {
            var properties = Props(
                Prop("operationsPerFrame", "number", "Maximum operations processed in one editor frame. Defaults to 25."),
                Prop("frameBudgetMs", "number", "Soft per-frame execution budget in milliseconds. Defaults to 8."),
                Prop("timeoutMs", "number", "Maximum total execution time in milliseconds. Defaults to 90000."));
            properties["mode"] = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Execution mode. auto batches multi-operation requests, immediate runs in one frame, and batched yields across frames." },
                { "enum", new List<object> { "auto", "immediate", "batched" } },
            };
            if (includeContinueOnError)
                properties["continueOnError"] = Prop("continueOnError", "boolean",
                    "Continue processing later operations after one fails. Defaults to false.").Value;
            var schema = Schema(properties);
            schema["description"] = "Optional batching, frame-budget, timeout, and failure-continuation settings for this operation.";
            return schema;
        }

        internal static Dictionary<string, object> ComponentSetReferenceSchema()
        {
            var referenceProperties = Props(
                Prop("path", "string", "Target scene GameObject path or name."),
                Prop("instanceId", "string", "Target scene GameObject instance ID."),
                Prop("componentType", "string", "Component containing the property."),
                Prop("propertyName", "string", "ObjectReference property to assign."),
                Prop("assetPath", "string", "Asset path to assign."),
                Prop("referenceGameObject", "string", "Scene GameObject path or name to assign."),
                Prop("referenceComponentType", "string", "Component type on the referenced GameObject."),
                Prop("referenceInstanceId", "number", "Unity object instance ID to assign."),
                Prop("clear", "boolean", "Clear the reference."));
            var properties = Props(
                Prop("path", "string", "Default target GameObject inherited by reference items."),
                Prop("instanceId", "string", "Default target instance ID inherited by reference items."),
                Prop("componentType", "string", "Default component short, full, or assembly-qualified type name inherited by reference items."));
            properties["execution"] = ExecutionSchema();
            properties["references"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Reference assignments. Every item requires propertyName and one reference source or clear=true." },
                { "items", Schema(referenceProperties, "propertyName") },
            };
            return Schema(properties, "references");
        }

        internal static Dictionary<string, object> AnimationUpdateTransitionSchema()
        {
            var conditionProperties = Props(
                Prop("parameter", "string", "Animator parameter name."),
                Prop("mode", "string", "AnimatorConditionMode value such as If, IfNot, Greater, Less, Equals, or NotEqual."),
                Prop("threshold", "number", "Condition threshold. Trigger and bool conditions normally use 0."));
            var updateConditionProperties = new Dictionary<string, object>(conditionProperties)
            {
                ["index"] = Prop("index", "number", "Zero-based condition index to update.").Value,
            };

            var properties = Props(
                Prop("controllerPath", "string", "AnimatorController asset path."),
                Prop("layerIndex", "number", "Layer index. Defaults to 0."),
                Prop("sourceState", "string", "Source state name. Required unless fromAnyState is true."),
                Prop("destinationState", "string", "Destination state, state machine, or Exit filter."),
                Prop("fromAnyState", "boolean", "Modify an Any State transition."),
                Prop("transitionIndex", "number", "Optional transition index under the source."),
                Prop("hasExitTime", "boolean", "Transition has exit time."),
                Prop("exitTime", "number", "Transition exit time."),
                Prop("duration", "number", "Transition duration."),
                Prop("offset", "number", "Transition offset."),
                Prop("hasFixedDuration", "boolean", "Use fixed duration."),
                Prop("interruptionSource", "string", "TransitionInterruptionSource value."),
                Prop("orderedInterruption", "boolean", "Ordered interruption flag."),
                Prop("canTransitionToSelf", "boolean", "Any State can transition to self flag."));
            properties["conditions"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Replace all conditions with condition objects." },
                { "items", Schema(conditionProperties, "parameter") },
            };
            properties["addConditions"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Append condition objects." },
                { "items", Schema(conditionProperties, "parameter") },
            };
            properties["updateConditions"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Update condition objects by zero-based index." },
                { "items", Schema(updateConditionProperties, "index") },
            };
            properties["removeConditionIndexes"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Remove conditions by zero-based index." },
                { "items", new Dictionary<string, object> { { "type", "number" } } },
            };

            return Schema(properties, "controllerPath");
        }

        internal static Dictionary<string, object> PrefabAssetConfigureComponentSchema()
        {
            var referenceProperties = Props(
                Prop("propertyName", "string", "ObjectReference serialized property name or path."),
                Prop("referenceAssetPath", "string", "Project asset path to assign. Ambiguous compatible objects require an exact subasset selector."),
                Prop("referenceSubAssetName", "string", "Optional exact object name within referenceAssetPath."),
                Prop("referenceSubAssetLocalId", "string", "Optional exact local file ID within referenceAssetPath, encoded as a decimal string."),
                Prop("referencePrefabPath", "string", "Path of a GameObject inside the same prefab to assign."),
                Prop("referenceComponentType", "string", "When using referencePrefabPath, assign this component instead of the GameObject."),
                Prop("referenceComponentIndex", "number", "Component index on referencePrefabPath when multiple components of the same type exist. Defaults to 0."),
                Prop("clear", "boolean", "Clear the ObjectReference."));
            var properties = Props(
                Prop("assetPath", "string", "Prefab asset path to edit."),
                Prop("prefabPath", "string", "Path of the GameObject inside the prefab. Empty means root."),
                Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                Prop("componentIndex", "number", "Component index when multiple components of the same type exist. Defaults to 0."),
                Prop("addIfMissing", "boolean", "Add the component when componentIndex equals the current component count. Defaults to true."),
                Prop("createPathIfMissing", "boolean", "Create missing prefabPath GameObjects before configuring the component. New children inherit their parent layer. Defaults to false."),
                JsonValueMapProp("properties", "Serialized property names/paths mapped to JSON values."),
                Prop("waitForTypes", "boolean", "Wait for referenced component types before editing. Defaults to true."),
                Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                Prop("refreshAssets", "boolean", "Schedule AssetDatabase.Refresh only when a referenced component type is missing. Defaults to true."),
                Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff. Defaults to the VM Unity Automation user preference (disabled initially)."),
                Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full. Defaults to summary."));
            properties["references"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "ObjectReference assignments applied to the configured component." },
                { "items", Schema(referenceProperties, "propertyName") },
            };
            return Schema(properties, "assetPath", "componentType");
        }

        internal static Dictionary<string, object> PrefabAssetTransactionEditSchema()
        {
            Dictionary<string, object> Operation(string type,
                Dictionary<string, object> operationProperties, params string[] required)
            {
                operationProperties["type"] = new Dictionary<string, object>
                {
                    { "type", "string" },
                    { "description", "Discriminant for this prefab edit operation." },
                    { "enum", new List<object> { type } },
                };
                return StrictSchema(operationProperties,
                    new[] { "type" }.Concat(required ?? new string[0]).ToArray());
            }

            Dictionary<string, object> ComponentProperties()
            {
                return Props(
                    Prop("prefabPath", "string", "GameObject path inside the prefab. Empty means root."),
                    Prop("componentType", "string", "Component short, full, or assembly-qualified type name."),
                    Prop("componentIndex", "number", "Component index when multiple components match. Defaults to 0."));
            }

            Dictionary<string, object> ReferenceProperties()
            {
                return Props(
                    Prop("referenceAssetPath", "string", "Project asset path to assign."),
                    Prop("referenceSubAssetName", "string", "Exact subasset name."),
                    Prop("referenceSubAssetLocalId", "string", "Exact subasset local file ID."),
                    Prop("referencePrefabPath", "string", "GameObject path in the same prefab."),
                    Prop("referenceComponentType", "string", "Component type selected from referencePrefabPath."),
                    Prop("referenceComponentIndex", "number", "Component index selected from referencePrefabPath."),
                    Prop("clear", "boolean", "Clear the object reference."));
            }

            var configureReferences = StrictSchema(new Dictionary<string, object>(
                ReferenceProperties())
            {
                ["propertyName"] = Prop("propertyName", "string",
                    "ObjectReference property name or path.").Value,
            }, "propertyName");

            var operationSchemas = new List<object>();

            Dictionary<string, object> addComponent = ComponentProperties();
            addComponent["properties"] = JsonValueMapProp("properties",
                "Initial serialized property values.").Value;
            operationSchemas.Add(Operation("addComponent", addComponent, "componentType"));

            Dictionary<string, object> configure = ComponentProperties();
            configure["addIfMissing"] = Prop("addIfMissing", "boolean",
                "Add the component at the next component index when missing.").Value;
            configure["createPathIfMissing"] = Prop("createPathIfMissing", "boolean",
                "Create missing prefabPath children.").Value;
            configure["properties"] = JsonValueMapProp("properties",
                "Serialized property values.").Value;
            configure["references"] = ArrayProp("references", configureReferences,
                "ObjectReference assignments.").Value;
            operationSchemas.Add(Operation("configureComponent", configure, "componentType"));

            Dictionary<string, object> setProperty = ComponentProperties();
            setProperty["propertyName"] = Prop("propertyName", "string",
                "Serialized property name or path.").Value;
            setProperty["value"] = AnyJsonValueProp("value",
                "Serialized JSON value to assign.").Value;
            operationSchemas.Add(Operation("setProperty", setProperty,
                "componentType", "propertyName", "value"));

            Dictionary<string, object> setReference = ComponentProperties();
            setReference["propertyName"] = Prop("propertyName", "string",
                "ObjectReference property name or path.").Value;
            foreach (KeyValuePair<string, object> pair in ReferenceProperties())
                setReference[pair.Key] = pair.Value;
            operationSchemas.Add(Operation("setReference", setReference,
                "componentType", "propertyName"));

            foreach (string arrayType in new[] { "arrayInsert", "arrayRemove", "arraySet", "arrayClear" })
            {
                Dictionary<string, object> array = ComponentProperties();
                array["propertyName"] = Prop("propertyName", "string",
                    "Serialized array or list property name or path.").Value;
                if (arrayType != "arrayClear")
                    array["index"] = Prop("index", "number", "Zero-based element index.").Value;
                if (arrayType == "arrayInsert" || arrayType == "arraySet")
                    array["value"] = AnyJsonValueProp("value", "Element value.").Value;
                var required = new List<string> { "componentType", "propertyName" };
                if (arrayType != "arrayClear") required.Add("index");
                if (arrayType == "arraySet") required.Add("value");
                operationSchemas.Add(Operation(arrayType, array, required.ToArray()));
            }

            operationSchemas.Add(Operation("addGameObject", Props(
                Prop("parentPrefabPath", "string", "Parent path. Empty means root."),
                Prop("name", "string", "Name of the new GameObject."),
                Prop("primitiveType", "string", "Optional Unity PrimitiveType."),
                Prop("layer", "string", "Optional layer name or numeric index."),
                Vector3Prop("position", "Optional local position x/y/z."),
                Vector3Prop("rotation", "Optional local Euler rotation x/y/z."),
                Vector3Prop("scale", "Optional local scale x/y/z.")), "name"));

            operationSchemas.Add(Operation("instantiatePrefab", Props(
                Prop("sourcePrefabPath", "string", "Prefab asset to instantiate."),
                Prop("parentPrefabPath", "string", "Parent path. Empty means root."),
                Prop("name", "string", "Optional name override."),
                Prop("siblingIndex", "number", "Optional sibling index."),
                Vector3Prop("position", "Optional local position x/y/z."),
                Vector3Prop("rotation", "Optional local Euler rotation x/y/z."),
                Vector3Prop("scale", "Optional local scale x/y/z.")), "sourcePrefabPath"));

            operationSchemas.Add(Operation("removeComponent", ComponentProperties(),
                "componentType"));
            operationSchemas.Add(Operation("removeGameObject", Props(
                Prop("prefabPath", "string", "Non-root GameObject path to remove.")),
                "prefabPath"));
            operationSchemas.Add(Operation("moveGameObject", Props(
                Prop("prefabPath", "string", "Non-root GameObject path to move."),
                Prop("newParentPrefabPath", "string", "New parent path. Empty means root."),
                Prop("siblingIndex", "number", "Optional sibling index under the new parent."),
                Prop("worldPositionStays", "boolean", "Preserve world transform while reparenting.")),
                "prefabPath"));

            var properties = Props(
                Prop("assetPath", "string", "Prefab asset path to edit."),
                Prop("waitForTypes", "boolean", "Wait for all referenced component types before editing. Defaults to true."),
                Prop("typeResolveTimeoutMs", "number", "Maximum type wait time in milliseconds. Defaults to 30000."),
                Prop("typeResolveStableMs", "number", "Continuous idle time after type resolution before editing. Defaults to 500."),
                Prop("refreshAssets", "boolean", "Schedule AssetDatabase.Refresh only when a referenced component type is missing."),
                Prop("includePrefabFileDiff", "boolean", "Return before/after prefab YAML diff."),
                Prop("prefabFileDiffContextLines", "number", "Context lines around prefab YAML changes. Defaults to 2."),
                Prop("prefabFileDiffMaxLines", "number", "Maximum diff lines returned. Defaults to 200."),
                Prop("prefabFileDiffMode", "string", "Diff return mode: summary, minimal, or full."),
                ArrayProp("prefabFileDiffIgnoreContains", "string", "Diff lines containing these values are hidden."),
                ArrayProp("prefabFileDiffIgnoreYamlProperties", "string", "YAML properties hidden from the diff."));
            properties["execution"] = ExecutionSchema(includeContinueOnError: false);
            properties["operations"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Ordered, discriminated prefab edit operations. Unknown fields are rejected." },
                { "items", new Dictionary<string, object> { { "oneOf", operationSchemas } } },
            };
            return StrictSchema(properties, "assetPath", "operations");
        }
        internal static Dictionary<string, object> AssetMoveSchema()
        {
            var moveProperties = Props(
                Prop("path", "string", "Current asset path."),
                Prop("destinationPath", "string", "Destination asset path, or an existing folder path to keep the same file name."),
                Prop("destinationFolder", "string", "Existing folder path to keep the same file name.")
            );

            var properties = Props(
                Prop("dryRun", "boolean", "Validate every move and return expected paths without moving."));
            properties["execution"] = ExecutionSchema();
            properties["moves"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Move requests. Every item needs path and either destinationPath or destinationFolder. Duplicate sources and targets are rejected before execution." },
                { "items", Schema(moveProperties) }
            };

            return Schema(properties, "moves");
        }

        internal static Dictionary<string, object> AssetTransactionSchema()
        {
            Dictionary<string, object> Operation(string type,
                Dictionary<string, object> properties, params string[] required)
            {
                properties["type"] = new Dictionary<string, object>
                {
                    { "type", "string" },
                    { "description", "Discriminant for this asset transaction operation." },
                    { "enum", new List<object> { type } },
                };
                return StrictSchema(properties,
                    new[] { "type" }.Concat(required ?? new string[0]).ToArray());
            }

            var operations = new List<object>
            {
                Operation("ensure-folder", Props(
                    Prop("path", "string", "Folder path under Assets.")), "path"),
                Operation("copy", Props(
                    Prop("sourcePath", "string", "Existing source asset file."),
                    Prop("targetPath", "string", "New destination asset file.")),
                    "sourcePath", "targetPath"),
                Operation("move", Props(
                    Prop("sourcePath", "string", "Existing source asset file."),
                    Prop("targetPath", "string", "New destination asset file.")),
                    "sourcePath", "targetPath"),
                Operation("delete", Props(
                    Prop("path", "string", "Existing asset file to delete.")), "path"),
                Operation("serialized-set", Props(
                    Prop("assetPath", "string", "Serialized asset file to edit."),
                    Prop("assetType", "string", "Optional exact asset type used to load assetPath."),
                    Prop("propertyPath", "string", "Serialized property path to write."),
                    AnyJsonValueProp("value", "Serialized JSON value to assign."),
                    Prop("maxDepth", "number", "Maximum result value depth. Defaults to 3."),
                    Prop("maxArrayElements", "number", "Maximum returned array elements. Defaults to 50.")),
                    "assetPath", "propertyPath", "value"),
            };

            var referenceCheck = StrictSchema(Props(
                Prop("assetPath", "string", "Asset whose dependencies are verified after import."),
                ArrayProp("requiredDependencies", "string",
                    "Dependencies that must be present after the transaction.")),
                "assetPath", "requiredDependencies");
            var properties = Props(
                ArrayProp("operations", new Dictionary<string, object>
                {
                    { "oneOf", operations },
                }, "Ordered, strictly typed transaction operations."),
                ArrayProp("requiredAssets", "string",
                    "Assets or folders that must exist after import."),
                ArrayProp("referenceChecks", referenceCheck,
                    "Dependency postconditions verified after import."),
                Prop("dryRun", "boolean", "Validate and normalize without starting a Job."),
                Prop("idempotencyKey", "string",
                    "Optional caller-stable identity bound to the exact durable transaction request."));
            return StrictSchema(properties, "operations");
        }

        internal static Dictionary<string, object> AssetImportSchema()
        {
            var settingProperties = Props(
                Prop("overwrite", "boolean", "Replace an existing destination asset while preserving and restoring it if the batch rolls back. Defaults to false."),
                Prop("dedupeMode", "string", "Duplicate comparison: decodedPixels, fileBytes, or none. PNG/JPEG defaults to decodedPixels; other files default to none."),
                Prop("dedupeScope", "string", "Existing-asset search scope: assets (default), destinationFolder, or searchPath."),
                Prop("dedupeSearchPath", "string", "Folder under Assets/ used when dedupeScope is searchPath."),
                Prop("onDuplicate", "string", "Duplicate handling: skip (default), error, or report. report imports while returning duplicate metadata."),
                Prop("textureType", "string", "TextureImporterType such as Sprite or Default."),
                Prop("spriteMode", "string", "Sprite import mode: Single, Multiple, Polygon, or None."),
                Prop("pixelsPerUnit", "number", "Sprite pixels per unit."),
                Prop("filterMode", "string", "Texture filter mode: Point, Bilinear, or Trilinear."),
                Prop("isReadable", "boolean", "Enable CPU texture reads."),
                Prop("compression", "string", "Compression: uncompressed, low, normal, or high."),
                Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
                Prop("meshType", "string", "Sprite mesh type: FullRect or Tight."),
                Prop("mipmapEnabled", "boolean", "Generate mipmaps."));
            settingProperties["spriteSlice"] = new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", "Optional explicit fixed-grid sprite slicing applied after import. Use this for sparse animation frames instead of Unity automatic slicing." },
                { "properties", Props(
                    Prop("frameWidth", "number", "Required width of each grid frame in pixels."),
                    Prop("frameHeight", "number", "Required height of each grid frame in pixels."),
                    Prop("frameCount", "number", "Optional number of frames. Defaults to every full grid cell."),
                    Prop("baseName", "string", "Generated sprite-name prefix. Defaults to the imported file name."),
                    Prop("columns", "number", "Optional grid column count. Defaults to all full columns."),
                    Prop("startX", "number", "Grid start x in pixels. Defaults to 0."),
                    Prop("startY", "number", "Grid start y in top-left pixels. Defaults to 0."),
                    Prop("pivotX", "number", "Optional normalized pivot x. Must be supplied with pivotY."),
                    Prop("pivotY", "number", "Optional normalized pivot y. Must be supplied with pivotX."),
                    Prop("preserveSpriteIDs", "boolean", "Preserve existing sprite IDs by generated name when replacing an asset. Defaults to true.")
                ) },
                { "required", new List<string> { "frameWidth", "frameHeight" } },
                { "additionalProperties", false },
            };
            var importProperties = new Dictionary<string, object>(settingProperties)
            {
                ["sourcePath"] = Prop("sourcePath", "string", "Absolute external source file path.").Value,
                ["destinationPath"] = Prop("destinationPath", "string", "Destination Unity asset path under Assets/.").Value,
            };
            var properties = Props(
                Prop("dryRun", "boolean", "Validate every source, destination, collision, and importer setting without importing."));
            properties["defaults"] = new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", "Shared overwrite, duplicate detection, and TextureImporter settings inherited by every import item. Item fields override these defaults." },
                { "properties", settingProperties },
                { "additionalProperties", false },
            };
            properties["execution"] = ExecutionSchema();
            properties["imports"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Up to 500 import requests. Every item requires sourcePath and destinationPath. The full batch is preflighted before files are changed." },
                { "items", Schema(importProperties, "sourcePath", "destinationPath") },
                { "maxItems", 500 },
            };
            return Schema(properties, "imports");
        }

        internal static Dictionary<string, object> LocalizationUpsertEntriesSchema()
        {
            var entryProperties = Props(
                Prop("key", "string", "Shared localization key."),
                Prop("locale", "string", "Target Locale code."),
                Prop("value", "string", "String or Smart String value when type is string."),
                Prop("smart", "boolean", "Optional Smart String flag when type is string."),
                Prop("assetPath", "string", "Asset path when type is asset."),
                Prop("subAssetName", "string", "Optional exact sub-asset name at assetPath."));

            var properties = Props(
                Prop("collection", "string", "Table Collection name or GUID."),
                Prop("type", "string", "Collection type: string or asset. Defaults to string."),
                Prop("createTables", "boolean", "Create missing Locale tables. Defaults to true."));
            properties["execution"] = ExecutionSchema();
            properties["entries"] = new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", "Up to 500 Locale entry writes. The entire request is validated before changes are made." },
                { "items", Schema(entryProperties, "key", "locale") },
            };

            return Schema(properties, "collection", "entries");
        }

        internal static Dictionary<string, object> EditorWindowSchema(Dictionary<string, object> extraProps)
        {
            var props = Props(
                Prop("instanceId", "number", "EditorWindow instance id from uitoolkit/windows."),
                Prop("window", "string", "Window title, type name, full type name, or instance id."),
                Prop("windowType", "string", "EditorWindow type name or full type name."),
                Prop("title", "string", "EditorWindow title text.")
            );

            foreach (var pair in extraProps)
                props[pair.Key] = pair.Value;

            return Schema(props);
        }

        internal static Dictionary<string, object> RuntimeUIDocumentSchema(Dictionary<string, object> extraProps, params string[] required)
        {
            var props = Props(
                Prop("documentInstanceId", "number", "UIDocument instance id from uitoolkit/runtime-documents."),
                Prop("gameObjectPath", "string", "Scene GameObject path that owns the UIDocument."),
                Prop("gameObjectName", "string", "Scene GameObject name that owns the UIDocument."),
                Prop("documentName", "string", "UIDocument component name."),
                Prop("includeInactive", "boolean", "Include inactive scene UIDocuments. Defaults to true.")
            );

            foreach (var pair in extraProps)
                props[pair.Key] = pair.Value;

            return Schema(props, required);
        }

        internal static Dictionary<string, object> Schema(Dictionary<string, object> properties, params string[] required)
        {
            return WithJsonValueDefinition(ObjectSchema(properties, required));
        }

        internal static Dictionary<string, object> ObjectSchema(
            Dictionary<string, object> properties, params string[] required)
        {
            var schema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
                { "additionalProperties", false },
            };

            if (required != null && required.Length > 0)
                schema["required"] = required.ToList();

            return schema;
        }

        internal static Dictionary<string, object> WithJsonValueDefinition(
            Dictionary<string, object> schema)
        {
            if (schema == null)
                throw new System.ArgumentNullException(nameof(schema));
            schema["$defs"] = new Dictionary<string, object>
            {
                { "unityJsonValue", CreateJsonValueDefinition() },
            };
            return schema;
        }

        internal static Dictionary<string, object> StrictSchema(
            Dictionary<string, object> properties, params string[] required)
        {
            return Schema(properties, required);
        }

        internal static Dictionary<string, object> Props(params KeyValuePair<string, object>[] properties)
        {
            var result = new Dictionary<string, object>();
            foreach (var pair in properties)
                result[pair.Key] = pair.Value;
            return result;
        }

        internal static KeyValuePair<string, object> Prop(string name, string type, string description)
        {
            if (type == "object")
            {
                throw new System.InvalidOperationException(
                    $"Property '{name}' must use ObjectProp, JsonValueMapProp, or AnyJsonValueProp so its object contract is explicit.");
            }
            var schema = new Dictionary<string, object>
            {
                { "type", type },
                { "description", description },
            };
            return new KeyValuePair<string, object>(name, schema);
        }

        internal static KeyValuePair<string, object> ObjectProp(string name,
            string description, Dictionary<string, object> properties,
            params string[] required)
        {
            Dictionary<string, object> schema = ObjectSchema(properties, required);
            schema["description"] = description;
            return new KeyValuePair<string, object>(name, schema);
        }

        internal static KeyValuePair<string, object> Vector2Prop(
            string name, string description)
        {
            return ObjectProp(name, description, Props(
                Prop("x", "number", "X component."),
                Prop("y", "number", "Y component.")));
        }

        internal static KeyValuePair<string, object> Vector3Prop(
            string name, string description)
        {
            return ObjectProp(name, description, Props(
                Prop("x", "number", "X component."),
                Prop("y", "number", "Y component."),
                Prop("z", "number", "Z component.")));
        }

        internal static KeyValuePair<string, object> RectProp(
            string name, string description)
        {
            return ObjectProp(name, description, Props(
                Prop("x", "number", "Rectangle x coordinate."),
                Prop("y", "number", "Rectangle y coordinate."),
                Prop("width", "number", "Rectangle width."),
                Prop("height", "number", "Rectangle height.")));
        }

        internal static KeyValuePair<string, object> StringMapProp(
            string name, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", description },
                { "additionalProperties", new Dictionary<string, object>
                    {
                        { "type", "string" },
                    }
                },
            });
        }

        internal static KeyValuePair<string, object> OneOfProp(
            string name, string description, params Dictionary<string, object>[] variants)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "description", description },
                { "oneOf", variants.Cast<object>().ToList() },
            });
        }

        internal static KeyValuePair<string, object> EnumProp(string name,
            string description, params string[] values)
        {
            return new KeyValuePair<string, object>(name,
                new Dictionary<string, object>
                {
                    { "type", "string" },
                    { "description", description },
                    { "enum", values.Cast<object>().ToList() },
                });
        }

        internal static KeyValuePair<string, object> ArrayProp(
            string name,
            string itemType,
            string description)
        {
            if (itemType == "object")
            {
                throw new System.InvalidOperationException(
                    $"Array property '{name}' must provide an explicit item schema.");
            }
            var itemSchema = new Dictionary<string, object>
            {
                { "type", itemType },
            };
            return ArrayProp(name, itemSchema, description);
        }

        internal static KeyValuePair<string, object> ArrayProp(
            string name,
            Dictionary<string, object> itemSchema,
            string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", "array" },
                { "description", description },
                { "items", itemSchema },
            });
        }

        internal static KeyValuePair<string, object> AnyJsonValueProp(string name, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "$ref", JsonValueReference },
                { "description", description },
            });
        }

        internal static KeyValuePair<string, object> JsonValueMapProp(
            string name, string description)
        {
            return new KeyValuePair<string, object>(name, new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", description },
                { "additionalProperties", new Dictionary<string, object>
                    {
                        { "$ref", JsonValueReference },
                    }
                },
            });
        }

        private static Dictionary<string, object> CreateJsonValueDefinition()
        {
            Dictionary<string, object> Reference()
            {
                return new Dictionary<string, object> { { "$ref", JsonValueReference } };
            }

            return new Dictionary<string, object>
            {
                { "oneOf", new List<object>
                    {
                        new Dictionary<string, object> { { "type", "null" } },
                        new Dictionary<string, object> { { "type", "boolean" } },
                        new Dictionary<string, object> { { "type", "number" } },
                        new Dictionary<string, object> { { "type", "string" } },
                        new Dictionary<string, object>
                        {
                            { "type", "array" },
                            { "items", Reference() },
                        },
                        new Dictionary<string, object>
                        {
                            { "type", "object" },
                            { "properties", new Dictionary<string, object>() },
                            { "additionalProperties", Reference() },
                        },
                    }
                },
            };
        }
    }
}
