using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationToolInputSchemaComponents
    {
    internal static KeyValuePair<string, object> SpriteBorderProp(
        string name, string description)
    {
        Dictionary<string, object> array = new Dictionary<string, object>
        {
            { "type", "array" },
            { "items", new Dictionary<string, object> { { "type", "number" } } },
            { "minItems", 4 },
            { "maxItems", 4 },
        };
        return VmAutomationToolSchemaFactory.OneOfProp(name, description,
            new Dictionary<string, object> { { "type", "number" } },
            array,
            SpriteBorderObjectSchema("left", "bottom", "right", "top"));
    }

    internal static KeyValuePair<string, object> SpriteBorderObjectProp(
        string name, string description)
    {
        return VmAutomationToolSchemaFactory.OneOfProp(name, description,
            SpriteBorderObjectSchema("left", "bottom", "right", "top"),
            SpriteBorderObjectSchema("x", "y", "z", "w"));
    }

    internal static Dictionary<string, object> SpriteBorderObjectSchema(
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
        return VmAutomationToolSchemaFactory.ObjectSchema(properties, fields);
    }

    internal static KeyValuePair<string, object> AssetImportSettingsProp(
        string name, string description)
    {
        Dictionary<string, object> sampleSettings = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("loadType", "string", "AudioClipLoadType value."),
                VmAutomationToolSchemaFactory.Prop("compressionFormat", "string", "AudioCompressionFormat value."),
                VmAutomationToolSchemaFactory.Prop("quality", "number", "Audio compression quality."),
                VmAutomationToolSchemaFactory.Prop("sampleRateSetting", "string", "AudioSampleRateSetting value."),
                VmAutomationToolSchemaFactory.Prop("sampleRateOverride", "number", "Explicit sample rate override."),
                VmAutomationToolSchemaFactory.Prop("preloadAudioData", "boolean", "Preload decoded audio data.")));
        sampleSettings["description"] =
            "Default audio sample settings applied by the importer.";
        Dictionary<string, object> properties = VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop("userData", "string", "Importer user data."),
            VmAutomationToolSchemaFactory.Prop("assetBundleName", "string", "AssetBundle name."),
            VmAutomationToolSchemaFactory.Prop("assetBundleVariant", "string", "AssetBundle variant."),
            VmAutomationToolSchemaFactory.Prop("textureType", "string", "TextureImporterType value."),
            VmAutomationToolSchemaFactory.Prop("textureShape", "string", "TextureImporterShape value."),
            VmAutomationToolSchemaFactory.Prop("spriteImportMode", "string", "SpriteImportMode value."),
            VmAutomationToolSchemaFactory.Prop("spritePixelsPerUnit", "number", "Sprite pixels per unit."),
            VmAutomationToolSchemaFactory.Prop("sRGBTexture", "boolean", "Import as sRGB."),
            VmAutomationToolSchemaFactory.Prop("alphaSource", "string", "TextureImporterAlphaSource value."),
            VmAutomationToolSchemaFactory.Prop("alphaIsTransparency", "boolean", "Treat alpha as transparency."),
            VmAutomationToolSchemaFactory.Prop("mipmapEnabled", "boolean", "Generate mipmaps."),
            VmAutomationToolSchemaFactory.Prop("isReadable", "boolean", "Enable CPU read access."),
            VmAutomationToolSchemaFactory.Prop("streamingMipmaps", "boolean", "Enable mipmap streaming."),
            VmAutomationToolSchemaFactory.Prop("filterMode", "string", "FilterMode value."),
            VmAutomationToolSchemaFactory.Prop("anisoLevel", "number", "Anisotropic filtering level."),
            VmAutomationToolSchemaFactory.Prop("wrapMode", "string", "TextureWrapMode value."),
            VmAutomationToolSchemaFactory.Prop("wrapModeU", "string", "U-axis TextureWrapMode."),
            VmAutomationToolSchemaFactory.Prop("wrapModeV", "string", "V-axis TextureWrapMode."),
            VmAutomationToolSchemaFactory.Prop("wrapModeW", "string", "W-axis TextureWrapMode."),
            VmAutomationToolSchemaFactory.Prop("maxTextureSize", "number", "Maximum imported texture size."),
            VmAutomationToolSchemaFactory.Prop("textureCompression", "string", "TextureImporterCompression value."),
            VmAutomationToolSchemaFactory.Prop("compressionQuality", "number", "Texture compression quality."),
            VmAutomationToolSchemaFactory.Prop("crunchedCompression", "boolean", "Enable crunch compression."),
            VmAutomationToolSchemaFactory.Prop("npotScale", "string", "TextureImporterNPOTScale value."),
            VmAutomationToolSchemaFactory.Prop("globalScale", "number", "Model global scale."),
            VmAutomationToolSchemaFactory.Prop("useFileScale", "boolean", "Use model file scale."),
            VmAutomationToolSchemaFactory.Prop("importBlendShapes", "boolean", "Import model blend shapes."),
            VmAutomationToolSchemaFactory.Prop("importCameras", "boolean", "Import model cameras."),
            VmAutomationToolSchemaFactory.Prop("importLights", "boolean", "Import model lights."),
            VmAutomationToolSchemaFactory.Prop("importAnimation", "boolean", "Import model animation."),
            VmAutomationToolSchemaFactory.Prop("animationType", "string", "ModelImporterAnimationType value."),
            VmAutomationToolSchemaFactory.Prop("meshCompression", "string", "ModelImporterMeshCompression value."),
            VmAutomationToolSchemaFactory.Prop("addCollider", "boolean", "Generate model colliders."),
            VmAutomationToolSchemaFactory.Prop("keepQuads", "boolean", "Preserve model quads."),
            VmAutomationToolSchemaFactory.Prop("weldVertices", "boolean", "Weld model vertices."),
            VmAutomationToolSchemaFactory.Prop("indexFormat", "string", "Model index format."),
            VmAutomationToolSchemaFactory.Prop("importNormals", "string", "ModelImporterNormals value."),
            VmAutomationToolSchemaFactory.Prop("importTangents", "string", "ModelImporterTangents value."),
            VmAutomationToolSchemaFactory.Prop("forceToMono", "boolean", "Force audio to mono."),
            VmAutomationToolSchemaFactory.Prop("normalize", "boolean", "Normalize audio after forcing mono."),
            VmAutomationToolSchemaFactory.Prop("loadInBackground", "boolean", "Load audio in the background."),
            VmAutomationToolSchemaFactory.Prop("ambisonic", "boolean", "Import audio as ambisonic."),
            VmAutomationToolSchemaFactory.Prop("preloadAudioData", "boolean", "Preload audio data."));
        properties["defaultSampleSettings"] = sampleSettings;
        return VmAutomationToolSchemaFactory.ObjectProp(name, description, properties);
    }

    internal static KeyValuePair<string, object> AssetPlatformSettingsProp(
        string name, string description)
    {
        return VmAutomationToolSchemaFactory.ObjectProp(name, description,
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("overridden", "boolean", "Override platform texture settings."),
                VmAutomationToolSchemaFactory.Prop("maxTextureSize", "number", "Platform maximum texture size."),
                VmAutomationToolSchemaFactory.Prop("format", "string", "Platform texture format."),
                VmAutomationToolSchemaFactory.Prop("compressionQuality", "number", "Platform compression quality."),
                VmAutomationToolSchemaFactory.Prop("allowsAlphaSplitting", "boolean", "Allow platform alpha splitting."),
                VmAutomationToolSchemaFactory.Prop("loadType", "string", "Platform AudioClipLoadType value."),
                VmAutomationToolSchemaFactory.Prop("compressionFormat", "string", "Platform AudioCompressionFormat value."),
                VmAutomationToolSchemaFactory.Prop("quality", "number", "Platform audio quality."),
                VmAutomationToolSchemaFactory.Prop("sampleRateSetting", "string", "Platform AudioSampleRateSetting value."),
                VmAutomationToolSchemaFactory.Prop("sampleRateOverride", "number", "Platform sample rate override."),
                VmAutomationToolSchemaFactory.Prop("preloadAudioData", "boolean", "Preload platform audio data.")));
    }

    internal static KeyValuePair<string, object> MaterialPropertyMapProp(
        string name, string description)
    {
        Dictionary<string, object> number = new Dictionary<string, object>
            { { "type", "number" } };
        Dictionary<string, object> scalarWrapper = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("value", "number", "Wrapped numeric shader value.")),
            "value");
        Dictionary<string, object> color = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("r", "number", "Red component."),
                VmAutomationToolSchemaFactory.Prop("g", "number", "Green component."),
                VmAutomationToolSchemaFactory.Prop("b", "number", "Blue component."),
                VmAutomationToolSchemaFactory.Prop("a", "number", "Alpha component.")),
            "r", "g", "b");
        Dictionary<string, object> vector = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("x", "number", "X component."),
                VmAutomationToolSchemaFactory.Prop("y", "number", "Y component."),
                VmAutomationToolSchemaFactory.Prop("z", "number", "Z component."),
                VmAutomationToolSchemaFactory.Prop("w", "number", "W component.")),
            "x", "y", "z", "w");
        Dictionary<string, object> texture = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Texture asset path; empty clears the texture."),
                VmAutomationToolSchemaFactory.Vector2Prop("scale", "Texture scale."),
                VmAutomationToolSchemaFactory.Vector2Prop("offset", "Texture offset.")));
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

    internal static KeyValuePair<string, object> MaterialKeywordsProp(
        string name, string description)
    {
        return VmAutomationToolSchemaFactory.ObjectProp(name, description,
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.ArrayProp("enable", "string", "Keywords to enable."),
                VmAutomationToolSchemaFactory.ArrayProp("disable", "string", "Keywords to disable.")));
    }

    internal static Dictionary<string, object> DiscriminatedOperation(
        string type, Dictionary<string, object> properties,
        params string[] required)
    {
        properties["type"] = VmAutomationToolSchemaFactory.EnumProp("type",
            "Operation discriminator.", type).Value;
        return VmAutomationToolSchemaFactory.ObjectSchema(properties,
            new[] { "type" }.Concat(required ?? new string[0]).ToArray());
    }

    internal static Dictionary<string, object> DiscriminatedAction(
        string action, Dictionary<string, object> properties,
        params string[] required)
    {
        properties["action"] = VmAutomationToolSchemaFactory.EnumProp("action",
            "Operation action discriminator.", action).Value;
        return VmAutomationToolSchemaFactory.ObjectSchema(properties,
            new[] { "action" }.Concat(required ?? new string[0]).ToArray());
    }

    internal static Dictionary<string, object> OneOfOperations(
        params Dictionary<string, object>[] variants)
    {
        return new Dictionary<string, object>
        {
            { "oneOf", variants.Cast<object>().ToList() },
        };
    }

    internal static Dictionary<string, object> RequiredAlternative(
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

    internal static KeyValuePair<string, object> AudioMixerOperationArrayProp(
        string name, string description)
    {
        Dictionary<string, object> setGroupState = DiscriminatedAction(
            "set-group-state", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("groupLocalId", "string", "Target group local file ID."),
                VmAutomationToolSchemaFactory.Prop("mute", "boolean", "Set the group mute state."),
                VmAutomationToolSchemaFactory.Prop("solo", "boolean", "Set the group solo state."),
                VmAutomationToolSchemaFactory.Prop("bypassEffects", "boolean", "Set whether the group bypasses effects.")),
            "groupLocalId");
        setGroupState["anyOf"] = RequiredAlternative(
            "mute", "solo", "bypassEffects")["anyOf"];

        Dictionary<string, object> unexpose = DiscriminatedAction(
            "unexpose-parameter", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("guid", "string", "Exposed parameter GUID."),
                VmAutomationToolSchemaFactory.Prop("exposedName", "string", "Exposed parameter name.")));
        unexpose["anyOf"] = RequiredAlternative(
            "guid", "exposedName")["anyOf"];

        Dictionary<string, object> item = OneOfOperations(
            DiscriminatedAction("set-exposed-parameter",
                VmAutomationToolSchemaFactory.Props(
                    VmAutomationToolSchemaFactory.Prop("parameter", "string", "Exposed parameter name."),
                    VmAutomationToolSchemaFactory.Prop("value", "number", "Runtime exposed parameter value.")),
                "parameter", "value"),
            DiscriminatedAction("clear-exposed-parameter",
                VmAutomationToolSchemaFactory.Props(
                    VmAutomationToolSchemaFactory.Prop("parameter", "string", "Exposed parameter name.")),
                "parameter"),
            DiscriminatedAction("rename", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("targetLocalId", "string", "Mixer group, snapshot, or effect local file ID."),
                VmAutomationToolSchemaFactory.Prop("name", "string", "Replacement object name.")),
                "targetLocalId", "name"),
            DiscriminatedAction("create-group", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("name", "string", "New group name."),
                VmAutomationToolSchemaFactory.Prop("parentGroupLocalId", "string", "Optional parent group local file ID; defaults to the master group.")),
                "name"),
            DiscriminatedAction("remove-group", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("groupLocalId", "string", "Group local file ID.")),
                "groupLocalId"),
            setGroupState,
            DiscriminatedAction("create-snapshot", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("name", "string", "New snapshot name.")),
                "name"),
            DiscriminatedAction("remove-snapshot", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("snapshotLocalId", "string", "Snapshot local file ID.")),
                "snapshotLocalId"),
            DiscriminatedAction("set-target-snapshot", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("snapshotLocalId", "string", "Snapshot local file ID.")),
                "snapshotLocalId"),
            DiscriminatedAction("add-effect", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("groupLocalId", "string", "Target group local file ID."),
                VmAutomationToolSchemaFactory.Prop("effectName", "string", "Unity AudioMixer effect name."),
                VmAutomationToolSchemaFactory.Prop("index", "number", "Optional insertion index.")),
                "groupLocalId", "effectName"),
            DiscriminatedAction("remove-effect", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID.")),
                "effectLocalId"),
            DiscriminatedAction("set-effect-bypass", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID."),
                VmAutomationToolSchemaFactory.Prop("bypass", "boolean", "Requested effect bypass state.")),
                "effectLocalId", "bypass"),
            DiscriminatedAction("expose-effect-parameter", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID."),
                VmAutomationToolSchemaFactory.Prop("parameter", "string", "Effect parameter name."),
                VmAutomationToolSchemaFactory.Prop("exposedName", "string", "Optional exposed parameter name.")),
                "effectLocalId", "parameter"),
            unexpose,
            DiscriminatedAction("set-snapshot-parameter", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("effectLocalId", "string", "Effect local file ID."),
                VmAutomationToolSchemaFactory.Prop("parameter", "string", "Effect parameter name."),
                VmAutomationToolSchemaFactory.Prop("snapshotLocalId", "string", "Optional snapshot local file ID; defaults to the target snapshot."),
                VmAutomationToolSchemaFactory.Prop("value", "number", "Snapshot parameter value.")),
                "effectLocalId", "parameter", "value"),
            DiscriminatedAction("set-property", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("targetLocalId", "string", "Mixer group, snapshot, or effect local file ID."),
                VmAutomationToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path."),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Serialized JSON value to assign.")),
                "targetLocalId", "propertyPath", "value"));
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static Dictionary<string, object> BuildProfileSceneItemSchema()
    {
        return new Dictionary<string, object>
        {
            { "oneOf", new List<object>
                {
                    new Dictionary<string, object> { { "type", "string" } },
                    VmAutomationToolSchemaFactory.ObjectSchema(
                        VmAutomationToolSchemaFactory.Props(
                            VmAutomationToolSchemaFactory.Prop("path", "string", "Scene asset path."),
                            VmAutomationToolSchemaFactory.Prop("enabled", "boolean", "Whether the scene is enabled in the build.")),
                        "path"),
                }
            },
        };
    }

    internal static KeyValuePair<string, object> BuildProfileOperationArrayProp(
        string name, string description)
    {
        KeyValuePair<string, object> Scenes(string field) =>
            VmAutomationToolSchemaFactory.ArrayProp(field, BuildProfileSceneItemSchema(),
                "Ordered scene asset paths or path/enabled objects.");
        Dictionary<string, object> item = OneOfOperations(
            DiscriminatedAction("set-active", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path.")),
                "assetPath"),
            DiscriminatedAction("set-scenes", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path."),
                Scenes("scenes"),
                VmAutomationToolSchemaFactory.Prop("overrideGlobalScenes", "boolean", "Whether this profile overrides global scenes. Defaults to true.")),
                "assetPath", "scenes"),
            DiscriminatedAction("set-scripting-defines", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path."),
                VmAutomationToolSchemaFactory.ArrayProp("defines", "string", "Complete scripting define list.")),
                "assetPath", "defines"),
            DiscriminatedAction("set-global-scenes", VmAutomationToolSchemaFactory.Props(
                Scenes("scenes")), "scenes"),
            DiscriminatedAction("set-property", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("assetPath", "string", "BuildProfile asset path."),
                VmAutomationToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path."),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Serialized JSON value to assign.")),
                "assetPath", "propertyPath", "value"));
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static Dictionary<string, object> AddressablesEntryProperties()
    {
        return VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop("guid", "string", "Asset GUID."),
            VmAutomationToolSchemaFactory.Prop("assetPath", "string", "Asset path resolved to a GUID."));
    }

    internal static Dictionary<string, object> RequireAddressablesEntrySelector(
        Dictionary<string, object> schema)
    {
        schema["anyOf"] = RequiredAlternative("guid", "assetPath")["anyOf"];
        return schema;
    }

    internal static KeyValuePair<string, object> AddressablesOperationArrayProp(
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
            DiscriminatedAction("create-group", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("group", "string", "New group name."),
                VmAutomationToolSchemaFactory.Prop("setAsDefault", "boolean", "Set the new group as default."),
                VmAutomationToolSchemaFactory.Prop("copySchemas", "boolean", "Copy schemas to the new group. Defaults to true."),
                VmAutomationToolSchemaFactory.Prop("copySchemasFromGroup", "string", "Optional schema source group.")),
                "group"),
            DiscriminatedAction("remove-group", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("group", "string", "Existing group name.")),
                "group"),
            DiscriminatedAction("set-default-group", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("group", "string", "Existing group name.")),
                "group"),
            DiscriminatedAction("add-label", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("label", "string", "Label to add.")),
                "label"),
            DiscriminatedAction("remove-label", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("label", "string", "Label to remove.")),
                "label"),
            DiscriminatedAction("rename-label", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("oldLabel", "string", "Existing label."),
                VmAutomationToolSchemaFactory.Prop("newLabel", "string", "Replacement label.")),
                "oldLabel", "newLabel"),
            EntryOperation("create-or-move-entry", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("group", "string", "Target group; defaults to the default group."),
                VmAutomationToolSchemaFactory.Prop("address", "string", "Optional address override."))),
            EntryOperation("set-address", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("address", "string", "Replacement address.")),
                "address"),
            EntryOperation("set-label", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("label", "string", "Existing label."),
                VmAutomationToolSchemaFactory.Prop("enabled", "boolean", "Whether the label is assigned. Defaults to true.")),
                "label"),
            EntryOperation("remove-entry", VmAutomationToolSchemaFactory.Props()));
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static Dictionary<string, object> TimelineClipProperties()
    {
        return VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop("displayName", "string", "Timeline clip display name."),
            VmAutomationToolSchemaFactory.Prop("start", "number", "Clip start time."),
            VmAutomationToolSchemaFactory.Prop("duration", "number", "Clip duration."),
            VmAutomationToolSchemaFactory.Prop("clipIn", "number", "Clip source offset."),
            VmAutomationToolSchemaFactory.Prop("timeScale", "number", "Clip playback time scale."),
            VmAutomationToolSchemaFactory.Prop("easeInDuration", "number", "Clip ease-in duration."),
            VmAutomationToolSchemaFactory.Prop("easeOutDuration", "number", "Clip ease-out duration."));
    }

    internal static KeyValuePair<string, object> TimelineOperationArrayProp(
        string name, string description)
    {
        Dictionary<string, object> ClipOperationProperties(
            bool includeAssetType, bool includeIndex)
        {
            Dictionary<string, object> properties = VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."));
            if (includeAssetType)
                properties["clipAssetType"] = VmAutomationToolSchemaFactory.Prop(
                    "clipAssetType", "string", "PlayableAsset type name or full name.").Value;
            if (includeIndex)
                properties["clipIndex"] = VmAutomationToolSchemaFactory.Prop(
                    "clipIndex", "number", "Zero-based clip index.").Value;
            foreach (KeyValuePair<string, object> property in TimelineClipProperties())
                properties[property.Key] = property.Value;
            return properties;
        }

        Dictionary<string, object> item = OneOfOperations(
            DiscriminatedAction("create-track", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("trackType", "string", "TrackAsset type name or full name."),
                VmAutomationToolSchemaFactory.Prop("name", "string", "Optional track name."),
                VmAutomationToolSchemaFactory.Prop("parentTrackLocalId", "string", "Optional parent track local file ID.")),
                "trackType"),
            DiscriminatedAction("delete-track", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID.")),
                "trackLocalId"),
            DiscriminatedAction("rename-track", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."),
                VmAutomationToolSchemaFactory.Prop("name", "string", "Replacement track name.")),
                "trackLocalId", "name"),
            DiscriminatedAction("set-track-property", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."),
                VmAutomationToolSchemaFactory.Prop("propertyPath", "string", "Serialized property path."),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Serialized JSON value to assign.")),
                "trackLocalId", "propertyPath", "value"),
            DiscriminatedAction("create-clip",
                ClipOperationProperties(includeAssetType: true, includeIndex: false),
                "trackLocalId", "clipAssetType"),
            DiscriminatedAction("delete-clip", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("trackLocalId", "string", "Track local file ID."),
                VmAutomationToolSchemaFactory.Prop("clipIndex", "number", "Zero-based clip index.")),
                "trackLocalId", "clipIndex"),
            DiscriminatedAction("set-clip",
                ClipOperationProperties(includeAssetType: false, includeIndex: true),
                "trackLocalId", "clipIndex"));
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static Dictionary<string, object> CinemachineSelectorProperties()
    {
        return VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop("path", "string", "GameObject path or exact name."),
            VmAutomationToolSchemaFactory.Prop("gameObjectPath", "string", "Legacy GameObject path alias."),
            VmAutomationToolSchemaFactory.Prop("scenePath", "string", "Optional loaded scene asset path."),
            VmAutomationToolSchemaFactory.Prop("instanceId", "number", "Loaded scene GameObject instance ID."));
    }

    internal static Dictionary<string, object> RequireCinemachineSelector(
        Dictionary<string, object> schema)
    {
        schema["anyOf"] = RequiredAlternative(
            "path", "gameObjectPath", "instanceId")["anyOf"];
        return schema;
    }

    internal static KeyValuePair<string, object> CinemachineOperationArrayProp(
        string name, string description)
    {
        Dictionary<string, object> Common()
        {
            Dictionary<string, object> properties = CinemachineSelectorProperties();
            properties["componentType"] = VmAutomationToolSchemaFactory.Prop(
                "componentType", "string", "Cinemachine component short, full, or assembly-qualified type name; optional when exactly one matches.").Value;
            properties["componentIndex"] = VmAutomationToolSchemaFactory.Prop(
                "componentIndex", "number", "Zero-based component index. Defaults to 0.").Value;
            return properties;
        }

        Dictionary<string, object> setEnabled = RequireCinemachineSelector(
            DiscriminatedAction("set-enabled", Common(), "enabled"));
        RequireProperties(setEnabled)["enabled"] = VmAutomationToolSchemaFactory.Prop(
            "enabled", "boolean", "Requested Behaviour enabled state.").Value;

        Dictionary<string, object> setPropertyProperties = Common();
        setPropertyProperties["propertyPath"] = VmAutomationToolSchemaFactory.Prop(
            "propertyPath", "string", "Serialized property path.").Value;
        setPropertyProperties["value"] = VmAutomationToolSchemaFactory.AnyJsonValueProp(
            "value", "Serialized JSON value to assign.").Value;
        Dictionary<string, object> setProperty = RequireCinemachineSelector(
            DiscriminatedAction("set-property", setPropertyProperties,
                "propertyPath", "value"));

        Dictionary<string, object> target = RequireCinemachineSelector(
            VmAutomationToolSchemaFactory.ObjectSchema(CinemachineSelectorProperties()));
        target["description"] = "Target GameObject selector for an object-reference assignment.";

        Dictionary<string, object> ReferenceProperties()
        {
            Dictionary<string, object> properties = Common();
            properties["propertyPath"] = VmAutomationToolSchemaFactory.Prop(
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
            properties["targetKind"] = VmAutomationToolSchemaFactory.EnumProp(
                "targetKind", targetKind == "transform"
                    ? "Assigned target kind. Omit to use the default transform target."
                    : "Assigned target kind.", targetKind).Value;
            var required = new List<string> { "propertyPath", "target" };
            if (requireTargetKind)
                required.Add("targetKind");
            if (includeComponentSelector)
            {
                properties["targetComponentType"] = VmAutomationToolSchemaFactory.Prop(
                    "targetComponentType", "string", "Target component short, full, or assembly-qualified type name.").Value;
                properties["targetComponentIndex"] = VmAutomationToolSchemaFactory.Prop(
                    "targetComponentIndex", "number", "Zero-based target component index. Defaults to 0.").Value;
                required.Add("targetComponentType");
            }
            return RequireCinemachineSelector(DiscriminatedAction(
                "set-object-reference", properties, required.ToArray()));
        }

        return VmAutomationToolSchemaFactory.ArrayProp(name,
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

    internal static Dictionary<string, object> RequireProperties(
        Dictionary<string, object> schema)
    {
        return (Dictionary<string, object>)schema["properties"];
    }

    internal static KeyValuePair<string, object> VFXGraphOperationArrayProp()
    {
        var definitions = VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop("catalogId", "string", "Exact catalog ID."),
            VmAutomationToolSchemaFactory.Prop("kind", "string", "Model kind."),
            VmAutomationToolSchemaFactory.Prop("nodeId", "string", "Exact model local ID or earlier alias."),
            VmAutomationToolSchemaFactory.Prop("dataObjectId", "string", "Exact VFX data object local ID returned by vfxgraph/info."),
            VmAutomationToolSchemaFactory.Prop("parameterId", "string", "Exact parameter definition ID or earlier alias."),
            VmAutomationToolSchemaFactory.Prop("parameterNodeId", "string", "Exact <parameterId>:<nodeId> occurrence ID or earlier occurrence alias."),
            VmAutomationToolSchemaFactory.Prop("parentContextId", "string", "Parent VFX context ID or alias."),
            VmAutomationToolSchemaFactory.Prop("fromNodeId", "string", "Source node or parameter occurrence ID."),
            VmAutomationToolSchemaFactory.Prop("toNodeId", "string", "Destination node or parameter occurrence ID."),
            VmAutomationToolSchemaFactory.Prop("fromContextId", "string", "Source context ID."),
            VmAutomationToolSchemaFactory.Prop("toContextId", "string", "Destination context ID."),
            VmAutomationToolSchemaFactory.Prop("fromSlot", "string", "Exact output slot selector returned by catalog or info."),
            VmAutomationToolSchemaFactory.Prop("toSlot", "string", "Exact input slot selector returned by catalog or info."),
            VmAutomationToolSchemaFactory.Prop("direction", "string", "Slot direction: input or output."),
            VmAutomationToolSchemaFactory.Prop("slotPath", "string", "Exact slot selector returned by catalog or info."),
            VmAutomationToolSchemaFactory.Prop("fromIndex", "number", "Source flow slot index."),
            VmAutomationToolSchemaFactory.Prop("toIndex", "number", "Destination flow slot index."),
            VmAutomationToolSchemaFactory.Prop("index", "number", "Insertion or ordering index."),
            VmAutomationToolSchemaFactory.Prop("alias", "string", "Request-local alias for the created model or occurrence."),
            VmAutomationToolSchemaFactory.Prop("name", "string", "Semantic name."),
            VmAutomationToolSchemaFactory.Prop("category", "string", "Parameter category."),
            VmAutomationToolSchemaFactory.Prop("categoryName", "string", "Exact category name selector."),
            VmAutomationToolSchemaFactory.Prop("categoryIndex", "number", "Exact category index selector."),
            VmAutomationToolSchemaFactory.Prop("attributeName", "string", "Exact custom attribute name."),
            VmAutomationToolSchemaFactory.Prop("valueType", "string", "VFX value type or parameter type."),
            VmAutomationToolSchemaFactory.Prop("description", "string", "Description text."),
            VmAutomationToolSchemaFactory.Prop("tooltip", "string", "Parameter tooltip."),
            VmAutomationToolSchemaFactory.Prop("order", "number", "Parameter order."),
            VmAutomationToolSchemaFactory.Prop("exposed", "boolean", "Expose the parameter."),
            VmAutomationToolSchemaFactory.Prop("isOutput", "boolean", "Use an output parameter."),
            VmAutomationToolSchemaFactory.Prop("collapsed", "boolean", "Collapsed state."),
            VmAutomationToolSchemaFactory.Prop("superCollapsed", "boolean", "Super-collapsed state."),
            VmAutomationToolSchemaFactory.Prop("expanded", "boolean", "Parameter occurrence expanded state."),
            VmAutomationToolSchemaFactory.Prop("enabled", "boolean", "Block enabled state."),
            VmAutomationToolSchemaFactory.Prop("removeUsages", "boolean", "Explicitly remove models using a custom attribute."),
            VmAutomationToolSchemaFactory.Prop("parameterDisposition", "string", "Category parameter disposition: uncategorize or delete."),
            VmAutomationToolSchemaFactory.Prop("valueFilter", "string", "Parameter value filter: Default, Range, or Enum."),
            VmAutomationToolSchemaFactory.ArrayProp("enumValues", "string", "Parameter enum labels."),
            VmAutomationToolSchemaFactory.ArrayProp("contents", "string", "Group model IDs or sticky:<index> selectors."),
            VmAutomationToolSchemaFactory.Prop("title", "string", "Group or sticky-note title."),
            VmAutomationToolSchemaFactory.Prop("theme", "string", "Sticky-note theme."),
            VmAutomationToolSchemaFactory.Prop("textSize", "string", "Sticky-note text size."),
            VmAutomationToolSchemaFactory.Prop("colorTheme", "number", "Sticky-note color theme index."),
            VmAutomationToolSchemaFactory.Prop("groupIndex", "number", "Exact group index."),
            VmAutomationToolSchemaFactory.Prop("stickyNoteIndex", "number", "Exact sticky-note index."),
            VmAutomationToolSchemaFactory.Prop("settingName", "string", "Graph setting name."),
            VmAutomationToolSchemaFactory.Vector2Prop("position", "Graph position."),
            VmAutomationToolSchemaFactory.RectProp("bounds", "Graph UI bounds."),
            VmAutomationToolSchemaFactory.JsonValueMapProp("settings", "Typed VFX model settings."),
            VmAutomationToolSchemaFactory.JsonValueMapProp("slots", "Initial input slot values by exact path."),
            VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Typed value."),
            VmAutomationToolSchemaFactory.AnyJsonValueProp("space", "VFX coordinate space enum value."),
            VmAutomationToolSchemaFactory.AnyJsonValueProp("min", "Parameter range minimum."),
            VmAutomationToolSchemaFactory.AnyJsonValueProp("max", "Parameter range maximum."));

        Dictionary<string, object> Operation(string op, string[] fields,
            params string[] required)
        {
            var properties = new Dictionary<string, object>
            {
                ["op"] = VmAutomationToolSchemaFactory.EnumProp("op",
                    "VFX graph operation discriminator.", op).Value,
            };
            foreach (string field in fields)
                properties[field] = definitions[field];
            return VmAutomationToolSchemaFactory.ObjectSchema(properties,
                new[] { "op" }.Concat(required).ToArray());
        }

        var variants = new List<object>
        {
            Operation("add-node", new[] { "catalogId", "kind", "parentContextId", "index", "position", "collapsed", "superCollapsed", "enabled", "settings", "slots", "alias" }, "catalogId", "kind"),
            Operation("remove-node", new[] { "nodeId", "parameterNodeId" }),
            Operation("set-node", new[] { "nodeId", "position", "collapsed", "superCollapsed", "enabled", "name", "settings" }, "nodeId"),
            Operation("set-data-object", new[] { "dataObjectId", "space", "settings" }, "dataObjectId"),
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
        return VmAutomationToolSchemaFactory.ArrayProp("operations",
            new Dictionary<string, object> { { "oneOf", variants } },
            "Atomic ordered semantic VFX graph operations.");
    }

    internal static KeyValuePair<string, object> VFXComponentOperationArrayProp()
    {
        Dictionary<string, object> Operation(string op,
            Dictionary<string, object> properties, params string[] required)
        {
            properties["op"] = VmAutomationToolSchemaFactory.EnumProp("op",
                "VFX component operation discriminator.", op).Value;
            return VmAutomationToolSchemaFactory.ObjectSchema(properties,
                new[] { "op" }.Concat(required).ToArray());
        }
        var variants = new List<object>
        {
            Operation("set-asset", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("assetPath", "string", "VisualEffectAsset path."),
                VmAutomationToolSchemaFactory.Prop("clear", "boolean", "Clear the assigned asset."))),
            Operation("set-enabled", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("value", "boolean", "Enabled state.")), "value"),
            Operation("set-seed", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("startSeed", "number", "Start seed."),
                VmAutomationToolSchemaFactory.Prop("resetSeedOnPlay", "boolean", "Reset seed when playing."))),
            Operation("set-initial-event", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("name", "string", "Initial event name.")), "name"),
            Operation("set-rendering", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Documented persistent rendering property."),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Typed property value.")), "propertyName", "value"),
            Operation("set-override", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Exact exposed property name."),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Typed exposed value.")), "propertyName", "value"),
            Operation("reset-override", VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("propertyName", "string", "Exact exposed property name.")), "propertyName"),
        };
        return VmAutomationToolSchemaFactory.ArrayProp("operations",
            new Dictionary<string, object> { { "oneOf", variants } },
            "Ordered persistent VisualEffect component operations.");
    }

    internal static KeyValuePair<string, object> VFXEventAttributeArrayProp()
    {
        Dictionary<string, object> item = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("name", "string", "Event attribute name."),
                VmAutomationToolSchemaFactory.EnumProp("type", "Event attribute value type.", "bool", "int", "uint", "float", "vector2", "vector3", "vector4", "matrix4x4"),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Typed event attribute value.")),
            "name", "type", "value");
        return VmAutomationToolSchemaFactory.ArrayProp("eventAttributes", item,
            "Typed attributes attached to send-event.");
    }

    internal static KeyValuePair<string, object> VFXSettingsOperationArrayProp()
    {
        Dictionary<string, object> item = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.EnumProp("scope", "Settings owner.", "project", "user"),
                VmAutomationToolSchemaFactory.Prop("name", "string", "Documented VFX setting name."),
                VmAutomationToolSchemaFactory.AnyJsonValueProp("value", "Typed setting value."),
                VmAutomationToolSchemaFactory.EnumProp("reimport", "Explicit graph recompilation policy.", "none", "all")),
            "scope", "name", "value");
        return VmAutomationToolSchemaFactory.ArrayProp("operations", item,
            "Ordered project and per-user VFX settings changes.");
    }

    internal static Dictionary<string, object> UxmlOperationItemSchema()
    {
        Dictionary<string, object> TargetProperties()
        {
            return VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path."),
                VmAutomationToolSchemaFactory.Prop("name", "string", "Exact UXML name attribute."));
        }

        var variants = new List<object>();
        variants.Add(DiscriminatedOperation("add-element",
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("parentPath", "string", "Parent element tree path."),
                VmAutomationToolSchemaFactory.Prop("parentName", "string", "Exact parent UXML name attribute."),
                VmAutomationToolSchemaFactory.Prop("elementType", "string", "UXML element type. Defaults to VisualElement."),
                VmAutomationToolSchemaFactory.StringMapProp("attributes", "Initial UXML attributes."),
                VmAutomationToolSchemaFactory.Prop("index", "number", "Optional child insertion index."))));
        foreach (string type in new[] { "remove-element", "remove-attribute", "add-class", "remove-class", "set-text" })
        {
            Dictionary<string, object> properties = TargetProperties();
            if (type == "remove-attribute")
                properties["attribute"] = VmAutomationToolSchemaFactory.Prop("attribute", "string", "Attribute name to remove.").Value;
            else if (type == "add-class" || type == "remove-class")
                properties["className"] = VmAutomationToolSchemaFactory.Prop("className", "string", "USS class name.").Value;
            else if (type == "set-text")
                properties["text"] = VmAutomationToolSchemaFactory.Prop("text", "string", "Replacement text attribute.").Value;
            string required = type == "remove-attribute" ? "attribute" :
                type == "add-class" || type == "remove-class" ? "className" : null;
            variants.Add(DiscriminatedOperation(type, properties,
                required == null ? new string[0] : new[] { required }));
        }
        Dictionary<string, object> move = TargetProperties();
        move["parentPath"] = VmAutomationToolSchemaFactory.Prop("parentPath", "string", "New parent tree path.").Value;
        move["parentName"] = VmAutomationToolSchemaFactory.Prop("parentName", "string", "Exact new parent UXML name attribute.").Value;
        move["index"] = VmAutomationToolSchemaFactory.Prop("index", "number", "Optional child insertion index.").Value;
        variants.Add(DiscriminatedOperation("move-element", move));
        Dictionary<string, object> setAttribute = TargetProperties();
        setAttribute["attribute"] = VmAutomationToolSchemaFactory.Prop("attribute", "string", "Attribute name to set.").Value;
        setAttribute["value"] = VmAutomationToolSchemaFactory.Prop("value", "string", "Attribute value.").Value;
        variants.Add(DiscriminatedOperation("set-attribute", setAttribute, "attribute"));
        return new Dictionary<string, object> { { "oneOf", variants } };
    }

    internal static Dictionary<string, object> UssOperationItemSchema()
    {
        Dictionary<string, object> Common()
        {
            return VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("selector", "string", "Exact USS selector."));
        }

        var variants = new List<object>();
        variants.Add(DiscriminatedOperation("remove-selector", Common(), "selector"));
        Dictionary<string, object> upsert = Common();
        upsert["declarations"] = VmAutomationToolSchemaFactory.StringMapProp(
            "declarations", "Complete selector declarations.").Value;
        variants.Add(DiscriminatedOperation("upsert-selector", upsert,
            "selector", "declarations"));
        foreach (string type in new[] { "set-declaration", "remove-declaration" })
        {
            Dictionary<string, object> properties = Common();
            properties["property"] = VmAutomationToolSchemaFactory.Prop("property", "string", "USS property name.").Value;
            if (type == "set-declaration")
                properties["value"] = VmAutomationToolSchemaFactory.Prop("value", "string", "USS property value.").Value;
            variants.Add(DiscriminatedOperation(type, properties, "selector", "property"));
        }
        return new Dictionary<string, object> { { "oneOf", variants } };
    }

    internal static KeyValuePair<string, object> UxmlOperationArrayProp(
        string name, string description)
    {
        return VmAutomationToolSchemaFactory.ArrayProp(name, UxmlOperationItemSchema(), description);
    }

    internal static KeyValuePair<string, object> UssOperationArrayProp(
        string name, string description)
    {
        return VmAutomationToolSchemaFactory.ArrayProp(name, UssOperationItemSchema(), description);
    }

    internal static KeyValuePair<string, object> UIAuthoringEditArrayProp(
        string name, string description)
    {
        Dictionary<string, object> Edit(string kind,
            Dictionary<string, object> operationItem)
        {
            return VmAutomationToolSchemaFactory.ObjectSchema(
                VmAutomationToolSchemaFactory.Props(
                    VmAutomationToolSchemaFactory.EnumProp("kind", "Authoring edit kind.", kind),
                    VmAutomationToolSchemaFactory.Prop("assetPath", "string", "UXML or USS asset path."),
                    VmAutomationToolSchemaFactory.ArrayProp("operations", operationItem,
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
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static KeyValuePair<string, object> AnimatorConditionArrayProp(
        string name, string description)
    {
        Dictionary<string, object> item = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("parameter", "string", "Animator parameter name."),
                VmAutomationToolSchemaFactory.Prop("mode", "string", "AnimatorConditionMode value."),
                VmAutomationToolSchemaFactory.Prop("threshold", "number", "Condition threshold.")));
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static Dictionary<string, object> UIToolkitLocatorProperties(
        string prefix = "")
    {
        string Prefix(string suffix) => string.IsNullOrEmpty(prefix)
            ? char.ToLowerInvariant(suffix[0]) + suffix.Substring(1)
            : prefix + suffix;
        return VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop(Prefix("Path"), "string", "Element tree path."),
            VmAutomationToolSchemaFactory.Prop(Prefix("ElementPath"), "string", "Legacy element tree path alias."),
            VmAutomationToolSchemaFactory.Prop(Prefix("VisualElementPath"), "string", "Slash-separated VisualElementPath names."),
            VmAutomationToolSchemaFactory.ArrayProp(Prefix("VisualElementNames"), "string", "VisualElementPath names."),
            VmAutomationToolSchemaFactory.Prop(Prefix("Name"), "string", "VisualElement.name exact match."),
            VmAutomationToolSchemaFactory.Prop(Prefix("ClassName"), "string", "USS class exact match."),
            VmAutomationToolSchemaFactory.Prop(Prefix("TypeName"), "string", "VisualElement type-name match."),
            VmAutomationToolSchemaFactory.Prop(Prefix("Text"), "string", "TextElement text match."));
    }

    internal static KeyValuePair<string, object> UIToolkitQueryArrayProp(
        string name, string description)
    {
        Dictionary<string, object> properties = UIToolkitLocatorProperties();
        properties["pixelScale"] = VmAutomationToolSchemaFactory.Prop("pixelScale", "number",
            "Pixel grid scale for diagnostics.").Value;
        return VmAutomationToolSchemaFactory.ArrayProp(name,
            VmAutomationToolSchemaFactory.ObjectSchema(properties), description);
    }

    internal static KeyValuePair<string, object> UIToolkitVisualCheckArrayProp(
        string name, string description)
    {
        Dictionary<string, object> properties = UIToolkitLocatorProperties();
        foreach (KeyValuePair<string, object> property in VmAutomationToolSchemaFactory.Props(
                     VmAutomationToolSchemaFactory.Prop("type", "string", "Visual check type."),
                     VmAutomationToolSchemaFactory.Prop("kind", "string", "Legacy visual check type alias."),
                     VmAutomationToolSchemaFactory.Prop("pixelScale", "number", "Pixel grid scale."),
                     VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Allowed numeric delta."),
                     VmAutomationToolSchemaFactory.Prop("expectedScale", "number", "Expected background scale."),
                     VmAutomationToolSchemaFactory.Prop("scale", "number", "Legacy expected background scale alias."),
                     VmAutomationToolSchemaFactory.Prop("width", "number", "Expected element width."),
                     VmAutomationToolSchemaFactory.Prop("height", "number", "Expected element height."),
                     VmAutomationToolSchemaFactory.Prop("expectedWidth", "number", "Expected element width alias."),
                     VmAutomationToolSchemaFactory.Prop("expectedHeight", "number", "Expected element height alias.")))
            properties[property.Key] = property.Value;
        return VmAutomationToolSchemaFactory.ArrayProp(name,
            VmAutomationToolSchemaFactory.ObjectSchema(properties), description);
    }

    internal static KeyValuePair<string, object> UIToolkitResourceQueryArrayProp(
        string name, string description)
    {
        Dictionary<string, object> properties = UIToolkitLocatorProperties();
        foreach (KeyValuePair<string, object> property in VmAutomationToolSchemaFactory.Props(
                     VmAutomationToolSchemaFactory.Prop("expectedBackgroundContains", "string", "Required background reference substring."),
                     VmAutomationToolSchemaFactory.ArrayProp("forbiddenBackgroundContains", "string", "Forbidden background reference substrings."),
                     VmAutomationToolSchemaFactory.Prop("requireBackground", "boolean", "Require a resolved background image.")))
            properties[property.Key] = property.Value;
        return VmAutomationToolSchemaFactory.ArrayProp(name,
            VmAutomationToolSchemaFactory.ObjectSchema(properties), description);
    }

    internal static KeyValuePair<string, object> UIToolkitLayoutAssertionArrayProp(
        string name, string description)
    {
        Dictionary<string, object> properties = UIToolkitAssertionLocatorProperties();
        foreach (string prefix in new[] { "first", "second", "inner", "outer" })
        foreach (KeyValuePair<string, object> property in
                 UIToolkitAssertionLocatorProperties(prefix))
            properties[property.Key] = property.Value;
        foreach (KeyValuePair<string, object> property in VmAutomationToolSchemaFactory.Props(
                     VmAutomationToolSchemaFactory.Prop("type", "string", "Layout assertion type."),
                     VmAutomationToolSchemaFactory.Prop("kind", "string", "Legacy layout assertion type alias."),
                     VmAutomationToolSchemaFactory.Prop("axis", "string", "Comparison axis: x or y."),
                     VmAutomationToolSchemaFactory.Prop("edge", "string", "Shared edge for alignment."),
                     VmAutomationToolSchemaFactory.Prop("firstEdge", "string", "First element edge."),
                     VmAutomationToolSchemaFactory.Prop("secondEdge", "string", "Second element edge."),
                     VmAutomationToolSchemaFactory.Prop("tolerance", "number", "Allowed layout delta."),
                     VmAutomationToolSchemaFactory.Prop("width", "number", "Expected width."),
                     VmAutomationToolSchemaFactory.Prop("height", "number", "Expected height."),
                     VmAutomationToolSchemaFactory.Prop("expectedWidth", "number", "Expected width alias."),
                     VmAutomationToolSchemaFactory.Prop("expectedHeight", "number", "Expected height alias.")))
            properties[property.Key] = property.Value;
        return VmAutomationToolSchemaFactory.ArrayProp(name,
            VmAutomationToolSchemaFactory.ObjectSchema(properties), description);
    }

    internal static Dictionary<string, object> UIToolkitAssertionLocatorProperties(
        string prefix = "")
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("path", "string", "Element tree path."),
                VmAutomationToolSchemaFactory.Prop("elementPath", "string", "Legacy element tree path alias."),
                VmAutomationToolSchemaFactory.Prop("visualElementPath", "string", "Slash-separated VisualElementPath names."),
                VmAutomationToolSchemaFactory.ArrayProp("visualElementNames", "string", "VisualElementPath names."),
                VmAutomationToolSchemaFactory.Prop("name", "string", "VisualElement.name exact match."));
        }

        return VmAutomationToolSchemaFactory.Props(
            VmAutomationToolSchemaFactory.Prop(prefix + "Path", "string", "Prefixed element tree path."),
            VmAutomationToolSchemaFactory.Prop(prefix + "VisualElementPath", "string", "Prefixed slash-separated VisualElementPath names."),
            VmAutomationToolSchemaFactory.ArrayProp(prefix + "Names", "string", "Prefixed VisualElementPath names."),
            VmAutomationToolSchemaFactory.Prop(prefix + "Name", "string", "Prefixed VisualElement.name exact match."));
    }

    internal static KeyValuePair<string, object> AnnotationRectArrayProp(
        string name, string description)
    {
        Dictionary<string, object> item = VmAutomationToolSchemaFactory.ObjectSchema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("x", "number", "Rectangle x coordinate."),
                VmAutomationToolSchemaFactory.Prop("y", "number", "Rectangle y coordinate."),
                VmAutomationToolSchemaFactory.Prop("width", "number", "Rectangle width."),
                VmAutomationToolSchemaFactory.Prop("height", "number", "Rectangle height."),
                VmAutomationToolSchemaFactory.Prop("color", "string", "Optional HTML border color."),
                VmAutomationToolSchemaFactory.Prop("thickness", "number", "Optional border thickness.")),
            "x", "y", "width", "height");
        return VmAutomationToolSchemaFactory.ArrayProp(name, item, description);
    }

    internal static KeyValuePair<string, object> GitPackageExpectationArrayProp()
    {
        Dictionary<string, object> item = VmAutomationToolSchemaFactory.Schema(
            VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("name", "string", "Git package name."),
                VmAutomationToolSchemaFactory.Prop("gitUrl", "string", "Optional Git URL. Defaults to the current manifest Git URL."),
                VmAutomationToolSchemaFactory.Prop("revision", "string", "Required full 40-character Git commit SHA.")),
            "name", "revision");
        return VmAutomationToolSchemaFactory.ArrayProp(
            "expectedPackages", item,
            "Exact Git package targets that must match manifest, lockfile, and Unity's registered package state after resolution.");
    }
    }
}
