using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPSpriteSheetCommands
    {
        public static object GetSheetInfo(Dictionary<string, object> args)
        {
            var texturePath = GetTexturePath(args);
            var importer = GetTextureImporter(texturePath, out var texture, out var error);
            if (importer == null)
                return new { error };

            var sprites = LoadSprites(texturePath);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "texturePath", texturePath },
                { "textureWidth", texture == null ? 0 : texture.width },
                { "textureHeight", texture == null ? 0 : texture.height },
                { "spriteImportMode", importer.spriteImportMode.ToString() },
                { "pixelsPerUnit", importer.spritePixelsPerUnit },
                { "spriteCount", sprites.Count },
                { "sprites", sprites.Select(SpriteToDictionary).ToList() },
            };
        }

        public static object ReplaceAndSlice(Dictionary<string, object> args)
        {
            var result = ReplaceTextureFile(args);
            if (result.TryGetValue("error", out _))
                return result;

            var sliceResult = SliceSheet(args) as Dictionary<string, object>;
            if (sliceResult == null)
                return result;

            foreach (var pair in sliceResult)
                result[pair.Key] = pair.Value;

            return result;
        }

        public static object SliceSheet(Dictionary<string, object> args)
        {
            var texturePath = GetTexturePath(args);
            var importer = GetTextureImporter(texturePath, out var texture, out var error);
            if (importer == null)
                return new { error };

            int frameWidth = GetInt(args, "frameWidth", 0);
            int frameHeight = GetInt(args, "frameHeight", 0);
            if ((frameWidth <= 0 || frameHeight <= 0) && TryGetDictionary(args, "frameSize", out var frameSize))
            {
                frameWidth = GetInt(frameSize, "x", frameWidth);
                frameWidth = GetInt(frameSize, "width", frameWidth);
                frameHeight = GetInt(frameSize, "y", frameHeight);
                frameHeight = GetInt(frameSize, "height", frameHeight);
            }

            if (frameWidth <= 0 || frameHeight <= 0)
                return new { error = "frameWidth and frameHeight are required and must be greater than 0." };

            var existingSprites = GetSpriteRects(importer);
            var existingByName = existingSprites
                .GroupBy(sprite => sprite.name)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var firstExisting = existingSprites.FirstOrDefault();
            var generatedRects = BuildFrameRects(args, texturePath, texture.width, texture.height, frameWidth, frameHeight,
                existingByName, firstExisting).ToArray();

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            var provider = GetSpriteDataProvider(importer);
            provider.SetSpriteRects(generatedRects);

            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIdProvider?.SetNameFileIdPairs(generatedRects
                .Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));

            provider.Apply();
            importer.SaveAndReimport();
            if (SynchronizeImportedSpriteNameTable(texturePath) == false)
                return new { error = $"Failed to synchronize Sprite name-fileID metadata for '{texturePath}'." };
            AssetDatabase.Refresh();

            var sprites = LoadSprites(texturePath);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "texturePath", texturePath },
                { "textureWidth", texture.width },
                { "textureHeight", texture.height },
                { "frameWidth", frameWidth },
                { "frameHeight", frameHeight },
                { "spriteCount", generatedRects.Length },
                { "sprites", sprites.Select(SpriteToDictionary).ToList() },
            };
        }

        public static object UpdateAnimationClip(Dictionary<string, object> args)
        {
            var clipPath = GetString(args, "clipPath");
            if (string.IsNullOrEmpty(clipPath))
                clipPath = GetString(args, "animationClipPath");
            if (string.IsNullOrEmpty(clipPath))
                return new { error = "clipPath is required." };

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
                return new { error = $"AnimationClip not found at '{clipPath}'." };

            var texturePath = GetTexturePath(args);
            var sprites = LoadSprites(texturePath);
            if (sprites.Count == 0)
                return new { error = $"No sprites found at '{texturePath}'." };

            var spriteNames = GetStringList(args, "spriteNames");
            if (spriteNames.Count > 0)
            {
                var nameSet = new HashSet<string>(spriteNames, StringComparer.Ordinal);
                sprites = sprites.Where(sprite => nameSet.Contains(sprite.name)).ToList();
            }

            sprites = sprites.OrderBy(sprite => ExtractTrailingNumber(sprite.name)).ThenBy(sprite => sprite.name).ToList();

            if (sprites.Count == 0)
                return new { error = "No sprites matched the requested spriteNames." };

            float frameRate = GetFloat(args, "frameRate", clip.frameRate <= 0 ? 12 : clip.frameRate);
            if (frameRate <= 0)
                frameRate = 12;

            clip.frameRate = frameRate;
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = GetString(args, "bindingPath"),
                propertyName = "m_Sprite",
            };

            var keyframes = sprites
                .Select((sprite, index) => new ObjectReferenceKeyframe
                {
                    time = index / frameRate,
                    value = sprite,
                })
                .ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = GetBool(args, "loopTime", settings.loopTime);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "clipPath", clipPath },
                { "texturePath", texturePath },
                { "bindingPath", binding.path },
                { "frameRate", frameRate },
                { "keyframeCount", keyframes.Length },
                { "sprites", sprites.Select(sprite => sprite.name).ToList() },
            };
        }

        public static object ReplaceSliceAndUpdateClip(Dictionary<string, object> args)
        {
            var replaceAndSliceResult = ReplaceAndSlice(args) as Dictionary<string, object>;
            if (replaceAndSliceResult == null || replaceAndSliceResult.ContainsKey("error"))
                return replaceAndSliceResult ?? new Dictionary<string, object> { { "error", "replace-and-slice failed." } };

            var clipPath = GetString(args, "clipPath");
            if (string.IsNullOrEmpty(clipPath))
                clipPath = GetString(args, "animationClipPath");

            if (string.IsNullOrEmpty(clipPath))
                return replaceAndSliceResult;

            replaceAndSliceResult["animationClip"] = UpdateAnimationClip(args);
            return replaceAndSliceResult;
        }

        private static Dictionary<string, object> ReplaceTextureFile(Dictionary<string, object> args)
        {
            var texturePath = GetTexturePath(args);
            var sourcePath = GetString(args, "sourcePath");
            if (string.IsNullOrEmpty(sourcePath))
                sourcePath = GetString(args, "filePath");
            if (string.IsNullOrEmpty(sourcePath))
                return new Dictionary<string, object> { { "error", "sourcePath is required." } };

            var absoluteSourcePath = ResolveAbsolutePath(sourcePath);
            if (File.Exists(absoluteSourcePath) == false)
                return new Dictionary<string, object> { { "error", $"Source file not found: {absoluteSourcePath}" } };

            var absoluteTexturePath = ResolveAbsolutePath(texturePath);
            if (File.Exists(absoluteTexturePath) == false)
                return new Dictionary<string, object> { { "error", $"Texture asset not found: {texturePath}" } };

            File.Copy(absoluteSourcePath, absoluteTexturePath, overwrite: true);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "sourcePath", absoluteSourcePath },
                { "texturePath", texturePath },
                { "textureGuid", AssetDatabase.AssetPathToGUID(texturePath) },
            };
        }

        private static IEnumerable<SpriteRect> BuildFrameRects(Dictionary<string, object> args, string texturePath,
            int textureWidth, int textureHeight, int frameWidth, int frameHeight,
            Dictionary<string, SpriteRect> existingByName, SpriteRect fallbackRect)
        {
            int startX = GetInt(args, "startX", 0);
            int startY = GetInt(args, "startY", 0);
            int columns = GetInt(args, "columns", Math.Max(1, (textureWidth - startX) / frameWidth));
            int frameCount = GetInt(args, "frameCount", 0);
            if (frameCount <= 0)
            {
                int rows = Math.Max(0, (textureHeight - startY) / frameHeight);
                frameCount = columns * rows;
            }

            var baseName = GetString(args, "baseName");
            if (string.IsNullOrEmpty(baseName))
                baseName = Path.GetFileNameWithoutExtension(texturePath);

            bool preserveSpriteIDs = GetBool(args, "preserveSpriteIDs", true);
            bool hasPivotX = TryGetFloat(args, "pivotX", out float pivotX);
            bool hasPivotY = TryGetFloat(args, "pivotY", out float pivotY);
            bool hasPivot = hasPivotX && hasPivotY;
            var fallbackPivot = fallbackRect == null ? new Vector2(0.5f, 0.5f) : fallbackRect.pivot;
            var fallbackAlignment = fallbackRect == null ? SpriteAlignment.Custom : fallbackRect.alignment;
            var fallbackBorder = fallbackRect == null ? Vector4.zero : fallbackRect.border;

            for (int i = 0; i < frameCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                var spriteName = $"{baseName}_{i}";
                existingByName.TryGetValue(spriteName, out var existing);

                var rect = new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(startX + column * frameWidth, textureHeight - startY - (row + 1) * frameHeight,
                        frameWidth, frameHeight),
                    pivot = hasPivot ? new Vector2(pivotX, pivotY) : existing?.pivot ?? fallbackPivot,
                    alignment = hasPivot ? SpriteAlignment.Custom : existing?.alignment ?? fallbackAlignment,
                    border = existing?.border ?? fallbackBorder,
                    spriteID = preserveSpriteIDs && existing != null ? existing.spriteID : GUID.Generate(),
                    customData = existing?.customData,
                };

                yield return rect;
            }
        }

        internal static ISpriteEditorDataProvider GetSpriteDataProvider(TextureImporter importer)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            return provider;
        }

        internal static SpriteRect[] GetSpriteRects(TextureImporter importer)
        {
            var provider = GetSpriteDataProvider(importer);
            return provider.GetSpriteRects() ?? Array.Empty<SpriteRect>();
        }

        internal static List<Sprite> LoadSprites(string texturePath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(texturePath)
                .OfType<Sprite>()
                .ToList();
        }

        internal static bool TryRenameSpritePrefixPreservingIds(string texturePath,
            string oldBaseName, string newBaseName, out int renamedCount, out string error)
        {
            renamedCount = 0;
            error = null;
            if (string.IsNullOrWhiteSpace(oldBaseName) || string.IsNullOrWhiteSpace(newBaseName))
            {
                error = "Old and new Sprite base names are required.";
                return false;
            }

            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                error = $"Texture at '{texturePath}' is not a Multiple Sprite sheet.";
                return false;
            }

            var provider = GetSpriteDataProvider(importer);
            var spriteRects = provider.GetSpriteRects() ?? Array.Empty<SpriteRect>();
            var expectedNamesBySpriteId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var spriteRect in spriteRects)
            {
                if (TryReplaceSpriteBaseName(spriteRect.name, oldBaseName, newBaseName,
                        out string renamed))
                {
                    spriteRect.name = renamed;
                    renamedCount++;
                }

                expectedNamesBySpriteId[spriteRect.spriteID.ToString()] = spriteRect.name;
            }

            if (renamedCount == 0)
                return true;
            if (spriteRects.Select(rect => rect.name).Distinct(StringComparer.Ordinal).Count() !=
                spriteRects.Length)
            {
                error = $"Renaming Sprite prefix '{oldBaseName}' to '{newBaseName}' would create duplicate names.";
                return false;
            }

            provider.SetSpriteRects(spriteRects);
            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIdProvider?.SetNameFileIdPairs(spriteRects
                .Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            if (SynchronizeImportedSpriteNameTable(texturePath) == false)
            {
                error = $"Failed to synchronize Sprite name-fileID metadata for '{texturePath}'.";
                return false;
            }

            importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                error = $"TextureImporter disappeared after renaming Sprite names at '{texturePath}'.";
                return false;
            }

            var reimportedRects = GetSpriteRects(importer);
            if (reimportedRects.Length != expectedNamesBySpriteId.Count)
            {
                error = $"Sprite count changed while renaming '{texturePath}'.";
                return false;
            }

            foreach (var spriteRect in reimportedRects)
            {
                if (expectedNamesBySpriteId.TryGetValue(spriteRect.spriteID.ToString(),
                        out string expectedName) == false ||
                    string.Equals(spriteRect.name, expectedName, StringComparison.Ordinal) == false)
                {
                    error = $"Sprite IDs or names changed unexpectedly while renaming '{texturePath}'.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryReplaceSpriteBaseName(string spriteName, string oldBaseName,
            string newBaseName, out string renamed)
        {
            renamed = spriteName;
            if (string.Equals(spriteName, oldBaseName, StringComparison.Ordinal))
            {
                renamed = newBaseName;
                return true;
            }

            string oldPrefix = oldBaseName + "_";
            if (spriteName.StartsWith(oldPrefix, StringComparison.Ordinal) == false)
                return false;

            renamed = newBaseName + spriteName.Substring(oldBaseName.Length);
            return true;
        }

        internal static bool SynchronizeImportedSpriteNameTable(string texturePath)
        {
            var identities = new List<KeyValuePair<long, string>>();
            foreach (var sprite in LoadSprites(texturePath)
                         .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
                         .ThenBy(sprite => sprite.name, StringComparer.Ordinal))
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _,
                        out long localId) == false)
                    return false;
                identities.Add(new KeyValuePair<long, string>(localId, sprite.name));
            }

            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
                return false;

            var serializedImporter = new SerializedObject(importer);
            serializedImporter.Update();
            var legacyNameTable = serializedImporter.FindProperty("m_InternalIDToNameTable") ??
                                  serializedImporter.FindProperty("internalIDToNameTable");
            if (legacyNameTable != null && legacyNameTable.isArray)
            {
                legacyNameTable.arraySize = identities.Count;
                for (int index = 0; index < identities.Count; index++)
                {
                    var row = legacyNameTable.GetArrayElementAtIndex(index);
                    var classId = row.FindPropertyRelative("first.first");
                    var localId = row.FindPropertyRelative("first.second");
                    var name = row.FindPropertyRelative("second");
                    if (classId == null || localId == null || name == null)
                        return false;

                    classId.longValue = 213;
                    localId.longValue = identities[index].Key;
                    name.stringValue = identities[index].Value;
                }

                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }

            AssetDatabase.ForceReserializeAssets(new[] { texturePath },
                ForceReserializeAssetsOptions.ReserializeMetadata);

            importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                return false;
            serializedImporter = new SerializedObject(importer);
            serializedImporter.Update();
            legacyNameTable = serializedImporter.FindProperty("m_InternalIDToNameTable") ??
                              serializedImporter.FindProperty("internalIDToNameTable");
            if (legacyNameTable == null || legacyNameTable.isArray == false)
                return true;
            if (legacyNameTable.arraySize != identities.Count)
                return false;

            for (int index = 0; index < identities.Count; index++)
            {
                var row = legacyNameTable.GetArrayElementAtIndex(index);
                var classId = row.FindPropertyRelative("first.first");
                var localId = row.FindPropertyRelative("first.second");
                var name = row.FindPropertyRelative("second");
                if (classId == null || localId == null || name == null ||
                    classId.longValue != 213 ||
                    localId.longValue != identities[index].Key ||
                    string.Equals(name.stringValue, identities[index].Value,
                        StringComparison.Ordinal) == false)
                    return false;
            }

            return true;
        }

        private static Dictionary<string, object> SpriteToDictionary(Sprite sprite)
        {
            return new Dictionary<string, object>
            {
                { "name", sprite.name },
                { "rect", RectToDictionary(sprite.rect) },
                { "pivot", VectorToDictionary(sprite.pivot) },
                { "pixelsPerUnit", sprite.pixelsPerUnit },
            };
        }

        private static Dictionary<string, object> RectToDictionary(Rect rect)
        {
            return new Dictionary<string, object>
            {
                { "x", rect.x },
                { "y", rect.y },
                { "width", rect.width },
                { "height", rect.height },
                { "xMax", rect.xMax },
                { "yMax", rect.yMax },
            };
        }

        private static Dictionary<string, object> VectorToDictionary(Vector2 vector)
        {
            return new Dictionary<string, object>
            {
                { "x", vector.x },
                { "y", vector.y },
            };
        }

        private static TextureImporter GetTextureImporter(string texturePath, out Texture2D texture, out string error)
        {
            texture = null;
            error = "";
            if (string.IsNullOrEmpty(texturePath))
            {
                error = "texturePath is required.";
                return null;
            }

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                error = $"TextureImporter not found at '{texturePath}'.";
                return null;
            }

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                error = $"Texture2D not found at '{texturePath}'.";
                return null;
            }

            return importer;
        }

        private static string GetTexturePath(Dictionary<string, object> args)
        {
            var texturePath = GetString(args, "texturePath");
            if (string.IsNullOrEmpty(texturePath))
                texturePath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(texturePath))
                texturePath = GetString(args, "path");
            return texturePath;
        }

        private static string ResolveAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static bool TryGetDictionary(Dictionary<string, object> args, string key,
            out Dictionary<string, object> dictionary)
        {
            dictionary = null;
            if (args == null || args.TryGetValue(key, out var value) == false)
                return false;

            dictionary = value as Dictionary<string, object>;
            return dictionary != null;
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string key)
        {
            var result = new List<string>();
            if (args == null || args.TryGetValue(key, out var value) == false || value == null)
                return result;

            if (value is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item != null)
                        result.Add(item.ToString());
                }
            }
            else
            {
                result.Add(value.ToString());
            }

            return result;
        }

        private static int ExtractTrailingNumber(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int end = text.Length - 1;
            while (end >= 0 && char.IsDigit(text[end]) == false)
                end--;
            if (end < 0)
                return 0;

            int start = end;
            while (start > 0 && char.IsDigit(text[start - 1]))
                start--;

            return int.TryParse(text.Substring(start, end - start + 1), out var value) ? value : 0;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || args.TryGetValue(key, out var value) == false || value == null)
                return defaultValue;
            if (value is bool boolValue)
                return boolValue;
            return bool.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || args.TryGetValue(key, out var value) == false || value == null)
                return defaultValue;
            return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
        {
            return TryGetFloat(args, key, out var value) ? value : defaultValue;
        }

        private static bool TryGetFloat(Dictionary<string, object> args, string key, out float value)
        {
            value = 0;
            if (args == null || args.TryGetValue(key, out var raw) == false || raw == null)
                return false;

            return float.TryParse(raw.ToString(), out value);
        }
    }
}
