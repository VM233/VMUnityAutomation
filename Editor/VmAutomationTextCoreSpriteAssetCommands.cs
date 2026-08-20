using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Owns bounded, transactional publication of named PNG images into one existing
    /// TextCore SpriteAsset and its external Multiple-Sprite PNG atlas.
    /// </summary>
    internal static class VmAutomationTextCoreSpriteAssetCommands
    {
        private const int DefaultPackingPadding = 0;
        private const int MaxRequestSpriteCount = 16;
        private const int MaxExistingSpriteCount = 512;
        private const int MaxSourceDimension = 512;
        private const int MaxSpriteDimension = 512;
        private const int MaxAtlasDimension = 2048;
        private const long MaxSourceBytes = 32L * 1024L * 1024L;
        private const long MaxSnapshotBytes = 32L * 1024L * 1024L;
        private const uint SpriteCharacterUnicode = 0xFFFE;
        private static readonly object RequestGate = new object();

        internal static object UpsertImages(Dictionary<string, object> arguments)
        {
            return Execute(arguments, null);
        }

        internal static object UpsertImagesForTesting(
            Dictionary<string, object> arguments, Action afterMutation)
        {
            return Execute(arguments, afterMutation);
        }

        private static object Execute(Dictionary<string, object> arguments,
            Action afterMutation)
        {
            lock (RequestGate)
            {
                SnapshotSet snapshot = null;
                TargetContext target = null;
                bool mutationStarted = false;

                try
                {
                    Request request = ParseRequest(arguments);
                    target = LoadAndValidateTarget(request);
                    List<PreparedSprite> prepared = PrepareSprites(request, target);
                    MutationPlan plan = BuildPlan(request, target, prepared);
                    snapshot = SnapshotSet.Capture(target.SpriteAssetPath,
                        target.AtlasPath);

                    mutationStarted = true;
                    WriteAtlas(target, plan);
                    ApplySpriteImporter(target, plan);
                    SaveSpriteAsset(target, plan);

                    afterMutation?.Invoke();

                    AssetDatabase.ImportAsset(target.SpriteAssetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    return VerifyAndBuildResult(request, target, plan);
                }
                catch (Exception exception)
                {
                    string errorCode = exception is CommandException commandException
                        ? commandException.ErrorCode
                        : "tool_execution_failed";
                    if (!mutationStarted || snapshot == null || target == null)
                        return VmAutomationResponse.Error(exception.Message, errorCode, false);

                    try
                    {
                        snapshot.Restore(target.SpriteAssetPath, target.AtlasPath);
                        return VmAutomationResponse.Error(exception.Message, errorCode, false,
                            new Dictionary<string, object>
                            {
                                { "rollbackVerified", true },
                            });
                    }
                    catch (Exception rollbackException)
                    {
                        return VmAutomationResponse.Error(
                            exception.Message + " Rollback failed: " +
                            rollbackException.Message,
                            "rollback_failed", false,
                            new Dictionary<string, object>
                            {
                                { "rollbackVerified", false },
                            });
                    }
                }
            }
        }

        private static Request ParseRequest(Dictionary<string, object> arguments)
        {
            arguments = arguments ?? new Dictionary<string, object>();
            string spriteAssetPath = RequireAssetPath(arguments,
                "spriteAssetPath", ".asset");
            int spriteWidth = GetBoundedInt(arguments, "spriteWidth", -1,
                1, MaxSpriteDimension, true);
            int spriteHeight = GetBoundedInt(arguments, "spriteHeight", -1,
                1, MaxSpriteDimension, true);
            int packingPadding = GetBoundedInt(arguments, "packingPadding",
                DefaultPackingPadding, 0, 64);

            if (!arguments.TryGetValue("sprites", out object spritesValue) ||
                !(spritesValue is IList spriteList) || spritesValue is string)
            {
                throw new CommandException("sprites must be an array.",
                    "invalid_arguments");
            }
            if (spriteList.Count < 1 || spriteList.Count > MaxRequestSpriteCount)
            {
                throw new CommandException(
                    $"sprites must contain between 1 and {MaxRequestSpriteCount} entries.",
                    "invalid_arguments");
            }

            var requests = new List<SpriteRequest>(spriteList.Count);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < spriteList.Count; index++)
            {
                Dictionary<string, object> sprite =
                    VmAutomationResponse.ToDictionary(spriteList[index]);
                if (sprite == null)
                {
                    throw new CommandException($"sprites[{index}] must be an object.",
                        "invalid_arguments");
                }

                string prefix = $"sprites[{index}].";
                string name = GetRequiredString(sprite, "name", prefix);
                if (name.Length > 128 || name.Any(char.IsControl))
                {
                    throw new CommandException(prefix +
                        "name must contain 1 to 128 non-control characters.",
                        "invalid_arguments");
                }
                if (!names.Add(name))
                {
                    throw new CommandException(
                        $"sprites contains duplicate name '{name}'.",
                        "invalid_arguments");
                }

                string imagePath = RequireAssetPath(sprite, "imagePath", ".png",
                    prefix);
                float glyphScale = GetBoundedFloat(sprite, "glyphScale", 1,
                    0.01f, 100, prefix);
                float bearingX = GetBoundedFloat(sprite, "bearingX", 0,
                    -MaxAtlasDimension, MaxAtlasDimension, prefix);
                float bearingY = GetBoundedFloat(sprite, "bearingY", spriteHeight,
                    -MaxAtlasDimension, MaxAtlasDimension, prefix);
                float advance = GetBoundedFloat(sprite, "advance", spriteWidth,
                    0, MaxAtlasDimension, prefix);
                float characterScale = GetBoundedFloat(sprite,
                    "characterScale", 1, 0.01f, 100, prefix);
                requests.Add(new SpriteRequest(name, imagePath, glyphScale,
                    bearingX, bearingY, advance, characterScale));
            }

            return new Request(spriteAssetPath, spriteWidth, spriteHeight,
                packingPadding, requests);
        }

        private static TargetContext LoadAndValidateTarget(Request request)
        {
            SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                request.SpriteAssetPath);
            if (spriteAsset == null)
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' was not found.",
                    "asset_not_found");
            }

            if (!(spriteAsset.spriteSheet is Texture2D atlasTexture))
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' must reference one Texture2D sprite sheet.",
                    "unsupported_sprite_asset");
            }
            string atlasPath = NormalizeAssetPath(
                AssetDatabase.GetAssetPath(atlasTexture));
            if (!atlasPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandException(
                    $"TextCore SpriteAsset atlas '{atlasPath}' must be a PNG asset.",
                    "unsupported_sprite_asset");
            }
            if (request.Sprites.Any(sprite => string.Equals(sprite.ImagePath,
                    atlasPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new CommandException(
                    "A source image cannot be the target SpriteAsset atlas.",
                    "invalid_arguments");
            }

            if (!(AssetImporter.GetAtPath(atlasPath) is TextureImporter importer) ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                throw new CommandException(
                    $"TextCore SpriteAsset atlas '{atlasPath}' must use a Multiple Sprite TextureImporter.",
                    "unsupported_sprite_asset");
            }
            if (atlasTexture.width < 1 || atlasTexture.height < 1 ||
                atlasTexture.width > MaxAtlasDimension ||
                atlasTexture.height > MaxAtlasDimension)
            {
                throw new CommandException(
                    $"Atlas dimensions must be between 1 and {MaxAtlasDimension} pixels per axis.",
                    "unsupported_sprite_asset");
            }
            if (spriteAsset.material == null ||
                spriteAsset.material.mainTexture != atlasTexture)
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' material must reference its sprite sheet as mainTexture.",
                    "unsupported_sprite_asset");
            }
            if (EditorUtility.IsDirty(spriteAsset) ||
                EditorUtility.IsDirty(spriteAsset.material) ||
                EditorUtility.IsDirty(atlasTexture) || EditorUtility.IsDirty(importer))
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' or its atlas has unsaved Editor changes. Save or discard them before mutation.",
                    "asset_dirty");
            }

            SpriteRect[] existingRects =
                VmAutomationSpriteSheetCommands.GetSpriteRects(importer);
            if (existingRects.Length > MaxExistingSpriteCount)
            {
                throw new CommandException(
                    $"The target atlas contains {existingRects.Length} slices; the supported maximum is {MaxExistingSpriteCount}.",
                    "input_too_large");
            }
            if (existingRects.Select(rect => rect.name)
                    .Distinct(StringComparer.Ordinal).Count() != existingRects.Length)
            {
                throw new CommandException(
                    $"Atlas '{atlasPath}' contains duplicate Sprite names.",
                    "unsupported_sprite_asset");
            }
            if (spriteAsset.spriteCharacterTable == null ||
                spriteAsset.spriteGlyphTable == null)
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' has no initialized Sprite character/glyph tables.",
                    "unsupported_sprite_asset");
            }
            if (spriteAsset.spriteCharacterTable.Where(character =>
                        character != null).Select(character => character.name)
                    .Distinct(StringComparer.Ordinal).Count() !=
                spriteAsset.spriteCharacterTable.Count(character => character != null))
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' contains duplicate character names.",
                    "unsupported_sprite_asset");
            }
            if (spriteAsset.spriteGlyphTable.Where(glyph => glyph != null)
                    .Select(glyph => glyph.index).Distinct().Count() !=
                spriteAsset.spriteGlyphTable.Count(glyph => glyph != null))
            {
                throw new CommandException(
                    $"TextCore SpriteAsset '{request.SpriteAssetPath}' contains duplicate glyph indices.",
                    "unsupported_sprite_asset");
            }

            byte[] atlasBytes = ReadBoundedFile(ToAbsoluteProjectPath(atlasPath),
                MaxSnapshotBytes, atlasPath);
            DecodedImage atlasImage = DecodePng(atlasBytes, atlasPath,
                MaxAtlasDimension);
            if (atlasImage.Width != atlasTexture.width ||
                atlasImage.Height != atlasTexture.height)
            {
                throw new CommandException(
                    $"Atlas file dimensions {atlasImage.Width}x{atlasImage.Height} do not match the imported texture dimensions {atlasTexture.width}x{atlasTexture.height}.",
                    "unsupported_sprite_asset");
            }

            Identity spriteAssetIdentity = Identity.Read(spriteAsset,
                "TextCore SpriteAsset");
            Identity atlasIdentity = Identity.Read(atlasTexture, "atlas texture");
            Identity materialIdentity = Identity.Read(spriteAsset.material,
                "SpriteAsset material");
            Dictionary<string, long> existingLocalIds =
                ReadSpriteLocalIds(atlasPath);

            return new TargetContext(request.SpriteAssetPath, atlasPath,
                atlasImage, existingRects, existingLocalIds,
                spriteAssetIdentity, atlasIdentity, materialIdentity,
                importer.maxTextureSize);
        }

        private static List<PreparedSprite> PrepareSprites(Request request,
            TargetContext target)
        {
            long totalSourceBytes = 0;
            var prepared = new List<PreparedSprite>(request.Sprites.Count);
            foreach (SpriteRequest sprite in request.Sprites)
            {
                string absolutePath = ToAbsoluteProjectPath(sprite.ImagePath);
                byte[] bytes = ReadBoundedFile(absolutePath, MaxSourceBytes,
                    sprite.ImagePath);
                totalSourceBytes += bytes.LongLength;
                if (totalSourceBytes > MaxSourceBytes)
                {
                    throw new CommandException(
                        "Combined PNG source bytes exceed the 32 MB request limit.",
                        "input_too_large");
                }

                DecodedImage image = DecodePng(bytes, sprite.ImagePath,
                    MaxSourceDimension);
                Color32[] pixels = Resample(image.Pixels, image.Width,
                    image.Height, request.SpriteWidth, request.SpriteHeight);
                prepared.Add(new PreparedSprite(sprite, pixels));
            }
            return prepared;
        }

        private static MutationPlan BuildPlan(Request request,
            TargetContext target, List<PreparedSprite> prepared)
        {
            var existingByName = target.ExistingRects.ToDictionary(
                rect => rect.name, StringComparer.Ordinal);
            var entries = new List<PlannedSprite>(prepared.Count);
            var newEntries = new List<PreparedSprite>();
            foreach (PreparedSprite sprite in prepared)
            {
                if (!existingByName.TryGetValue(sprite.Request.Name,
                        out SpriteRect existing))
                {
                    newEntries.Add(sprite);
                    continue;
                }

                Rect rect = existing.rect;
                if (!IsExactIntegerRect(rect) ||
                    (int)rect.width != request.SpriteWidth ||
                    (int)rect.height != request.SpriteHeight)
                {
                    throw new CommandException(
                        $"Existing Sprite '{sprite.Request.Name}' has rect {rect.width}x{rect.height}; upsert requires {request.SpriteWidth}x{request.SpriteHeight}.",
                        "sprite_size_mismatch");
                }
                entries.Add(new PlannedSprite(sprite, existing, false));
            }

            int atlasWidth = target.AtlasImage.Width;
            int atlasHeight = target.AtlasImage.Height;
            int cellWidth = checked(request.SpriteWidth +
                                    request.PackingPadding * 2);
            int cellHeight = checked(request.SpriteHeight +
                                     request.PackingPadding * 2);
            int columns = atlasWidth / cellWidth;
            if (newEntries.Count > 0 && columns < 1)
            {
                throw new CommandException(
                    $"Atlas width {atlasWidth} cannot fit a {request.SpriteWidth}-pixel Sprite with {request.PackingPadding}-pixel padding.",
                    "atlas_full");
            }

            int addedRows = newEntries.Count == 0
                ? 0
                : (newEntries.Count + columns - 1) / columns;
            int newAtlasHeight = checked(atlasHeight + addedRows * cellHeight);
            if (newAtlasHeight > MaxAtlasDimension ||
                newAtlasHeight > target.MaxTextureSize)
            {
                throw new CommandException(
                    $"Appending {newEntries.Count} Sprite(s) requires atlas size {atlasWidth}x{newAtlasHeight}, beyond the target max texture size.",
                    "atlas_full");
            }

            var rects = target.ExistingRects.ToList();
            for (int index = 0; index < newEntries.Count; index++)
            {
                int column = index % columns;
                int row = index / columns;
                var rect = new SpriteRect
                {
                    name = newEntries[index].Request.Name,
                    rect = new Rect(
                        request.PackingPadding + column * cellWidth,
                        atlasHeight + request.PackingPadding + row * cellHeight,
                        request.SpriteWidth,
                        request.SpriteHeight),
                    alignment = SpriteAlignment.Custom,
                    pivot = Vector2.zero,
                    border = Vector4.zero,
                    spriteID = GUID.Generate(),
                };
                rects.Add(rect);
                entries.Add(new PlannedSprite(newEntries[index], rect, true));
            }

            entries = request.Sprites.Select(sprite => entries.Single(entry =>
                string.Equals(entry.Prepared.Request.Name, sprite.Name,
                    StringComparison.Ordinal))).ToList();
            return new MutationPlan(atlasWidth, newAtlasHeight, rects, entries);
        }

        private static void WriteAtlas(TargetContext target, MutationPlan plan)
        {
            var pixels = new Color32[plan.AtlasWidth * plan.AtlasHeight];
            Array.Copy(target.AtlasImage.Pixels, pixels,
                target.AtlasImage.Pixels.Length);
            foreach (PlannedSprite entry in plan.Entries)
            {
                Rect rect = entry.Rect.rect;
                Blit(entry.Prepared.Pixels, (int)rect.width, (int)rect.height,
                    pixels, plan.AtlasWidth, (int)rect.x, (int)rect.y);
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(plan.AtlasWidth, plan.AtlasHeight,
                    TextureFormat.RGBA32, false, false);
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    throw new CommandException("Failed to encode the updated atlas PNG.",
                        "atlas_encode_failed");
                }
                VmAutomationPersistenceFile.WriteAllBytes(
                    ToAbsoluteProjectPath(target.AtlasPath), png);
            }
            finally
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
        }

        private static void ApplySpriteImporter(TargetContext target,
            MutationPlan plan)
        {
            if (!(AssetImporter.GetAtPath(target.AtlasPath) is
                    TextureImporter importer))
            {
                throw new CommandException(
                    $"TextureImporter disappeared for '{target.AtlasPath}'.",
                    "transaction_postcondition_failed");
            }

            ISpriteEditorDataProvider provider =
                VmAutomationSpriteSheetCommands.GetSpriteDataProvider(importer);
            provider.SetSpriteRects(plan.AllRects.ToArray());
            ISpriteNameFileIdDataProvider nameProvider =
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameProvider?.SetNameFileIdPairs(plan.AllRects.Select(rect =>
                new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            if (!VmAutomationSpriteSheetCommands.SynchronizeImportedSpriteNameTable(
                    target.AtlasPath))
            {
                throw new CommandException(
                    $"Failed to synchronize Sprite name-fileID metadata for '{target.AtlasPath}'.",
                    "transaction_postcondition_failed");
            }

            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(
                target.AtlasPath);
            if (imported == null || imported.width != plan.AtlasWidth ||
                imported.height != plan.AtlasHeight)
            {
                throw new CommandException(
                    "The imported atlas dimensions do not match the mutation plan.",
                    "transaction_postcondition_failed");
            }
        }

        private static void SaveSpriteAsset(TargetContext target,
            MutationPlan plan)
        {
            SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                target.SpriteAssetPath);
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                target.AtlasPath);
            if (spriteAsset == null || atlas == null)
            {
                throw new CommandException(
                    "SpriteAsset or atlas could not be reloaded after atlas import.",
                    "transaction_postcondition_failed");
            }

            Dictionary<string, Sprite> sprites =
                VmAutomationSpriteSheetCommands.LoadSprites(target.AtlasPath)
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
            var charactersByName = spriteAsset.spriteCharacterTable
                .Where(character => character != null)
                .ToDictionary(character => character.name,
                    StringComparer.Ordinal);
            var glyphsByIndex = spriteAsset.spriteGlyphTable
                .Where(glyph => glyph != null)
                .ToDictionary(glyph => glyph.index);
            uint nextGlyphIndex = glyphsByIndex.Count == 0
                ? 0
                : checked(glyphsByIndex.Keys.Max() + 1);

            foreach (PlannedSprite entry in plan.Entries)
            {
                if (!sprites.TryGetValue(entry.Prepared.Request.Name,
                        out Sprite importedSprite))
                {
                    throw new CommandException(
                        $"Imported Sprite '{entry.Prepared.Request.Name}' was not found.",
                        "transaction_postcondition_failed");
                }

                SpriteRequest request = entry.Prepared.Request;
                Rect rect = entry.Rect.rect;
                var metrics = new GlyphMetrics(rect.width, rect.height,
                    request.BearingX, request.BearingY, request.Advance);
                var glyphRect = new GlyphRect((int)rect.x, (int)rect.y,
                    (int)rect.width, (int)rect.height);

                if (charactersByName.TryGetValue(request.Name,
                        out SpriteCharacter character))
                {
                    if (!glyphsByIndex.TryGetValue(character.glyphIndex,
                            out SpriteGlyph glyph))
                    {
                        throw new CommandException(
                            $"Sprite character '{request.Name}' references missing glyph index {character.glyphIndex}.",
                            "unsupported_sprite_asset");
                    }
                    glyph.metrics = metrics;
                    glyph.glyphRect = glyphRect;
                    glyph.scale = request.GlyphScale;
                    glyph.atlasIndex = 0;
                    glyph.sprite = importedSprite;
                    character.glyph = glyph;
                    character.textAsset = spriteAsset;
                    character.scale = request.CharacterScale;
                }
                else
                {
                    var glyph = new SpriteGlyph(nextGlyphIndex++, metrics,
                        glyphRect, request.GlyphScale, 0, importedSprite);
                    character = new SpriteCharacter(SpriteCharacterUnicode,
                        spriteAsset, glyph)
                    {
                        name = request.Name,
                        scale = request.CharacterScale,
                    };
                    spriteAsset.spriteGlyphTable.Add(glyph);
                    spriteAsset.spriteCharacterTable.Add(character);
                    glyphsByIndex.Add(glyph.index, glyph);
                    charactersByName.Add(character.name, character);
                }
            }

            spriteAsset.UpdateLookupTables();
            EditorUtility.SetDirty(spriteAsset);
            AssetDatabase.SaveAssetIfDirty(spriteAsset);
        }

        private static object VerifyAndBuildResult(Request request,
            TargetContext target, MutationPlan plan)
        {
            SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                target.SpriteAssetPath);
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                target.AtlasPath);
            if (spriteAsset == null || atlas == null)
            {
                throw new CommandException(
                    "Persisted SpriteAsset or atlas could not be reloaded.",
                    "transaction_postcondition_failed");
            }

            target.SpriteAssetIdentity.AssertSame(spriteAsset,
                "TextCore SpriteAsset");
            target.AtlasIdentity.AssertSame(atlas, "atlas texture");
            target.MaterialIdentity.AssertSame(spriteAsset.material,
                "SpriteAsset material");
            if (spriteAsset.spriteSheet != atlas ||
                spriteAsset.material.mainTexture != atlas)
            {
                throw new CommandException(
                    "Persisted SpriteAsset texture references changed unexpectedly.",
                    "transaction_postcondition_failed");
            }

            Dictionary<string, Sprite> sprites =
                VmAutomationSpriteSheetCommands.LoadSprites(target.AtlasPath)
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
            Dictionary<string, long> localIds = ReadSpriteLocalIds(
                target.AtlasPath);
            foreach (KeyValuePair<string, long> before in
                     target.ExistingSpriteLocalIds)
            {
                if (!localIds.TryGetValue(before.Key, out long after) ||
                    after != before.Value)
                {
                    throw new CommandException(
                        $"Existing Sprite identity changed for '{before.Key}'.",
                        "transaction_postcondition_failed");
                }
            }

            var charactersByName = spriteAsset.spriteCharacterTable
                .Where(character => character != null)
                .ToDictionary(character => character.name,
                    StringComparer.Ordinal);
            var glyphsByIndex = spriteAsset.spriteGlyphTable
                .Where(glyph => glyph != null)
                .ToDictionary(glyph => glyph.index);
            var results = new List<object>(plan.Entries.Count);
            foreach (PlannedSprite entry in plan.Entries)
            {
                string name = entry.Prepared.Request.Name;
                if (!charactersByName.TryGetValue(name,
                        out SpriteCharacter character) ||
                    !glyphsByIndex.TryGetValue(character.glyphIndex,
                        out SpriteGlyph glyph) ||
                    !sprites.TryGetValue(name, out Sprite sprite) ||
                    !localIds.TryGetValue(name, out long localId))
                {
                    throw new CommandException(
                        $"Persisted SpriteAsset mapping for '{name}' is incomplete.",
                        "transaction_postcondition_failed");
                }

                Rect expectedRect = entry.Rect.rect;
                if (!GlyphRectEquals(glyph.glyphRect, expectedRect) ||
                    !RectEquals(sprite.rect, expectedRect) ||
                    glyph.sprite != sprite ||
                    !NearlyEqual(glyph.scale,
                        entry.Prepared.Request.GlyphScale) ||
                    !NearlyEqual(character.scale,
                        entry.Prepared.Request.CharacterScale) ||
                    !MetricsEqual(glyph.metrics, entry.Prepared.Request,
                        request.SpriteWidth, request.SpriteHeight))
                {
                    throw new CommandException(
                        $"Persisted SpriteAsset readback for '{name}' does not match the request.",
                        "transaction_postcondition_failed");
                }

                results.Add(new Dictionary<string, object>
                {
                    { "name", name },
                    { "imagePath", entry.Prepared.Request.ImagePath },
                    { "created", entry.Created },
                    { "glyphIndex", glyph.index },
                    { "spriteLocalId", localId },
                    { "rect", RectResult(expectedRect) },
                    { "metrics", MetricsResult(glyph.metrics) },
                    { "glyphScale", glyph.scale },
                    { "characterScale", character.scale },
                });
            }

            string spriteAssetAbsolute = ToAbsoluteProjectPath(
                target.SpriteAssetPath);
            string atlasAbsolute = ToAbsoluteProjectPath(target.AtlasPath);
            return VmAutomationResponse.Success(null, new Dictionary<string, object>
            {
                { "spriteAssetPath", target.SpriteAssetPath },
                { "atlasPath", target.AtlasPath },
                { "spriteAssetGuid", target.SpriteAssetIdentity.Guid },
                { "atlasGuid", target.AtlasIdentity.Guid },
                { "atlasWidth", atlas.width },
                { "atlasHeight", atlas.height },
                { "spriteWidth", request.SpriteWidth },
                { "spriteHeight", request.SpriteHeight },
                { "packingPadding", request.PackingPadding },
                { "sprites", results },
                { "spriteAssetSha256", HashFile(spriteAssetAbsolute) },
                { "spriteAssetMetaSha256", HashFile(spriteAssetAbsolute + ".meta") },
                { "atlasSha256", HashFile(atlasAbsolute) },
                { "atlasMetaSha256", HashFile(atlasAbsolute + ".meta") },
            });
        }

        private static DecodedImage DecodePng(byte[] bytes, string path,
            int maxDimension)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false,
                    false);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    throw new CommandException(
                        $"PNG source '{path}' could not be decoded.",
                        "invalid_image");
                }
                if (texture.width < 1 || texture.height < 1 ||
                    texture.width > maxDimension || texture.height > maxDimension)
                {
                    throw new CommandException(
                        $"PNG '{path}' dimensions must be between 1 and {maxDimension} pixels per axis.",
                        "invalid_image");
                }
                return new DecodedImage(texture.width, texture.height,
                    texture.GetPixels32());
            }
            finally
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
        }

        private static Color32[] Resample(Color32[] source, int sourceWidth,
            int sourceHeight, int targetWidth, int targetHeight)
        {
            var result = new Color32[targetWidth * targetHeight];
            for (int y = 0; y < targetHeight; y++)
            for (int x = 0; x < targetWidth; x++)
            {
                double sourceX = (x + 0.5) * sourceWidth / targetWidth - 0.5;
                double sourceY = (y + 0.5) * sourceHeight / targetHeight - 0.5;
                sourceX = Math.Max(0, Math.Min(sourceWidth - 1, sourceX));
                sourceY = Math.Max(0, Math.Min(sourceHeight - 1, sourceY));
                int x0 = (int)Math.Floor(sourceX);
                int y0 = (int)Math.Floor(sourceY);
                int x1 = Math.Min(sourceWidth - 1, x0 + 1);
                int y1 = Math.Min(sourceHeight - 1, y0 + 1);
                double tx = sourceX - x0;
                double ty = sourceY - y0;
                result[y * targetWidth + x] = Bilinear(
                    source[y0 * sourceWidth + x0],
                    source[y0 * sourceWidth + x1],
                    source[y1 * sourceWidth + x0],
                    source[y1 * sourceWidth + x1], tx, ty);
            }
            return result;
        }

        private static Color32 Bilinear(Color32 bottomLeft,
            Color32 bottomRight, Color32 topLeft, Color32 topRight,
            double tx, double ty)
        {
            return new Color32(
                Interpolate(bottomLeft.r, bottomRight.r, topLeft.r, topRight.r,
                    tx, ty),
                Interpolate(bottomLeft.g, bottomRight.g, topLeft.g, topRight.g,
                    tx, ty),
                Interpolate(bottomLeft.b, bottomRight.b, topLeft.b, topRight.b,
                    tx, ty),
                Interpolate(bottomLeft.a, bottomRight.a, topLeft.a, topRight.a,
                    tx, ty));
        }

        private static byte Interpolate(byte bottomLeft, byte bottomRight,
            byte topLeft, byte topRight, double tx, double ty)
        {
            double bottom = bottomLeft + (bottomRight - bottomLeft) * tx;
            double top = topLeft + (topRight - topLeft) * tx;
            return (byte)Math.Round(bottom + (top - bottom) * ty,
                MidpointRounding.AwayFromZero);
        }

        private static void Blit(Color32[] source, int width, int height,
            Color32[] target, int targetWidth, int targetX, int targetY)
        {
            for (int y = 0; y < height; y++)
            {
                Array.Copy(source, y * width, target,
                    (targetY + y) * targetWidth + targetX, width);
            }
        }

        private static Dictionary<string, long> ReadSpriteLocalIds(
            string atlasPath)
        {
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (Sprite sprite in VmAutomationSpriteSheetCommands.LoadSprites(atlasPath))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,
                        out string _, out long localId))
                {
                    throw new CommandException(
                        $"Could not read local file ID for Sprite '{sprite.name}'.",
                        "transaction_postcondition_failed");
                }
                if (result.ContainsKey(sprite.name))
                {
                    throw new CommandException(
                        $"Atlas '{atlasPath}' imported duplicate Sprite name '{sprite.name}'.",
                        "unsupported_sprite_asset");
                }
                result.Add(sprite.name, localId);
            }
            return result;
        }

        private static byte[] ReadBoundedFile(string absolutePath,
            long maximumBytes, string displayPath)
        {
            if (!File.Exists(absolutePath))
            {
                throw new CommandException(
                    $"File '{displayPath}' does not exist.", "asset_not_found");
            }
            var info = new FileInfo(absolutePath);
            if (info.Length > maximumBytes)
            {
                throw new CommandException(
                    $"File '{displayPath}' exceeds the {maximumBytes / (1024 * 1024)} MB limit.",
                    "input_too_large");
            }
            return VmAutomationPersistenceFile.ReadAllBytes(absolutePath);
        }

        private static string RequireAssetPath(Dictionary<string, object> values,
            string key, string extension, string prefix = "")
        {
            string path = NormalizeAssetPath(GetRequiredString(values, key, prefix));
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandException(prefix + key +
                    $" must be an Assets-relative {extension} path.",
                    "invalid_arguments");
            }
            return path;
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Trim().Replace('\\', '/');
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new IOException("Unity project root could not be resolved.");
            return Path.GetFullPath(Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string GetRequiredString(Dictionary<string, object> values,
            string key, string prefix = "")
        {
            if (!values.TryGetValue(key, out object value) || value == null ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                throw new CommandException(prefix + key + " is required.",
                    "invalid_arguments");
            }
            return value.ToString().Trim();
        }

        private static int GetBoundedInt(Dictionary<string, object> values,
            string key, int defaultValue, int minimum, int maximum,
            bool required = false)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
            {
                if (!required)
                    return defaultValue;
                throw new CommandException(key + " is required.",
                    "invalid_arguments");
            }
            double number = ConvertNumber(value, key);
            if (Math.Truncate(number) != number || number < minimum ||
                number > maximum)
            {
                throw new CommandException(
                    $"{key} must be an integer between {minimum} and {maximum}.",
                    "invalid_arguments");
            }
            return (int)number;
        }

        private static float GetBoundedFloat(Dictionary<string, object> values,
            string key, float defaultValue, float minimum, float maximum,
            string prefix)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
                return defaultValue;
            double number = ConvertNumber(value, prefix + key);
            if (number < minimum || number > maximum)
            {
                throw new CommandException(
                    $"{prefix}{key} must be between {minimum} and {maximum}.",
                    "invalid_arguments");
            }
            return (float)number;
        }

        private static double ConvertNumber(object value, string field)
        {
            try
            {
                double number = Convert.ToDouble(value,
                    CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new FormatException();
                return number;
            }
            catch (Exception)
            {
                throw new CommandException(field + " must be a finite number.",
                    "invalid_arguments");
            }
        }

        private static bool IsExactIntegerRect(Rect rect)
        {
            return NearlyEqual(rect.x, Mathf.Round(rect.x)) &&
                   NearlyEqual(rect.y, Mathf.Round(rect.y)) &&
                   NearlyEqual(rect.width, Mathf.Round(rect.width)) &&
                   NearlyEqual(rect.height, Mathf.Round(rect.height));
        }

        private static bool RectEquals(Rect left, Rect right)
        {
            return NearlyEqual(left.x, right.x) && NearlyEqual(left.y, right.y) &&
                   NearlyEqual(left.width, right.width) &&
                   NearlyEqual(left.height, right.height);
        }

        private static bool GlyphRectEquals(GlyphRect left, Rect right)
        {
            return left.x == (int)right.x && left.y == (int)right.y &&
                   left.width == (int)right.width &&
                   left.height == (int)right.height;
        }

        private static bool MetricsEqual(GlyphMetrics metrics,
            SpriteRequest request, int width, int height)
        {
            return NearlyEqual(metrics.width, width) &&
                   NearlyEqual(metrics.height, height) &&
                   NearlyEqual(metrics.horizontalBearingX, request.BearingX) &&
                   NearlyEqual(metrics.horizontalBearingY, request.BearingY) &&
                   NearlyEqual(metrics.horizontalAdvance, request.Advance);
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= 0.0001f;
        }

        private static Dictionary<string, object> RectResult(Rect rect)
        {
            return new Dictionary<string, object>
            {
                { "x", (int)rect.x }, { "y", (int)rect.y },
                { "width", (int)rect.width }, { "height", (int)rect.height },
            };
        }

        private static Dictionary<string, object> MetricsResult(
            GlyphMetrics metrics)
        {
            return new Dictionary<string, object>
            {
                { "width", metrics.width }, { "height", metrics.height },
                { "bearingX", metrics.horizontalBearingX },
                { "bearingY", metrics.horizontalBearingY },
                { "advance", metrics.horizontalAdvance },
            };
        }

        private static string HashFile(string path)
        {
            return Hash(VmAutomationPersistenceFile.ReadAllBytes(path));
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
            }
        }

        private sealed class SnapshotSet
        {
            private readonly List<FileSnapshot> _files;

            private SnapshotSet(List<FileSnapshot> files)
            {
                _files = files;
            }

            internal static SnapshotSet Capture(string spriteAssetPath,
                string atlasPath)
            {
                var paths = new[]
                {
                    ToAbsoluteProjectPath(atlasPath),
                    ToAbsoluteProjectPath(atlasPath) + ".meta",
                    ToAbsoluteProjectPath(spriteAssetPath),
                    ToAbsoluteProjectPath(spriteAssetPath) + ".meta",
                };
                var files = new List<FileSnapshot>(paths.Length);
                long totalBytes = 0;
                foreach (string path in paths)
                {
                    if (!File.Exists(path))
                    {
                        throw new CommandException(
                            $"Transaction file '{path}' does not exist.",
                            "asset_not_found");
                    }
                    byte[] bytes = VmAutomationPersistenceFile.ReadAllBytes(path);
                    totalBytes += bytes.LongLength;
                    if (totalBytes > MaxSnapshotBytes)
                    {
                        throw new CommandException(
                            "SpriteAsset transaction snapshot exceeds the 32 MB limit.",
                            "input_too_large");
                    }
                    files.Add(new FileSnapshot(path, bytes, Hash(bytes)));
                }
                return new SnapshotSet(files);
            }

            internal void Restore(string spriteAssetPath, string atlasPath)
            {
                foreach (FileSnapshot file in _files)
                    VmAutomationPersistenceFile.WriteAllBytes(file.Path, file.Bytes);

                ClearDirtyAssets(atlasPath);
                ClearDirtyAssets(spriteAssetPath);
                AssetDatabase.ImportAsset(atlasPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(spriteAssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                ClearDirtyAssets(atlasPath);
                ClearDirtyAssets(spriteAssetPath);

                foreach (FileSnapshot file in _files)
                {
                    if (!string.Equals(HashFile(file.Path), file.Sha256,
                            StringComparison.Ordinal))
                    {
                        throw new IOException(
                            $"Restored transaction file '{file.Path}' does not match its byte snapshot.");
                    }
                }
            }

            private static void ClearDirtyAssets(string assetPath)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(
                             assetPath))
                {
                    EditorUtility.ClearDirty(asset);
                }
                if (AssetImporter.GetAtPath(assetPath) is Object importer)
                    EditorUtility.ClearDirty(importer);
            }
        }

        private sealed class FileSnapshot
        {
            internal FileSnapshot(string path, byte[] bytes, string sha256)
            {
                Path = path;
                Bytes = bytes;
                Sha256 = sha256;
            }

            internal string Path { get; }
            internal byte[] Bytes { get; }
            internal string Sha256 { get; }
        }

        private sealed class Identity
        {
            private Identity(string guid, long localId)
            {
                Guid = guid;
                LocalId = localId;
            }

            internal string Guid { get; }
            internal long LocalId { get; }

            internal static Identity Read(Object asset, string label)
            {
                if (asset == null ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset,
                        out string guid, out long localId))
                {
                    throw new CommandException(
                        $"Could not read {label} GUID/local file ID.",
                        "unsupported_sprite_asset");
                }
                return new Identity(guid, localId);
            }

            internal void AssertSame(Object asset, string label)
            {
                Identity current = Read(asset, label);
                if (!string.Equals(Guid, current.Guid, StringComparison.Ordinal) ||
                    LocalId != current.LocalId)
                {
                    throw new CommandException(
                        $"{label} identity changed during publication.",
                        "transaction_postcondition_failed");
                }
            }
        }

        private sealed class Request
        {
            internal Request(string spriteAssetPath, int spriteWidth,
                int spriteHeight, int packingPadding,
                List<SpriteRequest> sprites)
            {
                SpriteAssetPath = spriteAssetPath;
                SpriteWidth = spriteWidth;
                SpriteHeight = spriteHeight;
                PackingPadding = packingPadding;
                Sprites = sprites;
            }

            internal string SpriteAssetPath { get; }
            internal int SpriteWidth { get; }
            internal int SpriteHeight { get; }
            internal int PackingPadding { get; }
            internal List<SpriteRequest> Sprites { get; }
        }

        private sealed class SpriteRequest
        {
            internal SpriteRequest(string name, string imagePath,
                float glyphScale, float bearingX, float bearingY,
                float advance, float characterScale)
            {
                Name = name;
                ImagePath = imagePath;
                GlyphScale = glyphScale;
                BearingX = bearingX;
                BearingY = bearingY;
                Advance = advance;
                CharacterScale = characterScale;
            }

            internal string Name { get; }
            internal string ImagePath { get; }
            internal float GlyphScale { get; }
            internal float BearingX { get; }
            internal float BearingY { get; }
            internal float Advance { get; }
            internal float CharacterScale { get; }
        }

        private sealed class PreparedSprite
        {
            internal PreparedSprite(SpriteRequest request, Color32[] pixels)
            {
                Request = request;
                Pixels = pixels;
            }

            internal SpriteRequest Request { get; }
            internal Color32[] Pixels { get; }
        }

        private sealed class PlannedSprite
        {
            internal PlannedSprite(PreparedSprite prepared, SpriteRect rect,
                bool created)
            {
                Prepared = prepared;
                Rect = rect;
                Created = created;
            }

            internal PreparedSprite Prepared { get; }
            internal SpriteRect Rect { get; }
            internal bool Created { get; }
        }

        private sealed class MutationPlan
        {
            internal MutationPlan(int atlasWidth, int atlasHeight,
                List<SpriteRect> allRects, List<PlannedSprite> entries)
            {
                AtlasWidth = atlasWidth;
                AtlasHeight = atlasHeight;
                AllRects = allRects;
                Entries = entries;
            }

            internal int AtlasWidth { get; }
            internal int AtlasHeight { get; }
            internal List<SpriteRect> AllRects { get; }
            internal List<PlannedSprite> Entries { get; }
        }

        private sealed class TargetContext
        {
            internal TargetContext(string spriteAssetPath, string atlasPath,
                DecodedImage atlasImage, SpriteRect[] existingRects,
                Dictionary<string, long> existingSpriteLocalIds,
                Identity spriteAssetIdentity, Identity atlasIdentity,
                Identity materialIdentity, int maxTextureSize)
            {
                SpriteAssetPath = spriteAssetPath;
                AtlasPath = atlasPath;
                AtlasImage = atlasImage;
                ExistingRects = existingRects;
                ExistingSpriteLocalIds = existingSpriteLocalIds;
                SpriteAssetIdentity = spriteAssetIdentity;
                AtlasIdentity = atlasIdentity;
                MaterialIdentity = materialIdentity;
                MaxTextureSize = maxTextureSize;
            }

            internal string SpriteAssetPath { get; }
            internal string AtlasPath { get; }
            internal DecodedImage AtlasImage { get; }
            internal SpriteRect[] ExistingRects { get; }
            internal Dictionary<string, long> ExistingSpriteLocalIds { get; }
            internal Identity SpriteAssetIdentity { get; }
            internal Identity AtlasIdentity { get; }
            internal Identity MaterialIdentity { get; }
            internal int MaxTextureSize { get; }
        }

        private sealed class DecodedImage
        {
            internal DecodedImage(int width, int height, Color32[] pixels)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
            }

            internal int Width { get; }
            internal int Height { get; }
            internal Color32[] Pixels { get; }
        }

        private sealed class CommandException : Exception
        {
            internal CommandException(string message, string errorCode)
                : base(message)
            {
                ErrorCode = errorCode;
            }

            internal string ErrorCode { get; }
        }
    }
}
