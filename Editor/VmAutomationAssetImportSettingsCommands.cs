using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Semantic importer settings for the importer families most commonly edited by agents.
    /// Callers never need to know Unity's internal serialized importer property names.
    /// </summary>
    internal static class VmAutomationAssetImportSettingsCommands
    {
        private static readonly HashSet<string> CommonKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "userData", "assetBundleName", "assetBundleVariant",
        };

        private static readonly HashSet<string> TextureKeys = new HashSet<string>(CommonKeys,
            StringComparer.Ordinal)
        {
            "textureType", "textureShape", "spriteImportMode", "spritePixelsPerUnit",
            "sRGBTexture", "alphaSource", "alphaIsTransparency", "mipmapEnabled",
            "isReadable", "streamingMipmaps", "filterMode", "anisoLevel",
            "wrapMode", "wrapModeU", "wrapModeV", "wrapModeW", "maxTextureSize",
            "textureCompression", "compressionQuality", "crunchedCompression", "npotScale",
        };

        private static readonly HashSet<string> ModelKeys = new HashSet<string>(CommonKeys,
            StringComparer.Ordinal)
        {
            "globalScale", "useFileScale", "importBlendShapes", "importCameras",
            "importLights", "importAnimation", "animationType", "isReadable",
            "meshCompression", "addCollider", "keepQuads", "weldVertices", "indexFormat",
            "importNormals", "importTangents",
        };

        private static readonly HashSet<string> AudioKeys = new HashSet<string>(CommonKeys,
            StringComparer.Ordinal)
        {
            "forceToMono", "normalize", "loadInBackground", "ambisonic", "preloadAudioData",
            "defaultSampleSettings",
        };

        private static readonly HashSet<string> TexturePlatformKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "overridden", "maxTextureSize", "format", "compressionQuality",
                "allowsAlphaSplitting",
            };

        private static readonly HashSet<string> AudioSampleKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "loadType", "compressionFormat", "quality", "sampleRateSetting",
                "sampleRateOverride", "preloadAudioData",
            };

        public static object Get(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[] { "assetPath", "platform", "_agentId" },
                    out object keyError))
                return keyError;
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return VmAutomationResponse.Error("assetPath is required.", "invalid_arguments");

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return VmAutomationResponse.Error($"No AssetImporter was found for '{assetPath}'.",
                    "asset_importer_not_found");

            string platform = GetString(args, "platform");
            return new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", assetPath },
                { "importerType", importer.GetType().Name },
                { "settings", ReadSettings(importer, platform) },
                { "platform", platform },
            };
        }

        public static object Set(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[]
                    {
                        "assetPath", "settings", "platform", "platformSettings",
                        "reimport", "dryRun", "_agentId",
                    },
                    out object keyError))
                return keyError;
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return VmAutomationResponse.Error("assetPath is required.", "invalid_arguments");
            if (!(args != null && args.TryGetValue("settings", out object settingsValue) &&
                  settingsValue is Dictionary<string, object> settings))
                return VmAutomationResponse.Error("settings must be an object.", "invalid_arguments");
            if (args.TryGetValue("platformSettings", out object platformValue) &&
                platformValue != null &&
                !(platformValue is Dictionary<string, object>))
            {
                return VmAutomationResponse.Error("platformSettings must be an object.",
                    "invalid_arguments");
            }
            Dictionary<string, object> platformSettings =
                platformValue as Dictionary<string, object>;

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return VmAutomationResponse.Error($"No AssetImporter was found for '{assetPath}'.",
                    "asset_importer_not_found");

            HashSet<string> allowed = GetAllowedKeys(importer);
            if (allowed == null)
                return VmAutomationResponse.Error(
                    $"Importer type '{importer.GetType().Name}' is not supported. Supported importers: TextureImporter, ModelImporter, AudioImporter.",
                    "asset_importer_unsupported");
            string unknown = settings.Keys.FirstOrDefault(key => !allowed.Contains(key));
            if (unknown != null)
                return VmAutomationResponse.Error(
                    $"Setting '{unknown}' is not supported for {importer.GetType().Name}.",
                    "unknown_import_setting", false, new Dictionary<string, object>
                    {
                        { "allowedSettings", allowed.OrderBy(key => key).ToArray() },
                    });

            string platform = GetString(args, "platform");
            if (platformSettings != null && string.IsNullOrEmpty(platform))
                return VmAutomationResponse.Error(
                    "platform is required when platformSettings is provided.",
                    "invalid_arguments");
            if (platformSettings != null && importer is ModelImporter)
                return VmAutomationResponse.Error(
                    "ModelImporter does not expose platformSettings through this semantic contract.",
                    "asset_importer_platform_unsupported");
            if (settings.Count == 0 &&
                (platformSettings == null || platformSettings.Count == 0))
                return VmAutomationResponse.Error(
                    "Provide at least one settings or platformSettings field.",
                    "invalid_arguments");
            try
            {
                ValidateSettings(importer, settings, platformSettings);
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "invalid_import_setting");
            }

            var before = ReadSettings(importer, platform);
            bool dryRun = GetBool(args, "dryRun", false);
            if (dryRun)
            {
                var response = new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "assetPath", assetPath },
                    { "importerType", importer.GetType().Name },
                    { "before", before },
                    { "requested", settings },
                    { "platform", platform },
                };
                if (platformSettings != null)
                    response["requestedPlatformSettings"] = platformSettings;
                return response;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VM Unity Automation Set Import Settings");
            try
            {
                Undo.RecordObject(importer, "VM Unity Automation Set Import Settings");
                ApplyCommonSettings(importer, settings);
                if (importer is TextureImporter textureImporter)
                    ApplyTextureSettings(textureImporter, settings, platform,
                        platformSettings);
                else if (importer is ModelImporter modelImporter)
                    ApplyModelSettings(modelImporter, settings);
                else if (importer is AudioImporter audioImporter)
                    ApplyAudioSettings(audioImporter, settings, platform,
                        platformSettings);

                EditorUtility.SetDirty(importer);
                bool reimport = GetBool(args, "reimport", true);
                if (reimport)
                    importer.SaveAndReimport();
                else
                    AssetDatabase.WriteImportSettingsIfDirty(assetPath);

                AssetImporter refreshed = AssetImporter.GetAtPath(assetPath);
                Undo.CollapseUndoOperations(undoGroup);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", assetPath },
                    { "importerType", importer.GetType().Name },
                    { "reimported", reimport },
                    { "before", before },
                    { "after", ReadSettings(refreshed ?? importer, platform) },
                    { "platform", platform },
                };
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "import_settings_update_failed");
            }
        }

        private static Dictionary<string, object> ReadSettings(AssetImporter importer, string platform)
        {
            var result = new Dictionary<string, object>
            {
                { "userData", importer.userData ?? "" },
                { "assetBundleName", importer.assetBundleName ?? "" },
                { "assetBundleVariant", importer.assetBundleVariant ?? "" },
            };

            if (importer is TextureImporter texture)
                AddTextureSettings(result, texture, platform);
            else if (importer is ModelImporter model)
                AddModelSettings(result, model);
            else if (importer is AudioImporter audio)
                AddAudioSettings(result, audio, platform);
            return result;
        }

        private static void AddTextureSettings(Dictionary<string, object> result,
            TextureImporter importer, string platform)
        {
            result["textureType"] = importer.textureType.ToString();
            result["textureShape"] = importer.textureShape.ToString();
            result["spriteImportMode"] = importer.spriteImportMode.ToString();
            result["spritePixelsPerUnit"] = importer.spritePixelsPerUnit;
            result["sRGBTexture"] = importer.sRGBTexture;
            result["alphaSource"] = importer.alphaSource.ToString();
            result["alphaIsTransparency"] = importer.alphaIsTransparency;
            result["mipmapEnabled"] = importer.mipmapEnabled;
            result["isReadable"] = importer.isReadable;
            result["streamingMipmaps"] = importer.streamingMipmaps;
            result["filterMode"] = importer.filterMode.ToString();
            result["anisoLevel"] = importer.anisoLevel;
            result["wrapMode"] = importer.wrapMode.ToString();
            result["wrapModeU"] = importer.wrapModeU.ToString();
            result["wrapModeV"] = importer.wrapModeV.ToString();
            result["wrapModeW"] = importer.wrapModeW.ToString();
            result["maxTextureSize"] = importer.maxTextureSize;
            result["textureCompression"] = importer.textureCompression.ToString();
            result["compressionQuality"] = importer.compressionQuality;
            result["crunchedCompression"] = importer.crunchedCompression;
            result["npotScale"] = importer.npotScale.ToString();
            if (!string.IsNullOrEmpty(platform))
                result["platformSettings"] = TexturePlatformSettings(importer.GetPlatformTextureSettings(platform));
        }

        private static void AddModelSettings(Dictionary<string, object> result, ModelImporter importer)
        {
            result["globalScale"] = importer.globalScale;
            result["useFileScale"] = importer.useFileScale;
            result["importBlendShapes"] = importer.importBlendShapes;
            result["importCameras"] = importer.importCameras;
            result["importLights"] = importer.importLights;
            result["importAnimation"] = importer.importAnimation;
            result["animationType"] = importer.animationType.ToString();
            result["isReadable"] = importer.isReadable;
            result["meshCompression"] = importer.meshCompression.ToString();
            result["addCollider"] = importer.addCollider;
            result["keepQuads"] = importer.keepQuads;
            result["weldVertices"] = importer.weldVertices;
            result["indexFormat"] = importer.indexFormat.ToString();
            result["importNormals"] = importer.importNormals.ToString();
            result["importTangents"] = importer.importTangents.ToString();
        }

        private static void AddAudioSettings(Dictionary<string, object> result, AudioImporter importer,
            string platform)
        {
            result["forceToMono"] = importer.forceToMono;
            if (TryGetBoolProperty(importer, "normalize", out bool normalize))
                result["normalize"] = normalize;
            result["loadInBackground"] = importer.loadInBackground;
            result["ambisonic"] = importer.ambisonic;
            AudioImporterSampleSettings defaultSettings = importer.defaultSampleSettings;
            if (TryGetSampleSettingBool(defaultSettings, "preloadAudioData",
                    out bool preloadAudioData) ||
                TryGetBoolProperty(importer, "preloadAudioData", out preloadAudioData))
                result["preloadAudioData"] = preloadAudioData;
            result["defaultSampleSettings"] = AudioSampleSettings(defaultSettings);
            if (!string.IsNullOrEmpty(platform) &&
                TryGetOverrideSampleSettings(importer, platform,
                    out AudioImporterSampleSettings overrideSettings))
            {
                result["platformSettings"] = AudioSampleSettings(overrideSettings);
            }
        }

        private static void ApplyCommonSettings(AssetImporter importer,
            Dictionary<string, object> settings)
        {
            SetString(settings, "userData", value => importer.userData = value);
            SetString(settings, "assetBundleName", value => importer.assetBundleName = value);
            SetString(settings, "assetBundleVariant", value => importer.assetBundleVariant = value);
        }

        private static void ApplyTextureSettings(TextureImporter importer,
            Dictionary<string, object> settings, string platform,
            Dictionary<string, object> platformSettings)
        {
            SetEnum<TextureImporterType>(settings, "textureType", value => importer.textureType = value);
            SetEnum<TextureImporterShape>(settings, "textureShape", value => importer.textureShape = value);
            SetEnum<SpriteImportMode>(settings, "spriteImportMode", value => importer.spriteImportMode = value);
            SetFloat(settings, "spritePixelsPerUnit", value => importer.spritePixelsPerUnit = value);
            SetBool(settings, "sRGBTexture", value => importer.sRGBTexture = value);
            SetEnum<TextureImporterAlphaSource>(settings, "alphaSource", value => importer.alphaSource = value);
            SetBool(settings, "alphaIsTransparency", value => importer.alphaIsTransparency = value);
            SetBool(settings, "mipmapEnabled", value => importer.mipmapEnabled = value);
            SetBool(settings, "isReadable", value => importer.isReadable = value);
            SetBool(settings, "streamingMipmaps", value => importer.streamingMipmaps = value);
            SetEnum<FilterMode>(settings, "filterMode", value => importer.filterMode = value);
            SetInt(settings, "anisoLevel", value => importer.anisoLevel = value);
            SetEnum<TextureWrapMode>(settings, "wrapMode", value => importer.wrapMode = value);
            SetEnum<TextureWrapMode>(settings, "wrapModeU", value => importer.wrapModeU = value);
            SetEnum<TextureWrapMode>(settings, "wrapModeV", value => importer.wrapModeV = value);
            SetEnum<TextureWrapMode>(settings, "wrapModeW", value => importer.wrapModeW = value);
            SetInt(settings, "maxTextureSize", value => importer.maxTextureSize = value);
            SetEnum<TextureImporterCompression>(settings, "textureCompression",
                value => importer.textureCompression = value);
            SetInt(settings, "compressionQuality", value => importer.compressionQuality = value);
            SetBool(settings, "crunchedCompression", value => importer.crunchedCompression = value);
            SetEnum<TextureImporterNPOTScale>(settings, "npotScale", value => importer.npotScale = value);

            if (!string.IsNullOrEmpty(platform) && platformSettings != null)
            {
                TextureImporterPlatformSettings value = importer.GetPlatformTextureSettings(platform);
                value.name = platform;
                SetBool(platformSettings, "overridden", item => value.overridden = item);
                SetInt(platformSettings, "maxTextureSize", item => value.maxTextureSize = item);
                SetEnum<TextureImporterFormat>(platformSettings, "format", item => value.format = item);
                SetInt(platformSettings, "compressionQuality", item => value.compressionQuality = item);
                SetBool(platformSettings, "allowsAlphaSplitting", item => value.allowsAlphaSplitting = item);
                importer.SetPlatformTextureSettings(value);
            }
        }

        private static void ApplyModelSettings(ModelImporter importer,
            Dictionary<string, object> settings)
        {
            SetFloat(settings, "globalScale", value => importer.globalScale = value);
            SetBool(settings, "useFileScale", value => importer.useFileScale = value);
            SetBool(settings, "importBlendShapes", value => importer.importBlendShapes = value);
            SetBool(settings, "importCameras", value => importer.importCameras = value);
            SetBool(settings, "importLights", value => importer.importLights = value);
            SetBool(settings, "importAnimation", value => importer.importAnimation = value);
            SetEnum<ModelImporterAnimationType>(settings, "animationType", value => importer.animationType = value);
            SetBool(settings, "isReadable", value => importer.isReadable = value);
            SetEnum<ModelImporterMeshCompression>(settings, "meshCompression",
                value => importer.meshCompression = value);
            SetBool(settings, "addCollider", value => importer.addCollider = value);
            SetBool(settings, "keepQuads", value => importer.keepQuads = value);
            SetBool(settings, "weldVertices", value => importer.weldVertices = value);
            SetEnum<ModelImporterIndexFormat>(settings, "indexFormat", value => importer.indexFormat = value);
            SetEnum<ModelImporterNormals>(settings, "importNormals", value => importer.importNormals = value);
            SetEnum<ModelImporterTangents>(settings, "importTangents", value => importer.importTangents = value);
        }

        private static void ApplyAudioSettings(AudioImporter importer,
            Dictionary<string, object> settings, string platform,
            Dictionary<string, object> platformSettings)
        {
            SetBool(settings, "forceToMono", value => importer.forceToMono = value);
            SetOptionalBoolProperty(settings, "normalize", importer);
            SetBool(settings, "loadInBackground", value => importer.loadInBackground = value);
            SetBool(settings, "ambisonic", value => importer.ambisonic = value);

            AudioImporterSampleSettings defaultSampleSettings = importer.defaultSampleSettings;
            bool defaultSettingsChanged = false;
            if (settings.TryGetValue("preloadAudioData", out object preloadValue))
            {
                bool preload = Convert.ToBoolean(preloadValue);
                if (TrySetSampleSettingBool(ref defaultSampleSettings,
                        "preloadAudioData", preload))
                {
                    defaultSettingsChanged = true;
                }
                else
                {
                    SetRequiredBoolProperty(importer, "preloadAudioData", preload);
                }
            }
            if (settings.TryGetValue("defaultSampleSettings", out object defaultValue) &&
                defaultValue is Dictionary<string, object> defaultSettings)
            {
                ApplyAudioSampleSettings(defaultSettings, ref defaultSampleSettings);
                defaultSettingsChanged = true;
            }
            if (defaultSettingsChanged)
                importer.defaultSampleSettings = defaultSampleSettings;

            if (!string.IsNullOrEmpty(platform) && platformSettings != null)
            {
                if (!TryGetOverrideSampleSettings(importer, platform,
                        out AudioImporterSampleSettings value))
                    value = importer.defaultSampleSettings;
                ApplyAudioSampleSettings(platformSettings, ref value);
                importer.SetOverrideSampleSettings(platform, value);
            }
        }

        private static void ApplyAudioSampleSettings(Dictionary<string, object> settings,
            ref AudioImporterSampleSettings value)
        {
            if (settings.TryGetValue("loadType", out object loadType))
                value.loadType = ParseEnum<AudioClipLoadType>(loadType);
            if (settings.TryGetValue("compressionFormat", out object compressionFormat))
                value.compressionFormat = ParseEnum<AudioCompressionFormat>(compressionFormat);
            if (settings.TryGetValue("quality", out object quality))
                value.quality = Convert.ToSingle(quality);
            if (settings.TryGetValue("sampleRateSetting", out object sampleRateSetting))
                value.sampleRateSetting = ParseEnum<AudioSampleRateSetting>(sampleRateSetting);
            if (settings.TryGetValue("sampleRateOverride", out object sampleRateOverride))
                value.sampleRateOverride = Convert.ToUInt32(sampleRateOverride);
            if (settings.TryGetValue("preloadAudioData", out object preloadAudioData) &&
                !TrySetSampleSettingBool(ref value, "preloadAudioData",
                    Convert.ToBoolean(preloadAudioData)))
                throw new ArgumentException(
                    "preloadAudioData is unavailable in AudioImporterSampleSettings for this Unity version.");
        }

        private static Dictionary<string, object> TexturePlatformSettings(
            TextureImporterPlatformSettings value)
        {
            return new Dictionary<string, object>
            {
                { "name", value.name ?? "" },
                { "overridden", value.overridden },
                { "maxTextureSize", value.maxTextureSize },
                { "format", value.format.ToString() },
                { "compressionQuality", value.compressionQuality },
                { "allowsAlphaSplitting", value.allowsAlphaSplitting },
            };
        }

        private static Dictionary<string, object> AudioSampleSettings(AudioImporterSampleSettings value)
        {
            var result = new Dictionary<string, object>
            {
                { "loadType", value.loadType.ToString() },
                { "compressionFormat", value.compressionFormat.ToString() },
                { "quality", value.quality },
                { "sampleRateSetting", value.sampleRateSetting.ToString() },
                { "sampleRateOverride", value.sampleRateOverride },
            };
            if (TryGetSampleSettingBool(value, "preloadAudioData", out bool preloadAudioData))
                result["preloadAudioData"] = preloadAudioData;
            return result;
        }

        private static HashSet<string> GetAllowedKeys(AssetImporter importer)
        {
            if (importer is TextureImporter) return TextureKeys;
            if (importer is ModelImporter) return ModelKeys;
            if (importer is AudioImporter audioImporter)
            {
                var keys = new HashSet<string>(AudioKeys, StringComparer.Ordinal);
                if (!HasWritableBoolProperty(audioImporter, "normalize"))
                    keys.Remove("normalize");
                if (!HasWritableBoolProperty(audioImporter, "preloadAudioData") &&
                    !HasSampleSettingBool("preloadAudioData"))
                    keys.Remove("preloadAudioData");
                return keys;
            }
            return null;
        }

        private static bool TryGetOverrideSampleSettings(AudioImporter importer, string platform,
            out AudioImporterSampleSettings settings)
        {
            settings = default;
            Type type = typeof(AudioImporter);
            MethodInfo contains = type.GetMethod("ContainsSampleSettingsOverride",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            if (contains != null && !Convert.ToBoolean(
                    contains.Invoke(importer, new object[] { platform })))
                return false;

            MethodInfo direct = type.GetMethod("GetOverrideSampleSettings",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            if (direct != null)
            {
                object result = direct.Invoke(importer, new object[] { platform });
                if (result is AudioImporterSampleSettings typed)
                {
                    settings = typed;
                    return true;
                }
            }

            MethodInfo withOut = type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                 BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "GetOverrideSampleSettings")
                        return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(string) &&
                           parameters[1].ParameterType ==
                           typeof(AudioImporterSampleSettings).MakeByRefType();
                });
            if (withOut == null)
                return false;
            object[] args = { platform, default(AudioImporterSampleSettings) };
            object invocationResult = withOut.Invoke(importer, args);
            if (invocationResult is bool found && !found)
                return false;
            settings = (AudioImporterSampleSettings)args[1];
            return true;
        }

        private static bool HasSampleSettingBool(string fieldName)
        {
            return typeof(AudioImporterSampleSettings).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.FieldType == typeof(bool);
        }

        private static bool TryGetSampleSettingBool(AudioImporterSampleSettings settings,
            string fieldName, out bool value)
        {
            FieldInfo field = typeof(AudioImporterSampleSettings).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.FieldType != typeof(bool))
            {
                value = false;
                return false;
            }
            value = Convert.ToBoolean(field.GetValue(settings));
            return true;
        }

        private static bool TrySetSampleSettingBool(ref AudioImporterSampleSettings settings,
            string fieldName, bool value)
        {
            FieldInfo field = typeof(AudioImporterSampleSettings).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.FieldType != typeof(bool))
                return false;
            object boxed = settings;
            field.SetValue(boxed, value);
            settings = (AudioImporterSampleSettings)boxed;
            return true;
        }

        private static bool HasWritableBoolProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.PropertyType == typeof(bool) && property.CanWrite;
        }

        private static bool TryGetBoolProperty(object target, string propertyName,
            out bool value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.PropertyType != typeof(bool) || !property.CanRead)
            {
                value = false;
                return false;
            }
            value = Convert.ToBoolean(property.GetValue(target));
            return true;
        }

        private static void SetOptionalBoolProperty(Dictionary<string, object> settings,
            string key, object target)
        {
            if (!settings.TryGetValue(key, out object value))
                return;
            SetRequiredBoolProperty(target, key, Convert.ToBoolean(value));
        }

        private static void SetRequiredBoolProperty(object target, string propertyName,
            bool value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.PropertyType != typeof(bool) || !property.CanWrite)
                throw new ArgumentException(
                    $"{propertyName} is unavailable for {target.GetType().Name} in this Unity version.");
            property.SetValue(target, value);
        }

        private static void ValidateSettings(AssetImporter importer,
            Dictionary<string, object> settings,
            Dictionary<string, object> platformSettings)
        {
            ValidateValues(settings,
                new[] { "forceToMono", "normalize", "loadInBackground", "ambisonic",
                    "preloadAudioData", "useFileScale", "importBlendShapes", "importCameras",
                    "importLights", "importAnimation", "isReadable", "addCollider", "keepQuads",
                    "weldVertices", "sRGBTexture", "alphaIsTransparency", "mipmapEnabled",
                    "streamingMipmaps", "crunchedCompression" },
                new[] { "anisoLevel", "maxTextureSize", "compressionQuality" },
                new[] { "globalScale", "spritePixelsPerUnit" });

            if (importer is TextureImporter)
            {
                ValidateEnum<TextureImporterType>(settings, "textureType");
                ValidateEnum<TextureImporterShape>(settings, "textureShape");
                ValidateEnum<SpriteImportMode>(settings, "spriteImportMode");
                ValidateEnum<TextureImporterAlphaSource>(settings, "alphaSource");
                ValidateEnum<FilterMode>(settings, "filterMode");
                ValidateEnum<TextureWrapMode>(settings, "wrapMode");
                ValidateEnum<TextureWrapMode>(settings, "wrapModeU");
                ValidateEnum<TextureWrapMode>(settings, "wrapModeV");
                ValidateEnum<TextureWrapMode>(settings, "wrapModeW");
                ValidateEnum<TextureImporterCompression>(settings, "textureCompression");
                ValidateEnum<TextureImporterNPOTScale>(settings, "npotScale");
                ValidateObjectKeys(platformSettings, TexturePlatformKeys,
                    "platformSettings");
                ValidateValues(platformSettings,
                    new[] { "overridden", "allowsAlphaSplitting" },
                    new[] { "maxTextureSize", "compressionQuality" },
                    Array.Empty<string>());
                ValidateEnum<TextureImporterFormat>(platformSettings, "format");
                ValidateRange(settings, "anisoLevel", 0d, 16d);
                ValidatePositive(settings, "spritePixelsPerUnit");
                ValidatePositive(settings, "maxTextureSize");
                ValidateRange(settings, "compressionQuality", 0d, 100d);
                ValidatePositive(platformSettings, "maxTextureSize");
                ValidateRange(platformSettings, "compressionQuality", 0d, 100d);
            }
            else if (importer is ModelImporter)
            {
                ValidateEnum<ModelImporterAnimationType>(settings, "animationType");
                ValidateEnum<ModelImporterMeshCompression>(settings, "meshCompression");
                ValidateEnum<ModelImporterIndexFormat>(settings, "indexFormat");
                ValidateEnum<ModelImporterNormals>(settings, "importNormals");
                ValidateEnum<ModelImporterTangents>(settings, "importTangents");
                ValidatePositive(settings, "globalScale");
            }
            else if (importer is AudioImporter)
            {
                Dictionary<string, object> sample = null;
                if (settings.TryGetValue("defaultSampleSettings", out object value))
                {
                    sample = value as Dictionary<string, object>;
                    if (sample == null)
                        throw new ArgumentException(
                            "defaultSampleSettings must be an object.");
                }
                ValidateAudioSampleSettings(sample, "defaultSampleSettings");
                ValidateAudioSampleSettings(platformSettings, "platformSettings");
            }
        }

        private static void ValidateAudioSampleSettings(
            Dictionary<string, object> settings, string label)
        {
            ValidateObjectKeys(settings, AudioSampleKeys, label);
            ValidateEnum<AudioClipLoadType>(settings, "loadType");
            ValidateEnum<AudioCompressionFormat>(settings, "compressionFormat");
            ValidateEnum<AudioSampleRateSetting>(settings, "sampleRateSetting");
            ValidateValues(settings, new[] { "preloadAudioData" },
                Array.Empty<string>(), new[] { "quality" });
            if (settings != null &&
                settings.TryGetValue("sampleRateOverride", out object sampleRateOverride))
                Convert.ToUInt32(sampleRateOverride);
            ValidateRange(settings, "quality", 0d, 1d);
        }

        private static void ValidatePositive(Dictionary<string, object> values,
            string key)
        {
            if (values == null || !values.TryGetValue(key, out object value))
                return;
            if (Convert.ToDouble(value) <= 0d)
                throw new ArgumentException($"{key} must be greater than zero.");
        }

        private static void ValidateRange(Dictionary<string, object> values,
            string key, double minimum, double maximum)
        {
            if (values == null || !values.TryGetValue(key, out object value))
                return;
            double numeric = Convert.ToDouble(value);
            if (numeric < minimum || numeric > maximum)
                throw new ArgumentException(
                    $"{key} must be between {minimum} and {maximum}.");
        }

        private static bool TryValidateTopLevelKeys(Dictionary<string, object> values,
            IEnumerable<string> allowed, out object error)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values?.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (string.IsNullOrEmpty(unknown))
            {
                error = null;
                return true;
            }

            error = VmAutomationResponse.Error(
                $"Unsupported argument '{unknown}'. Allowed arguments: " +
                string.Join(", ", allowedSet.Where(key => key != "_agentId")
                    .OrderBy(key => key)) + ".",
                "invalid_arguments");
            return false;
        }

        private static void ValidateObjectKeys(Dictionary<string, object> values,
            HashSet<string> allowed, string label)
        {
            if (values == null)
                return;
            string unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key));
            if (unknown != null)
            {
                throw new ArgumentException(
                    $"{label}.{unknown} is not supported. Expected one of: {string.Join(", ", allowed.OrderBy(key => key))}.");
            }
        }

        private static void ValidateValues(Dictionary<string, object> values,
            IEnumerable<string> boolKeys, IEnumerable<string> intKeys,
            IEnumerable<string> floatKeys)
        {
            if (values == null)
                return;
            foreach (string key in boolKeys)
            {
                if (values.TryGetValue(key, out object value))
                    Convert.ToBoolean(value);
            }
            foreach (string key in intKeys)
            {
                if (values.TryGetValue(key, out object value))
                    Convert.ToInt32(value);
            }
            foreach (string key in floatKeys)
            {
                if (values.TryGetValue(key, out object value))
                    Convert.ToSingle(value);
            }
        }

        private static void ValidateEnum<T>(Dictionary<string, object> values, string key)
            where T : struct
        {
            if (values != null && values.TryGetValue(key, out object value))
                ParseEnum<T>(value);
        }

        private static T ParseEnum<T>(object value) where T : struct
        {
            if (value != null && Enum.TryParse(value.ToString(), true, out T parsed))
                return parsed;
            throw new ArgumentException(
                $"Unknown {typeof(T).Name} value '{value}'. Expected one of: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }

        private static void SetEnum<T>(Dictionary<string, object> values, string key, Action<T> setter)
            where T : struct
        {
            if (values.TryGetValue(key, out object value))
                setter(ParseEnum<T>(value));
        }

        private static void SetString(Dictionary<string, object> values, string key,
            Action<string> setter)
        {
            if (values.TryGetValue(key, out object value))
                setter(value?.ToString() ?? "");
        }

        private static void SetBool(Dictionary<string, object> values, string key, Action<bool> setter)
        {
            if (values.TryGetValue(key, out object value))
                setter(Convert.ToBoolean(value));
        }

        private static void SetInt(Dictionary<string, object> values, string key, Action<int> setter)
        {
            if (values.TryGetValue(key, out object value))
                setter(Convert.ToInt32(value));
        }

        private static void SetFloat(Dictionary<string, object> values, string key, Action<float> setter)
        {
            if (values.TryGetValue(key, out object value))
                setter(Convert.ToSingle(value));
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static bool GetBool(Dictionary<string, object> values, string key, bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

    }
}
